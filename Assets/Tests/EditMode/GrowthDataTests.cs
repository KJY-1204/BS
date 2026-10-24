using System.Collections.Generic;
using NUnit.Framework;

namespace BS.Tests
{
    public class GrowthDataTests
    {
        static int[] NoRanks() => new int[SkillCatalog.MaxSkillId + 1];

        // ---- SkillCatalog ----

        [Test]
        public void SkillIds_AreUniqueNonZeroAndFitThePackedFormat()
        {
            var seen = new HashSet<byte>();
            foreach (SkillDef def in SkillCatalog.All)
            {
                Assert.AreNotEqual(0, def.Id);
                Assert.LessOrEqual(def.Id, SkillCatalog.MaxSkillId, def.Name);
                Assert.IsTrue(seen.Add(def.Id), $"id {def.Id} used twice");
                Assert.GreaterOrEqual(def.MaxRank, 1);
                Assert.LessOrEqual(def.MaxRank, SkillCatalog.MaxPackedRank);
            }
        }

        [Test]
        public void Prerequisites_PointAtRealSkills()
        {
            foreach (SkillDef def in SkillCatalog.All)
            {
                if (def.RequiresSkill == 0) continue;
                SkillDef required = SkillCatalog.Get(def.RequiresSkill);
                Assert.IsNotNull(required, $"{def.Name} requires a skill that does not exist");
                Assert.LessOrEqual(def.RequiresRank, required.MaxRank);
                Assert.AreNotEqual(def.Id, def.RequiresSkill);
            }
        }

        [Test]
        public void ActiveSkills_HaveSlotsCostsAndRankValues()
        {
            var slots = new HashSet<(byte, int)>();
            foreach (SkillDef def in SkillCatalog.All)
            {
                if (!def.IsActive) { Assert.AreEqual(-1, def.Slot, def.Name); continue; }

                Assert.GreaterOrEqual(def.Slot, 0);
                Assert.Less(def.Slot, SkillCatalog.SkillSlots);
                Assert.AreNotEqual(CharacterIds.None, def.Character, "an active skill belongs to a character");
                if (def.EvolvesFrom != 0)
                {
                    Assert.AreEqual(SkillCatalog.Get(def.EvolvesFrom).Slot, def.Slot, "an evolution has the slot of the skill it replaces");
                    continue;
                }
                Assert.IsTrue(slots.Add((def.Character, def.Slot)), "two skills of one character share a slot");
                Assert.Greater(def.ManaCost, 0);
                Assert.Greater(def.Cooldown, 0f);
                Assert.AreSame(def, SkillCatalog.ForSlot(def.Slot, def.Character));
            }
            Assert.AreEqual(SkillCatalog.SkillSlots * CharacterCatalog.Count, slots.Count);

            SkillDef burst = SkillCatalog.Get(SkillIds.Burst);
            Assert.AreEqual(burst.MaxRank, burst.Radius.Length);
            Assert.AreEqual(burst.MaxRank, burst.Damage.Length);
            SkillDef rapid = SkillCatalog.Get(SkillIds.RapidFire);
            Assert.AreEqual(rapid.MaxRank, rapid.Duration.Length);
            Assert.AreEqual(rapid.MaxRank, rapid.CooldownMultiplier.Length);
        }

        [Test]
        public void UnknownSkillsAndSlotsAreNull()
        {
            Assert.IsNull(SkillCatalog.Get(0));
            Assert.IsNull(SkillCatalog.Get(200));
            Assert.IsNull(SkillCatalog.ForSlot(5, CharacterIds.Reimu));
            Assert.IsNull(SkillCatalog.ForSlot(0, CharacterIds.None), "no character, no skills");
        }

