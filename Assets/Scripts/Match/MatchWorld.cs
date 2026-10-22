using System;
using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>One character in a match as the step sees it: where it stands and what it is. The server fills these from the avatars and the harness from its own bodies.</summary>
    public struct MatchBody
    {
        public ulong ClientId;
        /// <summary>The role: <see cref="RoleTable.BossRole"/> for the boss, 1 to 4 for the survivors.</summary>
        public byte Role;
        public byte Character;
        public Vector2 Position;
        /// <summary>True while the player gives move input.</summary>
        public bool Moving;
        /// <summary>False for a fallen survivor.</summary>
        public bool Alive;
    }

    /// <summary>How a world is made: the starting values that the server takes from its development arguments and the harness from its settings.</summary>
    public sealed class MatchWorldOptions
    {
        public int MobTarget = MobConfig.DefaultTarget;
        public int MobSeed = 12345;
        public SummonConfig Summon = SummonConfig.Default;
        public int SummonSeed = 4242;
        /// <summary>The random source of the skill choices; null for a new one.</summary>
        public System.Random Random;
    }

    /// <summary>What the combat phase did, for the server's statistics.</summary>
    public struct CombatStepResult
    {
        public MobActionResult MobActions;
        /// <summary>How many announced boss skills took effect.</summary>
        public int BossSkillsReleased;
    }

    /// <summary>
    /// Everything a match advances, as plain data, and the order it advances in. The server components keep the network (reading the
    /// avatars, sending messages, writing replicated values) and call the phase methods at the Unity execution order of
    /// <see cref="MatchPhases"/>; the harness calls <see cref="Step"/>, which runs the same phases in the same order.
    /// </summary>
    public sealed partial class MatchWorld
    {
        /// <summary>The world of the running server. Made again when the server's mob component wakes up.</summary>
        public static MatchWorld Current { get; private set; }

        /// <summary>The owner a ranged mob's projectiles carry. It is not a client, and its shooter slot tells clients which shape to draw.</summary>
        public const ulong MobShooterOwner = ulong.MaxValue - 1;
        /// <summary>The shooter slot of every mob's projectile (0 is the boss, 1 to 4 the survivors).</summary>
        public const byte MobShooterSlot = 5;
        /// <summary>The owner id of the boss when there is none.</summary>
        public const ulong NoBoss = ulong.MaxValue;

        public static MatchWorld SetCurrent(MatchWorld world)
        {
            Current = world;
            return world;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        public MobSim Mobs { get; }
        public SummonSim Summon { get; }
        public SurvivorSim Survivors { get; } = new SurvivorSim();
        public AttackSim Attacks { get; } = new AttackSim();
        public ProjectileSim Projectiles { get; } = new ProjectileSim();
        public BossSim Boss { get; } = new BossSim();
        public BossSkillSim BossSkills { get; } = new BossSkillSim();
        public MobActionRunner MobActions { get; } = new MobActionRunner();
        public LevelingSim Leveling { get; }
        public SkillSim Skills { get; } = new SkillSim();
        public MatchSim Clock { get; } = new MatchSim();
        public SurvivorTargets SurvivorTargets { get; }

        /// <summary>The shooter slot of each client (the boss 0, the survivors 1 to 4), which projectile events carry so clients draw the right shape.</summary>
        public Dictionary<ulong, byte> Slots { get; } = new Dictionary<ulong, byte>();

        /// <summary>The client that plays the boss, or <see cref="NoBoss"/>.</summary>
        public ulong BossOwnerId { get; set; } = NoBoss;

        /// <summary>The damage of a boss shot at full boss health.</summary>
        public int BossShotDamage { get; set; } = BossAttack.BaseDamage;

        /// <summary>Explosion events the combat phase made (mob blasts and boss shockwaves). The server publishes and clears them.</summary>
        public List<CombatEvent> BlastEvents { get; } = new List<CombatEvent>();

        /// <summary>What the latest combat phase did.</summary>
        public CombatStepResult LastCombat { get; private set; }

        /// <summary>The combat events of the latest combat phase: every projectile event and the explosions. The server publishes them.</summary>
        public List<CombatEvent> CombatEvents { get; } = new List<CombatEvent>();

        /// <summary>Where mobs died to shots in the latest combat phase (a destroyed device is reported apart), for the experience.</summary>
        public List<Vector2> ShotKills { get; } = new List<Vector2>();

        public byte SlotOf(ulong clientId)
        {
            if (clientId == MobShooterOwner) return MobShooterSlot;
            return Slots.TryGetValue(clientId, out byte slot) ? slot : (byte)0;
        }

        public bool IsBossOwner(ulong clientId) => BossOwnerId != NoBoss && BossOwnerId == clientId;

        /// <summary>Every character of the match (the boss included) with where it stands. Filled before the phases that need it.</summary>
        public List<MatchBody> Bodies { get; } = new List<MatchBody>();

        /// <summary>When set, each phase that runs is appended to it (tests).</summary>
        public List<MatchPhase> Trace { get; set; }

        /// <summary>True while the match advances. The server sets it from the room's phase each tick; the harness keeps it true until the match has a result.</summary>
        public bool MatchRunning { get; set; }

        readonly List<SurvivorInfo> survivorInfos = new List<SurvivorInfo>();

        /// <summary>The survivors as the mobs see them, as of the last mob phase.</summary>
        public IReadOnlyList<SurvivorInfo> SurvivorInfos => survivorInfos;

        /// <summary>The world of a running server: the starting values come from the development arguments.</summary>
        public static MatchWorld ForServer()
        {
            var options = new MatchWorldOptions
            {
                MobTarget = DevArgs.MobTarget.HasValue ? DevArgs.MobTarget.Value : MobConfig.DefaultTarget,
            };

            SummonConfig summon = SummonConfig.Default;
            if (DevArgs.SummonEnergy > 0)
            {
                summon.MaxEnergy = DevArgs.SummonEnergy;
                summon.StartEnergy = DevArgs.SummonEnergy;
            }
            options.Summon = summon;

            var world = new MatchWorld(options);
            if (DevArgs.XpPerKill > 0) world.Leveling.XpPerKill = DevArgs.XpPerKill;
            if (DevArgs.OfferSeconds > 0f) world.Leveling.OfferSeconds = DevArgs.OfferSeconds;
            return world;
        }

        public MatchWorld(MatchWorldOptions options = null)
        {
            options = options ?? new MatchWorldOptions();
            Mobs = new MobSim(MobConfig.HardCap, Math.Clamp(options.MobTarget, 0, MobConfig.HardCap), options.MobSeed);
            Summon = new SummonSim(options.Summon, options.SummonSeed);
            Leveling = new LevelingSim(options.Random);
            SurvivorTargets = new SurvivorTargets(Survivors, SlotOf);
        }

        /// <summary>Runs every phase once, in the order of <see cref="MatchPhases.All"/>. A match that is not running does not advance.</summary>
        public void Step(float dt)
        {
            if (!MatchRunning) return;
            for (int i = 0; i < MatchPhases.All.Count; i++) Run(MatchPhases.All[i].Phase, dt);
            if (ClockDecided) MatchRunning = false; // the match has its result: nothing advances any more
        }

        /// <summary>Runs one phase. The server calls this from the component that owns the phase.</summary>
        public void Run(MatchPhase phase, float dt)
        {
            Trace?.Add(phase);
            switch (phase)
            {
                case MatchPhase.Mobs: StepMobs(dt); break;
                case MatchPhase.Summon: StepSummon(dt); break;
                case MatchPhase.Combat: StepCombat(dt); break;
                case MatchPhase.Growth: StepGrowth(dt); break;
                case MatchPhase.Clock: StepClock(dt); break;
            }
        }

        /// <summary>Mobs move, make mobs and expire. They chase the survivors in <see cref="Bodies"/>.</summary>
        public void StepMobs(float dt)
        {
            survivorInfos.Clear();
            for (int i = 0; i < Bodies.Count; i++)
            {
                MatchBody body = Bodies[i];
                if (body.Role == RoleTable.BossRole) continue; // the boss is never a default mob target
                survivorInfos.Add(new SurvivorInfo { ClientId = body.ClientId, Position = body.Position, Alive = body.Alive });
            }
            Mobs.Step(dt, survivorInfos);
        }

        /// <summary>The summon energy refills and the cooldowns run while the match runs and the boss stands.</summary>
        public void StepSummon(float dt) => Summon.Step(dt, MatchRunning && Boss.Exists);

        readonly List<SurvivorBody> survivorBodies = new List<SurvivorBody>();
        readonly List<ShooterBody> shooters = new List<ShooterBody>();

        /// <summary>
        /// Contact damage and revival, what the mobs do on their own, the boss's skills, the shots of everyone (the boss's too, harder and
        /// faster the weaker it is) and the projectiles with their hits.
        /// </summary>
        public void StepCombat(float dt)
        {
            survivorBodies.Clear();
            bool hasBossBody = false;
            Vector2 bossPosition = default;
            for (int i = 0; i < Bodies.Count; i++)
            {
                MatchBody body = Bodies[i];
                if (body.Role == RoleTable.BossRole) { hasBossBody = true; bossPosition = body.Position; continue; }
                survivorBodies.Add(new SurvivorBody { ClientId = body.ClientId, Position = body.Position, Moving = body.Moving });
            }

            if (hasBossBody) Boss.SetPosition(bossPosition);

            Survivors.Step(dt, survivorBodies, Mobs, Boss.Exists ? Boss : null);

            var result = new CombatStepResult();
            result.MobActions = MobActions.Run(Mobs, Projectiles, Survivors, survivorBodies, MobShooterOwner, BlastEvents);

            bool standing = BossOwnerId != NoBoss && Boss.Exists && Boss.CanBeHit;
            result.BossSkillsReleased = BossSkills.Step(dt, true, standing, Projectiles, Survivors, survivorBodies, BossOwnerId, BlastEvents, Mobs);

            shooters.Clear();
            for (int i = 0; i < survivorBodies.Count; i++)
                shooters.Add(new ShooterBody { ClientId = survivorBodies[i].ClientId, Position = survivorBodies[i].Position, CanFire = Survivors.IsAlive(survivorBodies[i].ClientId) });

            // The boss shoots too: a weaker boss fires faster and harder, from its health at the moment each shot leaves.
            if (hasBossBody && Boss.Exists)
            {
                float fraction = Boss.MaxHealth > 0 ? Boss.Health / (float)Boss.MaxHealth : 0f;
                Attacks.SetModifiers(BossOwnerId, BossAttack.Damage(BossShotDamage, fraction), BossAttack.CooldownMultiplier(fraction));
                shooters.Add(new ShooterBody { ClientId = BossOwnerId, Position = Boss.Position, CanFire = Boss.CanBeHit });
            }

            SurvivorTargets.SetBodies(survivorBodies);
            Attacks.Step(dt, shooters, Projectiles);
            Projectiles.Step(dt, Mobs, Boss.Exists ? Boss : null, SurvivorTargets);

            // What happened this step, for the clients (events) and for the experience (kills).
            CombatEvents.Clear();
            Projectiles.DrainEvents(CombatEvents);
            CombatEvents.AddRange(BlastEvents);
            BlastEvents.Clear();
            ShotKills.Clear();
            for (int i = 0; i < CombatEvents.Count; i++)
            {
                CombatEvent e = CombatEvents[i];
                if (e.Kind == CombatEventKind.Hit && e.Killed && !e.SurvivorTarget && e.MobId != BossSim.MobId && !Mobs.WasDeviceKill(e.MobId))
                    ShotKills.Add(e.Position);
            }

            LastCombat = result;
        }

        /// <summary>The latest aim and fire state of a player. Only living survivors and the standing boss can fire.</summary>
        public void SetAim(ulong clientId, ushort angle, bool firing)
        {
            bool canFire = IsBossOwner(clientId) ? Boss.CanBeHit : Survivors.IsAlive(clientId);
            Attacks.SetAim(clientId, angle, firing && canFire);
        }

        /// <summary>
        /// A boss skill request (slot 0 or 1). Only the boss player's request counts, and only while the match runs and the boss stands.
        /// The events of the announcement are appended to <paramref name="events"/>.
        /// </summary>
        public BossSkillResult UseBossSkill(ulong clientId, int slot, ushort aim, List<CombatEvent> events)
        {
            bool fromBoss = IsBossOwner(clientId);
            float fraction = Boss.Exists && Boss.MaxHealth > 0 ? Boss.Health / (float)Boss.MaxHealth : 0f;
            var state = new BossSkillState
            {
                MatchRunning = MatchRunning,
                BossStanding = BossOwnerId != NoBoss && Boss.Exists && Boss.CanBeHit,
                BossPosition = Boss.Position,
                Aim = aim,
                HealthFraction = fraction,
            };
            return BossSkills.TryUse(slot, fromBoss, state, clientId, events);
        }

        /// <summary>The boss player left the match (the server sets it).</summary>
        public bool BossLeft { get; set; }

        /// <summary>True when the latest clock phase gave the match its result.</summary>
        public bool ClockDecided { get; private set; }

        /// <summary>
        /// The clock runs and the match gets its result: the boss falling beats the boss leaving, which beats every survivor falling,
        /// which beats the time running out. A match without a boss (practice) has no clock.
        /// </summary>
        public void StepClock(float dt)
        {
            ClockDecided = false;
            if (!Clock.Enabled) return;

            var input = new MatchInput
            {
                BossFell = Boss.Exists && Boss.HasFallen,
                BossLeft = BossLeft,
                SurvivorsAlive = Survivors.AliveCount,
            };
            ClockDecided = Clock.Step(dt, input);
        }
    }
}
