using System.Collections.Generic;

namespace BS
{
    public static class SkillIds
    {
        public const byte Power = 1;
        public const byte Haste = 2;
        public const byte Vitality = 3;
        public const byte Focus = 4;
        public const byte Burst = 5;
        public const byte RapidFire = 6;
    }

    /// <summary>One skill as data. The selection rules only read this, never the skill's identity.</summary>
    public sealed class SkillDef
    {
        public byte Id;
        public string Name;
        public bool IsActive;
        /// <summary>Key slot for an active skill (0 = Q, 1 = E), -1 for passives.</summary>
        public int Slot = -1;
        public int MaxRank;
        /// <summary>Skill that must reach <see cref="RequiresRank"/> first, or 0 for none.</summary>
        public byte RequiresSkill;
        public int RequiresRank;
        public string Description;

        // Per-rank values (index = rank - 1). Unused ones stay null.
        public float[] Radius;
        public int[] Damage;
        public float[] Duration;
        public float[] CooldownMultiplier;

        public int ManaCost;
        public float Cooldown;
    }

    /// <summary>The temporary skill tree shared by every character in this version.</summary>
    public static class SkillCatalog
    {
        public const int SkillSlots = 2;
        /// <summary>Ranks are packed four bits per skill id into one 64 bit value.</summary>
        public const int MaxSkillId = 15;
        public const int MaxPackedRank = 15;

        // Passive effects per rank
        public const int PowerDamagePerRank = 2;
        public const float HasteCooldownPerRank = 0.08f;
        public const int VitalityHealthPerRank = 15;
        public const int FocusManaPerRank = 15;
        public const float FocusRegenPerRank = 1f;

        static readonly SkillDef[] defs =
        {
            new SkillDef { Id = SkillIds.Power, Name = "힘", MaxRank = 5, Description = "투사체 피해 +2" },
            new SkillDef { Id = SkillIds.Haste, Name = "기민", MaxRank = 5, Description = "발사 쿨타임 -8%" },
            new SkillDef { Id = SkillIds.Vitality, Name = "활력", MaxRank = 5, Description = "최대 체력 +15" },
            new SkillDef { Id = SkillIds.Focus, Name = "집중", MaxRank = 5, Description = "최대 마나 +15, 회복 +1/초" },
            new SkillDef
            {
                Id = SkillIds.Burst, Name = "폭발", IsActive = true, Slot = 0, MaxRank = 3,
                Radius = new[] { 2.5f, 3.0f, 3.5f }, Damage = new[] { 20, 30, 40 },
                ManaCost = 30, Cooldown = 8f, Description = "주변 몹에게 피해 (Q)",
            },
            new SkillDef
            {
                Id = SkillIds.RapidFire, Name = "연사", IsActive = true, Slot = 1, MaxRank = 3,
                RequiresSkill = SkillIds.Haste, RequiresRank = 1,
                Duration = new[] { 4f, 5f, 6f }, CooldownMultiplier = new[] { 0.6f, 0.5f, 0.4f },
                ManaCost = 25, Cooldown = 12f, Description = "잠시 발사 속도 증가 (E)",
            },
        };

        static readonly Dictionary<byte, SkillDef> byId = BuildIndex();

        static Dictionary<byte, SkillDef> BuildIndex()
        {
            var map = new Dictionary<byte, SkillDef>();
            foreach (SkillDef def in defs) map[def.Id] = def;
            return map;
        }

        public static IReadOnlyList<SkillDef> All => defs;

        public static SkillDef Get(byte id) => byId.TryGetValue(id, out SkillDef def) ? def : null;

        public static SkillDef ForSlot(int slot)
        {
            for (int i = 0; i < defs.Length; i++)
                if (defs[i].IsActive && defs[i].Slot == slot) return defs[i];
            return null;
        }

        /// <summary>True when the skill may be offered: prerequisite met and not at its maximum rank.</summary>
        public static bool IsAvailable(SkillDef def, IReadOnlyList<int> ranks)
        {
            if (def == null) return false;
            if (RankOf(ranks, def.Id) >= def.MaxRank) return false;
            if (def.RequiresSkill != 0 && RankOf(ranks, def.RequiresSkill) < def.RequiresRank) return false;
            return true;
        }

        public static int RankOf(IReadOnlyList<int> ranks, byte skillId) =>
            skillId < ranks.Count ? ranks[skillId] : 0;

        public static ulong Pack(IReadOnlyList<int> ranks)
        {
            ulong packed = 0;
            for (int id = 1; id <= MaxSkillId && id < ranks.Count; id++)
                packed |= (ulong)(ranks[id] & MaxPackedRank) << (id * 4);
            return packed;
        }

        public static void Unpack(ulong packed, int[] into)
        {
            for (int id = 0; id < into.Length; id++)
                into[id] = id >= 1 && id <= MaxSkillId ? (int)((packed >> (id * 4)) & MaxPackedRank) : 0;
        }
    }
}