        [Test]
        public void IsAvailable_HonoursPrerequisitesAndMaxRank()
        {
            int[] ranks = NoRanks();
            SkillDef rapid = SkillCatalog.Get(SkillIds.RapidFire);
            SkillDef haste = SkillCatalog.Get(SkillIds.Haste);

            Assert.IsFalse(SkillCatalog.IsAvailable(rapid, ranks, CharacterIds.Marisa), "needs Haste first");
            Assert.IsTrue(SkillCatalog.IsAvailable(haste, ranks, CharacterIds.Marisa));

            ranks[SkillIds.Haste] = 1;
            Assert.IsTrue(SkillCatalog.IsAvailable(rapid, ranks, CharacterIds.Marisa), "prerequisite met");

            ranks[SkillIds.Haste] = haste.MaxRank;
            Assert.IsFalse(SkillCatalog.IsAvailable(haste, ranks, CharacterIds.Marisa), "at its maximum rank");
            Assert.IsTrue(SkillCatalog.IsAvailable(rapid, ranks, CharacterIds.Marisa));

            ranks[SkillIds.RapidFire] = rapid.MaxRank;
            Assert.IsFalse(SkillCatalog.IsAvailable(rapid, ranks, CharacterIds.Marisa));
            Assert.IsFalse(SkillCatalog.IsAvailable(null, ranks, CharacterIds.Marisa));
        }

        [Test]
        public void PackedRanks_RoundTrip()
        {
            int[] ranks = NoRanks();
            ranks[SkillIds.Power] = 5;
            ranks[SkillIds.Haste] = 3;
            ranks[SkillIds.Burst] = 2;
            ranks[SkillIds.RapidFire] = 1;

            int[] back = new int[SkillCatalog.MaxSkillId + 1];
            SkillCatalog.Unpack(SkillCatalog.PackAll(ranks), back);

            CollectionAssert.AreEqual(ranks, back);
            Assert.AreEqual(0UL, SkillCatalog.PackLow(NoRanks()));
            Assert.AreEqual(0UL, SkillCatalog.PackHigh(NoRanks()));
        }

        // ---- Config ----

        [Test]
        public void XpCurve_GrowsAndMatchesTheFormula()
        {
            Assert.AreEqual(30, GrowthConfig.XpNeeded(1));
            Assert.AreEqual(50, GrowthConfig.XpNeeded(2));
            for (int level = 1; level < GrowthConfig.MaxLevel; level++)
                Assert.Greater(GrowthConfig.XpNeeded(level + 1), GrowthConfig.XpNeeded(level));
        }

        [Test]
        public void TheCapIsTwentyAndTheCurveKeepsTheEarlyLevelsAndSlowsAfterTheNinth()
        {
            Assert.AreEqual(20, GrowthConfig.MaxLevel);
            Assert.AreEqual(190, GrowthConfig.XpNeeded(9));
            Assert.AreEqual(200, GrowthConfig.XpNeeded(10));
            Assert.AreEqual(210, GrowthConfig.XpNeeded(11));
            Assert.AreEqual(290, GrowthConfig.XpNeeded(19));
            for (int level = 1; level <= 9; level++)
                Assert.AreEqual(30 + 20 * (level - 1), GrowthConfig.XpNeeded(level), $"level {level} keeps its earlier value");
        }

        [Test]
        public void TotalExperienceToLevelTenIsUnchangedAndToTheCapIsAbout3_5TimesThat()
        {
            int toTen = 0, toCap = 0;
            for (int level = 1; level < 10; level++) toTen += GrowthConfig.XpNeeded(level);
            for (int level = 1; level < GrowthConfig.MaxLevel; level++) toCap += GrowthConfig.XpNeeded(level);

            Assert.AreEqual(990, toTen);
            Assert.AreEqual(3440, toCap);
            Assert.AreEqual(3.5f, toCap / (float)toTen, 0.1f);
        }

