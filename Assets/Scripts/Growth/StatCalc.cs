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
    }

    /// <summary>Works out a survivor's stats from level and learned skill ranks.</summary>
    public static class StatCalc
    {
        public const float MinCooldownMultiplier = 0.2f;

        public static SurvivorStats Compute(int level, IReadOnlyList<int> ranks)
        {
            int gained = level > 1 ? level - 1 : 0;

            int power = SkillCatalog.RankOf(ranks, SkillIds.Power);
            int haste = SkillCatalog.RankOf(ranks, SkillIds.Haste);
            int vitality = SkillCatalog.RankOf(ranks, SkillIds.Vitality);
            int focus = SkillCatalog.RankOf(ranks, SkillIds.Focus);

            float multiplier = 1f - SkillCatalog.HasteCooldownPerRank * haste;
            if (multiplier < MinCooldownMultiplier) multiplier = MinCooldownMultiplier;

            return new SurvivorStats
            {
                MaxHealth = CombatConfig.SurvivorMaxHealth + GrowthConfig.HealthPerLevel * gained + SkillCatalog.VitalityHealthPerRank * vitality,
                MaxMana = GrowthConfig.BaseMaxMana + GrowthConfig.ManaPerLevel * gained + SkillCatalog.FocusManaPerRank * focus,
                ManaRegenPerSecond = GrowthConfig.BaseManaRegenPerSecond + SkillCatalog.FocusRegenPerRank * focus,
                ProjectileDamage = CombatConfig.ProjectileDamage + GrowthConfig.DamagePerLevel * gained + SkillCatalog.PowerDamagePerRank * power,
                CooldownMultiplier = multiplier,
            };
        }
    }
}
