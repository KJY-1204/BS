using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BS
{
    /// <summary>
    /// Runs growth on the server right after combat: experience for kills, level ups and the skill choices
    /// they open, mana, key skills and their effects, and the stats that follow from all of it. Results go to
    /// the simulations that need them and to the replicated values and owner-only messages.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [RequireComponent(typeof(CombatServer))]
    public class GrowthServer : MonoBehaviour
    {
        const int MaxRequestsPerStep = 64;

        public static GrowthServer Instance { get; private set; }

        public LevelingSim Leveling { get; private set; }
        public SkillSim Skills { get; } = new SkillSim();

        sealed class Entry
        {
            public PlayerAvatar Avatar;
            public PlayerGrowth Growth;
            public SurvivorStats Stats;
        }

        struct SkillRequest
        {
            public ulong ClientId;
            public int Slot;
        }

        readonly Dictionary<ulong, Entry> entries = new Dictionary<ulong, Entry>();
        readonly List<ulong> order = new List<ulong>();
        readonly List<GrowthBody> bodies = new List<GrowthBody>();
        readonly List<Vector2> kills = new List<Vector2>();
        readonly List<GrowthBody> skillBodies = new List<GrowthBody>();
        readonly List<GrowthEvent> growthEvents = new List<GrowthEvent>();
        readonly List<CombatEvent> skillEvents = new List<CombatEvent>();
        readonly List<SkillRequest> requests = new List<SkillRequest>();

        CombatServer combat;
        MobServer mobs;
        NetworkManager nm;
        bool running;

        // Development scenario (-bsDevXp)
        float roomStartedAt = -1f;
        bool devXpDone;

        // Measurement (dev logging)
        float statsTimer;
        int killsHandled, levelUps, skillsFired, skillsRefused, autoPicks, manualPicks;

        void Awake()
        {
            Instance = this;
            combat = GetComponent<CombatServer>();
            mobs = GetComponent<MobServer>();

            Leveling = new LevelingSim();
            if (DevArgs.XpPerKill > 0) Leveling.XpPerKill = DevArgs.XpPerKill;
            if (DevArgs.OfferSeconds > 0f) Leveling.OfferSeconds = DevArgs.OfferSeconds;
        }

        void Start()
        {
            nm = NetworkManager.Singleton;
            nm.OnServerStarted += HandleServerStarted;
            nm.OnServerStopped += HandleServerStopped;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (nm == null) return;
            nm.OnServerStarted -= HandleServerStarted;
            nm.OnServerStopped -= HandleServerStopped;
        }

        void HandleServerStarted()
        {
            // Characters are created before this event, so nothing is reset here.
            running = true;
        }

        void HandleServerStopped(bool wasHost)
        {
            running = false;
            ResetAll();
        }

        /// <summary>Forgets every survivor's growth: a new room starts everyone from the beginning.</summary>
        public void ResetAll()
        {
            entries.Clear();
            Leveling.Clear();
            Skills.Clear();
            requests.Clear();
            kills.Clear();
            roomStartedAt = -1f;
            devXpDone = false;
        }

        // ---- Registration (called by CombatServer) ----

        public void Register(PlayerAvatar avatar)
        {
            ulong id = avatar.OwnerClientId;
            var entry = new Entry { Avatar = avatar, Growth = avatar.GetComponent<PlayerGrowth>() };
            entries[id] = entry;

            Leveling.Register(id, avatar.CharacterId);
            GrantDevSkills(id, avatar.CharacterId);
            entry.Stats = StatCalc.Compute(1, Leveling.Ranks(id), CharacterCatalog.Get(avatar.CharacterId));
            Skills.Register(id, entry.Stats.MaxMana, entry.Stats.ManaRegenPerSecond);
            ApplyStats(id, entry);
            WriteReplicated(id, entry);
        }

        /// <summary>-bsDevSkills: the survivor starts with their character's Q and E skills at the given rank.</summary>
        void GrantDevSkills(ulong id, byte character)
        {
            if (DevArgs.DevSkillsRank <= 0) return;
            for (int slot = 0; slot < SkillCatalog.SkillSlots; slot++)
            {
                SkillDef def = SkillCatalog.ForSlot(slot, character);
                if (def != null) Leveling.GrantRank(id, def.Id, Math.Min(DevArgs.DevSkillsRank, def.MaxRank));
            }
            if (DevArgs.DevPassiveRank > 0)
            {
                foreach (SkillDef def in SkillCatalog.All)
                    if (!def.IsActive && def.Passive != PassiveEffectKind.None && def.Character == character)
                        Leveling.GrantRank(id, def.Id, Math.Min(DevArgs.DevPassiveRank, def.MaxRank));
            }
            Debug.Log($"[BS] dev: gave client {id} the skills of character {character} at rank {DevArgs.DevSkillsRank}, own passive rank {DevArgs.DevPassiveRank}");
        }

        public void Unregister(PlayerAvatar avatar)
        {
            ulong id = avatar.OwnerClientId;
            entries.Remove(id);
            Leveling.Unregister(id);
            Skills.Unregister(id);
        }

        // ---- Requests from players (the server decides what they mean) ----

        public void OnPick(ulong clientId, byte offerId, int index)
        {
            if (!running || !RoleTable.MatchRunning) return;
            if (Leveling.Pick(clientId, offerId, index)) manualPicks++;
        }

        public void OnSkillRequest(ulong clientId, int slot)
        {
            if (!running || !RoleTable.MatchRunning || requests.Count >= MaxRequestsPerStep) return;
            requests.Add(new SkillRequest { ClientId = clientId, Slot = slot });
        }

        // ---- The step ----

        void FixedUpdate()
        {
            if (!running || !RoleTable.MatchRunning) return; // a match that has ended no longer advances

            float dt = Time.fixedDeltaTime;
            RunDevScenario();

            kills.Clear();
            IReadOnlyList<Vector2> shotKills = combat.KillsThisStep;
            for (int i = 0; i < shotKills.Count; i++) kills.Add(shotKills[i]);

            skillEvents.Clear();
            bool areaDamage = ProcessSkillRequests();
            if (areaDamage) mobs.FlushEvents();

            GiveExperience();

            Leveling.Step(dt);
            Skills.Step(dt);
            EndEffectsOfTheDead();

            ProcessGrowthEvents();
            KeepAttackModifiersCurrent();
            WriteAllReplicated();

            if (skillEvents.Count > 0) combat.Publish(skillEvents);

            if (DevArgs.GrowthLog) LogStats(dt);
        }

        bool ProcessSkillRequests()
        {
            bool areaDamage = false;

            // Where every survivor stands and whether alive, for the skills that heal.
            skillBodies.Clear();
            if (requests.Count > 0)
            {
                foreach (KeyValuePair<ulong, Entry> pair in entries)
                {
                    Entry e = pair.Value;
                    if (e.Avatar == null || !e.Avatar.IsSpawned) continue;
                    skillBodies.Add(new GrowthBody { ClientId = pair.Key, Position = e.Avatar.transform.position, Alive = combat.Survivors.IsAlive(pair.Key) });
                }
            }

            for (int i = 0; i < requests.Count; i++)
            {
                SkillRequest request = requests[i];
                if (!entries.TryGetValue(request.ClientId, out Entry entry)) continue;

                bool alive = combat.Survivors.IsAlive(request.ClientId);
                SkillResult result = Skills.TryActivate(request.ClientId, entry.Avatar.CharacterId, request.Slot, alive, Leveling.Ranks(request.ClientId), out SkillActivation activation);

                if (entry.Growth != null)
                    entry.Growth.ServerSendSkillResult(request.Slot, result, result == SkillResult.Fired ? activation.Cooldown : 0f);

                if (result != SkillResult.Fired)
                {
                    skillsRefused++;
                    continue;
                }

                skillsFired++;
                Vector2 where = entry.Avatar.transform.position;
                ushort aim = combat.Attacks.AimOf(request.ClientId);
                CharacterDef character = CharacterCatalog.Get(entry.Avatar.CharacterId);

                var context = new SkillContext
                {
                    CasterId = request.ClientId,
                    Origin = where,
                    AimAngle = aim,
                    Mobs = mobs.Sim,
                    Projectiles = combat.Projectiles,
                    Survivors = combat.Survivors,
                    Boss = combat.Boss.Exists ? combat.Boss : null,
                    Bodies = skillBodies,
                    Shape = character != null ? character.Attack.Shape : ProjectileShape.Default,
                    Deaths = kills,
                };
                int touched = SkillExecutor.Apply(activation, context);
                if (touched > 0) areaDamage = true;

                skillEvents.Add(new CombatEvent
                {
                    Kind = CombatEventKind.Skill,
                    Owner = request.ClientId,
                    SkillId = activation.SkillId,
                    Rank = (byte)activation.Rank,
                    Position = where,
                    Angle = aim,
                    RangeScale = (byte)activation.RangeScale,
                    DurationScale = (byte)activation.DurationScale,
                });

                if (DevArgs.GrowthLog)
                    Debug.Log($"[BS] skill fired client={request.ClientId} skill={activation.SkillId} rank={activation.Rank} manaCost={SkillCatalog.EffectiveManaCost(SkillCatalog.Get(activation.SkillId), Leveling.Ranks(request.ClientId), entry.Avatar.CharacterId)} cooldown={activation.Cooldown:F2} damage={activation.Damage} amount={activation.Amount} rangeScale={activation.RangeScale} durationScale={activation.DurationScale} at {where.x:F1},{where.y:F1} aim={aim} touched={touched}");
            }

            requests.Clear();
            return areaDamage;
        }

        void GiveExperience()
        {
            if (kills.Count == 0) return;

            bodies.Clear();
            foreach (KeyValuePair<ulong, Entry> pair in entries)
            {
                Entry e = pair.Value;
                if (e.Avatar == null || !e.Avatar.IsSpawned) continue;
                bodies.Add(new GrowthBody { ClientId = pair.Key, Position = e.Avatar.transform.position, Alive = combat.Survivors.IsAlive(pair.Key) });
            }

            for (int i = 0; i < kills.Count; i++) Leveling.AwardKill(kills[i], bodies);
            killsHandled += kills.Count;
        }

        void EndEffectsOfTheDead()
        {
            foreach (ulong id in entries.Keys)
                if (!combat.Survivors.IsAlive(id)) Skills.EndBuff(id);
        }

        void ProcessGrowthEvents()
        {
            growthEvents.Clear();
            Leveling.DrainEvents(growthEvents);

            for (int i = 0; i < growthEvents.Count; i++)
            {
                GrowthEvent e = growthEvents[i];
                if (!entries.TryGetValue(e.ClientId, out Entry entry)) continue;

                switch (e.Kind)
                {
                    case GrowthEventKind.LevelUp:
                        levelUps++;
                        ApplyStats(e.ClientId, entry);
                        if (DevArgs.GrowthLog)
                            Debug.Log($"[BS] growth: level up client={e.ClientId} level={e.Level} maxHealth={entry.Stats.MaxHealth} maxMana={entry.Stats.MaxMana} damage={entry.Stats.ProjectileDamage} health={combat.Survivors.Health(e.ClientId)}");
                        break;

                    case GrowthEventKind.OfferOpened:
                        entry.Growth?.ServerSendOffer(e.OfferId, e.Choices, e.Seconds);
                        if (DevArgs.GrowthLog)
                            Debug.Log($"[BS] growth: offer opened client={e.ClientId} id={e.OfferId} choices={string.Join(",", e.Choices)}");
                        break;

                    case GrowthEventKind.OfferClosed:
                        if (e.Auto) autoPicks++;
                        ApplyStats(e.ClientId, entry);
                        entry.Growth?.ServerSendOfferClosed(e.OfferId, e.Skill, e.Auto);
                        if (DevArgs.GrowthLog)
                            Debug.Log($"[BS] growth: offer closed client={e.ClientId} id={e.OfferId} skill={e.Skill} auto={e.Auto} rank={Leveling.RankOf(e.ClientId, e.Skill)}");
                        break;
                }
            }
        }

        // ---- Stats ----

        /// <summary>Works out a survivor's stats and hands them to the simulations that use them.</summary>
        void ApplyStats(ulong id, Entry entry)
        {
            entry.Stats = StatCalc.Compute(Leveling.Level(id), Leveling.Ranks(id), CharacterCatalog.Get(entry.Avatar.CharacterId));
            // -bsSurvivorHealth replaces the starting health in load tests; growth still adds on top of it.
            int maxHealth = entry.Stats.MaxHealth;
            if (DevArgs.SurvivorHealth > 0) maxHealth += DevArgs.SurvivorHealth - CombatConfig.SurvivorMaxHealth;
            combat.Survivors.SetMaxHealth(id, maxHealth);
            Skills.SetStats(id, entry.Stats.MaxMana, entry.Stats.ManaRegenPerSecond);
            combat.Attacks.SetModifiers(id, entry.Stats.ProjectileDamage, entry.Stats.CooldownMultiplier * Skills.AttackMultiplier(id));
        }

        /// <summary>A running Rapid Fire changes the attack cooldown, so the modifiers are refreshed every step.</summary>
        void KeepAttackModifiersCurrent()
        {
            order.Clear();
            order.AddRange(entries.Keys);
            for (int i = 0; i < order.Count; i++)
            {
                ulong id = order[i];
                SurvivorStats s = entries[id].Stats;
                combat.Attacks.SetModifiers(id, s.ProjectileDamage, s.CooldownMultiplier * Skills.AttackMultiplier(id));
            }
        }

        void WriteAllReplicated()
        {
            foreach (KeyValuePair<ulong, Entry> pair in entries) WriteReplicated(pair.Key, pair.Value);
        }

        void WriteReplicated(ulong id, Entry entry)
        {
            if (entry.Growth == null || !entry.Growth.IsSpawned) return;
            entry.Growth.ServerWrite(Leveling.Level(id), Leveling.Xp(id), Mathf.FloorToInt(Skills.Mana(id)),
                                     Mathf.RoundToInt(Skills.MaxMana(id)), SkillCatalog.Pack(Leveling.Ranks(id)));
        }

        // ---- Development helpers ----

        void RunDevScenario()
        {
            // Counts from the start of the match, which is when the first survivor exists.
            if (entries.Count == 0) { roomStartedAt = -1f; devXpDone = false; return; }
            if (roomStartedAt < 0f) roomStartedAt = Time.time;
            if (devXpDone || DevArgs.DevXpTime < 0f || Time.time - roomStartedAt < DevArgs.DevXpTime) return;
            if (entries.Count == 0) return;

            devXpDone = true;
            order.Clear();
            order.AddRange(entries.Keys);
            for (int i = 0; i < order.Count; i++) Leveling.AddXp(order[i], DevArgs.DevXpAmount);
            Debug.Log($"[BS] dev: gave {DevArgs.DevXpAmount} experience to {order.Count} survivors");
        }

        void LogStats(float dt)
        {
            statsTimer += dt;
            if (statsTimer < 5f) return;
            statsTimer = 0f;

            var sb = new System.Text.StringBuilder();
            foreach (KeyValuePair<ulong, Entry> pair in entries)
                sb.Append($"[{pair.Key}: lv{Leveling.Level(pair.Key)} xp{Leveling.Xp(pair.Key)} mana{Mathf.FloorToInt(Skills.Mana(pair.Key))}/{Mathf.RoundToInt(Skills.MaxMana(pair.Key))} regen{pair.Value.Stats.ManaRegenPerSecond:F1}] ");

            Debug.Log($"[BS] growth server: survivors={entries.Count} kills={killsHandled} levelUps={levelUps} skillsFired={skillsFired} refused={skillsRefused} " +
                      $"picks manual={manualPicks} auto={autoPicks} {sb}");
            killsHandled = levelUps = skillsFired = skillsRefused = autoPicks = manualPicks = 0;
        }
    }
}
