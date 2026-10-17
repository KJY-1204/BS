using System.Collections.Generic;

namespace BS
{
    public static class SkillIds
    {
        public const byte Power = 1;
        public const byte Haste = 2;
        public const byte Vitality = 3;
        public const byte Focus = 4;
        /// <summary>Reimu, Q: area burst around her.</summary>
        public const byte Burst = 5;
        /// <summary>Marisa, E: Stardust, a faster basic attack for a time.</summary>
        public const byte RapidFire = 6;
        /// <summary>Reimu, E: Kekkai, invulnerable for a time.</summary>
        public const byte Barrier = 7;
        /// <summary>Marisa, Q: Master Spark, a beam along her aim.</summary>
        public const byte MasterSpark = 8;
        /// <summary>Sakuya, Q: Time Stop, every mob stops for a time.</summary>
        public const byte TimeStop = 9;
        /// <summary>Sakuya, E: Knife Barrage, a ring of knives.</summary>
        public const byte KnifeBarrage = 10;
        /// <summary>Sanae, Q: Miracle, heals her and nearby survivors.</summary>
        public const byte Miracle = 11;
        /// <summary>Sanae, E: Wind Veil, pushes nearby mobs away.</summary>
        public const byte WindVeil = 12;
    }

    /// <summary>What an active skill does when the server fires it. Passives have <see cref="None"/>.</summary>
    public enum SkillEffectKind : byte
    {
        None = 0,
        /// <summary>Damages every mob within a radius of the caster.</summary>
        AreaDamage,
        /// <summary>The caster takes no damage for a time.</summary>
        Barrier,
        /// <summary>Damages every mob on a line along the caster's aim.</summary>
        Beam,
        /// <summary>A shorter basic attack interval for a time.</summary>
        AttackSpeed,
        /// <summary>Every mob stops for a time.</summary>
        TimeStop,
        /// <summary>Fires a ring of projectiles in the shape of the caster's basic shots.</summary>
        Volley,
        /// <summary>Heals the caster and the living survivors within a radius.</summary>
        Heal,
        /// <summary>Pushes the mobs within a radius away from the caster.</summary>
        Push,
    }

