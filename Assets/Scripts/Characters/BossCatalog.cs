using UnityEngine;

namespace BS
{
    /// <summary>What a boss skill does when its warning has passed.</summary>
    public enum BossSkillEffect : byte
    {
        /// <summary>A ring of the boss's own projectiles around it.</summary>
        ShotRing = 0,
        /// <summary>Hurts the survivors within a radius of where it was used.</summary>
        Shockwave = 1,
        /// <summary>Hurts the survivors on a straight line along the aim the boss had.</summary>
        Beam = 2,
        /// <summary>Summons mobs around the boss.</summary>
        Roar = 3,
    }

    /// <summary>One boss skill as data. Values are starting assumptions, tuned by playtest.</summary>
    public sealed class BossSkillDef
    {
        public string Name;
        public string Key;
        public BossSkillEffect Effect;
        public float CooldownSeconds;
        public int Damage;
        /// <summary>How long the skill is announced before it takes effect.</summary>
        public float WarningSeconds = BossSkills.WarningSeconds;
        /// <summary>The shots of a ring, or the mobs of a roar.</summary>
        public int Count;
        /// <summary>The radius of a shockwave.</summary>
        public float Radius;
        /// <summary>The length of a beam.</summary>
        public float Length;
        /// <summary>Half the width of a beam.</summary>
        public float HalfWidth;
    }

    /// <summary>The boss's basic attack as data: a single straight shot.</summary>
    public sealed class BossAttackDef
    {
        public int BaseDamage;
        public float CooldownSeconds;
        public ProjectileShape Shape;

        public AttackDef ToAttackDef() => new AttackDef
        {
            CooldownSeconds = CooldownSeconds,
            Shots = 1,
            SpreadDegrees = 0f,
            DamageMultiplier = 1f,
            Shape = Shape,
        };
    }

    /// <summary>One kind of boss as data. Values are starting assumptions, tuned by playtest.</summary>
    public sealed class BossDef
    {
        /// <summary>The kind number (1 standard, 2 giant, 3 sniper); 0 for a definition that is no kind (tests).</summary>
        public byte Id;
        public string Name;
        public int MaxHealth;
        public float MoveSpeed;
        public float Radius;
        public int ContactDamage;
        public Color Color;
        /// <summary>How long a match with this boss lasts before the boss wins on time.</summary>
        public float MatchSeconds;
        public BossAttackDef Attack;
        /// <summary>The skills of the Q and E keys.</summary>
        public BossSkillDef[] Skills;

        /// <summary>The first skill (of the kind's skills) that has this effect, or null.</summary>
        public BossSkillDef SkillWithEffect(BossSkillEffect effect)
        {
            if (Skills == null) return null;
            for (int i = 0; i < Skills.Length; i++) if (Skills[i].Effect == effect) return Skills[i];
            return null;
        }
    }

    /// <summary>
    /// The boss kinds. Their health and speed were set with the match harness so that a match with a boss lasts for minutes and is
    /// decided late: every boss is slower than every survivor (a boss that is as fast catches one and the survivors fall within a minute),
    /// and has far more health than a survivor. The order by speed is giant, standard, sniper.
    /// </summary>
    public static class BossCatalog
    {
        public const byte Standard = 1;
        public const byte Giant = 2;
        public const byte Sniper = 3;

        /// <summary>How long a match lasts for every kind.</summary>
        const float DefaultMatchSeconds = 900f;

