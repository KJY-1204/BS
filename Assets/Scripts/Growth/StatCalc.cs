using System.Collections.Generic;

namespace BS
{
    public struct SurvivorStats
    {
        public int MaxHealth;
        public int MaxMana;
        public float ManaRegenPerSecond;
        public int ProjectileDamage;
        /// <summary>Basic attack cooldown multiplier from passive skills (below 1 is faster).</summary>
        public float CooldownMultiplier;
        /// <summary>The share of each hit the survivor takes (below 1 is less), from the damage-reduction passives.</summary>
        public float DamageTakenMultiplier;
    }

    /// <summary>Works out a survivor's stats from level and learned skill ranks.</summary>
    public static class StatCalc
    {
        public const float MinCooldownMultiplier = 0.2f;

        public static SurvivorStats Compute(int level, IReadOnlyList<int> ranks) => Compute(level, ranks, null);

        /// <summary>
        /// Starting health, mana and mana recovery come from the character (the shared defaults when there is none);
        /// what levels and skills add is the same for every character.
        /// </summary>
        public static SurvivorStats Compute(int level, IReadOnlyList<int> ranks, CharacterDef character)
        {
            int baseHealth = character != null ? character.MaxHealth : CombatConfig.SurvivorMaxHealth;
            int baseMana = character != null ? character.MaxMana : GrowthConfig.BaseMaxMana;
            float baseRegen = character != null ? character.ManaRegenPerSecond : GrowthConfig.BaseManaRegenPerSecond;

            int gained = level > 1 ? level - 1 : 0;

            int power = SkillCatalog.RankOf(ranks, SkillIds.Power);
            int haste = SkillCatalog.RankOf(ranks, SkillIds.Haste);
            int vitality = SkillCatalog.RankOf(ranks, SkillIds.Vitality);
            int focus = SkillCatalog.RankOf(ranks, SkillIds.Focus);

            byte characterId = character != null ? character.Id : CharacterIds.None;
            float multiplier = 1f - SkillCatalog.HasteCooldownPerRank * haste;
            if (multiplier < MinCooldownMultiplier) multiplier = MinCooldownMultiplier;

            return new SurvivorStats
            {
                MaxHealth = baseHealth + GrowthConfig.HealthPerLevel * gained + SkillCatalog.VitalityHealthPerRank * vitality,
                MaxMana = baseMana + GrowthConfig.ManaPerLevel * gained + SkillCatalog.FocusManaPerRank * focus,
                ManaRegenPerSecond = baseRegen + SkillCatalog.FocusRegenPerRank * focus +
                                     SkillCatalog.PassiveTotal(ranks, character != null ? character.Id : CharacterIds.None, PassiveEffectKind.ManaRegen),
                ProjectileDamage = (int)System.Math.Round(
                    (CombatConfig.ProjectileDamage + GrowthConfig.DamagePerLevel * gained + SkillCatalog.PowerDamagePerRank * power) *
                    (1f + SkillCatalog.PassiveTotal(ranks, characterId, PassiveEffectKind.BasicDamage)), System.MidpointRounding.AwayFromZero),
                CooldownMultiplier = multiplier,
                DamageTakenMultiplier = SkillCatalog.DamageTakenMultiplier(ranks, characterId),
            };
        }
    }
}
