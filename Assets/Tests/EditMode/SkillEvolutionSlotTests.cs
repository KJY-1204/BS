using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace BS.Tests
{
    public class SkillEvolutionSlotTests
    {
        static readonly byte[] Characters = { CharacterIds.Reimu, CharacterIds.Marisa, CharacterIds.Sakuya, CharacterIds.Sanae };
        static readonly int[] NewSlots = { 1, 2 };

        static int[] NoRanks() => new int[SkillCatalog.MaxSkillId + 1];

        static SkillDef Base(int slot, byte character) => SkillCatalog.ForSlot(slot, character);

        static List<SkillDef> EvolutionsOf(SkillDef baseSkill) => SkillCatalog.All.Where(d => d.EvolvesFrom == baseSkill.Id).ToList();

        // ---- 2.1 the data ----

        [Test]
        public void TheSixteenNewEvolutionsAreIdsFortyOneToFiftySixTwoForEachEAndRSkill()
        {
            var evolutions = new List<SkillDef>();
            foreach (byte c in Characters)
                foreach (int slot in NewSlots)
                {
                    List<SkillDef> pair = EvolutionsOf(Base(slot, c));
                    Assert.AreEqual(2, pair.Count, $"character {c} slot {slot}");
                    evolutions.AddRange(pair);
                }

            CollectionAssert.AreEquivalent(Enumerable.Range(41, 16).Select(i => (byte)i), evolutions.Select(d => d.Id));
            foreach (SkillDef d in evolutions) Assert.AreEqual(d.Slot, SkillCatalog.Get(d.EvolvesFrom).Slot);
        }

        [Test]
        public void EachNewEvolutionIsAOneRankSkillOfTheSameKindAndExcludesItsSibling()
        {
            foreach (byte c in Characters)
                foreach (int slot in NewSlots)
                {
                    SkillDef b = Base(slot, c);
                    List<SkillDef> pair = EvolutionsOf(b);
                    foreach (SkillDef e in pair)
                    {
                        Assert.IsTrue(e.IsActive, e.Name);
                        Assert.AreEqual(c, e.Character, e.Name);
                        Assert.AreEqual(slot, e.Slot, e.Name);
                        Assert.AreEqual(1, e.MaxRank, e.Name);
                        Assert.AreEqual(b.Effect, e.Effect, e.Name);
                        Assert.AreEqual(b.Id, e.RequiresSkill, e.Name);
                        Assert.AreEqual(b.MaxRank, e.RequiresRank, $"{e.Name} is offered at the base skill's maximum rank");
                        Assert.AreEqual(pair.Single(o => o != e).Id, e.ExclusiveWith, e.Name);
                        Assert.Greater(e.ManaCost, 0, e.Name);
                        Assert.Greater(e.Cooldown, 0f, e.Name);
                        Assert.IsFalse(string.IsNullOrEmpty(e.Description), e.Name);
                    }
                    Assert.AreNotEqual(pair[0].Name, pair[1].Name);
                }
        }

        [Test]
        public void TheNamesFitTwoLinesOnTheSkillBar()
        {
            foreach (SkillDef e in SkillCatalog.All.Where(d => d.EvolvesFrom != 0))
            {
                Assert.LessOrEqual(e.Name.Length, 16, e.Name);
                StringAssert.Contains("·", e.Name);
            }
        }

        [Test]
        public void EveryEvolutionOfAnRSkillCostsMoreAndCoolsDownLongerThanTheBaseQAndESkillsOfItsCharacter()
        {
            foreach (byte c in Characters)
            {
                SkillDef q = Base(0, c), e = Base(1, c);
                foreach (SkillDef evolution in EvolutionsOf(Base(SkillCatalog.UltimateSlot, c)))
                    foreach (SkillDef baseSkill in new[] { q, e })
                    {
                        Assert.Greater(evolution.ManaCost, baseSkill.ManaCost, $"{evolution.Name} against {baseSkill.Name}");
                        Assert.Greater(evolution.Cooldown, baseSkill.Cooldown, $"{evolution.Name} against {baseSkill.Name}");
                    }
            }
        }

        [Test]
        public void TheRSkillsStillBeatTheBaseQAndESkillsOfTheSameKind()
        {
            // The rule compares base skills: it holds whatever the evolutions do.
            Assert.Greater(SkillCatalog.Get(SkillIds.KillingDoll).Count[0], SkillCatalog.Get(SkillIds.KnifeBarrage).Count[0]);
            Assert.Greater(SkillCatalog.Get(SkillIds.GreatMiracle).Radius[0], SkillCatalog.Get(SkillIds.Miracle).Radius[0]);
            Assert.Greater(SkillCatalog.Get(SkillIds.FantasyHeaven).Damage[0], SkillCatalog.Get(SkillIds.Burst).Damage[0]);
        }

        [Test]
        public void EveryEvolutionTakesIdsThatFitThePackedValuesAndTheRanksComeBack()
        {
            var leveling = new LevelingSim(new System.Random(1));
            leveling.Register(1, CharacterIds.Sakuya);
            foreach (SkillDef d in SkillCatalog.All.Where(d => d.EvolvesFrom != 0)) leveling.GrantRank(1, d.Id, 1);
            leveling.GrantRank(1, SkillIds.KillingDoll, 3);

            int[] back = NoRanks();
            SkillCatalog.Unpack(SkillCatalog.PackAll(leveling.Ranks(1)), back);

            foreach (SkillDef d in SkillCatalog.All) Assert.AreEqual(leveling.RankOf(1, d.Id), back[d.Id], d.Name);
            for (int id = 41; id <= 56; id++) Assert.AreEqual(1, back[id], $"id {id}");
        }

        // ---- 2.2 how they are offered ----

        static LevelingSim NewSim(int seed, byte character)
        {
            var sim = new LevelingSim(new System.Random(seed));
            sim.Register(1, character);
            return sim;
        }

        [Test]
        public void AnEOrREvolutionIsNeverOfferedBeforeItsBaseSkillIsAtItsMaximumRank()
        {
            foreach (byte c in Characters)
                foreach (int slot in NewSlots)
                {
                    SkillDef b = Base(slot, c);
                    byte[] ids = EvolutionsOf(b).Select(d => d.Id).ToArray();
                    for (int seed = 0; seed < 40; seed++)
                    {
                        LevelingSim sim = NewSim(seed, c);
                        sim.GrantRank(1, b.Id, b.MaxRank - 1);
                        sim.AddXp(1, GrowthConfig.XpNeeded(1));
                        foreach (byte id in ids) CollectionAssert.DoesNotContain(sim.CurrentChoices(1), id, $"character {c} slot {slot} seed {seed}");
                    }
                }
        }

        [Test]
        public void AtTheMaximumRankEitherEvolutionCanBeOfferedAndChoosingOneGivesRankOneAndKeepsTheBaseRank()
        {
            foreach (byte c in Characters)
                foreach (int slot in NewSlots)
                {
                    SkillDef b = Base(slot, c);
                    foreach (SkillDef target in EvolutionsOf(b))
                    {
                        bool offered = false;
                        for (int seed = 0; seed < 120 && !offered; seed++)
                        {
                            LevelingSim sim = NewSim(seed, c);
                            sim.GrantRank(1, SkillCatalog.ForSlot(0, c).Id, 3); // the R skill's own prerequisite
                            sim.GrantRank(1, b.Id, b.MaxRank);
                            sim.AddXp(1, GrowthConfig.XpNeeded(1));
                            int index = System.Array.IndexOf(sim.CurrentChoices(1), target.Id);
                            if (index < 0) continue;
                            offered = true;
                            Assert.IsTrue(sim.Pick(1, sim.CurrentOfferId(1), index));
                            Assert.AreEqual(1, sim.RankOf(1, target.Id));
                            Assert.AreEqual(b.MaxRank, sim.RankOf(1, b.Id));
                        }
                        Assert.IsTrue(offered, $"{target.Name} was never offered in 120 tries");
                    }
                }
        }

        [Test]
        public void AfterTheChoiceNeitherEvolutionOfThatSkillIsOfferedAgain()
        {
            foreach (byte c in Characters)
                foreach (int slot in NewSlots)
                {
                    SkillDef b = Base(slot, c);
                    byte[] ids = EvolutionsOf(b).Select(d => d.Id).ToArray();
                    LevelingSim sim = NewSim(5, c);
                    sim.GrantRank(1, b.Id, b.MaxRank);
                    sim.GrantRank(1, ids[1], 1);

                    for (int guard = 0; guard < 200 && sim.Level(1) < GrowthConfig.MaxLevel; guard++)
                    {
                        sim.AddXp(1, GrowthConfig.XpNeeded(sim.Level(1)));
                        while (sim.HasOffer(1))
                        {
                            foreach (byte id in ids) CollectionAssert.DoesNotContain(sim.CurrentChoices(1), id, $"character {c} slot {slot}");
                            sim.Pick(1, sim.CurrentOfferId(1), 0);
                        }
                    }
                    Assert.AreEqual(0, sim.RankOf(1, ids[0]), "the other evolution was never learned");
                }
        }

        [Test]
        public void TheSlotsAreIndependentAChoiceInOneLeavesTheEvolutionsOfTheOthersOffered()
        {
            foreach (byte c in Characters)
            {
                var ranks = NoRanks();
                for (int slot = 0; slot < SkillCatalog.SkillSlots; slot++) ranks[Base(slot, c).Id] = 3;
                ranks[EvolutionsOf(Base(0, c))[0].Id] = 1; // the Q skill has evolved

                foreach (int slot in NewSlots)
                    foreach (SkillDef e in EvolutionsOf(Base(slot, c)))
                        Assert.IsTrue(SkillCatalog.IsAvailable(e, ranks, c), $"{e.Name}");
            }
        }

        [Test]
        public void AnotherCharactersEvolutionOfAnESkillOrRSkillIsNeverOffered()
        {
            foreach (byte c in Characters)
            {
                var ranks = NoRanks();
                foreach (SkillDef d in SkillCatalog.All.Where(d => d.IsActive && d.EvolvesFrom == 0)) ranks[d.Id] = 3;
                foreach (SkillDef e in SkillCatalog.All.Where(d => d.EvolvesFrom != 0 && d.Character != c))
                    Assert.IsFalse(SkillCatalog.IsAvailable(e, ranks, c), $"{e.Name} for character {c}");
            }
        }

        [Test]
        public void AnEvolvedRSkillDoesNotChangeWhatOpensOrWhatTheQAndESlotsFire()
        {
            foreach (byte c in Characters)
            {
                var ranks = NoRanks();
                ranks[Base(0, c).Id] = 3;
                ranks[Base(1, c).Id] = 3;
                ranks[Base(2, c).Id] = 3;
                SkillDef rEvolution = EvolutionsOf(Base(2, c))[0];
                ranks[rEvolution.Id] = 1;

                Assert.AreSame(rEvolution, SkillCatalog.ActiveForSlot(2, c, ranks));
                Assert.AreSame(Base(0, c), SkillCatalog.ActiveForSlot(0, c, ranks));
                Assert.AreSame(Base(1, c), SkillCatalog.ActiveForSlot(1, c, ranks));
            }
        }
    }
}
