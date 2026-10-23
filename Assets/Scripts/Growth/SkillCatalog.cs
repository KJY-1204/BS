using System;
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
        /// <summary>Reimu's own passive: her skills cost less mana.</summary>
        public const byte SpiritSaving = 13;
        /// <summary>Marisa's own passive: her skills hit and heal harder.</summary>
        public const byte FirePower = 14;
        /// <summary>Sakuya's own passive: her skills cool down sooner.</summary>
        public const byte TimeSense = 15;
        /// <summary>Sanae's own passive: her mana comes back faster.</summary>
        public const byte Faith = 16;
        /// <summary>Reimu's second own passive: her timed skills last longer.</summary>
        public const byte BarrierExtension = 17;
        /// <summary>Sanae's second own passive: her skills reach farther.</summary>
        public const byte WideWind = 18;
        /// <summary>Marisa's second own passive: her beam reaches farther and is wider.</summary>
        public const byte CritMagic = 19;
        /// <summary>Sakuya's second own passive: her time stop lasts longer.</summary>
        public const byte TimeRule = 20;
        /// <summary>Reimu's third own passive (needs Kekkai): she takes less damage.</summary>
        public const byte Guard = 21;
        /// <summary>Reimu's fourth own passive (needs Burst at rank three): her skills hit and heal harder.</summary>
        public const byte SealingArray = 22;
        /// <summary>Marisa's third own passive (needs Stardust): her basic attack hits harder.</summary>
        public const byte StarBullet = 23;
        /// <summary>Marisa's fourth own passive (needs Master Spark at rank three): her mana comes back faster.</summary>
        public const byte ManaCycle = 24;
        /// <summary>Sakuya's third own passive (needs Knife Barrage): her skills hit harder.</summary>
        public const byte PreciseThrow = 25;
        /// <summary>Sakuya's fourth own passive (needs Time Stop at rank three): her skills cost less mana.</summary>
        public const byte ElegantSaving = 26;
        /// <summary>Sanae's third own passive (needs Wind Veil): she receives more experience.</summary>
        public const byte Merit = 27;
        /// <summary>Sanae's fourth own passive (needs Miracle at rank three): her skills hit and heal harder.</summary>
        public const byte DivineBlessing = 28;
        /// <summary>Reimu, R: a large area burst (needs Burst at rank two).</summary>
        public const byte FantasyHeaven = 29;
        /// <summary>Marisa, R: a long, wide beam (needs Master Spark at rank two).</summary>
        public const byte FinalSpark = 30;
        /// <summary>Sakuya, R: a large ring of knives (needs Time Stop at rank two).</summary>
        public const byte KillingDoll = 31;
        /// <summary>Sanae, R: a wide, strong heal (needs Miracle at rank two).</summary>
        public const byte GreatMiracle = 32;
    }

    /// <summary>What a character's own passive does. The common passives are worked out by <see cref="StatCalc"/> and have none of these.</summary>
    public enum PassiveEffectKind : byte
    {
        None = 0,
        /// <summary>The mana cost of the character's active skills falls.</summary>
        SkillManaCost,
        /// <summary>The damage and healing of the character's active skills rise.</summary>
        SkillPower,
        /// <summary>The cooldown of the character's active skills falls.</summary>
        SkillCooldown,
        /// <summary>Mana recovery per second rises.</summary>
        ManaRegen,
        /// <summary>The radius, length, width and push distance of the character's active skills grow.</summary>
        SkillRange,
        /// <summary>How long the character's timed skills last grows.</summary>
        SkillDuration,
        /// <summary>The damage the survivor takes falls.</summary>
        DamageTaken,
        /// <summary>The damage of the survivor's basic attack rises.</summary>
        BasicDamage,
        /// <summary>The experience the survivor receives from each kill rises.</summary>
        ExperienceGain,
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
        /// <summary>Key slot for an active skill (0 = Q, 1 = E, 2 = R), -1 for passives.</summary>
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

        /// <summary>For a character's own passive: what it changes and by how much per rank (a fraction, or mana per second for mana recovery).</summary>
        public PassiveEffectKind Passive;
        public float PassivePerRank;
    }

    /// <summary>The temporary skill tree shared by every character in this version.</summary>
    public static class SkillCatalog
    {
        public const int SkillSlots = 3;
        /// <summary>The slot of the R skills, which need the character's Q skill at <see cref="UltimateRequiresQRank"/> first.</summary>
        public const int UltimateSlot = 2;
        public const int UltimateRequiresQRank = 2;
        /// <summary>Ranks are packed three bits per skill id into 64 bit values, 21 ids to a value (ids 1 to 21 in the low one, 22 to 42 in the high one).</summary>
        public const int MaxSkillId = 42;
        /// <summary>How many skill ids one packed 64 bit value holds.</summary>
        public const int IdsPerPackedValue = 21;
        /// <summary>Ranks are packed three bits per skill, so a skill's maximum rank can be at most this.</summary>
        public const int MaxPackedRank = 7;
        const int PackBits = 3;

        /// <summary>A scale of 100 percent leaves a skill's range or duration as defined.</summary>
        public const int NormalScalePercent = 100;
        /// <summary>The largest scale the passives can give; it is sent in one byte.</summary>
        public const int MaxScalePercent = 250;

        /// <summary>However much cooldown shortening a survivor has, a skill's cooldown never falls below this share of its normal value.</summary>
        public const float MinSkillCooldownMultiplier = 0.4f;
        /// <summary>However much cost reduction a survivor has, a skill never costs less than this share removed (and never less than 1 mana).</summary>
        public const float MaxManaCostReduction = 0.9f;

        // Passive effects per rank
        public const int PowerDamagePerRank = 2;
        public const float HasteCooldownPerRank = 0.08f;
        public const int VitalityHealthPerRank = 15;
        public const int FocusManaPerRank = 15;
        public const float FocusRegenPerRank = 1f;

        // The characters' own passives, per rank
        public const float SpiritSavingCostPerRank = 0.10f;
        public const float FirePowerPerRank = 0.15f;
        public const float TimeSenseCooldownPerRank = 0.08f;
        public const float FaithRegenPerRank = 1.5f;
        public const float BarrierExtensionPerRank = 0.20f;
        public const float WideWindPerRank = 0.15f;
        public const float CritMagicRangePerRank = 0.15f;
        public const float TimeRuleDurationPerRank = 0.15f;
        public const float GuardDamageTakenPerRank = 0.08f;
        public const float SealingArrayPowerPerRank = 0.15f;
        public const float StarBulletDamagePerRank = 0.10f;
        public const float ManaCycleRegenPerRank = 1.5f;
        public const float PreciseThrowPowerPerRank = 0.15f;
        public const float ElegantSavingCostPerRank = 0.10f;
        public const float MeritExperiencePerRank = 0.10f;
        public const float DivineBlessingPowerPerRank = 0.15f;

        /// <summary>However much damage reduction a survivor has, a hit never loses more than this share of its damage.</summary>
        public const float MaxDamageTakenReduction = 0.6f;

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
            new SkillDef
            {
                Id = SkillIds.FantasyHeaven, Name = "몽상천생", IsActive = true, Slot = UltimateSlot, Character = CharacterIds.Reimu, Effect = SkillEffectKind.AreaDamage, MaxRank = 3,
                RequiresSkill = SkillIds.Burst, RequiresRank = UltimateRequiresQRank,
                Radius = new[] { 5f, 6f, 7f }, Damage = new[] { 80, 110, 140 },
                ManaCost = 70, Cooldown = 40f, Description = "넓은 범위의 몹에게 큰 피해 (R)",
            },
            new SkillDef
            {
                Id = SkillIds.FinalSpark, Name = "파이널 스파크", IsActive = true, Slot = UltimateSlot, Character = CharacterIds.Marisa, Effect = SkillEffectKind.Beam, MaxRank = 3,
                RequiresSkill = SkillIds.MasterSpark, RequiresRank = UltimateRequiresQRank,
                Length = new[] { 16f, 16f, 16f }, HalfWidth = new[] { 1.2f, 1.4f, 1.6f }, Damage = new[] { 120, 160, 200 },
                ManaCost = 80, Cooldown = 45f, Description = "길고 넓은 직선의 모든 몹에게 큰 피해 (R)",
            },
            new SkillDef
            {
                Id = SkillIds.KillingDoll, Name = "킬링 도올", IsActive = true, Slot = UltimateSlot, Character = CharacterIds.Sakuya, Effect = SkillEffectKind.Volley, MaxRank = 3,
                RequiresSkill = SkillIds.TimeStop, RequiresRank = UltimateRequiresQRank,
                Count = new[] { 36, 48, 60 }, Damage = new[] { 12, 14, 16 },
                ManaCost = 70, Cooldown = 40f, Description = "사방으로 나이프를 빽빽하게 발사 (R)",
            },
            new SkillDef
            {
                Id = SkillIds.GreatMiracle, Name = "대기적", IsActive = true, Slot = UltimateSlot, Character = CharacterIds.Sanae, Effect = SkillEffectKind.Heal, MaxRank = 3,
                RequiresSkill = SkillIds.Miracle, RequiresRank = UltimateRequiresQRank,
                Radius = new[] { 8f, 8f, 8f }, Amount = new[] { 80, 110, 140 },
                ManaCost = 70, Cooldown = 45f, Description = "넓은 범위의 생존자를 크게 회복 (R)",
            },
            new SkillDef
            {
                Id = SkillIds.SpiritSaving, Name = "영력 절약", Character = CharacterIds.Reimu, MaxRank = 3,
                RequiresSkill = SkillIds.Burst, RequiresRank = 1,
                Passive = PassiveEffectKind.SkillManaCost, PassivePerRank = SpiritSavingCostPerRank, Description = "스킬 마나 소모 -10%",
            },
            new SkillDef
            {
                Id = SkillIds.FirePower, Name = "화력 제일", Character = CharacterIds.Marisa, MaxRank = 3,
                RequiresSkill = SkillIds.MasterSpark, RequiresRank = 1,
                Passive = PassiveEffectKind.SkillPower, PassivePerRank = FirePowerPerRank, Description = "스킬 피해와 회복량 +15%",
            },
            new SkillDef
            {
                Id = SkillIds.TimeSense, Name = "시간 감각", Character = CharacterIds.Sakuya, MaxRank = 3,
                RequiresSkill = SkillIds.TimeStop, RequiresRank = 1,
                Passive = PassiveEffectKind.SkillCooldown, PassivePerRank = TimeSenseCooldownPerRank, Description = "스킬 쿨타임 -8%",
            },
            new SkillDef
            {
                Id = SkillIds.Faith, Name = "신앙", Character = CharacterIds.Sanae, MaxRank = 3,
                RequiresSkill = SkillIds.Miracle, RequiresRank = 1,
                Passive = PassiveEffectKind.ManaRegen, PassivePerRank = FaithRegenPerRank, Description = "마나 회복 +1.5/초",
            },
            new SkillDef
            {
                Id = SkillIds.BarrierExtension, Name = "결계 연장", Character = CharacterIds.Reimu, MaxRank = 3,
                RequiresSkill = SkillIds.Barrier, RequiresRank = 1,
                Passive = PassiveEffectKind.SkillDuration, PassivePerRank = BarrierExtensionPerRank, Description = "스킬 지속 시간 +20%",
            },
            new SkillDef
            {
                Id = SkillIds.WideWind, Name = "넓은 바람", Character = CharacterIds.Sanae, MaxRank = 3,
                RequiresSkill = SkillIds.WindVeil, RequiresRank = 1,
                Passive = PassiveEffectKind.SkillRange, PassivePerRank = WideWindPerRank, Description = "스킬 범위 +15%",
            },
            new SkillDef
            {
                Id = SkillIds.CritMagic, Name = "극대 마력", Character = CharacterIds.Marisa, MaxRank = 3,
                RequiresSkill = SkillIds.MasterSpark, RequiresRank = 1,
                Passive = PassiveEffectKind.SkillRange, PassivePerRank = CritMagicRangePerRank, Description = "스킬 범위 +15%",
            },
            new SkillDef
            {
                Id = SkillIds.TimeRule, Name = "시간 지배", Character = CharacterIds.Sakuya, MaxRank = 3,
                RequiresSkill = SkillIds.TimeStop, RequiresRank = 1,
                Passive = PassiveEffectKind.SkillDuration, PassivePerRank = TimeRuleDurationPerRank, Description = "스킬 지속 시간 +15%",
            },
            new SkillDef
            {
                Id = SkillIds.Guard, Name = "수호의 부적", Character = CharacterIds.Reimu, MaxRank = 3,
                RequiresSkill = SkillIds.Barrier, RequiresRank = 1,
                Passive = PassiveEffectKind.DamageTaken, PassivePerRank = GuardDamageTakenPerRank, Description = "받는 피해 -8%",
            },
            new SkillDef
            {
                Id = SkillIds.SealingArray, Name = "봉마진", Character = CharacterIds.Reimu, MaxRank = 3,
                RequiresSkill = SkillIds.Burst, RequiresRank = 3,
                Passive = PassiveEffectKind.SkillPower, PassivePerRank = SealingArrayPowerPerRank, Description = "스킬 피해와 회복량 +15%",
            },
            new SkillDef
            {
                Id = SkillIds.StarBullet, Name = "별빛 탄환", Character = CharacterIds.Marisa, MaxRank = 3,
                RequiresSkill = SkillIds.RapidFire, RequiresRank = 1,
                Passive = PassiveEffectKind.BasicDamage, PassivePerRank = StarBulletDamagePerRank, Description = "기본 공격 피해 +10%",
            },
            new SkillDef
            {
                Id = SkillIds.ManaCycle, Name = "마력 순환", Character = CharacterIds.Marisa, MaxRank = 3,
                RequiresSkill = SkillIds.MasterSpark, RequiresRank = 3,
                Passive = PassiveEffectKind.ManaRegen, PassivePerRank = ManaCycleRegenPerRank, Description = "마나 회복 +1.5/초",
            },
            new SkillDef
            {
                Id = SkillIds.PreciseThrow, Name = "정밀 투척", Character = CharacterIds.Sakuya, MaxRank = 3,
                RequiresSkill = SkillIds.KnifeBarrage, RequiresRank = 1,
                Passive = PassiveEffectKind.SkillPower, PassivePerRank = PreciseThrowPowerPerRank, Description = "스킬 피해와 회복량 +15%",
            },
            new SkillDef
            {
                Id = SkillIds.ElegantSaving, Name = "우아한 절약", Character = CharacterIds.Sakuya, MaxRank = 3,
                RequiresSkill = SkillIds.TimeStop, RequiresRank = 3,
                Passive = PassiveEffectKind.SkillManaCost, PassivePerRank = ElegantSavingCostPerRank, Description = "스킬 마나 소모 -10%",
            },
            new SkillDef
            {
                Id = SkillIds.Merit, Name = "공덕", Character = CharacterIds.Sanae, MaxRank = 3,
                RequiresSkill = SkillIds.WindVeil, RequiresRank = 1,
                Passive = PassiveEffectKind.ExperienceGain, PassivePerRank = MeritExperiencePerRank, Description = "경험치 획득 +10%",
            },
            new SkillDef
            {
                Id = SkillIds.DivineBlessing, Name = "신령의 가호", Character = CharacterIds.Sanae, MaxRank = 3,
                RequiresSkill = SkillIds.Miracle, RequiresRank = 3,
                Passive = PassiveEffectKind.SkillPower, PassivePerRank = DivineBlessingPowerPerRank, Description = "스킬 피해와 회복량 +15%",
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

        /// <summary>
        /// What the character's own passives add up to for one kind of effect: rank times the value per rank, for each passive of
        /// that kind the character owns (a passive of another character is never counted, whatever the ranks say).
        /// </summary>
        public static float PassiveTotal(IReadOnlyList<int> ranks, byte character, PassiveEffectKind kind)
        {
            if (kind == PassiveEffectKind.None || character == CharacterIds.None) return 0f;
            float total = 0f;
            for (int i = 0; i < defs.Length; i++)
            {
                SkillDef def = defs[i];
                if (def.Passive != kind || def.Character != character) continue;
                total += RankOf(ranks, def.Id) * def.PassivePerRank;
            }
            return total;
        }

        /// <summary>The mana a skill really costs this survivor: lowered by the cost passive, never below one.</summary>
        public static int EffectiveManaCost(SkillDef def, IReadOnlyList<int> ranks, byte character)
        {
            if (def == null) return 0;
            float reduction = Math.Min(MaxManaCostReduction, PassiveTotal(ranks, character, PassiveEffectKind.SkillManaCost));
            return Math.Max(1, (int)Math.Round(def.ManaCost * (1f - reduction), MidpointRounding.AwayFromZero));
        }

        /// <summary>The cooldown a skill really has for this survivor: shortened by the cooldown passive, never below the lower bound.</summary>
        public static float EffectiveCooldown(SkillDef def, IReadOnlyList<int> ranks, byte character)
        {
            if (def == null) return 0f;
            float multiplier = Math.Max(MinSkillCooldownMultiplier, 1f - PassiveTotal(ranks, character, PassiveEffectKind.SkillCooldown));
            return def.Cooldown * multiplier;
        }

        /// <summary>A skill's damage or healing with the power passive applied (a value of zero stays zero).</summary>
        public static int EffectivePower(int value, IReadOnlyList<int> ranks, byte character)
        {
            if (value <= 0) return value;
            float total = PassiveTotal(ranks, character, PassiveEffectKind.SkillPower);
            return Math.Max(1, (int)Math.Round(value * (1f + total), MidpointRounding.AwayFromZero));
        }

        /// <summary>The share of each hit this survivor takes: one less the damage-reduction passives, the reduction capped.</summary>
        public static float DamageTakenMultiplier(IReadOnlyList<int> ranks, byte character) =>
            1f - Math.Min(MaxDamageTakenReduction, PassiveTotal(ranks, character, PassiveEffectKind.DamageTaken));

        /// <summary>The experience this survivor receives for a kill worth <paramref name="baseAmount"/>: raised by the experience passives, never below the base amount.</summary>
        public static int EffectiveExperience(int baseAmount, IReadOnlyList<int> ranks, byte character)
        {
            if (baseAmount <= 0) return baseAmount;
            float total = PassiveTotal(ranks, character, PassiveEffectKind.ExperienceGain);
            return Math.Max(baseAmount, (int)Math.Round(baseAmount * (1f + total), MidpointRounding.AwayFromZero));
        }

        /// <summary>The range scale (percent) of a skill use: the radius, length, width and push distance are multiplied by it.</summary>
        public static int RangeScalePercent(IReadOnlyList<int> ranks, byte character) =>
            ScalePercent(PassiveTotal(ranks, character, PassiveEffectKind.SkillRange));

        /// <summary>The duration scale (percent) of a skill use: the time of a timed effect is multiplied by it.</summary>
        public static int DurationScalePercent(IReadOnlyList<int> ranks, byte character) =>
            ScalePercent(PassiveTotal(ranks, character, PassiveEffectKind.SkillDuration));

        /// <summary>A defined value multiplied by a whole-percent scale (a scale of 0 means none was given and counts as 100). The server and the clients both use this, so they get the same number.</summary>
        public static float Scaled(float value, int percent) =>
            percent <= 0 || percent == NormalScalePercent ? value : value * percent / 100f;

        static int ScalePercent(float total) =>
            Math.Min(MaxScalePercent, Math.Max(NormalScalePercent, (int)Math.Round(NormalScalePercent * (1f + total), MidpointRounding.AwayFromZero)));

        public static int RankOf(IReadOnlyList<int> ranks, byte skillId) =>
            skillId < ranks.Count ? ranks[skillId] : 0;

        /// <summary>The ranks of skill ids 1 to 21, three bits each, id 1 in the lowest bits.</summary>
        public static ulong PackLow(IReadOnlyList<int> ranks) => PackRange(ranks, 1);

        /// <summary>The ranks of skill ids 22 to 42, three bits each, id 22 in the lowest bits.</summary>
        public static ulong PackHigh(IReadOnlyList<int> ranks) => PackRange(ranks, IdsPerPackedValue + 1);

        static ulong PackRange(IReadOnlyList<int> ranks, int firstId)
        {
            ulong packed = 0;
            for (int id = firstId; id < firstId + IdsPerPackedValue && id <= MaxSkillId && id < ranks.Count; id++)
                packed |= (ulong)(ranks[id] & MaxPackedRank) << ((id - firstId) * PackBits);
            return packed;
        }

        public static void Unpack(ulong low, ulong high, int[] into)
        {
            for (int id = 0; id < into.Length; id++)
            {
                if (id < 1 || id > MaxSkillId)
                {
                    into[id] = 0;
                    continue;
                }
                bool inLow = id <= IdsPerPackedValue;
                ulong packed = inLow ? low : high;
                int slot = inLow ? id - 1 : id - IdsPerPackedValue - 1;
                into[id] = (int)((packed >> (slot * PackBits)) & MaxPackedRank);
            }
        }
    }
}