    /// <summary>One skill as data. The selection rules only read this, never the skill's identity.</summary>
    public sealed class SkillDef
    {
        public byte Id;
        public string Name;
        public bool IsActive;
        /// <summary>Key slot for an active skill (0 = Q, 1 = E), -1 for passives.</summary>
        public int Slot = -1;
        /// <summary>The character that owns an active skill; 0 means common to every character (the passives).</summary>
        public byte Character;
        public SkillEffectKind Effect;
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
        /// <summary>Beam length.</summary>
        public float[] Length;
        /// <summary>Beam half width.</summary>
        public float[] HalfWidth;
        /// <summary>Number of projectiles in a ring.</summary>
        public int[] Count;
        /// <summary>How far mobs are pushed.</summary>
        public float[] Distance;
        /// <summary>Health restored.</summary>
        public int[] Amount;

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
                Id = SkillIds.Burst, Name = "몽상봉인", IsActive = true, Slot = 0, Character = CharacterIds.Reimu, Effect = SkillEffectKind.AreaDamage, MaxRank = 3,
                Radius = new[] { 2.5f, 3.0f, 3.5f }, Damage = new[] { 20, 30, 40 },
                ManaCost = 30, Cooldown = 8f, Description = "주변 몹에게 피해 (Q)",
            },
            new SkillDef
            {
                Id = SkillIds.Barrier, Name = "결계", IsActive = true, Slot = 1, Character = CharacterIds.Reimu, Effect = SkillEffectKind.Barrier, MaxRank = 3,
                Duration = new[] { 1.5f, 2.0f, 2.5f },
                ManaCost = 30, Cooldown = 14f, Description = "잠시 피해를 받지 않음 (E)",
            },
            new SkillDef
            {
                Id = SkillIds.MasterSpark, Name = "마스터 스파크", IsActive = true, Slot = 0, Character = CharacterIds.Marisa, Effect = SkillEffectKind.Beam, MaxRank = 3,
                Length = new[] { 12f, 12f, 12f }, HalfWidth = new[] { 0.6f, 0.7f, 0.8f }, Damage = new[] { 40, 60, 80 },
                ManaCost = 40, Cooldown = 10f, Description = "조준 방향 직선의 모든 몹에게 피해 (Q)",
            },
            new SkillDef
            {
                Id = SkillIds.RapidFire, Name = "스타더스트", IsActive = true, Slot = 1, Character = CharacterIds.Marisa, Effect = SkillEffectKind.AttackSpeed, MaxRank = 3,
                RequiresSkill = SkillIds.Haste, RequiresRank = 1,
                Duration = new[] { 4f, 5f, 6f }, CooldownMultiplier = new[] { 0.6f, 0.5f, 0.4f },
                ManaCost = 25, Cooldown = 12f, Description = "잠시 발사 속도 증가 (E)",
            },
            new SkillDef
            {
                Id = SkillIds.TimeStop, Name = "시간 정지", IsActive = true, Slot = 0, Character = CharacterIds.Sakuya, Effect = SkillEffectKind.TimeStop, MaxRank = 3,
                Duration = new[] { 2.0f, 2.5f, 3.0f },
                ManaCost = 45, Cooldown = 20f, Description = "잠시 모든 몹이 멈춤 (Q)",
            },
            new SkillDef
            {
                Id = SkillIds.KnifeBarrage, Name = "나이프 난사", IsActive = true, Slot = 1, Character = CharacterIds.Sakuya, Effect = SkillEffectKind.Volley, MaxRank = 3,
                Count = new[] { 12, 16, 20 }, Damage = new[] { 10, 12, 14 },
                ManaCost = 30, Cooldown = 9f, Description = "사방으로 나이프 발사 (E)",
            },
            new SkillDef
            {
                Id = SkillIds.Miracle, Name = "기적", IsActive = true, Slot = 0, Character = CharacterIds.Sanae, Effect = SkillEffectKind.Heal, MaxRank = 3,
                Radius = new[] { 4f, 4f, 4f }, Amount = new[] { 30, 45, 60 },
                ManaCost = 35, Cooldown = 12f, Description = "자신과 가까운 생존자 회복 (Q)",
            },
            new SkillDef
            {
                Id = SkillIds.WindVeil, Name = "바람의 장막", IsActive = true, Slot = 1, Character = CharacterIds.Sanae, Effect = SkillEffectKind.Push, MaxRank = 3,
                Radius = new[] { 4f, 4f, 4f }, Distance = new[] { 3f, 4f, 5f },
                ManaCost = 25, Cooldown = 10f, Description = "주변 몹을 밀어냄 (E)",
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

        /// <summary>The active skill a character has on a key slot, or null (also for no character).</summary>
        public static SkillDef ForSlot(int slot, byte character)
        {
            if (character == CharacterIds.None) return null;
            for (int i = 0; i < defs.Length; i++)
                if (defs[i].IsActive && defs[i].Slot == slot && defs[i].Character == character) return defs[i];
            return null;
        }

        /// <summary>Common skills belong to everyone; any other skill belongs to one character.</summary>
        public static bool IsOwnedBy(SkillDef def, byte character) =>
            def != null && (def.Character == CharacterIds.None || def.Character == character);

        /// <summary>
        /// True when the skill may be offered to a survivor of this character: it is theirs or common, its
        /// prerequisite is met and it is not at its maximum rank.
        /// </summary>
        public static bool IsAvailable(SkillDef def, IReadOnlyList<int> ranks, byte character)
        {
            if (def == null) return false;
            if (!IsOwnedBy(def, character)) return false;
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
