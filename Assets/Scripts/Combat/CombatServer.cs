using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BS
{
    /// <summary>
    /// Runs survivor combat on the server right after the mob step: contact damage, revival, firing,
    /// projectiles and their hits. Results are copied to the replicated survivor values.
    /// </summary>
    [DefaultExecutionOrder(MatchPhases.CombatOrder)]
    [RequireComponent(typeof(MobServer))]
    public class CombatServer : MonoBehaviour
    {
        public static CombatServer Instance { get; private set; }

        /// <summary>The owner a ranged mob's projectiles carry. It is not a client, and its shooter slot tells clients which shape to draw.</summary>
        public const ulong MobShooterOwner = MatchWorld.MobShooterOwner;
        /// <summary>The shooter slot of every mob's projectile (0 is the boss, 1 to 4 the survivors).</summary>
        public const byte MobShooterSlot = MatchWorld.MobShooterSlot;

        public SurvivorSim Survivors => MatchWorld.Current.Survivors;
        public AttackSim Attacks => MatchWorld.Current.Attacks;
        public ProjectileSim Projectiles => MatchWorld.Current.Projectiles;

        /// <summary>The boss of the running match; <see cref="BossSim.Exists"/> is false when there is none.</summary>
        public BossSim Boss => MatchWorld.Current.Boss;

        /// <summary>Where mobs died in the latest step (shots only); growth reads it to hand out experience.</summary>
        public IReadOnlyList<Vector2> KillsThisStep => MatchWorld.Current.ShotKills;

        /// <summary>The survivors that were registered when this room started, in registration order.</summary>
        public IReadOnlyList<PlayerAvatar> RegisteredAvatars => registered;

        readonly List<PlayerAvatar> registered = new List<PlayerAvatar>();
        readonly List<CombatEvent> events = new List<CombatEvent>();
        BossSkillSim bossSkills => MatchWorld.Current.BossSkills;
        readonly List<CombatEvent> skillRequestEvents = new List<CombatEvent>();
        List<CombatEvent> blastEvents => MatchWorld.Current.BlastEvents;
        readonly List<ulong> remoteClients = new List<ulong>();
        Dictionary<ulong, byte> slots => MatchWorld.Current.Slots;

        PlayerAvatar bossAvatar;
        MobServer mobs;
        NetworkManager nm;
        bool running;
        byte[] buffer;
        System.Func<ulong, byte> slotOf;

        // Measurement (dev logging)
        float statsTimer;
        long bytesSent;
        int firesSent, hitsSent, killsSent, endsSent, skillsSent;
        int bossFiresSent, survivorHitsSent;
        int mobShotsSent, blastsSent, blastHitsSent;
        int bossSkillsUsed, bossSkillsReleased;

        void Awake()
        {
            Instance = this;
            mobs = GetComponent<MobServer>();
            if (DevArgs.SurvivorHealth > 0) Survivors.MaxHealth = DevArgs.SurvivorHealth;
            if (DevArgs.MobSpeed > 0f) mobs.Sim.Speed = DevArgs.MobSpeed;
            if (DevArgs.MobHealth > 0) mobs.Sim.MaxHealth = DevArgs.MobHealth;
            buffer = new byte[CombatCodec.MaxBatchBytes + 64];
            slotOf = MatchWorld.Current.SlotOf; // one delegate for every publish
            bossSkills.BaseDamageOverride = DevArgs.BossSkillDamage;
            MatchWorld.Current.BossPhasesEnabled = DevArgs.BossPhases;
        }

        ulong BossOwnerId => MatchWorld.Current.BossOwnerId;

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
            // The host's own character is created before this event fires, so nothing is reset here;
            // everything is cleared when the room ends instead.
            running = true;
        }

        void HandleServerStopped(bool wasHost)
        {
            running = false;
            ResetAll();
        }

        /// <summary>A match ended but the room goes on: every survivor, shot and growth state is gone.</summary>
        public void ResetMatch() => ResetAll();

        void ResetAll()
        {
            registered.Clear();
            slots.Clear();
            Survivors.Clear();
            Attacks.Clear();
            Projectiles.Clear();
            events.Clear();
            blastEvents.Clear();
            Boss.Clear();
            bossAvatar = null;
            bossSkills.Reset();
            GrowthServer.Instance?.ResetAll();
        }

        /// <summary>Called when the boss's character spawns on the server: the boss starts the match at full health.</summary>
        public void RegisterBoss(PlayerAvatar avatar)
        {
            bossAvatar = avatar;
            MatchWorld.Current.BossOwnerId = avatar.OwnerClientId;
            BossDef def = BossCatalog.ForMatch();
            Boss.Reset(def, avatar.transform.position);
            MatchWorld.Current.RescuePoint.Reset(System.Environment.TickCount, avatar.transform.position);

            // The boss shoots like a survivor does, but its shots are hostile: they hurt survivors and pass through mobs.
            slots[avatar.OwnerClientId] = RoleTable.BossRole;
            Attacks.Register(avatar.OwnerClientId, def.Attack.ToAttackDef(), true);
            bossSkills.Reset(def);
            Debug.Log($"[BS] boss registered: owner={avatar.OwnerClientId} kind={def.Id} {def.Name} health={Boss.MaxHealth} shotDamage={BossShotDamage}");
        }

        /// <summary>Called when the boss's character despawns (the boss player left, or the match ended).</summary>
        public void UnregisterBoss(PlayerAvatar avatar)
        {
            if (bossAvatar != avatar) return;
            Attacks.Unregister(avatar.OwnerClientId);
            slots.Remove(avatar.OwnerClientId);
            bossAvatar = null;
            MatchWorld.Current.BossOwnerId = MatchWorld.NoBoss;
            Boss.Clear();
        }

        /// <summary>The damage of a boss shot at full boss health (the development override replaces the shared value).</summary>
        public static int BossShotDamage => DevArgs.BossShotDamage > 0 ? DevArgs.BossShotDamage : BossCatalog.Current.Attack.BaseDamage;

        bool IsBossOwner(ulong clientId) => MatchWorld.Current.IsBossOwner(clientId);

        /// <summary>Called when a survivor's character spawns on the server.</summary>
        public void RegisterSurvivor(PlayerAvatar avatar)
        {
            if (!registered.Contains(avatar)) registered.Add(avatar);
            slots[avatar.OwnerClientId] = avatar.Role;
            Survivors.Register(avatar.OwnerClientId, StartingMaxHealth(CharacterCatalog.Get(avatar.CharacterId)));
            Attacks.Register(avatar.OwnerClientId, CharacterCatalog.Get(avatar.CharacterId)?.Attack ?? AttackDef.Default);
            GrowthServer.Instance?.Register(avatar);
        }

        /// <summary>
        /// The health a survivor of this character starts with. The development override replaces the shared starting
        /// health, so a character keeps its difference from the others.
        /// </summary>
        public static int StartingMaxHealth(CharacterDef character)
        {
            int health = character != null ? character.MaxHealth : CombatConfig.SurvivorMaxHealth;
            if (DevArgs.SurvivorHealth > 0) health += DevArgs.SurvivorHealth - CombatConfig.SurvivorMaxHealth;
            return health;
        }

        public void UnregisterSurvivor(PlayerAvatar avatar)
        {
            // A character from a room that already ended can despawn after the next room registered the same
            // client again. Only the character that is actually registered may remove the client's state.
            if (!registered.Remove(avatar)) return;
            if (IsRegistered(avatar.OwnerClientId)) return;

            slots.Remove(avatar.OwnerClientId);
            Survivors.Unregister(avatar.OwnerClientId);
            Attacks.Unregister(avatar.OwnerClientId);
            GrowthServer.Instance?.Unregister(avatar);
        }

        bool IsRegistered(ulong clientId)
        {
            for (int i = 0; i < registered.Count; i++)
                if (registered[i] != null && registered[i].OwnerClientId == clientId) return true;
            return false;
        }

        /// <summary>Latest aim and fire state from a player. Only living survivors and the standing boss can fire.</summary>
        public void SetAim(ulong clientId, ushort angle, bool firing)
        {
            if (!running || !RoleTable.MatchRunning) return;
            MatchWorld.Current.SetAim(clientId, angle, firing);
        }

        void FixedUpdate()
        {
            if (!running || !RoleTable.MatchRunning) return; // a match that has ended no longer advances

            float dt = Time.fixedDeltaTime;

            // The registered survivors and the boss, as the step sees them.
            MatchWorld world = MatchWorld.Current;
            world.MatchRunning = true;
            world.BossShotDamage = BossShotDamage;
            world.Bodies.Clear();
            for (int i = 0; i < registered.Count; i++)
            {
                PlayerAvatar a = registered[i];
                if (a == null || !a.IsSpawned) continue;
                world.Bodies.Add(new MatchBody
                {
                    ClientId = a.OwnerClientId, Role = a.Role, Character = a.CharacterId,
                    Position = a.transform.position, Moving = a.ServerMoving, Alive = a.IsAlive,
                });
            }
            if (bossAvatar != null && bossAvatar.IsSpawned)
                world.Bodies.Add(new MatchBody { ClientId = bossAvatar.OwnerClientId, Role = RoleTable.BossRole, Position = bossAvatar.transform.position, Alive = true });

            world.Run(MatchPhase.Combat, dt);

            CombatStepResult step = world.LastCombat;
            mobShotsSent += step.MobActions.ShotsFired;
            blastsSent += step.MobActions.Blasts;
            blastHitsSent += step.MobActions.SurvivorsHurt;
            bossSkillsReleased += step.BossSkillsReleased;
            WriteBossSkillCooldowns();

            for (int i = 0; i < registered.Count; i++)
            {
                PlayerAvatar a = registered[i];
                if (a == null || !a.IsSpawned || a.Combat == null) continue;
                ulong id = a.OwnerClientId;
                a.Combat.ServerWrite(Survivors.Health(id), Survivors.IsAlive(id) ? 0f : Survivors.ReviveGauge(id), Survivors.MaxHealthOf(id));
            }

            WriteBossHealth();

            if (world.CombatEvents.Count > 0) PublishEvents(world.CombatEvents);

            // Mob kills made by projectiles this step go out now, with the same time as the hits.
            mobs.FlushEvents();
        }

        /// <summary>The boss's skills advance with the match: cooldowns run, announced skills take effect, and the boss's screen gets the cooldowns.</summary>
        void WriteBossSkillCooldowns()
        {
            if (bossAvatar == null || !bossAvatar.IsSpawned) return;
            PlayerSummon view = bossAvatar.GetComponent<PlayerSummon>();
            if (view != null && view.IsSpawned) view.ServerWriteSkills(bossSkills.CooldownLeft(0), bossSkills.CooldownLeft(1), bossSkills.CooldownLeft(BossSkills.PhaseSlot));
        }

        /// <summary>
        /// A boss skill request (Q, E or the third skill on F) from a player. Only the boss player's request counts, and only while the match runs and the
        /// boss stands. The skill is announced to everyone now and takes effect when its warning time has passed.
        /// </summary>
        public void OnBossSkillRequest(ulong clientId, int slot, ushort aim)
        {
            if (!running) return;

            bool fromBoss = IsBossOwner(clientId);
            float fraction = Boss.Exists && Boss.MaxHealth > 0 ? Boss.Health / (float)Boss.MaxHealth : 0f;

            MatchWorld.Current.MatchRunning = RoleTable.MatchRunning;
            skillRequestEvents.Clear();
            BossSkillResult result = MatchWorld.Current.UseBossSkill(clientId, slot, aim, skillRequestEvents);
            if (result == BossSkillResult.Used) bossSkillsUsed++;

            if (DevArgs.CombatLog)
                Debug.Log($"[BS] boss skill: slot={slot} result={result} fromBoss={fromBoss} cooldown={bossSkills.CooldownLeft(slot):F1} health={fraction:F2}");

            if (skillRequestEvents.Count > 0) Publish(skillRequestEvents);
        }

        /// <summary>Copies the boss's health to its replicated values so every client shows the bar.</summary>
        public void WriteBossHealth()
        {
            if (bossAvatar == null || !bossAvatar.IsSpawned || bossAvatar.Combat == null || !Boss.Exists) return;
            bossAvatar.Combat.ServerWrite(Boss.Health, 0f, Boss.MaxHealth);
        }

        // Development scenarios (-bsDevKill, -bsDevMeet): run once, some seconds after the room started.
        float roomStartedAt = -1f;
        bool devKillDone, devMeetDone;

        void RunDevScenarios()
        {
            if (roomStartedAt < 0f) roomStartedAt = Time.time;
            float elapsed = Time.time - roomStartedAt;

            if (!devKillDone && DevArgs.DevKillRole >= 0 && elapsed >= DevArgs.DevKillTime)
            {
                PlayerAvatar target = FindByRole((byte)DevArgs.DevKillRole);
                if (target != null)
                {
                    devKillDone = true;
                    Survivors.Kill(target.OwnerClientId);
                    Debug.Log($"[BS] dev: killed survivor role {DevArgs.DevKillRole} (client {target.OwnerClientId})");
                }
            }

            if (!devMeetDone && DevArgs.DevMeetRoleA >= 0 && elapsed >= DevArgs.DevMeetTime)
            {
                PlayerAvatar a = FindByRole((byte)DevArgs.DevMeetRoleA);
                PlayerAvatar b = FindByRole((byte)DevArgs.DevMeetRoleB);
                if (a != null && b != null)
                {
                    devMeetDone = true;
                    a.ServerTeleport((Vector2)b.transform.position + new Vector2(1f, 0f));
                    Debug.Log($"[BS] dev: moved role {DevArgs.DevMeetRoleA} next to role {DevArgs.DevMeetRoleB}");
                }
            }
        }

        PlayerAvatar FindByRole(byte role)
        {
            for (int i = 0; i < registered.Count; i++)
                if (registered[i] != null && registered[i].Role == role) return registered[i];
            return null;
        }

        void Update()
        {
            if (!running) { roomStartedAt = -1f; devKillDone = devMeetDone = false; return; }

            // The scenarios count from the start of the match, which is when the first survivor exists.
            if (registered.Count == 0) { roomStartedAt = -1f; devKillDone = devMeetDone = false; }
            else RunDevScenarios();

            if (!DevArgs.CombatLog) return;

            statsTimer += Time.unscaledDeltaTime;
            if (statsTimer < 5f) return;

            int clients = remoteClients.Count;
            double perClient = clients > 0 ? bytesSent / (double)clients / statsTimer / 1024.0 : 0.0;
            Debug.Log($"[BS] combat server: fires={firesSent} hits={hitsSent} kills={killsSent} ends={endsSent} skills={skillsSent} " +
                      $"bossFires={bossFiresSent} survivorHits={survivorHitsSent} mobShots={mobShotsSent} blasts={blastsSent} blastHits={blastHitsSent} bossSkills={bossSkillsUsed}/{bossSkillsReleased} beamHits={bossSkills.BeamHits} roarMobs={bossSkills.RoarMobs} {BossStatsText()} projectiles={Projectiles.Count} survivors={Survivors.Count} clients={clients} payloadPerClient={perClient:F3}KB/s");
            statsTimer = 0f;
            bytesSent = 0;
            firesSent = hitsSent = killsSent = endsSent = skillsSent = 0;
            bossFiresSent = survivorHitsSent = 0;
            mobShotsSent = blastsSent = blastHitsSent = 0;
            bossSkillsUsed = bossSkillsReleased = 0;
        }

        /// <summary>The boss's health and what it fires with at that health (dev logging).</summary>
        string BossStatsText()
        {
            if (!Boss.Exists) return "boss=none";
            float fraction = Boss.MaxHealth > 0 ? Boss.Health / (float)Boss.MaxHealth : 0f;
            return $"boss={Boss.HealthPercent}% shotDamage={BossAttack.Damage(BossShotDamage, fraction)} cooldownMult={BossAttack.CooldownMultiplier(fraction):F2}";
        }

        // ---- Sending ----

        /// <summary>Sends events made outside the shot simulation (skills) in the same stream and with the same time.</summary>
        public void Publish(IReadOnlyList<CombatEvent> list)
        {
            if (!running || list.Count == 0) return;
            PublishEvents(list);
        }

        void PublishEvents(IReadOnlyList<CombatEvent> list)
        {
            uint timeMs = mobs.ServerTimeMs;

            for (int i = 0; i < list.Count; i++)
            {
                switch (list[i].Kind)
                {
                    case CombatEventKind.Fire: firesSent++; if (list[i].Owner == BossOwnerId) bossFiresSent++; break;
                    case CombatEventKind.Hit:
                        hitsSent++;
                        if (list[i].SurvivorTarget) survivorHitsSent++;
                        else if (list[i].Killed) killsSent++;
                        break;
                    case CombatEventKind.Skill: skillsSent++; break;
                    case CombatEventKind.Explosion: break;
                    default: endsSent++; break;
                }
            }

            if (DevArgs.CombatDump)
                for (int i = 0; i < list.Count; i++)
                {
                    int id = list[i].Kind == CombatEventKind.Skill ? list[i].SkillId : list[i].Kind == CombatEventKind.Explosion ? list[i].Radius10 : list[i].ProjectileId;
                    Debug.Log($"[BS] combatdump server t={timeMs} {list[i].Kind} id={id} mob={list[i].MobId} kill={(list[i].Killed ? 1 : 0)} cont={(list[i].Continues ? 1 : 0)} angle={list[i].Angle} owner={list[i].Owner} surv={(list[i].SurvivorTarget ? 1 : 0)}{(list[i].Kind == CombatEventKind.Skill ? $" range={list[i].RangeScale} dur={list[i].DurationScale}" : "")}");
                }

            // The host shows its own events at server time; everyone else gets them as a message.
            CombatClient.Instance?.ApplyLocal(list, MobMessages.ServerTimeFromMs(timeMs), slotOf);

            remoteClients.Clear();
            foreach (ulong id in nm.ConnectedClientsIds)
                if (id != NetworkManager.ServerClientId) remoteClients.Add(id);
            if (remoteClients.Count == 0) return;

            int start = 0;
            while (start < list.Count)
            {
                int count = CombatCodec.CountThatFit(list, start);
                int length = CombatCodec.WriteBatch(buffer, timeMs, list, start, count, slotOf);
                using (var writer = new FastBufferWriter(length, Unity.Collections.Allocator.Temp))
                {
                    writer.WriteBytesSafe(buffer, length);
                    nm.CustomMessagingManager.SendNamedMessage(CombatMessages.Events, remoteClients, writer, NetworkDelivery.ReliableSequenced);
                }
                bytesSent += (long)length * remoteClients.Count;
                start += count;
            }
        }
    }
}
