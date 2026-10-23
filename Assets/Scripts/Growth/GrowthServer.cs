using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BS
{
    /// <summary>
    /// Runs growth on the server right after combat: experience for kills, level ups and the skill choices
    /// they open, mana, key skills and their effects, and the stats that follow from all of it. The simulation is the world's growth
    /// phase (<see cref="MatchWorld.StepGrowth"/>); this component hands it where the survivors stand and the requests that came in, and
    /// sends the results to the replicated values and owner-only messages.
    /// </summary>
    [DefaultExecutionOrder(MatchPhases.GrowthOrder)]
    [RequireComponent(typeof(CombatServer))]
    public class GrowthServer : MonoBehaviour
    {
        public static GrowthServer Instance { get; private set; }

        public LevelingSim Leveling { get; private set; }
        public SkillSim Skills => MatchWorld.Current.Skills;

        sealed class Entry
        {
            public PlayerAvatar Avatar;
            public PlayerGrowth Growth;
        }

        readonly Dictionary<ulong, Entry> entries = new Dictionary<ulong, Entry>();
        readonly List<ulong> order = new List<ulong>();

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

            Leveling = MatchWorld.Current.Leveling; // the development arguments were applied when the world was made
            // -bsSurvivorHealth replaces the starting health in load tests; growth still adds on top of it.
            MatchWorld.Current.SurvivorHealthBonus = DevArgs.SurvivorHealth > 0 ? DevArgs.SurvivorHealth - CombatConfig.SurvivorMaxHealth : 0;
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
            MatchWorld.Current.ClearGrowth();
            roomStartedAt = -1f;
            devXpDone = false;
        }

        // ---- Registration (called by CombatServer) ----

        public void Register(PlayerAvatar avatar)
        {
            ulong id = avatar.OwnerClientId;
            var entry = new Entry { Avatar = avatar, Growth = avatar.GetComponent<PlayerGrowth>() };
            entries[id] = entry;

            MatchWorld.Current.StartGrowth(id, avatar.CharacterId, granted => GrantDevSkills(granted, avatar.CharacterId));
            WriteReplicated(id, entry);
        }

        /// <summary>-bsDevSkills: the survivor starts with their character's Q, E and R skills at the given rank.</summary>
        void GrantDevSkills(ulong id, byte character)
        {
            if (DevArgs.DevSkillsRank <= 0) return;
            GrantSkills(Leveling, id, character, DevArgs.DevSkillsRank, DevArgs.DevPassiveRank, DevArgs.DevEvolve);
            Debug.Log($"[BS] dev: gave client {id} the skills of character {character} at rank {DevArgs.DevSkillsRank}, own passive rank {DevArgs.DevPassiveRank}, evolution {DevArgs.DevEvolve}");
        }

        /// <summary>
        /// Gives a survivor each of the character's active skills (every slot, the R skill too, whatever its prerequisite) at the rank, and with a
        /// positive <paramref name="passiveRank"/> the character's own passives as well, and with <paramref name="evolution"/> 1 or 2 the matching evolution of the Q skill. A rank above a skill's maximum is cut to it.
        /// </summary>
        public static void GrantSkills(LevelingSim leveling, ulong id, byte character, int rank, int passiveRank, int evolution = 0)
        {
            for (int slot = 0; slot < SkillCatalog.SkillSlots; slot++)
            {
                SkillDef def = SkillCatalog.ForSlot(slot, character);
                if (def != null) leveling.GrantRank(id, def.Id, Math.Min(rank, def.MaxRank));
            }
            // The first (1) or second (2) evolution of the Q skill, whatever the Q skill's rank.
            SkillDef evolved = SkillCatalog.EvolutionOf(SkillCatalog.ForSlot(0, character), evolution);
            if (evolved != null) leveling.GrantRank(id, evolved.Id, 1);
            if (passiveRank <= 0) return;
            foreach (SkillDef def in SkillCatalog.All)
                if (!def.IsActive && def.Passive != PassiveEffectKind.None && def.Character == character)
                    leveling.GrantRank(id, def.Id, Math.Min(passiveRank, def.MaxRank));
        }

        public void Unregister(PlayerAvatar avatar)
        {
            ulong id = avatar.OwnerClientId;
            entries.Remove(id);
            MatchWorld.Current.StopGrowth(id);
        }

        // ---- Requests from players (the server decides what they mean) ----

        public void OnPick(ulong clientId, byte offerId, int index)
        {
            if (!running || !RoleTable.MatchRunning) return;
            if (MatchWorld.Current.Pick(clientId, offerId, index)) manualPicks++;
        }

        public void OnSkillRequest(ulong clientId, int slot)
        {
            if (!running || !RoleTable.MatchRunning) return;
            MatchWorld.Current.RequestSkill(clientId, slot);
        }

        // ---- The step ----

        void FixedUpdate()
        {
            if (!running || !RoleTable.MatchRunning) return; // a match that has ended no longer advances

            float dt = Time.fixedDeltaTime;
            RunDevScenario();

            // Where the survivors with growth stand now.
            MatchWorld world = MatchWorld.Current;
            world.MatchRunning = true;
            world.Bodies.Clear();
            foreach (KeyValuePair<ulong, Entry> pair in entries)
            {
                Entry e = pair.Value;
                if (e.Avatar == null || !e.Avatar.IsSpawned) continue;
                world.Bodies.Add(new MatchBody
                {
                    ClientId = pair.Key, Role = e.Avatar.Role, Character = e.Avatar.CharacterId,
                    Position = e.Avatar.transform.position, Moving = e.Avatar.ServerMoving, Alive = e.Avatar.IsAlive,
                });
            }

            world.Run(MatchPhase.Growth, dt);

            SendResults(world);
            if (world.LastGrowth.AreaDamage) mobs.FlushEvents();
            killsHandled += world.LastGrowth.KillsHandled;

            WriteAllReplicated();

            if (world.SkillEvents.Count > 0) combat.Publish(world.SkillEvents);

            if (DevArgs.GrowthLog) LogStats(dt);
        }

        /// <summary>Tells the owners what their skill requests and level ups came to, and writes what the development log shows.</summary>
        void SendResults(MatchWorld world)
        {
            for (int i = 0; i < world.SkillOutcomes.Count; i++)
            {
                SkillOutcome o = world.SkillOutcomes[i];
                if (!entries.TryGetValue(o.ClientId, out Entry entry)) continue;

                bool fired = o.Result == SkillResult.Fired;
                if (entry.Growth != null)
                    entry.Growth.ServerSendSkillResult(o.Slot, o.Result, fired ? o.Activation.Cooldown : 0f);

                if (!fired)
                {
                    skillsRefused++;
                    continue;
                }

                skillsFired++;
                if (DevArgs.GrowthLog)
                    Debug.Log($"[BS] skill fired client={o.ClientId} skill={o.Activation.SkillId} rank={o.Activation.Rank} manaCost={SkillCatalog.EffectiveManaCost(SkillCatalog.Get(o.Activation.SkillId), Leveling.Ranks(o.ClientId), entry.Avatar.CharacterId)} cooldown={o.Activation.Cooldown:F2} damage={o.Activation.Damage} amount={o.Activation.Amount} rangeScale={o.Activation.RangeScale} durationScale={o.Activation.DurationScale} at {o.Where.x:F1},{o.Where.y:F1} aim={o.Aim} touched={o.Touched}");
            }

            for (int i = 0; i < world.GrowthEvents.Count; i++)
            {
                GrowthEvent e = world.GrowthEvents[i];
                if (!entries.TryGetValue(e.ClientId, out Entry entry)) continue;

                switch (e.Kind)
                {
                    case GrowthEventKind.LevelUp:
                        levelUps++;
                        if (DevArgs.GrowthLog)
                        {
                            SurvivorStats stats = world.StatsOf(e.ClientId);
                            Debug.Log($"[BS] growth: level up client={e.ClientId} level={e.Level} maxHealth={stats.MaxHealth} maxMana={stats.MaxMana} damage={stats.ProjectileDamage} health={combat.Survivors.Health(e.ClientId)} taken={stats.DamageTakenMultiplier:F2}");
                        }
                        break;

                    case GrowthEventKind.OfferOpened:
                        entry.Growth?.ServerSendOffer(e.OfferId, e.Choices, e.Seconds);
                        if (DevArgs.GrowthLog)
                            Debug.Log($"[BS] growth: offer opened client={e.ClientId} id={e.OfferId} choices={string.Join(",", e.Choices)}");
                        break;

                    case GrowthEventKind.OfferClosed:
                        if (e.Auto) autoPicks++;
                        entry.Growth?.ServerSendOfferClosed(e.OfferId, e.Skill, e.Auto);
                        if (DevArgs.GrowthLog)
                            Debug.Log($"[BS] growth: offer closed client={e.ClientId} id={e.OfferId} skill={e.Skill} auto={e.Auto} rank={Leveling.RankOf(e.ClientId, e.Skill)}");
                        break;
                }
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
                                     Mathf.RoundToInt(Skills.MaxMana(id)), SkillCatalog.PackLow(Leveling.Ranks(id)), SkillCatalog.PackHigh(Leveling.Ranks(id)));
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

            MatchWorld world = MatchWorld.Current;
            var sb = new System.Text.StringBuilder();
            foreach (KeyValuePair<ulong, Entry> pair in entries)
                sb.Append($"[{pair.Key}: lv{Leveling.Level(pair.Key)} xp{Leveling.Xp(pair.Key)} mana{Mathf.FloorToInt(Skills.Mana(pair.Key))}/{Mathf.RoundToInt(Skills.MaxMana(pair.Key))} regen{world.StatsOf(pair.Key).ManaRegenPerSecond:F1}] ");

            Debug.Log($"[BS] growth server: survivors={entries.Count} kills={killsHandled} levelUps={levelUps} skillsFired={skillsFired} refused={skillsRefused} " +
                      $"picks manual={manualPicks} auto={autoPicks} {sb}");
            killsHandled = levelUps = skillsFired = skillsRefused = autoPicks = manualPicks = 0;
        }
    }
}
