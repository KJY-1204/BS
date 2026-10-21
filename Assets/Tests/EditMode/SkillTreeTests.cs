using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class SkillTreeTests
    {
        static readonly byte[] Characters = { CharacterIds.Reimu, CharacterIds.Marisa, CharacterIds.Sakuya, CharacterIds.Sanae };

        /// <summary>The third passive of each character: it needs the E skill at rank one.</summary>
        static readonly byte[] ThirdPassives = { SkillIds.Guard, SkillIds.StarBullet, SkillIds.PreciseThrow, SkillIds.Merit };

        /// <summary>The fourth passive of each character: it needs the Q skill at rank three.</summary>
        static readonly byte[] FourthPassives = { SkillIds.SealingArray, SkillIds.ManaCycle, SkillIds.ElegantSaving, SkillIds.DivineBlessing };

        static int[] NoRanks() => new int[SkillCatalog.MaxSkillId + 1];

        static int[] Ranks(params (byte id, int rank)[] set)
        {
            int[] ranks = NoRanks();
            foreach ((byte id, int rank) in set) ranks[id] = rank;
            return ranks;
        }

        static SkillDef Q(byte character) => SkillCatalog.ForSlot(0, character);
        static SkillDef E(byte character) => SkillCatalog.ForSlot(1, character);

        // ---- 1.1 the definitions ----

        [Test]
        public void TheEightNewPassivesBelongToTheirCharacterAndKeepTheirNumbers()
        {
            for (int i = 0; i < 4; i++)
            {
                SkillDef third = SkillCatalog.Get(ThirdPassives[i]);
                SkillDef fourth = SkillCatalog.Get(FourthPassives[i]);
                Assert.AreEqual(Characters[i], third.Character, third.Name);
                Assert.AreEqual(Characters[i], fourth.Character, fourth.Name);
                Assert.IsFalse(third.IsActive);
                Assert.IsFalse(fourth.IsActive);
                Assert.AreEqual(3, third.MaxRank);
                Assert.AreEqual(3, fourth.MaxRank);
                Assert.IsFalse(string.IsNullOrEmpty(third.Name) || string.IsNullOrEmpty(third.Description));
                Assert.IsFalse(string.IsNullOrEmpty(fourth.Name) || string.IsNullOrEmpty(fourth.Description));
                Assert.Greater(third.PassivePerRank, 0f);
                Assert.Greater(fourth.PassivePerRank, 0f);
            }
            CollectionAssert.AreEqual(Enumerable.Range(21, 8).Select(x => (byte)x).ToList(),
                ThirdPassives.Concat(FourthPassives).OrderBy(x => x).ToList());
            Assert.AreEqual(SkillCatalog.All.Count, SkillCatalog.All.Select(d => d.Id).Distinct().Count());
        }

        [Test]
        public void TheNewPassivesHaveTheDesignedEffects()
        {
            Assert.AreEqual(PassiveEffectKind.DamageTaken, SkillCatalog.Get(SkillIds.Guard).Passive);
            Assert.AreEqual(PassiveEffectKind.SkillPower, SkillCatalog.Get(SkillIds.SealingArray).Passive);
            Assert.AreEqual(PassiveEffectKind.BasicDamage, SkillCatalog.Get(SkillIds.StarBullet).Passive);
            Assert.AreEqual(PassiveEffectKind.ManaRegen, SkillCatalog.Get(SkillIds.ManaCycle).Passive);
            Assert.AreEqual(PassiveEffectKind.SkillPower, SkillCatalog.Get(SkillIds.PreciseThrow).Passive);
            Assert.AreEqual(PassiveEffectKind.SkillManaCost, SkillCatalog.Get(SkillIds.ElegantSaving).Passive);
            Assert.AreEqual(PassiveEffectKind.ExperienceGain, SkillCatalog.Get(SkillIds.Merit).Passive);
            Assert.AreEqual(PassiveEffectKind.SkillPower, SkillCatalog.Get(SkillIds.DivineBlessing).Passive);
        }

        [Test]
        public void ThirdPassivesNeedTheESkillAtRankOneAndFourthNeedTheQSkillAtRankThree()
        {
            for (int i = 0; i < 4; i++)
            {
                SkillDef third = SkillCatalog.Get(ThirdPassives[i]);
                SkillDef fourth = SkillCatalog.Get(FourthPassives[i]);
                Assert.AreEqual(E(Characters[i]).Id, third.RequiresSkill, third.Name);
                Assert.AreEqual(1, third.RequiresRank, third.Name);
                Assert.AreEqual(Q(Characters[i]).Id, fourth.RequiresSkill, fourth.Name);
                Assert.AreEqual(3, fourth.RequiresRank, fourth.Name);
            }
        }

        [Test]
        public void EveryCharacterCanLearn38RanksAndAFullMatchDoesNotRunDry()
        {
            foreach (byte c in Characters)
            {
                int total = SkillCatalog.All.Where(d => SkillCatalog.IsOwnedBy(d, c)).Sum(d => d.MaxRank);
                Assert.AreEqual(38, total, $"character {c}");
                Assert.Greater(total, GrowthConfig.MaxLevel - 1);
            }
        }

        // ---- 1.3 who is offered what ----

        [Test]
        public void TheThirdPassiveOpensWithTheESkillAndNotBefore()
        {
            for (int i = 0; i < 4; i++)
            {
                byte c = Characters[i];
                SkillDef third = SkillCatalog.Get(ThirdPassives[i]);
                Assert.IsFalse(SkillCatalog.IsAvailable(third, NoRanks(), c), $"{third.Name} with nothing learned");
                Assert.IsFalse(SkillCatalog.IsAvailable(third, Ranks((Q(c).Id, 3)), c), $"{third.Name} with only the Q skill");
                Assert.IsTrue(SkillCatalog.IsAvailable(third, Ranks((E(c).Id, 1)), c), $"{third.Name} after the E skill");
            }
        }

        [Test]
        public void TheFourthPassiveNeedsTheQSkillAtRankThree()
        {
            for (int i = 0; i < 4; i++)
            {
                byte c = Characters[i];
                SkillDef fourth = SkillCatalog.Get(FourthPassives[i]);
                Assert.IsFalse(SkillCatalog.IsAvailable(fourth, Ranks((E(c).Id, 3)), c), $"{fourth.Name} with only the E skill");
                Assert.IsFalse(SkillCatalog.IsAvailable(fourth, Ranks((Q(c).Id, 1)), c), $"{fourth.Name} at Q rank one");
                Assert.IsFalse(SkillCatalog.IsAvailable(fourth, Ranks((Q(c).Id, 2)), c), $"{fourth.Name} at Q rank two");
                Assert.IsTrue(SkillCatalog.IsAvailable(fourth, Ranks((Q(c).Id, 3)), c), $"{fourth.Name} at Q rank three");
            }
        }

        [Test]
        public void NobodyIsOfferedAnotherCharactersNewPassive()
        {
            foreach (byte owner in Characters)
            foreach (byte other in Characters.Where(x => x != owner))
            {
                int[] ranks = Ranks((Q(owner).Id, 3), (E(owner).Id, 3), (Q(other).Id, 3), (E(other).Id, 3));
                foreach (byte id in ThirdPassives.Concat(FourthPassives))
                {
                    SkillDef def = SkillCatalog.Get(id);
                    if (def.Character == owner) Assert.IsFalse(SkillCatalog.IsAvailable(def, ranks, other), $"{def.Name} offered to {other}");
                }
            }
        }

        [Test]
        public void ANewPassiveAtItsMaximumIsNotOfferedAgain()
        {
            foreach (byte id in ThirdPassives.Concat(FourthPassives))
            {
                SkillDef def = SkillCatalog.Get(id);
                int[] ranks = Ranks((Q(def.Character).Id, 3), (E(def.Character).Id, 3), (id, def.MaxRank));
                Assert.IsFalse(SkillCatalog.IsAvailable(def, ranks, def.Character), def.Name);
            }
        }

        [Test]
        public void ARealPlaythroughOffersTheNewPassivesAndNeverBreaksTheRules()
        {
            foreach (byte c in Characters)
            {
                var sim = new LevelingSim(new System.Random(11));
                sim.Register(1, c);
                var offered = new HashSet<byte>();
                for (int step = 0; step < 100 && sim.Level(1) < sim.MaxLevel; step++)
                {
                    sim.AddXp(1, GrowthConfig.XpNeeded(sim.Level(1)));
                    while (sim.HasOffer(1))
                    {
                        foreach (byte choice in sim.CurrentChoices(1)) offered.Add(choice);
                        sim.Pick(1, sim.CurrentOfferId(1), 0);
                    }
                }
                foreach (SkillDef def in SkillCatalog.All)
                {
                    int rank = sim.RankOf(1, def.Id);
                    Assert.LessOrEqual(rank, def.MaxRank, def.Name);
                    if (rank > 0 && def.RequiresSkill != 0)
                        Assert.GreaterOrEqual(sim.RankOf(1, def.RequiresSkill), def.RequiresRank, $"{def.Name} learned without its prerequisite");
                    Assert.IsTrue(SkillCatalog.IsOwnedBy(def, c) || rank == 0, $"{def.Name} learned by the wrong character");
                }
            }
        }

        // ---- 2.1 damage taken ----

        [Test]
        public void TheDamageTakenMultiplierFollowsTheRankAndHasACap()
        {
            Assert.AreEqual(1f, SkillCatalog.DamageTakenMultiplier(NoRanks(), CharacterIds.Reimu), 0.0001f);
            for (int rank = 1; rank <= 3; rank++)
                Assert.AreEqual(1f - 0.08f * rank, SkillCatalog.DamageTakenMultiplier(Ranks((SkillIds.Guard, rank)), CharacterIds.Reimu), 0.0001f);

            // The cap is a safety limit the real data never reaches: rank three gives 24 percent, even the largest packable rank (7) only 56.
            Assert.Less(0.08f * SkillCatalog.Get(SkillIds.Guard).MaxRank, SkillCatalog.MaxDamageTakenReduction);
            Assert.AreEqual(1f - 0.08f * 7, SkillCatalog.DamageTakenMultiplier(Ranks((SkillIds.Guard, 7)), CharacterIds.Reimu), 0.0001f);
            Assert.AreEqual(1f, SkillCatalog.DamageTakenMultiplier(Ranks((SkillIds.Guard, 3)), CharacterIds.Marisa), 0.0001f, "another character does not count it");
        }

        static SurvivorSim NewSurvivors(params ulong[] clients)
        {
            var sim = new SurvivorSim();
            foreach (ulong id in clients) sim.Register(id, 100);
            return sim;
        }

        [Test]
        public void AReducedSurvivorLosesLessHealthPerHitAndOthersLoseAll()
        {
            SurvivorSim sim = NewSurvivors(1, 2);
            sim.SetDamageTakenMultiplier(1, 0.8f);
            sim.InvulnerabilitySeconds = 0f;

            Assert.IsTrue(sim.ApplyDamage(1, 10));
            Assert.IsTrue(sim.ApplyDamage(2, 10));

            Assert.AreEqual(92, sim.Health(1));
            Assert.AreEqual(90, sim.Health(2));
        }

        [Test]
        public void AHitOfOneStillDoesOneEvenWithTheLargestReduction()
        {
            SurvivorSim sim = NewSurvivors(1);
            sim.SetDamageTakenMultiplier(1, 1f - SkillCatalog.MaxDamageTakenReduction);
            sim.InvulnerabilitySeconds = 0f;

            sim.ApplyDamage(1, 1);

            Assert.AreEqual(99, sim.Health(1));
        }

        [Test]
        public void AProtectedOrFallenSurvivorStillTakesNothingWithOrWithoutTheReduction()
        {
            SurvivorSim sim = NewSurvivors(1);
            sim.SetDamageTakenMultiplier(1, 0.5f);
            Assert.IsTrue(sim.ApplyDamage(1, 10));
            Assert.AreEqual(95, sim.Health(1));
            Assert.IsFalse(sim.ApplyDamage(1, 10), "protected after the hit");
            Assert.AreEqual(95, sim.Health(1));

            sim.Kill(1);
            Assert.IsFalse(sim.ApplyDamage(1, 10), "fallen");
            Assert.AreEqual(0, sim.Health(1));
        }

        [Test]
        public void ABlastIsReducedThroughTheSameRule()
        {
            SurvivorSim sim = NewSurvivors(1, 2);
            sim.SetDamageTakenMultiplier(1, 0.5f);
            var bodies = new List<SurvivorBody>
            {
                new SurvivorBody { ClientId = 1, Position = Vector2.zero },
                new SurvivorBody { ClientId = 2, Position = new Vector2(1f, 0f) },
            };

            int hurt = sim.ApplyBlast(Vector2.zero, 3f, 30, bodies);

            Assert.AreEqual(2, hurt);
            Assert.AreEqual(85, sim.Health(1));
            Assert.AreEqual(70, sim.Health(2));
        }

        [Test]
        public void TheReductionSurvivesBeingRevived()
        {
            SurvivorSim sim = NewSurvivors(1, 2);
            sim.SetDamageTakenMultiplier(1, 0.5f);
            sim.ReviveInvulnerabilitySeconds = 0f;
            sim.InvulnerabilitySeconds = 0f;
            sim.Kill(1);
            var bodies = new List<SurvivorBody>
            {
                new SurvivorBody { ClientId = 1, Position = Vector2.zero },
                new SurvivorBody { ClientId = 2, Position = new Vector2(0.5f, 0f) },
            };
            var mobs = new MobSim(targetCount: 0);
            for (int i = 0; i < Mathf.RoundToInt((sim.ReviveSeconds + 1f) / 0.02f); i++) sim.Step(0.02f, bodies, mobs);
            Assert.IsTrue(sim.IsAlive(1), "revived by the teammate standing next to them");

            int before = sim.Health(1);
            sim.ApplyDamage(1, 10);

            Assert.AreEqual(before - 5, sim.Health(1));
        }

        [Test]
        public void StatCalcHandsTheMultiplierToTheSimulationsOnlyForTheOwner()
        {
            int[] ranks = Ranks((SkillIds.Guard, 2));
            Assert.AreEqual(0.84f, StatCalc.Compute(5, ranks, CharacterCatalog.Get(CharacterIds.Reimu)).DamageTakenMultiplier, 0.0001f);
            Assert.AreEqual(1f, StatCalc.Compute(5, ranks, CharacterCatalog.Get(CharacterIds.Marisa)).DamageTakenMultiplier, 0.0001f);
            Assert.AreEqual(1f, StatCalc.Compute(5, NoRanks(), CharacterCatalog.Get(CharacterIds.Reimu)).DamageTakenMultiplier, 0.0001f);
        }

        // ---- 2.2 basic attack damage ----

        [Test]
        public void TheBasicAttackPassiveRaisesTheBasicDamageForItsOwnerOnly()
        {
            CharacterDef marisa = CharacterCatalog.Get(CharacterIds.Marisa);
            int none = StatCalc.Compute(1, NoRanks(), marisa).ProjectileDamage;
            Assert.AreEqual(CombatConfig.ProjectileDamage, none);

            for (int rank = 1; rank <= 3; rank++)
            {
                int expected = (int)System.Math.Round(CombatConfig.ProjectileDamage * (1f + 0.10f * rank), System.MidpointRounding.AwayFromZero);
                Assert.AreEqual(expected, StatCalc.Compute(1, Ranks((SkillIds.StarBullet, rank)), marisa).ProjectileDamage, $"rank {rank}");
            }

            Assert.AreEqual(none, StatCalc.Compute(1, Ranks((SkillIds.StarBullet, 3)), CharacterCatalog.Get(CharacterIds.Reimu)).ProjectileDamage, "another character");
        }

        [Test]
        public void TheBasicAttackPassiveStacksOnLevelsAndTheCommonPowerPassive()
        {
            CharacterDef marisa = CharacterCatalog.Get(CharacterIds.Marisa);
            int[] ranks = Ranks((SkillIds.Power, 5), (SkillIds.StarBullet, 3));

            int damage = StatCalc.Compute(11, ranks, marisa).ProjectileDamage;

            int basis = CombatConfig.ProjectileDamage + GrowthConfig.DamagePerLevel * 10 + SkillCatalog.PowerDamagePerRank * 5;
            Assert.AreEqual((int)System.Math.Round(basis * 1.3f, System.MidpointRounding.AwayFromZero), damage);
        }

        [Test]
        public void ActiveSkillDamageIsNotChangedByTheBasicAttackPassive()
        {
            SkillDef spark = SkillCatalog.Get(SkillIds.MasterSpark);
            int damage = spark.Damage[2];
            Assert.AreEqual(damage, SkillCatalog.EffectivePower(damage, Ranks((SkillIds.StarBullet, 3)), CharacterIds.Marisa));
        }

        // ---- the repeated power passives ----

        [Test]
        public void TheNewPowerPassivesRaiseSkillDamageForTheirOwnersOnly()
        {
            foreach ((byte id, byte owner) in new[] { (SkillIds.SealingArray, CharacterIds.Reimu), (SkillIds.PreciseThrow, CharacterIds.Sakuya), (SkillIds.DivineBlessing, CharacterIds.Sanae) })
            {
                int[] ranks = Ranks((id, 2));
                Assert.AreEqual(130, SkillCatalog.EffectivePower(100, ranks, owner), SkillCatalog.Get(id).Name);
                Assert.AreEqual(100, SkillCatalog.EffectivePower(100, ranks, CharacterIds.Marisa), $"{SkillCatalog.Get(id).Name} for another character");
            }
        }

        [Test]
        public void TheNewManaPassivesDoTheirJob()
        {
            // mana cost (Sakuya) through the existing cost rule
            SkillDef stop = SkillCatalog.Get(SkillIds.TimeStop);
            int plain = SkillCatalog.EffectiveManaCost(stop, NoRanks(), CharacterIds.Sakuya);
            int saved = SkillCatalog.EffectiveManaCost(stop, Ranks((SkillIds.ElegantSaving, 3)), CharacterIds.Sakuya);
            Assert.Less(saved, plain);

            // mana recovery (Marisa) through the stat calculation
            CharacterDef marisa = CharacterCatalog.Get(CharacterIds.Marisa);
            float none = StatCalc.Compute(1, NoRanks(), marisa).ManaRegenPerSecond;
            float more = StatCalc.Compute(1, Ranks((SkillIds.ManaCycle, 3)), marisa).ManaRegenPerSecond;
            Assert.AreEqual(none + 1.5f * 3, more, 0.0001f);
        }

        // ---- 2.3 experience ----

        [Test]
        public void TheExperienceAmountFollowsTheRankRoundsAndNeverFallsBelowTheBase()
        {
            Assert.AreEqual(10, SkillCatalog.EffectiveExperience(10, NoRanks(), CharacterIds.Sanae));
            Assert.AreEqual(11, SkillCatalog.EffectiveExperience(10, Ranks((SkillIds.Merit, 1)), CharacterIds.Sanae));
            Assert.AreEqual(12, SkillCatalog.EffectiveExperience(10, Ranks((SkillIds.Merit, 2)), CharacterIds.Sanae));
            Assert.AreEqual(13, SkillCatalog.EffectiveExperience(10, Ranks((SkillIds.Merit, 3)), CharacterIds.Sanae));
            Assert.AreEqual(1, SkillCatalog.EffectiveExperience(1, Ranks((SkillIds.Merit, 1)), CharacterIds.Sanae), "0.1 rounds back to the base");
            Assert.AreEqual(10, SkillCatalog.EffectiveExperience(10, Ranks((SkillIds.Merit, 3)), CharacterIds.Reimu), "another character");
            Assert.AreEqual(0, SkillCatalog.EffectiveExperience(0, Ranks((SkillIds.Merit, 3)), CharacterIds.Sanae));
        }

        static GrowthBody Body(ulong id, float x, float y) => new GrowthBody { ClientId = id, Position = new Vector2(x, y), Alive = true };

        [Test]
        public void TwoSurvivorsAtTheSameKillReceiveTheirOwnAmounts()
        {
            var sim = new LevelingSim(new System.Random(1));
            sim.Register(1, CharacterIds.Sanae);
            sim.Register(2, CharacterIds.Reimu);
            sim.GrantRank(1, SkillIds.Merit, 3);

            int recipients = sim.AwardKill(Vector2.zero, new List<GrowthBody> { Body(1, 2, 0), Body(2, 0, 3) });

            Assert.AreEqual(2, recipients);
            Assert.AreEqual(13, sim.Xp(1));
            Assert.AreEqual(10, sim.Xp(2));
        }

        [Test]
        public void TheBoostedAmountRespectsTheShareDistanceAndTheCap()
        {
            var sim = new LevelingSim(new System.Random(1));
            sim.Register(1, CharacterIds.Sanae);
            sim.GrantRank(1, SkillIds.Merit, 3);

            sim.AwardKill(Vector2.zero, new List<GrowthBody> { Body(1, GrowthConfig.XpShareDistance + 1f, 0) });
            Assert.AreEqual(0, sim.Xp(1), "too far");

            sim.AddXp(1, 999999);
            Assert.AreEqual(sim.MaxLevel, sim.Level(1));
            sim.AwardKill(Vector2.zero, new List<GrowthBody> { Body(1, 1, 0) });
            Assert.AreEqual(0, sim.Xp(1), "at the cap nothing is kept");
            Assert.AreEqual(sim.MaxLevel, sim.Level(1));
        }
    }
}