        static readonly BossDef[] kinds =
        {
            new BossDef
            {
                Id = Standard, Name = "보스", MaxHealth = 190000, MoveSpeed = 4.5f, Radius = 0.9f, ContactDamage = 25,
                Color = new Color(0.85f, 0.15f, 0.15f), MatchSeconds = DefaultMatchSeconds,
                Attack = new BossAttackDef { BaseDamage = BossAttack.BaseDamage, CooldownSeconds = BossAttack.CooldownSeconds, Shape = BossAttack.Shape },
                Skills = new[]
                {
                    new BossSkillDef
                    {
                        Name = BossSkills.Names[0], Key = BossSkills.Keys[0], Effect = BossSkillEffect.ShotRing,
                        CooldownSeconds = BossSkills.BarrageCooldownSeconds, Damage = BossSkills.BarrageDamage, Count = BossSkills.BarrageShots,
                    },
                    new BossSkillDef
                    {
                        Name = BossSkills.Names[1], Key = BossSkills.Keys[1], Effect = BossSkillEffect.Shockwave,
                        CooldownSeconds = BossSkills.ShockwaveCooldownSeconds, Damage = BossSkills.ShockwaveDamage, Radius = BossSkills.ShockwaveRadius,
                    },
                },
            },
            new BossDef
            {
                Id = Giant, Name = "거인", MaxHealth = 230000, MoveSpeed = 4.0f, Radius = 1.3f, ContactDamage = 35,
                Color = new Color(0.55f, 0.30f, 0.10f), MatchSeconds = DefaultMatchSeconds,
                Attack = new BossAttackDef
                {
                    BaseDamage = 20, CooldownSeconds = 1.1f,
                    Shape = new ProjectileShape { Speed = 9f, Lifetime = 1.8f, Radius = 0.4f, Pierce = 1 },
                },
                Skills = new[]
                {
                    new BossSkillDef { Name = "대지 강타", Key = "Q", Effect = BossSkillEffect.Shockwave, CooldownSeconds = 7f, Damage = 35, Radius = 5.5f },
                    new BossSkillDef { Name = "포효", Key = "E", Effect = BossSkillEffect.Roar, CooldownSeconds = 14f, WarningSeconds = 1.0f, Count = 6 },
                },
            },
            new BossDef
            {
                Id = Sniper, Name = "저격수", MaxHealth = 170000, MoveSpeed = 5.0f, Radius = 0.7f, ContactDamage = 20,
                Color = new Color(0.55f, 0.25f, 0.85f), MatchSeconds = DefaultMatchSeconds,
                Attack = new BossAttackDef
                {
                    BaseDamage = 5, CooldownSeconds = 0.5f,
                    Shape = new ProjectileShape { Speed = 14f, Lifetime = 1.4f, Radius = 0.25f, Pierce = 1 },
                },
                Skills = new[]
                {
                    new BossSkillDef { Name = "탄막", Key = "Q", Effect = BossSkillEffect.ShotRing, CooldownSeconds = 9f, Damage = 10, Count = 16 },
                    new BossSkillDef
                    {
                        Name = "저격", Key = "E", Effect = BossSkillEffect.Beam, CooldownSeconds = 12f, Damage = 30, WarningSeconds = 1.2f,
                        Length = 24f, HalfWidth = 0.5f,
                    },
                },
            },
        };

        public static System.Collections.Generic.IReadOnlyList<BossDef> All => kinds;

        public static bool IsValid(byte kind) => kind >= 1 && kind <= kinds.Length;

        /// <summary>The kind with this number; a number that is no kind gives the standard kind.</summary>
        public static BossDef Get(byte kind) => kinds[IsValid(kind) ? kind - 1 : 0];

        /// <summary>The standard kind.</summary>
        public static BossDef Default => kinds[0];

        static byte currentKind = Standard;

        /// <summary>
        /// The kind of the match being played (or about to be): the server sets it when it starts a match and every client
        /// when it learns it. Standard until a match says otherwise.
        /// </summary>
        public static byte CurrentKind => currentKind;

        public static void SetCurrentKind(byte kind) => currentKind = IsValid(kind) ? kind : Standard;

        /// <summary>The kind of the current match.</summary>
        public static BossDef Current => Get(currentKind);

        /// <summary>
        /// The definition of the current match's kind with the development overrides applied (-bsBossHealth, -bsMatchSeconds). A copy,
        /// so changing it never changes the shared data.
        /// </summary>
        public static BossDef ForMatch() => ForMatch(currentKind);

        public static BossDef ForMatch(byte kind)
        {
            BossDef source = Get(kind);
            return new BossDef
            {
                Id = source.Id,
                Name = source.Name,
                MaxHealth = DevArgs.BossHealth > 0 ? DevArgs.BossHealth : source.MaxHealth,
                MoveSpeed = source.MoveSpeed,
                Radius = source.Radius,
                ContactDamage = source.ContactDamage,
                Color = source.Color,
                MatchSeconds = DevArgs.MatchSeconds > 0f ? DevArgs.MatchSeconds : source.MatchSeconds,
                Attack = source.Attack,
                Skills = source.Skills,
            };
        }
    }
}
