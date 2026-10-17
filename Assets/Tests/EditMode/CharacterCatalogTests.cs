using System.Linq;
using NUnit.Framework;

namespace BS.Tests
{
    public class CharacterCatalogTests
    {
        [Test]
        public void ThereAreExactlyFourCharacters()
        {
            Assert.AreEqual(4, CharacterCatalog.All.Count);
            Assert.AreEqual(CharacterCatalog.Count, CharacterCatalog.All.Count);
        }

        [Test]
        public void IdsAndNamesAreUnique_AndIdsAreOneToFour()
        {
            CollectionAssert.AreEquivalent(new byte[] { 1, 2, 3, 4 }, CharacterCatalog.All.Select(c => c.Id).ToArray());
            Assert.AreEqual(4, CharacterCatalog.All.Select(c => c.Name).Distinct().Count());
        }

        [Test]
        public void EveryValueIsPositive()
        {
            foreach (CharacterDef c in CharacterCatalog.All)
            {
                Assert.Greater(c.MaxHealth, 0, c.Name);
                Assert.Greater(c.MaxMana, 0, c.Name);
                Assert.Greater(c.ManaRegenPerSecond, 0f, c.Name);
                Assert.Greater(c.MoveSpeed, 0f, c.Name);
                Assert.IsFalse(string.IsNullOrEmpty(c.Name));
            }
        }

        [Test]
        public void NoTwoCharactersAreIdentical()
        {
            var colors = CharacterCatalog.All.Select(c => c.Color).Distinct().Count();
            Assert.AreEqual(4, colors, "colors differ");
            var values = CharacterCatalog.All.Select(c => (c.MaxHealth, c.MaxMana, c.ManaRegenPerSecond, c.MoveSpeed)).Distinct().Count();
            Assert.AreEqual(4, values, "starting values differ");
        }

        [Test]
        public void Get_ReturnsNullForNoneAndUnknown()
        {
            Assert.IsNull(CharacterCatalog.Get(CharacterIds.None));
            Assert.IsNull(CharacterCatalog.Get(99));
            Assert.IsFalse(CharacterCatalog.IsValid(0));
            Assert.AreEqual("레이무", CharacterCatalog.Get(CharacterIds.Reimu).Name);
        }

        [Test]
        public void TheBossColorStaysDistinctFromEveryCharacter()
        {
            var boss = new UnityEngine.Color(0.85f, 0.15f, 0.15f);
            foreach (CharacterDef c in CharacterCatalog.All)
                Assert.Greater(UnityEngine.Mathf.Abs(c.Color.g - boss.g) + UnityEngine.Mathf.Abs(c.Color.b - boss.b), 0.3f, c.Name);
        }
    }
}

namespace BS.Tests
{
    public class CharacterStatTests
    {
        static readonly int[] NoRanks = new int[16];

        [Test]
        public void NoCharacter_KeepsTheSharedDefaults()
        {
            SurvivorStats s = StatCalc.Compute(1, NoRanks);
            Assert.AreEqual(CombatConfig.SurvivorMaxHealth, s.MaxHealth);
            Assert.AreEqual(GrowthConfig.BaseMaxMana, s.MaxMana);
            Assert.AreEqual(GrowthConfig.BaseManaRegenPerSecond, s.ManaRegenPerSecond);
        }

        [Test]
        public void EachCharacterStartsWithItsOwnValues()
        {
            foreach (CharacterDef c in CharacterCatalog.All)
            {
                SurvivorStats s = StatCalc.Compute(1, NoRanks, c);
                Assert.AreEqual(c.MaxHealth, s.MaxHealth, c.Name);
                Assert.AreEqual(c.MaxMana, s.MaxMana, c.Name);
                Assert.AreEqual(c.ManaRegenPerSecond, s.ManaRegenPerSecond, c.Name);
            }
        }

        [Test]
        public void LevelUpAddsTheSameAmountToEveryCharacter_SoDifferencesStay()
        {
            CharacterDef a = CharacterCatalog.Get(CharacterIds.Marisa);
            CharacterDef b = CharacterCatalog.Get(CharacterIds.Sanae);
            SurvivorStats a1 = StatCalc.Compute(1, NoRanks, a), a5 = StatCalc.Compute(5, NoRanks, a);
            SurvivorStats b1 = StatCalc.Compute(1, NoRanks, b), b5 = StatCalc.Compute(5, NoRanks, b);

            Assert.AreEqual(a5.MaxHealth - a1.MaxHealth, b5.MaxHealth - b1.MaxHealth);
            Assert.AreEqual(a5.MaxMana - a1.MaxMana, b5.MaxMana - b1.MaxMana);
            Assert.AreEqual(b1.MaxHealth - a1.MaxHealth, b5.MaxHealth - a5.MaxHealth);
            Assert.AreEqual(a1.ProjectileDamage, b1.ProjectileDamage, "the attack is common");
        }

        [Test]
        public void SkillsStillAddOnTopOfTheCharacter()
        {
            var ranks = new int[16];
            ranks[SkillIds.Vitality] = 2;
            CharacterDef c = CharacterCatalog.Get(CharacterIds.Sakuya);
            Assert.AreEqual(c.MaxHealth + 2 * SkillCatalog.VitalityHealthPerRank, StatCalc.Compute(1, ranks, c).MaxHealth);
        }

        [Test]
        public void ASurvivorRegisteredWithItsMaximumStartsFull_AndAnotherKeepsTheDefault()
        {
            var sim = new SurvivorSim();
            sim.Register(1, 120);
            sim.Register(2);

            Assert.AreEqual(120, sim.Health(1));
            Assert.AreEqual(120, sim.MaxHealthOf(1));
            Assert.AreEqual(CombatConfig.SurvivorMaxHealth, sim.Health(2));
        }
    }
}
