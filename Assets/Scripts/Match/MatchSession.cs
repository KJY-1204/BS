using System;
using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>What plays a match: called once per step to say what each character does. The bots are controllers.</summary>
    public interface IMatchController
    {
        void Decide(MatchSession session, float dt);
    }

    /// <summary>A controller that does nothing: every character stands still (tests).</summary>
    public sealed class IdleController : IMatchController
    {
        public void Decide(MatchSession session, float dt) { }
    }

    /// <summary>
    /// One match of the harness: a <see cref="MatchWorld"/> set up the way the server sets up a match (the boss and the survivors at their
    /// spawn points, growth started, the mobs and summon at their starting values), the bodies' movement, and the commands a controller
    /// gives. Plain code: no scene, no network, no clock.
    /// </summary>
    public sealed class MatchSession
    {
        /// <summary>The client id the boss player has in a harness match; the survivors have their role (1 to 4).</summary>
        public const ulong BossId = 10;

        public MatchWorld World { get; }
        public HarnessSettings Settings { get; }
        public int Seed { get; }
        /// <summary>The boss's data for this match: a copy of the catalog's with the settings' overrides applied.</summary>
        public BossDef BossData { get; }
        public float Step { get; }
        /// <summary>Seconds played so far.</summary>
        public float Time { get; private set; }

        readonly List<ulong> survivorIds = new List<ulong>();
        readonly Dictionary<ulong, Vector2> directions = new Dictionary<ulong, Vector2>();
        readonly Dictionary<ulong, float> speeds = new Dictionary<ulong, float>();
        readonly List<SurvivorInfo> infos = new List<SurvivorInfo>();

        public IReadOnlyList<ulong> SurvivorIds => survivorIds;

        MatchSession(HarnessSettings settings, int seed)
        {
            Settings = settings;
            Seed = seed;
            Step = settings.step;

            BossData = CopyOfBoss((byte)settings.bossKind);
            HarnessOverrides.ApplyToBoss(settings.overrides, BossData);

            World = new MatchWorld(new MatchWorldOptions
            {
                MobTarget = MobConfig.BossMatchAutoTarget,
                MobSeed = Mix(seed, 7, 1),
                SummonSeed = Mix(seed, 13, 2),
                Random = new System.Random(Mix(seed, 31, 3)),
            });
        }

        static int Mix(int seed, int factor, int offset) => unchecked(seed * factor + offset);

        /// <summary>The boss's catalog data copied deep enough that changing a value never touches the catalog.</summary>
        static BossDef CopyOfBoss(byte kind)
        {
            BossDef source = BossCatalog.Get(kind);
            var skills = new BossSkillDef[source.Skills.Length];
            for (int i = 0; i < skills.Length; i++) skills[i] = source.Skills[i].Clone();

            return new BossDef
            {
                Id = source.Id, Name = source.Name, MaxHealth = source.MaxHealth, MoveSpeed = source.MoveSpeed, Radius = source.Radius,
                ContactDamage = source.ContactDamage, Color = source.Color, MatchSeconds = source.MatchSeconds,
                Attack = new BossAttackDef { BaseDamage = source.Attack.BaseDamage, CooldownSeconds = source.Attack.CooldownSeconds, Shape = source.Attack.Shape },
                Skills = skills,
                PhaseSkill = source.PhaseSkill?.Clone(),
                PhaseBurst = source.PhaseBurst?.Clone(),
            };
        }

        /// <summary>
        /// Sets up a match the way the server does when it starts one (see RoleTable.StartMatch and the servers' registration): the boss
        /// registered, each survivor registered with growth, the mobs and the summon energy at their starting values, the clock started.
        /// The caller restores <see cref="BossCatalog.CurrentKind"/>; this sets it for the match.
        /// </summary>
        public static MatchSession Start(HarnessSettings settings, int seed)
        {
            string problem = settings.Validate();
            if (problem != null) throw new ArgumentException(problem);

            BossCatalog.SetCurrentKind((byte)settings.bossKind);
            var session = new MatchSession(settings, seed);
            session.SetUp();
            return session;
        }

        void SetUp()
        {
            MatchWorld w = World;
            BossDef def = BossData;

            // Values that survivors take when they are registered (the health bonus, the experience) are in place first.
            HarnessOverrides.ApplyToWorld(Settings.overrides, w);

            // The boss
            Vector2 bossSpawn = ArenaConfig.SpawnPoint(RoleTable.BossRole);
            w.Boss.Reset(def, bossSpawn);
            w.Attacks.Register(BossId, def.Attack.ToAttackDef(), true);
            w.Slots[BossId] = RoleTable.BossRole;
            w.BossSkills.Reset(def);
            w.BossOwnerId = BossId;
            w.BossShotDamage = def.Attack.BaseDamage;
            w.Bodies.Add(new MatchBody { ClientId = BossId, Role = RoleTable.BossRole, Position = bossSpawn, Alive = true });
            speeds[BossId] = def.MoveSpeed;
            directions[BossId] = Vector2.zero;

            // The survivors: roles 1 to 4 in the order of the characters
            for (int i = 0; i < Settings.characters.Length; i++)
            {
                byte role = (byte)(i + 1);
                byte character = (byte)Settings.characters[i];
                CharacterDef cd = CharacterCatalog.Get(character);
                ulong id = role;

                // The development health bonus is part of the starting health, as when the server registers a survivor.
                w.Survivors.Register(id, (cd != null ? cd.MaxHealth : CombatConfig.SurvivorMaxHealth) + w.SurvivorHealthBonus);
                w.Attacks.Register(id, cd?.Attack ?? AttackDef.Default);
                w.Slots[id] = role;
                w.StartGrowth(id, character, granted => w.GrantStartSkills(granted, character));

                Vector2 spawn = ArenaConfig.SpawnPoint(role);
                w.Bodies.Add(new MatchBody { ClientId = id, Role = role, Character = character, Position = spawn, Alive = true });
                survivorIds.Add(id);
                speeds[id] = cd != null ? cd.MoveSpeed : 6f;
                directions[id] = Vector2.zero;
            }

            // The match values a match with a boss starts with
            w.Mobs.SpawnPerSecond = MobConfig.BossMatchSpawnPerSecond;
            w.Mobs.TargetCount = MobConfig.BossMatchAutoTarget;
            w.Summon.Reset();
            w.Clock.Start(true, def.MatchSeconds);

            HarnessOverrides.ApplyToWorld(Settings.overrides, w); // again: the match values above replaced some of them
            w.Summon.Reset(); // the energy starts at the value the overrides left
            w.MatchRunning = true;
        }

        // ---- What a controller can see ----

        public bool IsAlive(ulong id) => id == BossId ? World.Boss.CanBeHit : World.Survivors.IsAlive(id);

        public Vector2 PositionOf(ulong id)
        {
            for (int i = 0; i < World.Bodies.Count; i++)
                if (World.Bodies[i].ClientId == id) return World.Bodies[i].Position;
            return Vector2.zero;
        }

        public byte CharacterOf(ulong id) => World.Leveling.CharacterOf(id);

        /// <summary>The direction a character was last told to walk in (tests and logs).</summary>
        public Vector2 DirectionOf(ulong id) => directions.TryGetValue(id, out Vector2 d) ? d : Vector2.zero;

        public float SpeedOf(ulong id) => speeds.TryGetValue(id, out float s) ? s : 0f;

        // ---- What a controller can do ----

        /// <summary>The direction (and strength up to 1) a character walks in this step.</summary>
        public void Move(ulong id, Vector2 direction) => directions[id] = Vector2.ClampMagnitude(direction, 1f);

        /// <summary>Where a character aims and whether it holds the fire button.</summary>
        public void Aim(ulong id, ushort angle, bool firing) => World.SetAim(id, angle, firing);

        /// <summary>A survivor presses the key of a skill slot (Q is 0, E is 1).</summary>
        public bool UseSkill(ulong survivorId, int slot) => World.RequestSkill(survivorId, slot);

        /// <summary>The boss presses a skill key, aiming at <paramref name="aim"/>.</summary>
        public BossSkillResult UseBossSkill(int slot, ushort aim)
        {
            return World.UseBossSkill(BossId, slot, aim, new List<CombatEvent>());
        }

        SummonContext MakeContext()
        {
            infos.Clear();
            for (int i = 0; i < World.Bodies.Count; i++)
            {
                MatchBody b = World.Bodies[i];
                if (b.Role == RoleTable.BossRole) continue;
                infos.Add(new SurvivorInfo { ClientId = b.ClientId, Position = b.Position, Alive = b.Alive });
            }
            return new SummonContext
            {
                Mobs = World.Mobs,
                BossPosition = PositionOf(BossId),
                BossStanding = World.Boss.Exists && World.Boss.CanBeHit,
                MatchRunning = World.MatchRunning,
                Survivors = infos,
            };
        }

        /// <summary>The boss summons a group of a mob kind.</summary>
        public SummonResult Summon(byte kind) => World.Summon.Summon(MakeContext(), true, kind, out _);

        /// <summary>The boss places a device where it stands.</summary>
        public SummonResult PlaceDevice() => World.Summon.PlaceDevice(MakeContext(), true);

        // ---- Time ----

        /// <summary>Moves the bodies by what the controller said, runs one step of the world and returns whether the match goes on.</summary>
        public bool Advance()
        {
            for (int i = 0; i < World.Bodies.Count; i++)
            {
                MatchBody b = World.Bodies[i];
                bool alive = b.Role == RoleTable.BossRole ? World.Boss.CanBeHit : World.Survivors.IsAlive(b.ClientId);
                Vector2 dir = alive ? directions[b.ClientId] : Vector2.zero;
                b.Alive = b.Role == RoleTable.BossRole || alive;
                b.Moving = dir.sqrMagnitude > 1e-4f;
                if (b.Moving) b.Position = ArenaConfig.ClampCharacter(b.Position + dir * (speeds[b.ClientId] * Step));
                World.Bodies[i] = b;
            }

            World.Step(Step);
            Time += Step;
            return World.MatchRunning;
        }
    }
}
