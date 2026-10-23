using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace BS.Tests
{
    public class SkillEvolutionDataTests
    {
        static readonly byte[] Characters = { CharacterIds.Reimu, CharacterIds.Marisa, CharacterIds.Sakuya, CharacterIds.Sanae };

        static int[] NoRanks() => new int[SkillCatalog.MaxSkillId + 1];

        static SkillDef Q(byte character) => SkillCatalog.ForSlot(0, character);

        static List<SkillDef> EvolutionsOf(SkillDef baseSkill) => SkillCatalog.All.Where(d => d.EvolvesFrom == baseSkill.Id).ToList();

        // ---- 1.1 the lookup and the exclusion ----

        [Test]
        public void ForSlotNeverReturnsAnEvolution()
        {
            foreach (byte c in Characters)
                for (int slot = 0; slot < SkillCatalog.SkillSlots; slot++)
                    Assert.AreEqual(0, SkillCatalog.ForSlot(slot, c).EvolvesFrom, $"character {c} slot {slot}");
        }

        [Test]
        public void ActiveForSlotIsTheBaseSkillUntilAnEvolutionIsLearned()
        {
            foreach (byte c in Characters)
            {
                SkillDef q = Q(c);
                SkillDef first = EvolutionsOf(q)[0], second = EvolutionsOf(q)[1];
                int[] ranks = NoRanks();

                Assert.AreSame(q, SkillCatalog.ActiveForSlot(0, c, ranks));
                ranks[q.Id] = q.MaxRank;
                Assert.AreSame(q, SkillCatalog.ActiveForSlot(0, c, ranks), "the base skill at its maximum rank still fires itself");

                ranks[first.Id] = 1;
                Assert.AreSame(first, SkillCatalog.ActiveForSlot(0, c, ranks));
                ranks[first.Id] = 0;
                ranks[second.Id] = 1;
                Assert.AreSame(second, SkillCatalog.ActiveForSlot(0, c, ranks));
            }
        }

        [Test]
        public void AnEvolutionIsLearnedByItsOwnRank_WhateverTheBaseSkillsRank()
        {
            SkillDef q = Q(CharacterIds.Reimu);
            SkillDef evolution = EvolutionsOf(q)[0];
            int[] ranks = NoRanks();
            ranks[evolution.Id] = 1; // the base skill has no rank at all

            Assert.AreSame(evolution, SkillCatalog.ActiveForSlot(0, CharacterIds.Reimu, ranks));
        }

        [Test]
        public void OtherSlotsAreNeverReplaced()
        {
            int[] ranks = NoRanks();
            foreach (SkillDef d in SkillCatalog.All.Where(d => d.EvolvesFrom != 0)) ranks[d.Id] = 1;
            foreach (byte c in Characters)
                foreach (int slot in new[] { 1, 2 })
                    Assert.AreSame(SkillCatalog.ForSlot(slot, c), SkillCatalog.ActiveForSlot(slot, c, ranks));
            Assert.IsNull(SkillCatalog.ActiveForSlot(0, CharacterIds.None, ranks));
        }

        [Test]
        public void LearningOneEvolutionMakesTheOtherAndItselfUnavailable()
        {
            foreach (byte c in Characters)
            {
                SkillDef q = Q(c);
                SkillDef a = EvolutionsOf(q)[0], b = EvolutionsOf(q)[1];
                int[] ranks = NoRanks();
                ranks[q.Id] = q.MaxRank;
                Assert.IsTrue(SkillCatalog.IsAvailable(a, ranks, c));
                Assert.IsTrue(SkillCatalog.IsAvailable(b, ranks, c));

                ranks[a.Id] = 1;
                Assert.IsFalse(SkillCatalog.IsAvailable(b, ranks, c), "the other evolution");
                Assert.IsFalse(SkillCatalog.IsAvailable(a, ranks, c), "itself, at its maximum rank");
            }
        }

        // ---- 1.2 the data ----

        [Test]
        public void ThereAreEightEvolutionsTwoForEachQSkillWithIdsThirtyThreeToForty()
        {
            List<SkillDef> evolutions = SkillCatalog.All.Where(d => d.EvolvesFrom != 0).ToList();
            Assert.AreEqual(8, evolutions.Count);
            CollectionAssert.AreEquivalent(Enumerable.Range(33, 8).Select(i => (byte)i), evolutions.Select(d => d.Id));
            foreach (byte id in evolutions.Select(d => d.Id)) Assert.LessOrEqual(id, SkillCatalog.MaxSkillId);
            foreach (byte c in Characters) Assert.AreEqual(2, EvolutionsOf(Q(c)).Count, $"character {c}");
            Assert.AreEqual(SkillCatalog.All.Count, SkillCatalog.All.Select(d => d.Id).Distinct().Count());
        }

        [Test]
        public void EachEvolutionIsAOneRankSkillOfTheSameKindOnTheSameSlotAndExcludesItsSibling()
        {
            foreach (byte c in Characters)
            {
                SkillDef q = Q(c);
                List<SkillDef> pair = EvolutionsOf(q);
                foreach (SkillDef e in pair)
                {
                    Assert.IsTrue(e.IsActive);
                    Assert.AreEqual(c, e.Character);
                    Assert.AreEqual(q.Slot, e.Slot);
                    Assert.AreEqual(1, e.MaxRank);
                    Assert.AreEqual(q.Effect, e.Effect);
                    Assert.AreEqual(q.Id, e.RequiresSkill);
                    Assert.AreEqual(q.MaxRank, e.RequiresRank, "offered at the base skill's maximum rank");
                    Assert.AreEqual(pair.Single(o => o != e).Id, e.ExclusiveWith, "each excludes the other");
                    Assert.Greater(e.ManaCost, 0);
                    Assert.Greater(e.Cooldown, 0f);
                    Assert.IsFalse(string.IsNullOrEmpty(e.Name));
                    Assert.IsFalse(string.IsNullOrEmpty(e.Description));
                }
            }
        }

        [Test]
        public void TheRanksOfTheEvolutionsRoundTripThroughThePackedValues()
        {
            int[] ranks = NoRanks();
            foreach (SkillDef d in SkillCatalog.All.Where(d => d.EvolvesFrom != 0)) ranks[d.Id] = 1;
            ranks[SkillIds.GreatMiracle] = 3;
            ranks[SkillCatalog.MaxSkillId] = 2;

            int[] back = NoRanks();
            SkillCatalog.Unpack(SkillCatalog.PackLow(ranks), SkillCatalog.PackHigh(ranks), back);

            CollectionAssert.AreEqual(ranks, back);
        }

        static IEnumerable<(string name, double value, bool higherIsBetter)> Values(SkillDef d)
        {
            int i = d.MaxRank - 1;
            yield return ("cost", d.ManaCost, false);
            yield return ("cooldown", d.Cooldown, false);
            if (d.Radius != null) yield return ("radius", d.Radius[i], true);
            if (d.Length != null) yield return ("length", d.Length[i], true);
            if (d.HalfWidth != null) yield return ("half width", d.HalfWidth[i], true);
            if (d.Damage != null) yield return ("damage", d.Damage[i], true);
            if (d.Duration != null) yield return ("duration", d.Duration[i], true);
            if (d.Amount != null) yield return ("amount", d.Amount[i], true);
        }

        [Test]
        public void NoEvolutionIsStrictlyBetterThanItsBaseSkillAtTheMaximumRank()
        {
            foreach (SkillDef e in SkillCatalog.All.Where(d => d.EvolvesFrom != 0))
            {
                SkillDef baseSkill = SkillCatalog.Get(e.EvolvesFrom);
                var baseValues = Values(baseSkill).ToDictionary(v => v.name, v => v);
                bool worse = false;
                foreach ((string name, double value, bool higherIsBetter) in Values(e))
                {
                    if (!baseValues.TryGetValue(name, out var reference)) continue;
                    if (higherIsBetter ? value < reference.value : value > reference.value) worse = true;
                }
                Assert.IsTrue(worse, $"{e.Name} is worse in nothing");
            }
        }

        [Test]
        public void TheTwoEvolutionsOfASkillDifferInTheirNumbersAndNames()
        {
            foreach (byte c in Characters)
            {
                List<SkillDef> pair = EvolutionsOf(Q(c));
                Assert.AreNotEqual(pair[0].Name, pair[1].Name);
                var a = Values(pair[0]).Select(v => (v.name, v.value)).ToList();
                var b = Values(pair[1]).Select(v => (v.name, v.value)).ToList();
                CollectionAssert.AreNotEqual(a, b, $"character {c}");
            }
        }

        [Test]
        public void TheRSkillsStayAtLeastAsBigAsTheEvolutionsOfTheSameKind()
        {
            // The spark evolutions stay below Final Spark at its first rank, so the ultimate is never smaller than the skill it follows.
            SkillDef finalSpark = SkillCatalog.Get(SkillIds.FinalSpark);
            foreach (byte id in new[] { SkillIds.SparkWide, SkillIds.SparkLaser })
            {
                SkillDef e = SkillCatalog.Get(id);
                Assert.Less(e.Length[0], finalSpark.Length[0], e.Name);
                Assert.Less(e.HalfWidth[0], finalSpark.HalfWidth[0] + 0.001f, e.Name);
                Assert.Less(e.Damage[0], finalSpark.Damage[0], e.Name);
            }
        }

        // ---- 1.3 how an evolution is offered ----

        static LevelingSim NewSim(int seed, byte character)
        {
            var sim = new LevelingSim(new System.Random(seed));
            sim.Register(1, character);
            return sim;
        }

        [Test]
        public void ALevelUpNeverOffersAnEvolutionBeforeTheBaseSkillIsAtItsMaximumRank()
        {
            foreach (byte c in Characters)
            {
                SkillDef q = Q(c);
                byte[] ids = EvolutionsOf(q).Select(d => d.Id).ToArray();
                for (int seed = 0; seed < 50; seed++)
                {
                    LevelingSim sim = NewSim(seed, c);
                    sim.GrantRank(1, q.Id, q.MaxRank - 1);
                    sim.AddXp(1, GrowthConfig.XpNeeded(1));
                    foreach (byte id in ids) CollectionAssert.DoesNotContain(sim.CurrentChoices(1), id, $"character {c} seed {seed}");
                }
            }
        }

        [Test]
        public void AtTheMaximumRankEitherEvolutionCanBeOfferedAndChoosingOneGivesRankOne()
        {
            foreach (byte c in Characters)
            {
                SkillDef q = Q(c);
                foreach (SkillDef target in EvolutionsOf(q))
                {
                    bool offered = false;
                    for (int seed = 0; seed < 80 && !offered; seed++)
                    {
                        LevelingSim sim = NewSim(seed, c);
                        sim.GrantRank(1, q.Id, q.MaxRank);
                        sim.AddXp(1, GrowthConfig.XpNeeded(1));
                        int index = System.Array.IndexOf(sim.CurrentChoices(1), target.Id);
                        if (index < 0) continue;
                        offered = true;
                        Assert.IsTrue(sim.Pick(1, sim.CurrentOfferId(1), index));
                        Assert.AreEqual(1, sim.RankOf(1, target.Id));
                        Assert.AreEqual(q.MaxRank, sim.RankOf(1, q.Id), "the base skill keeps its rank");
                    }
                    Assert.IsTrue(offered, $"{target.Name} never offered in 80 tries");
                }
            }
        }

        [Test]
        public void AfterTheChoiceNeitherEvolutionIsOfferedAgainAndTheRSkillStillOpens()
        {
            foreach (byte c in Characters)
            {
                SkillDef q = Q(c);
                SkillDef r = SkillCatalog.ForSlot(SkillCatalog.UltimateSlot, c);
                byte[] ids = EvolutionsOf(q).Select(d => d.Id).ToArray();
                LevelingSim sim = NewSim(3, c);
                sim.GrantRank(1, q.Id, q.MaxRank);
                sim.GrantRank(1, ids[0], 1);

                for (int guard = 0; guard < 200 && sim.Level(1) < GrowthConfig.MaxLevel; guard++)
                {
                    sim.AddXp(1, GrowthConfig.XpNeeded(sim.Level(1)));
                    while (sim.HasOffer(1))
                    {
                        foreach (byte id in ids) CollectionAssert.DoesNotContain(sim.CurrentChoices(1), id, $"character {c}");
                        sim.Pick(1, sim.CurrentOfferId(1), 0);
                    }
                }
                Assert.IsTrue(SkillCatalog.IsAvailable(r, NoRanksWith(q.Id, q.MaxRank), c), "the R skill needs the base Q skill, whose rank is kept");
                Assert.AreEqual(0, sim.RankOf(1, ids[1]), "the other evolution was never learned");
            }
        }

        static int[] NoRanksWith(byte id, int rank)
        {
            int[] ranks = NoRanks();
            ranks[id] = rank;
            return ranks;
        }

        [Test]
        public void AnotherCharactersEvolutionIsNeverOffered()
        {
            foreach (byte c in Characters)
            {
                int[] ranks = NoRanks();
                foreach (byte other in Characters) ranks[Q(other).Id] = 3;
                foreach (SkillDef e in SkillCatalog.All.Where(d => d.EvolvesFrom != 0 && d.Character != c))
                    Assert.IsFalse(SkillCatalog.IsAvailable(e, ranks, c), $"{e.Name} for character {c}");
            }
        }
    }
}