        [Test]
        public void AtTheCapTheAutomaticGrowthTableGivesTheDocumentedTotals()
        {
            SurvivorStats one = StatCalc.Compute(1, NoRanks());
            SurvivorStats cap = StatCalc.Compute(GrowthConfig.MaxLevel, NoRanks());

            Assert.AreEqual(190, cap.MaxHealth - one.MaxHealth);
            Assert.AreEqual(95, cap.MaxMana - one.MaxMana);
            Assert.AreEqual(19, cap.ProjectileDamage - one.ProjectileDamage);
        }

        // ---- StatCalc ----

        [Test]
        public void LevelOne_HasTheBaseStats()
        {
            SurvivorStats s = StatCalc.Compute(1, NoRanks());

            Assert.AreEqual(CombatConfig.SurvivorMaxHealth, s.MaxHealth);
            Assert.AreEqual(GrowthConfig.BaseMaxMana, s.MaxMana);
            Assert.AreEqual(GrowthConfig.BaseManaRegenPerSecond, s.ManaRegenPerSecond);
            Assert.AreEqual(CombatConfig.ProjectileDamage, s.ProjectileDamage);
            Assert.AreEqual(1f, s.CooldownMultiplier);
        }

        [Test]
        public void EachLevel_AddsTheGrowthTableAmounts()
        {
            SurvivorStats one = StatCalc.Compute(1, NoRanks());
            SurvivorStats five = StatCalc.Compute(5, NoRanks());

            Assert.AreEqual(4 * GrowthConfig.HealthPerLevel, five.MaxHealth - one.MaxHealth);
            Assert.AreEqual(4 * GrowthConfig.ManaPerLevel, five.MaxMana - one.MaxMana);
            Assert.AreEqual(4 * GrowthConfig.DamagePerLevel, five.ProjectileDamage - one.ProjectileDamage);
        }

        [Test]
        public void PassiveRanks_ApplyTheirEffects()
        {
            int[] ranks = NoRanks();
            ranks[SkillIds.Power] = 2;
            ranks[SkillIds.Haste] = 3;
            ranks[SkillIds.Vitality] = 1;
            ranks[SkillIds.Focus] = 2;

            SurvivorStats s = StatCalc.Compute(1, ranks);

            Assert.AreEqual(CombatConfig.ProjectileDamage + 2 * SkillCatalog.PowerDamagePerRank, s.ProjectileDamage);
            Assert.AreEqual(1f - 3 * SkillCatalog.HasteCooldownPerRank, s.CooldownMultiplier, 1e-5f);
            Assert.AreEqual(CombatConfig.SurvivorMaxHealth + SkillCatalog.VitalityHealthPerRank, s.MaxHealth);
            Assert.AreEqual(GrowthConfig.BaseMaxMana + 2 * SkillCatalog.FocusManaPerRank, s.MaxMana);
            Assert.AreEqual(GrowthConfig.BaseManaRegenPerSecond + 2 * SkillCatalog.FocusRegenPerRank, s.ManaRegenPerSecond, 1e-5f);
        }

        [Test]
        public void LevelAndRanks_Add()
        {
            int[] ranks = NoRanks();
            ranks[SkillIds.Vitality] = 2;

            SurvivorStats s = StatCalc.Compute(4, ranks);

            Assert.AreEqual(CombatConfig.SurvivorMaxHealth + 3 * GrowthConfig.HealthPerLevel + 2 * SkillCatalog.VitalityHealthPerRank, s.MaxHealth);
        }

        [Test]
        public void CooldownMultiplier_NeverDropsBelowTheFloor()
        {
            int[] ranks = NoRanks();
            ranks[SkillIds.Haste] = 15;

            Assert.AreEqual(StatCalc.MinCooldownMultiplier, StatCalc.Compute(1, ranks).CooldownMultiplier);
        }

        [Test]
        public void ShortRankArraysAreTreatedAsNoRanks()
        {
            SurvivorStats s = StatCalc.Compute(1, new int[0]);
            Assert.AreEqual(CombatConfig.SurvivorMaxHealth, s.MaxHealth);
        }
    }
}
