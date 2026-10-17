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
        /// <summary>Reserved for the art step; empty until sprites exist.</summary>
        public string SpriteId = "";
    }

    /// <summary>The playable characters. Values are starting assumptions, tuned by playtest.</summary>
    public static class CharacterCatalog
    {
        public const int Count = 4;
        /// <summary>The boss has no character yet and keeps this speed.</summary>
        public const float BossMoveSpeed = 6f;

        static readonly CharacterDef[] defs =
        {
            new CharacterDef { Id = CharacterIds.Reimu, Name = "레이무", MaxHealth = 100, MaxMana = 100, ManaRegenPerSecond = 5f, MoveSpeed = 6f, Color = new Color(0.95f, 0.5f, 0.6f) },
            new CharacterDef { Id = CharacterIds.Marisa, Name = "마리사", MaxHealth = 80, MaxMana = 130, ManaRegenPerSecond = 6f, MoveSpeed = 5.5f, Color = new Color(0.95f, 0.85f, 0.25f) },
            new CharacterDef { Id = CharacterIds.Sakuya, Name = "사쿠야", MaxHealth = 90, MaxMana = 90, ManaRegenPerSecond = 5f, MoveSpeed = 7f, Color = new Color(0.4f, 0.55f, 0.95f) },
            new CharacterDef { Id = CharacterIds.Sanae, Name = "사나에", MaxHealth = 120, MaxMana = 110, ManaRegenPerSecond = 5f, MoveSpeed = 5.5f, Color = new Color(0.3f, 0.8f, 0.5f) },
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
