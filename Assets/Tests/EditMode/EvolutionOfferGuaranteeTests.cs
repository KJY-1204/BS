using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace BS.Tests
{
    public class EvolutionOfferGuaranteeTests
    {
        static LevelingSim NewSim(int seed, byte character, bool guarantee = true)
        {
            var sim = new LevelingSim(new System.Random(seed)) { GuaranteeEvolutionOffer = guarantee };
            sim.Register(1, character);
            return sim;
        }

        static bool IsEvolution(byte id) => SkillCatalog.Get(id).EvolvesFrom != 0;

        static void MaxEveryBaseSkillOfSlots(LevelingSim sim, byte character, params int[] slots)
        {
            foreach (int slot in slots)
            {
                SkillDef b = SkillCatalog.ForSlot(slot, character);
                sim.GrantRank(1, b.Id, b.MaxRank);
            }
        }

        static byte[] FirstOffer(LevelingSim sim)
        {
            sim.AddXp(1, GrowthConfig.XpNeeded(1));
            return sim.CurrentChoices(1);
        }

        [Test]
        public void AnAvailableEvolutionIsAlwaysInTheOfferAndTheOfferKeepsItsSizeWithNoRepeats()
        {
            for (byte c = 1; c <= 4; c++)
                for (int seed = 0; seed < 200; seed++)
                {
                    LevelingSim sim = NewSim(seed, c);
                    MaxEveryBaseSkillOfSlots(sim, c, 0, 1); // Q and E at the top: R is not available yet by choice, evolutions of Q and E are
                    byte[] offer = FirstOffer(sim);

                    Assert.AreEqual(GrowthConfig.OfferChoices, offer.Length, $"character {c} seed {seed}");
                    Assert.AreEqual(offer.Length, offer.Distinct().Count(), "no skill twice");
                    Assert.IsTrue(offer.Any(IsEvolution), $"character {c} seed {seed}: an evolution is offered");
                }
        }

        [Test]
        public void EveryAvailableEvolutionIsOfferedOverManyOffers()
        {
            const byte character = CharacterIds.Reimu;
            var seen = new HashSet<byte>();
            for (int seed = 0; seed < 400; seed++)
            {
                LevelingSim sim = NewSim(seed, character);
                MaxEveryBaseSkillOfSlots(sim, character, 0, 1);
                foreach (byte id in FirstOffer(sim)) if (IsEvolution(id)) seen.Add(id);
            }

            var expected = SkillCatalog.All.Where(d => d.EvolvesFrom != 0 && d.Character == character && d.Slot <= 1).Select(d => d.Id).ToList();
            Assert.AreEqual(4, expected.Count);
            foreach (byte id in expected) CollectionAssert.Contains(seen, id);
        }

        [Test]
        public void WithNoEvolutionAvailableTheOfferIsBuiltAsBefore()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                byte[] on = FirstOffer(NewSim(seed, CharacterIds.Marisa, guarantee: true));
                byte[] off = FirstOffer(NewSim(seed, CharacterIds.Marisa, guarantee: false));

                CollectionAssert.AreEqual(off, on, $"seed {seed}");
                Assert.IsFalse(on.Any(IsEvolution));
            }
        }

        [Test]
        public void ATakenEvolutionAndItsRivalAreNotOfferedAgainWithTheGuarantee()
        {
            const byte character = CharacterIds.Sakuya;
            SkillDef q = SkillCatalog.ForSlot(0, character);
            byte[] evolutions = SkillCatalog.All.Where(d => d.EvolvesFrom == q.Id).Select(d => d.Id).ToArray();
            for (int seed = 0; seed < 100; seed++)
            {
                LevelingSim sim = NewSim(seed, character);
                sim.GrantRank(1, q.Id, q.MaxRank);
                sim.GrantRank(1, evolutions[0], 1);
                byte[] offer = FirstOffer(sim);

                foreach (byte id in evolutions) CollectionAssert.DoesNotContain(offer, id, $"seed {seed}");
            }
        }

        [Test]
        public void TheGuaranteeCanBeSwitchedOffInTheHarnessAndABadValueIsRefused()
        {
            Assert.IsTrue(HarnessOverrides.IsKnown("evolutionOfferGuarantee"));

            HarnessSettings Settings(float v)
            {
                var s = new HarnessSettings();
                s.overrides.Add(new NamedValue { name = "evolutionOfferGuarantee", value = v });
                return s;
            }

            Assert.IsNull(Settings(0f).Validate());
            Assert.IsNull(Settings(1f).Validate());
            StringAssert.Contains("evolutionOfferGuarantee", Settings(2f).Validate());

            BossCatalog.SetCurrentKind(BossCatalog.Standard);
            Assert.IsFalse(MatchSession.Start(Settings(0f), 1).World.Leveling.GuaranteeEvolutionOffer);
            Assert.IsTrue(MatchSession.Start(Settings(1f), 1).World.Leveling.GuaranteeEvolutionOffer);
        }

        [Test]
        public void WithTheGuaranteeOffTheOfferIsOftenWithoutAnAvailableEvolution()
        {
            int without = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                LevelingSim sim = NewSim(seed, CharacterIds.Reimu, guarantee: false);
                MaxEveryBaseSkillOfSlots(sim, CharacterIds.Reimu, 0, 1);
                if (!FirstOffer(sim).Any(IsEvolution)) without++;
            }

            Assert.Greater(without, 0, "that is the situation the guarantee exists for");
        }
    }
}
