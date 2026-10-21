using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class CharacterPassiveTests
    {
        static readonly byte[] Characters = { CharacterIds.Reimu, CharacterIds.Marisa, CharacterIds.Sakuya, CharacterIds.Sanae };

        static int[] NoRanks() => new int[SkillCatalog.MaxSkillId + 1];

        static int[] Ranks(params (byte id, int rank)[] set)
        {
            int[] ranks = NoRanks();
            foreach ((byte id, int rank) in set) ranks[id] = rank;
            return ranks;
        }

        static SkillDef OwnPassive(byte character) =>
            SkillCatalog.All.Single(d => !d.IsActive && d.Passive != PassiveEffectKind.None && d.Character == character);

        static SkillDef QSkill(byte character) => SkillCatalog.ForSlot(0, character);

        // ---- the data ----

        [Test]
        public void EveryCharacterHasExactlyOneOwnPassive()
        {
            foreach (byte c in Characters)
                Assert.AreEqual(1, SkillCatalog.All.Count(d => !d.IsActive && d.Passive != PassiveEffectKind.None && d.Character == c), $"character {c}");
        }

        [Test]
        public void TheFourOwnPassivesHaveFourDifferentEffects()
        {
            var kinds = Characters.Select(c => OwnPassive(c).Passive).ToList();
            Assert.AreEqual(4, kinds.Distinct().Count());
            Assert.AreEqual(PassiveEffectKind.SkillManaCost, OwnPassive(CharacterIds.Reimu).Passive);
            Assert.AreEqual(PassiveEffectKind.SkillPower, OwnPassive(CharacterIds.Marisa).Passive);
            Assert.AreEqual(PassiveEffectKind.SkillCooldown, OwnPassive(CharacterIds.Sakuya).Passive);
            Assert.AreEqual(PassiveEffectKind.ManaRegen, OwnPassive(CharacterIds.Sanae).Passive);
        }

        [Test]
        public void EveryOwnPassiveHasRankThreeANameADescriptionAndAValue()
        {
            foreach (byte c in Characters)
            {
                SkillDef def = OwnPassive(c);
                Assert.AreEqual(3, def.MaxRank);
                Assert.IsFalse(string.IsNullOrEmpty(def.Name));
                Assert.IsFalse(string.IsNullOrEmpty(def.Description));
                Assert.Greater(def.PassivePerRank, 0f);
            }
        }

        [Test]
        public void EveryOwnPassiveRequiresTheQSkillOfItsCharacterAtRankOne()
        {
            foreach (byte c in Characters)
            {
                SkillDef def = OwnPassive(c);
                Assert.AreEqual(QSkill(c).Id, def.RequiresSkill, $"character {c}");
                Assert.AreEqual(1, def.RequiresRank);
            }
        }

        [Test]
        public void TheBasicPassivesAreUnchangedAndStillCommon()
        {
            foreach (byte id in new[] { SkillIds.Power, SkillIds.Haste, SkillIds.Vitality, SkillIds.Focus })
            {
                SkillDef def = SkillCatalog.Get(id);
                Assert.AreEqual(CharacterIds.None, def.Character);
                Assert.AreEqual(PassiveEffectKind.None, def.Passive);
                Assert.AreEqual(5, def.MaxRank);
            }
        }

        [Test]
        public void TheNewSkillNumbersAreFreeAndWithinThePackedRange()
        {
            foreach (byte c in Characters)
            {
                byte id = OwnPassive(c).Id;
                Assert.GreaterOrEqual(id, 13);
                Assert.LessOrEqual(id, SkillCatalog.MaxSkillId);
            }
            Assert.AreEqual(SkillCatalog.All.Count, SkillCatalog.All.Select(d => d.Id).Distinct().Count());
        }

        // ---- packing ----

        [Test]
        public void EveryIdFromOneToSixteenSurvivesPackingWithoutMixingNeighbours()
        {
            for (int id = 1; id <= SkillCatalog.MaxSkillId; id++)
            for (int rank = 0; rank <= SkillCatalog.MaxPackedRank; rank++)
            {
                int[] ranks = NoRanks();
                ranks[id] = rank;

                var back = NoRanks();
                SkillCatalog.Unpack(SkillCatalog.Pack(ranks), back);

                for (int other = 0; other < back.Length; other++)
                    Assert.AreEqual(other == id ? rank : 0, back[other], $"id {id} rank {rank} other {other}");
            }
        }

        [Test]
        public void AllSixteenSkillsFitTheSixtyFourBitsAtOnce()
        {
            int[] ranks = NoRanks();
            for (int id = 1; id <= 16; id++) ranks[id] = (id % 15) + 1;

            var back = NoRanks();
            SkillCatalog.Unpack(SkillCatalog.Pack(ranks), back);

            for (int id = 1; id <= 16; id++) Assert.AreEqual(ranks[id], back[id], $"id {id}");
            Assert.AreEqual(0, back[0], "id zero is not a skill");
        }

        [Test]
        public void IdSixteenUsesTheHighestFourBits()
        {
            int[] ranks = NoRanks();
            ranks[16] = 3;
            Assert.AreEqual(3UL << 60, SkillCatalog.Pack(ranks));
            ranks = NoRanks();
            ranks[1] = 2;
            Assert.AreEqual(2UL, SkillCatalog.Pack(ranks));
        }

        // ---- who is offered what ----

        [Test]
        public void AnOwnPassiveIsOfferedToItsCharacterOnlyAfterTheQSkill()
        {
            foreach (byte c in Characters)
            {
                SkillDef passive = OwnPassive(c);
                Assert.IsFalse(SkillCatalog.IsAvailable(passive, NoRanks(), c), $"character {c} before the Q skill");

                int[] ranks = Ranks((QSkill(c).Id, 1));
                Assert.IsTrue(SkillCatalog.IsAvailable(passive, ranks, c), $"character {c} after the Q skill");
            }
        }

        [Test]
        public void NobodyIsOfferedAnotherCharactersPassive()
        {
            foreach (byte owner in Characters)
            {
                SkillDef passive = OwnPassive(owner);
                foreach (byte other in Characters.Where(x => x != owner))
                {
                    int[] ranks = Ranks((QSkill(owner).Id, 3), (QSkill(other).Id, 3));
                    Assert.IsFalse(SkillCatalog.IsAvailable(passive, ranks, other), $"{passive.Name} offered to {other}");
                }
            }
        }

        [Test]
        public void AnOwnPassiveAtItsMaximumIsNotOfferedAgain()
        {
            foreach (byte c in Characters)
            {
                SkillDef passive = OwnPassive(c);
                int[] ranks = Ranks((QSkill(c).Id, 1), (passive.Id, passive.MaxRank));
                Assert.IsFalse(SkillCatalog.IsAvailable(passive, ranks, c));
            }
        }

        [Test]
        public void TheOfferPoolOfAnOwnerContainsOnlyItsOwnPassive()
        {
            var level = new LevelingSim(new System.Random(7));
            var seenPassives = new HashSet<byte>();

            for (int round = 0; round < 60; round++)
            {
                level.Register(1, CharacterIds.Marisa);
                level.GrantRank(1, SkillIds.MasterSpark, 1);
                level.AddXp(1, 1000000);
                byte[] choices = level.CurrentChoices(1);
                if (choices != null)
                    foreach (byte choice in choices)
                        if (SkillCatalog.Get(choice) != null && !SkillCatalog.Get(choice).IsActive) seenPassives.Add(choice);
                level.Unregister(1);
            }

            foreach (byte id in seenPassives)
            {
                SkillDef def = SkillCatalog.Get(id);
                Assert.IsTrue(def.Character == CharacterIds.None || def.Character == CharacterIds.Marisa, def.Name);
            }
            Assert.IsTrue(seenPassives.Contains(SkillIds.FirePower), "the character's own passive shows up in offers");
        }

        // ---- the effects ----

        [Test]
        public void TheCostPassiveLowersTheManaCostByRankWithAFloorOfOne()
        {
            SkillDef burst = SkillCatalog.Get(SkillIds.Burst);
            Assert.AreEqual(30, SkillCatalog.EffectiveManaCost(burst, Ranks((SkillIds.Burst, 1)), CharacterIds.Reimu));
            Assert.AreEqual(27, SkillCatalog.EffectiveManaCost(burst, Ranks((SkillIds.Burst, 1), (SkillIds.SpiritSaving, 1)), CharacterIds.Reimu));
            Assert.AreEqual(24, SkillCatalog.EffectiveManaCost(burst, Ranks((SkillIds.Burst, 1), (SkillIds.SpiritSaving, 2)), CharacterIds.Reimu));
            Assert.AreEqual(21, SkillCatalog.EffectiveManaCost(burst, Ranks((SkillIds.Burst, 1), (SkillIds.SpiritSaving, 3)), CharacterIds.Reimu));

            var cheap = new SkillDef { ManaCost = 1 };
            Assert.AreEqual(1, SkillCatalog.EffectiveManaCost(cheap, Ranks((SkillIds.SpiritSaving, 3)), CharacterIds.Reimu));
        }

        [Test]
        public void ThePowerPassiveRaisesDamageAndHealingButNotZero()
        {
            int[] ranks = Ranks((SkillIds.FirePower, 2));
            Assert.AreEqual(52, SkillCatalog.EffectivePower(40, ranks, CharacterIds.Marisa), "40 x 1.30");
            Assert.AreEqual(0, SkillCatalog.EffectivePower(0, ranks, CharacterIds.Marisa));
            Assert.AreEqual(40, SkillCatalog.EffectivePower(40, NoRanks(), CharacterIds.Marisa));
            Assert.AreEqual(40, SkillCatalog.EffectivePower(40, ranks, CharacterIds.Reimu), "another character's ranks do not count");
        }

        [Test]
        public void TheCooldownPassiveShortensWithALowerBound()
        {
            SkillDef stop = SkillCatalog.Get(SkillIds.TimeStop);
            Assert.AreEqual(20f, SkillCatalog.EffectiveCooldown(stop, NoRanks(), CharacterIds.Sakuya), 1e-4f);
            Assert.AreEqual(20f * 0.92f, SkillCatalog.EffectiveCooldown(stop, Ranks((SkillIds.TimeSense, 1)), CharacterIds.Sakuya), 1e-4f);
            Assert.AreEqual(20f * 0.76f, SkillCatalog.EffectiveCooldown(stop, Ranks((SkillIds.TimeSense, 3)), CharacterIds.Sakuya), 1e-4f);

            // A very strong passive (a test value) cannot make a cooldown shorter than the bound.
            var strong = new SkillDef { Id = 99, Character = CharacterIds.Sakuya, Passive = PassiveEffectKind.SkillCooldown, PassivePerRank = 0.5f };
            Assert.AreEqual(SkillCatalog.MinSkillCooldownMultiplier, Mathf.Max(SkillCatalog.MinSkillCooldownMultiplier, 1f - 1.5f), 1e-6f);
            Assert.AreEqual(10f * SkillCatalog.MinSkillCooldownMultiplier, 10f * Mathf.Max(SkillCatalog.MinSkillCooldownMultiplier, 1f - strong.PassivePerRank * 3), 1e-5f);
        }

        [Test]
        public void TheManaRecoveryPassiveRaisesTheRecoveryInTheStatCalculation()
        {
            CharacterDef sanae = CharacterCatalog.Get(CharacterIds.Sanae);
            float before = StatCalc.Compute(1, NoRanks(), sanae).ManaRegenPerSecond;
            float rank2 = StatCalc.Compute(1, Ranks((SkillIds.Faith, 2)), sanae).ManaRegenPerSecond;
            float rank3 = StatCalc.Compute(1, Ranks((SkillIds.Faith, 3)), sanae).ManaRegenPerSecond;

            Assert.AreEqual(before + 3f, rank2, 1e-4f);
            Assert.AreEqual(before + 4.5f, rank3, 1e-4f);
        }

        [Test]
        public void TheManaRecoveryPassiveChangesNothingElseInTheStats()
        {
            CharacterDef sanae = CharacterCatalog.Get(CharacterIds.Sanae);
            SurvivorStats a = StatCalc.Compute(4, NoRanks(), sanae);
            SurvivorStats b = StatCalc.Compute(4, Ranks((SkillIds.Faith, 3)), sanae);

            Assert.AreEqual(a.MaxHealth, b.MaxHealth);
            Assert.AreEqual(a.MaxMana, b.MaxMana);
            Assert.AreEqual(a.ProjectileDamage, b.ProjectileDamage);
            Assert.AreEqual(a.CooldownMultiplier, b.CooldownMultiplier, 1e-6f);
        }

        [Test]
        public void ARecoveryPassiveRankOfAnotherCharacterIsIgnoredByTheStats()
        {
            CharacterDef reimu = CharacterCatalog.Get(CharacterIds.Reimu);
            Assert.AreEqual(StatCalc.Compute(1, NoRanks(), reimu).ManaRegenPerSecond,
                            StatCalc.Compute(1, Ranks((SkillIds.Faith, 3)), reimu).ManaRegenPerSecond, 1e-5f);
            Assert.AreEqual(StatCalc.Compute(1, NoRanks()).ManaRegenPerSecond, StatCalc.Compute(1, Ranks((SkillIds.Faith, 3))).ManaRegenPerSecond, 1e-5f);
        }

        [Test]
        public void ACharacterWithoutPassivesGetsTheOldValues()
        {
            Assert.AreEqual(0f, SkillCatalog.PassiveTotal(NoRanks(), CharacterIds.Reimu, PassiveEffectKind.SkillManaCost), 1e-6f);
            Assert.AreEqual(0f, SkillCatalog.PassiveTotal(Ranks((SkillIds.Power, 5)), CharacterIds.Reimu, PassiveEffectKind.SkillManaCost), 1e-6f);
            Assert.AreEqual(0f, SkillCatalog.PassiveTotal(Ranks((SkillIds.SpiritSaving, 3)), CharacterIds.None, PassiveEffectKind.SkillManaCost), 1e-6f);
        }

        // ---- the skill simulation ----

        static SkillSim NewSim(int mana)
        {
            var sim = new SkillSim();
            sim.Register(1, mana, 0f);
            return sim;
        }

        [Test]
        public void ASkillCanBeCastWithTheReducedCostOnlyWhenThePassiveIsLearned()
        {
            // Burst costs 30. With 27 mana it only works with Spirit Saving rank 1 (cost 27).
            var without = NewSim(27);
            Assert.AreEqual(SkillResult.NoMana, without.TryActivate(1, CharacterIds.Reimu, 0, true, Ranks((SkillIds.Burst, 1)), out _));

            var with = NewSim(27);
            Assert.AreEqual(SkillResult.Fired, with.TryActivate(1, CharacterIds.Reimu, 0, true, Ranks((SkillIds.Burst, 1), (SkillIds.SpiritSaving, 1)), out _));
            Assert.AreEqual(0f, with.Mana(1), 1e-3f, "the reduced cost is what is spent");
        }

        [Test]
        public void MasterSparkHitsHarderWithFirePowerAndStardustIsUnchanged()
        {
            int[] plain = Ranks((SkillIds.MasterSpark, 1));
            int[] strong = Ranks((SkillIds.MasterSpark, 1), (SkillIds.FirePower, 2));

            NewSim(100).TryActivate(1, CharacterIds.Marisa, 0, true, plain, out SkillActivation a);
            NewSim(100).TryActivate(1, CharacterIds.Marisa, 0, true, strong, out SkillActivation b);
            Assert.Greater(a.Damage, 0);
            Assert.AreEqual(SkillCatalog.EffectivePower(a.Damage, strong, CharacterIds.Marisa), b.Damage);
            Assert.Greater(b.Damage, a.Damage);

            int[] stardust = Ranks((SkillIds.RapidFire, 1));
            int[] stardustStrong = Ranks((SkillIds.RapidFire, 1), (SkillIds.FirePower, 3));
            NewSim(100).TryActivate(1, CharacterIds.Marisa, 1, true, stardust, out SkillActivation c);
            NewSim(100).TryActivate(1, CharacterIds.Marisa, 1, true, stardustStrong, out SkillActivation d);
            Assert.AreEqual(c.Damage, d.Damage);
            Assert.AreEqual(c.Amount, d.Amount);
        }

        [Test]
        public void TheCooldownStartedByASkillIsShorterWithTimeSenseAndARunningOneIsNot()
        {
            int[] plain = Ranks((SkillIds.TimeStop, 1));
            int[] quick = Ranks((SkillIds.TimeStop, 1), (SkillIds.TimeSense, 2));
            SkillDef def = SkillCatalog.Get(SkillIds.TimeStop);
            int slot = def.Slot;

            var sim = NewSim(100);
            Assert.AreEqual(SkillResult.Fired, sim.TryActivate(1, CharacterIds.Sakuya, slot, true, plain, out SkillActivation first));
            Assert.AreEqual(def.Cooldown, sim.CooldownLeft(1, slot), 1e-3f);

            // Learning the passive now does not touch the cooldown that is already running.
            Assert.AreEqual(def.Cooldown, sim.CooldownLeft(1, slot), 1e-3f);

            var next = NewSim(100);
            Assert.AreEqual(SkillResult.Fired, next.TryActivate(1, CharacterIds.Sakuya, slot, true, quick, out SkillActivation second));
            Assert.AreEqual(def.Cooldown * 0.84f, next.CooldownLeft(1, slot), 1e-3f);
            Assert.AreEqual(def.Cooldown * 0.84f, second.Cooldown, 1e-3f);
            Assert.AreEqual(def.Cooldown, first.Cooldown, 1e-3f);
        }

        [Test]
        public void ACurrentManaIsNotChangedByLearningTheRecoveryPassive()
        {
            var sim = NewSim(20);
            CharacterDef sanae = CharacterCatalog.Get(CharacterIds.Sanae);
            SurvivorStats stats = StatCalc.Compute(1, Ranks((SkillIds.Faith, 3)), sanae);
            sim.SetStats(1, stats.MaxMana, stats.ManaRegenPerSecond);
            Assert.AreEqual(20f, sim.Mana(1), 1e-3f);
        }

        [Test]
        public void TheValuesTheHudShowsAreTheValuesTheServerSimulationUses()
        {
            foreach (byte c in Characters)
            {
                SkillDef passive = OwnPassive(c);
                for (int slot = 0; slot < SkillCatalog.SkillSlots; slot++)
                {
                    SkillDef def = SkillCatalog.ForSlot(slot, c);
                    int[] ranks = Ranks((def.Id, 1), (passive.Id, passive.MaxRank));
                    // The Q skill is the prerequisite, so give it too when this is the E slot.
                    ranks[QSkill(c).Id] = 1;

                    var sim = NewSim(100);
                    Assert.AreEqual(SkillResult.Fired, sim.TryActivate(1, c, slot, true, ranks, out SkillActivation activation), $"character {c} slot {slot}");

                    Assert.AreEqual(100f - SkillCatalog.EffectiveManaCost(def, ranks, c), sim.Mana(1), 1e-3f);
                    Assert.AreEqual(SkillCatalog.EffectiveCooldown(def, ranks, c), sim.CooldownLeft(1, slot), 1e-3f);
                    Assert.AreEqual(SkillCatalog.EffectiveCooldown(def, ranks, c), activation.Cooldown, 1e-3f);
                }
            }
        }
    }
}
