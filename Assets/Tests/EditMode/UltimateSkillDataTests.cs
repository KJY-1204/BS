using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace BS.Tests
{
    public class UltimateSkillDataTests
    {
        static readonly byte[] Characters = { CharacterIds.Reimu, CharacterIds.Marisa, CharacterIds.Sakuya, CharacterIds.Sanae };

        static int[] NoRanks() => new int[SkillCatalog.MaxSkillId + 1];

        static SkillDef Ultimate(byte character) => SkillCatalog.ForSlot(SkillCatalog.UltimateSlot, character);

        // ---- 1.1 the third slot and the ids ----

        [Test]
        public void ThereAreThreeSlotsAndEveryCharacterOwnsAnRSkill()
        {
            Assert.AreEqual(3, SkillCatalog.SkillSlots);
            Assert.AreEqual(2, SkillCatalog.UltimateSlot);
            foreach (byte c in Characters)
            {
                SkillDef r = Ultimate(c);
                Assert.IsNotNull(r, $"character {c}");
                Assert.IsTrue(r.IsActive);
                Assert.AreEqual(c, r.Character);
                Assert.AreEqual(3, r.MaxRank);
            }
        }

        [Test]
        public void TheFourRSkillsHaveTheirOwnIdsWithinThePackedRange()
        {
            byte[] ids = Characters.Select(c => Ultimate(c).Id).ToArray();
            CollectionAssert.AreEqual(new[] { SkillIds.FantasyHeaven, SkillIds.FinalSpark, SkillIds.KillingDoll, SkillIds.GreatMiracle }, ids);
            Assert.AreEqual(4, ids.Distinct().Count());
            Assert.AreEqual(SkillCatalog.All.Count, SkillCatalog.All.Select(d => d.Id).Distinct().Count(), "no id is shared with another skill");
            foreach (byte id in ids) Assert.LessOrEqual(id, SkillCatalog.MaxSkillId);
        }

        [Test]
        public void TheRanksOfTheRSkillsRoundTripThroughThePackedValues()
        {
            int[] ranks = NoRanks();
            ranks[SkillIds.FantasyHeaven] = 1;
            ranks[SkillIds.FinalSpark] = 2;
            ranks[SkillIds.KillingDoll] = 3;
            ranks[SkillIds.GreatMiracle] = 2;
            ranks[SkillIds.DivineBlessing] = 3; // the id next to them
            ranks[SkillCatalog.IdsPerPackedValue] = 3;
            ranks[SkillCatalog.IdsPerPackedValue + 1] = 1;

            int[] back = NoRanks();
            SkillCatalog.Unpack(SkillCatalog.PackLow(ranks), SkillCatalog.PackHigh(ranks), back);

            CollectionAssert.AreEqual(ranks, back);
        }

        [Test]
        public void UnknownSlotsStillGiveNothing()
        {
            Assert.IsNull(SkillCatalog.ForSlot(SkillCatalog.SkillSlots, CharacterIds.Reimu));
            Assert.IsNull(SkillCatalog.ForSlot(SkillCatalog.UltimateSlot, CharacterIds.None));
        }

        // ---- 1.2 the values ----

        [Test]
        public void AnRSkillCostsMoreAndCoolsDownLongerThanTheCharactersQAndE()
        {
            foreach (byte c in Characters)
            {
                SkillDef r = Ultimate(c);
                foreach (int slot in new[] { 0, 1 })
                {
                    SkillDef other = SkillCatalog.ForSlot(slot, c);
                    Assert.Greater(r.ManaCost, other.ManaCost, $"character {c} slot {slot}");
                    Assert.Greater(r.Cooldown, other.Cooldown, $"character {c} slot {slot}");
                }
            }
        }

        [Test]
        public void EveryRSkillHasValuesForEachRankThatNeverShrink()
        {
            foreach (byte c in Characters)
            {
                SkillDef r = Ultimate(c);
                foreach (float[] values in new[] { r.Radius, r.Length, r.HalfWidth })
                    if (values != null) AssertNonDecreasing(values.Select(v => (double)v).ToArray(), r.MaxRank, r.Name);
                foreach (int[] values in new[] { r.Damage, r.Count, r.Amount })
                    if (values != null) AssertNonDecreasing(values.Select(v => (double)v).ToArray(), r.MaxRank, r.Name);
                Assert.Greater(r.Cooldown, 0f);
                Assert.Greater(r.ManaCost, 0);
            }
        }

        static void AssertNonDecreasing(double[] values, int maxRank, string name)
        {
            Assert.AreEqual(maxRank, values.Length, name);
            for (int i = 1; i < values.Length; i++) Assert.GreaterOrEqual(values[i], values[i - 1], name);
        }

        [Test]
        public void TheRSkillsAreBiggerThanTheSkillsOfTheSameKindAtTheSameRank()
        {
            SkillDef q = SkillCatalog.Get(SkillIds.Burst), rr = SkillCatalog.Get(SkillIds.FantasyHeaven);
            SkillDef beam = SkillCatalog.Get(SkillIds.MasterSpark), finalSpark = SkillCatalog.Get(SkillIds.FinalSpark);
            SkillDef knives = SkillCatalog.Get(SkillIds.KnifeBarrage), doll = SkillCatalog.Get(SkillIds.KillingDoll);
            SkillDef miracle = SkillCatalog.Get(SkillIds.Miracle), great = SkillCatalog.Get(SkillIds.GreatMiracle);

            for (int i = 0; i < 3; i++)
            {
                Assert.Greater(rr.Radius[i], q.Radius[i]);
                Assert.Greater(rr.Damage[i], q.Damage[i]);

                Assert.Greater(finalSpark.Length[i], beam.Length[i]);
                Assert.Greater(finalSpark.HalfWidth[i], beam.HalfWidth[i]);
                Assert.Greater(finalSpark.Damage[i], beam.Damage[i]);

                Assert.Greater(doll.Count[i], knives.Count[i]);
                Assert.Greater(doll.Damage[i], knives.Damage[i]);

                Assert.Greater(great.Radius[i], miracle.Radius[i]);
                Assert.Greater(great.Amount[i], miracle.Amount[i]);
            }
        }

        [Test]
        public void TheEffectKindsAreTheExistingOnes()
        {
            Assert.AreEqual(SkillEffectKind.AreaDamage, Ultimate(CharacterIds.Reimu).Effect);
            Assert.AreEqual(SkillEffectKind.Beam, Ultimate(CharacterIds.Marisa).Effect);
            Assert.AreEqual(SkillEffectKind.Volley, Ultimate(CharacterIds.Sakuya).Effect);
            Assert.AreEqual(SkillEffectKind.Heal, Ultimate(CharacterIds.Sanae).Effect);
        }

        [Test]
        public void AnRSkillBelongsOnlyToItsCharacter()
        {
            foreach (byte c in Characters)
            foreach (byte other in Characters.Where(o => o != c))
                Assert.IsFalse(SkillCatalog.IsOwnedBy(Ultimate(c), other));
        }

        // ---- 1.3 how an R skill is earned ----

        [Test]
        public void TheRSkillNeedsTheOwnQSkillAtRankTwo()
        {
            foreach (byte c in Characters)
            {
                SkillDef q = SkillCatalog.ForSlot(0, c), r = Ultimate(c);
                Assert.AreEqual(q.Id, r.RequiresSkill, $"character {c}");
                Assert.AreEqual(2, r.RequiresRank);

                int[] ranks = NoRanks();
                Assert.IsFalse(SkillCatalog.IsAvailable(r, ranks, c), "no Q skill");
                ranks[q.Id] = 1;
                Assert.IsFalse(SkillCatalog.IsAvailable(r, ranks, c), "Q at rank one");
                ranks[q.Id] = 2;
                Assert.IsTrue(SkillCatalog.IsAvailable(r, ranks, c), "Q at rank two");
                ranks[r.Id] = r.MaxRank;
                Assert.IsFalse(SkillCatalog.IsAvailable(r, ranks, c), "at its maximum rank");
            }
        }

        [Test]
        public void TheRSkillOfAnotherCharacterIsNeverAvailable()
        {
            int[] ranks = NoRanks();
            foreach (byte c in Characters) ranks[SkillCatalog.ForSlot(0, c).Id] = 3;
            foreach (byte c in Characters)
            foreach (byte other in Characters.Where(o => o != c))
                Assert.IsFalse(SkillCatalog.IsAvailable(Ultimate(c), ranks, other));
        }

        static LevelingSim NewSim(int seed, byte character)
        {
            var sim = new LevelingSim(new System.Random(seed));
            sim.Register(1, character);
            return sim;
        }

        [Test]
        public void ALevelUpNeverOffersTheRSkillWhileTheQSkillIsAtRankOne()
        {
            foreach (byte c in Characters)
            {
                SkillDef q = SkillCatalog.ForSlot(0, c), r = Ultimate(c);
                for (int seed = 0; seed < 60; seed++)
                {
                    LevelingSim sim = NewSim(seed, c);
                    sim.GrantRank(1, q.Id, 1);
                    sim.AddXp(1, GrowthConfig.XpNeeded(1));
                    Assert.IsTrue(sim.HasOffer(1));
                    CollectionAssert.DoesNotContain(sim.CurrentChoices(1), r.Id, $"character {c} seed {seed}");
                }
            }
        }

        [Test]
        public void ALevelUpCanOfferTheRSkillOnceTheQSkillIsAtRankTwoAndChoosingItGivesRankOne()
        {
            foreach (byte c in Characters)
            {
                SkillDef q = SkillCatalog.ForSlot(0, c), r = Ultimate(c);
                bool offered = false;
                for (int seed = 0; seed < 60 && !offered; seed++)
                {
                    LevelingSim sim = NewSim(seed, c);
                    sim.GrantRank(1, q.Id, 2);
                    sim.AddXp(1, GrowthConfig.XpNeeded(1));
                    byte[] choices = sim.CurrentChoices(1);
                    int index = System.Array.IndexOf(choices, r.Id);
                    if (index < 0) continue;

                    offered = true;
                    Assert.IsTrue(sim.Pick(1, sim.CurrentOfferId(1), index));
                    Assert.AreEqual(1, sim.RankOf(1, r.Id), $"character {c}");
                }
                Assert.IsTrue(offered, $"character {c} was never offered the R skill in 60 tries");
            }
        }

        [Test]
        public void ARankThreeRSkillIsNotOfferedAgain()
        {
            foreach (byte c in Characters)
            {
                SkillDef q = SkillCatalog.ForSlot(0, c), r = Ultimate(c);
                for (int seed = 0; seed < 40; seed++)
                {
                    LevelingSim sim = NewSim(seed, c);
                    sim.GrantRank(1, q.Id, 2);
                    sim.GrantRank(1, r.Id, r.MaxRank);
                    sim.AddXp(1, GrowthConfig.XpNeeded(1));
                    CollectionAssert.DoesNotContain(sim.CurrentChoices(1), r.Id);
                }
            }
        }

        [Test]
        public void NothingGivesTheRSkillAutomaticallyAllTheWayToLevelTwenty()
        {
            foreach (byte c in Characters)
            {
                SkillDef q = SkillCatalog.ForSlot(0, c), r = Ultimate(c);
                LevelingSim sim = NewSim(7, c);
                // Take everything but the Q skill, so the prerequisite is never met.
                for (int guard = 0; guard < 200 && sim.Level(1) < GrowthConfig.MaxLevel; guard++)
                {
                    sim.AddXp(1, GrowthConfig.XpNeeded(sim.Level(1)));
                    while (sim.HasOffer(1))
                    {
                        byte[] choices = sim.CurrentChoices(1);
                        int index = 0;
                        while (index < choices.Length - 1 && choices[index] == q.Id) index++;
                        sim.Pick(1, sim.CurrentOfferId(1), index);
                    }
                }
                Assert.AreEqual(GrowthConfig.MaxLevel, sim.Level(1));
                Assert.AreEqual(0, sim.RankOf(1, r.Id), $"character {c}");
            }
        }
    }
}
