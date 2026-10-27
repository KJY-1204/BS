using UnityEngine;

namespace BS
{
    public static class CharacterIds
    {
        public const byte None = 0;
        public const byte Reimu = 1;
        public const byte Marisa = 2;
        public const byte Sakuya = 3;
        public const byte Sanae = 4;
    }

    /// <summary>How a projectile flies and how many mobs it can pass through. Straight lines only.</summary>
    public struct ProjectileShape
    {
        public float Speed;
        public float Lifetime;
        public float Radius;
        /// <summary>How many mobs one projectile can hit; 1 stops at the first.</summary>
        public int Pierce;

        public float Range => Speed * Lifetime;

        /// <summary>The shape every projectile had before characters had their own attacks.</summary>
        public static ProjectileShape Default => new ProjectileShape
        {
            Speed = CombatConfig.ProjectileSpeed,
            Lifetime = CombatConfig.ProjectileLifetimeSeconds,
            Radius = CombatConfig.ProjectileRadius,
            Pierce = 1,
        };
    }

    /// <summary>A character's basic attack as data.</summary>
    public struct AttackDef
    {
        public float CooldownSeconds;
        /// <summary>How many shots one attack fires together.</summary>
        public int Shots;
        /// <summary>The total angle the shots are spread over, in degrees, centred on the aim.</summary>
        public float SpreadDegrees;
        /// <summary>Multiplies the survivor's projectile damage.</summary>
        public float DamageMultiplier;
        public ProjectileShape Shape;

        /// <summary>The single straight shot every survivor fired before characters had their own attacks.</summary>
        public static AttackDef Default => new AttackDef
        {
            CooldownSeconds = CombatConfig.FireCooldownSeconds,
            Shots = 1,
            SpreadDegrees = 0f,
            DamageMultiplier = 1f,
            Shape = ProjectileShape.Default,
        };
    }

    /// <summary>One playable character as data. Rules read these values and never the character's identity.</summary>
    public sealed class CharacterDef
    {
        public byte Id;
        public string Name;
        public int MaxHealth;
        public int MaxMana;
        public float ManaRegenPerSecond;
        public float MoveSpeed;
        public Color Color;
        public AttackDef Attack;
        /// <summary>The key of the character's sprite set in the sprite library.</summary>
        public string SpriteId = "";
    }

    /// <summary>The playable characters. Values are starting assumptions, tuned by playtest.</summary>
    public static class CharacterCatalog
    {
        public const int Count = 4;
        /// <summary>The boss has no character yet; its speed lives in <see cref="BossCatalog"/>.</summary>
        public static float BossMoveSpeed => BossCatalog.Current.MoveSpeed;

        static readonly CharacterDef[] defs =
        {
            new CharacterDef { Id = CharacterIds.Reimu, SpriteId = "reimu", Name = "레이무", MaxHealth = 100, MaxMana = 100, ManaRegenPerSecond = 5f, MoveSpeed = 6f, Color = new Color(0.95f, 0.5f, 0.6f),
                Attack = new AttackDef { CooldownSeconds = 0.14f, Shots = 1, SpreadDegrees = 0f, DamageMultiplier = 0.6f, Shape = new ProjectileShape { Speed = 14f, Lifetime = 0.8f, Radius = 0.12f, Pierce = 1 } } },
            new CharacterDef { Id = CharacterIds.Marisa, SpriteId = "marisa", Name = "마리사", MaxHealth = 80, MaxMana = 130, ManaRegenPerSecond = 6f, MoveSpeed = 5.5f, Color = new Color(0.95f, 0.85f, 0.25f),
                Attack = new AttackDef { CooldownSeconds = 0.45f, Shots = 1, SpreadDegrees = 0f, DamageMultiplier = 1.4f, Shape = new ProjectileShape { Speed = 16f, Lifetime = 1.0f, Radius = 0.2f, Pierce = 4 } } },
            new CharacterDef { Id = CharacterIds.Sakuya, SpriteId = "sakuya", Name = "사쿠야", MaxHealth = 90, MaxMana = 90, ManaRegenPerSecond = 5f, MoveSpeed = 7f, Color = new Color(0.4f, 0.55f, 0.95f),
                Attack = new AttackDef { CooldownSeconds = 0.5f, Shots = 5, SpreadDegrees = 40f, DamageMultiplier = 0.5f, Shape = new ProjectileShape { Speed = 18f, Lifetime = 0.45f, Radius = 0.12f, Pierce = 1 } } },
            new CharacterDef { Id = CharacterIds.Sanae, SpriteId = "sanae", Name = "사나에", MaxHealth = 120, MaxMana = 110, ManaRegenPerSecond = 5f, MoveSpeed = 5.5f, Color = new Color(0.3f, 0.8f, 0.5f),
                Attack = new AttackDef { CooldownSeconds = 0.7f, Shots = 1, SpreadDegrees = 0f, DamageMultiplier = 2.2f, Shape = new ProjectileShape { Speed = 7f, Lifetime = 1.6f, Radius = 0.45f, Pierce = 1 } } },
        };

        public static System.Collections.Generic.IReadOnlyList<CharacterDef> All => defs;

        /// <summary>The character with this id, or null for none or an unknown id.</summary>
        public static CharacterDef Get(byte id)
        {
            for (int i = 0; i < defs.Length; i++)
                if (defs[i].Id == id) return defs[i];
            return null;
        }

        public static bool IsValid(byte id) => Get(id) != null;
    }
}
