using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class SkillEvolutionToolingTests
    {
        static readonly byte[] Characters = { CharacterIds.Reimu, CharacterIds.Marisa, CharacterIds.Sakuya, CharacterIds.Sanae };

        static HarnessSettings Settings(params (string name, float value)[] overrides)
        {
            var s = new HarnessSettings();
            s.overrides.Add(new NamedValue { name = "matchSeconds", value = 30f });
            s.overrides.Add(new NamedValue { name = "mobAutoTarget", value = 0f });
            foreach ((string name, float value) o in overrides) s.overrides.Add(new NamedValue { name = o.name, value = o.value });
            return s;
        }

        // ---- -bsDevEvolve ----

        [Test]
        public void TheDevelopmentGrantGivesTheChosenEvolutionOfTheQSkill()
        {
            foreach (byte c in Characters)
            {
                SkillDef q = SkillCatalog.ForSlot(0, c);
                foreach (int which in new[] { 1, 2 })
                {
                    var leveling = new LevelingSim(new System.Random(1));
                    leveling.Register(1, c);

                    GrowthServer.GrantSkills(leveling, 1, c, 3, 0, which);

                    SkillDef evolution = SkillCatalog.EvolutionOf(q, which);
                    Assert.AreEqual(1, leveling.RankOf(1, evolution.Id), $"character {c} evolution {which}");
                    Assert.AreEqual(0, leveling.RankOf(1, SkillCatalog.EvolutionOf(q, 3 - which).Id), "the other one is not given");
                    Assert.AreEqual(q.MaxRank, leveling.RankOf(1, q.Id));
                }
            }
        }

        [Test]
        public void TheDevelopmentGrantGivesNoEvolutionForZeroOrAnUnknownNumber()
        {
            foreach (int which in new[] { 0, 3, -1 })
            {
                var leveling = new LevelingSim(new System.Random(1));
                leveling.Register(1, CharacterIds.Marisa);

                GrowthServer.GrantSkills(leveling, 1, CharacterIds.Marisa, 3, 0, which);

                foreach (SkillDef e in SkillCatalog.All.Where(d => d.EvolvesFrom != 0)) Assert.AreEqual(0, leveling.RankOf(1, e.Id), $"evolution {which}");
            }
            Assert.IsNull(SkillCatalog.EvolutionOf(null, 1));
            Assert.IsNull(SkillCatalog.EvolutionOf(SkillCatalog.Get(SkillIds.Burst), 3));
        }

        // ---- the skill bar and the choice text ----

        [Test]
        public void ASlotShowsTheCooldownThenTheRankOrTheWordForAnEvolution()
        {
            SkillDef burst = SkillCatalog.Get(SkillIds.Burst);
            SkillDef focus = SkillCatalog.Get(SkillIds.BurstFocus);

            Assert.AreEqual("랭크 3", GrowthHud.SlotDetail(burst, 3, 0f));
            Assert.AreEqual("진화", GrowthHud.SlotDetail(focus, 1, 0f));
            Assert.AreEqual("4.5s", GrowthHud.SlotDetail(focus, 1, 4.5f));
            Assert.AreEqual("4.5s", GrowthHud.SlotDetail(burst, 3, 4.5f));
        }

        [Test]
        public void TheSkillBarShowsTheEvolutionOnTheQSlotOnlyOnceItIsLearned()
        {
            foreach (byte c in Characters)
            {
                SkillDef q = SkillCatalog.ForSlot(0, c);
                SkillDef evolution = SkillCatalog.EvolutionOf(q, 1);
                var ranks = new int[SkillCatalog.MaxSkillId + 1];
                ranks[q.Id] = 3;
                Assert.AreEqual(q.Name, SkillCatalog.ActiveForSlot(0, c, ranks).Name);

                ranks[evolution.Id] = 1;
                SkillDef shown = SkillCatalog.ActiveForSlot(0, c, ranks);
                Assert.AreEqual(evolution.Name, shown.Name);
                // The slot reads its cost and cooldown from the same definition the server fires.
                Assert.AreSame(evolution, shown);
            }
        }

        [Test]
        public void AnEvolutionInTheChoiceSaysItIsFinalAndOtherSkillsKeepTheirRankLine()
        {
            Assert.AreEqual("진화 (한 번 고르면 바꿀 수 없음)", GrowthHud.OfferRankText(SkillCatalog.Get(SkillIds.SparkWide), 0));
            Assert.AreEqual("랭크 1 -> 2 / 3", GrowthHud.OfferRankText(SkillCatalog.Get(SkillIds.Burst), 1));
            Assert.AreEqual("새로 배움 (최대 3)", GrowthHud.OfferRankText(SkillCatalog.Get(SkillIds.Burst), 0));
            foreach (SkillDef e in SkillCatalog.All.Where(d => d.EvolvesFrom != 0))
                Assert.IsTrue(e.Description.Length > 0 && e.Name.Contains("·"), "the choice text names the evolution and its trade");
        }

        // ---- the harness overrides ----

        [Test]
        public void TheOverridesAreKnownAndRangeChecked()
        {
            Assert.IsTrue(HarnessOverrides.IsKnown("startQRank"));
            Assert.IsTrue(HarnessOverrides.IsKnown("startEvolution"));
            Assert.IsNull(Settings(("startQRank", 3), ("startEvolution", 2)).Validate());
            Assert.IsNull(Settings(("startQRank", 0), ("startEvolution", 0)).Validate());
            StringAssert.Contains("startQRank", Settings(("startQRank", 4)).Validate());
            StringAssert.Contains("startQRank", Settings(("startQRank", -1)).Validate());
            StringAssert.Contains("startQRank", Settings(("startQRank", 1.5f)).Validate());
            StringAssert.Contains("startEvolution", Settings(("startEvolution", 3)).Validate());
            StringAssert.Contains("startEvolution", Settings(("startEvolution", 0.5f)).Validate());
        }

        [Test]
        public void EverySurvivorStartsWithTheQRankAndTheEvolutionTheOverridesName()
        {
            BossCatalog.SetCurrentKind(BossCatalog.Standard);
            MatchSession s = MatchSession.Start(Settings(("startQRank", 3), ("startEvolution", 2)), 1);

            Assert.Greater(s.SurvivorIds.Count, 0);
            foreach (ulong id in s.SurvivorIds)
            {
                byte c = s.CharacterOf(id);
                SkillDef q = SkillCatalog.ForSlot(0, c);
                Assert.AreEqual(3, s.World.Leveling.RankOf(id, q.Id), $"character {c}");
                Assert.AreEqual(1, s.World.Leveling.RankOf(id, SkillCatalog.EvolutionOf(q, 2).Id), $"character {c}");
                Assert.AreEqual(0, s.World.Leveling.RankOf(id, SkillCatalog.EvolutionOf(q, 1).Id));
                Assert.AreSame(SkillCatalog.EvolutionOf(q, 2), SkillCatalog.ActiveForSlot(0, c, s.World.Leveling.Ranks(id)));
            }
        }

        [Test]
        public void ARankWithoutAnEvolutionLeavesTheBaseSkillFiring()
        {
            BossCatalog.SetCurrentKind(BossCatalog.Standard);
            MatchSession s = MatchSession.Start(Settings(("startQRank", 2)), 1);
            foreach (ulong id in s.SurvivorIds)
            {
                SkillDef q = SkillCatalog.ForSlot(0, s.CharacterOf(id));
                Assert.AreEqual(2, s.World.Leveling.RankOf(id, q.Id));
                Assert.AreSame(q, SkillCatalog.ActiveForSlot(0, s.CharacterOf(id), s.World.Leveling.Ranks(id)));
            }
        }

        [Test]
        public void ZeroOverridesPlayTheSameMatchAsNoOverrides()
        {
            MatchRow none = MatchRunner.RunOne(Settings(), 7);
            MatchRow zeros = MatchRunner.RunOne(Settings(("startQRank", 0), ("startEvolution", 0)), 7);

            Assert.AreEqual(none.stateHash, zeros.stateHash);
            Assert.AreEqual(none.lengthSeconds, zeros.lengthSeconds);
            Assert.AreEqual(none.winner, zeros.winner);
        }
    }
}
