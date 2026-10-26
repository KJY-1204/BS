using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class RescueContestRewardTests
    {
        const float Dt = 0.02f;

        static BossSkillState State() => new BossSkillState { MatchRunning = true, BossStanding = true, HealthFraction = 1f };

        [TearDown]
        public void RestoreTheStandardKind() => BossCatalog.SetCurrentKind(BossCatalog.Standard);

        [Test]
        public void ARateAboveOneRunsTheCooldownsFasterButNotTheWarnings()
        {
            var sim = new BossSkillSim();
            sim.Reset(BossCatalog.Get(BossCatalog.Standard));
            var events = new List<CombatEvent>();
            var projectiles = new ProjectileSim();
            var survivors = new SurvivorSim();
            Assert.AreEqual(1f, sim.CooldownRate, "the default is real time");

            Assert.AreEqual(BossSkillResult.Used, sim.TryUse(BossSkills.Shockwave, true, State(), 1, events));
            float cooldown = sim.CooldownLeft(BossSkills.Shockwave);
            float warning = BossCatalog.Get(BossCatalog.Standard).Skills[BossSkills.Shockwave].WarningSeconds;
            sim.CooldownRate = 2f;

            int steps = Mathf.RoundToInt(warning * 0.6f / Dt);
            for (int i = 0; i < steps; i++) sim.Step(Dt, true, true, projectiles, survivors, new List<SurvivorBody>(), 1, events);

            Assert.AreEqual(cooldown - 2f * steps * Dt, sim.CooldownLeft(BossSkills.Shockwave), 0.001f, "the cooldown ran at twice the speed");
            Assert.AreEqual(1, sim.PendingCount, "the warning ran in real time and has not ended yet");
        }

        // ---- the world ----

        static MatchSession OpenPointSession(params (string name, float value)[] overrides)
        {
            BossCatalog.SetCurrentKind(BossCatalog.Standard);
            var settings = new HarnessSettings();
            settings.overrides.Add(new NamedValue { name = "mobAutoTarget", value = 0f });
            settings.overrides.Add(new NamedValue { name = "matchSeconds", value = 300f });
            settings.overrides.Add(new NamedValue { name = "survivorHealthBonus", value = 100000f });
            foreach ((string name, float value) o in overrides) settings.overrides.Add(new NamedValue { name = o.name, value = o.value });
            MatchSession s = MatchSession.Start(settings, 4);
            for (int i = 0; i < Mathf.RoundToInt(91f / Dt); i++) s.Advance();
            Assert.IsTrue(s.World.RescuePoint.IsOpen);
            return s;
        }

        static void PlaceBoss(MatchSession s, Vector2 at)
        {
            for (int i = 0; i < s.World.Bodies.Count; i++)
            {
                MatchBody b = s.World.Bodies[i];
                if (b.ClientId == MatchSession.BossId) { b.Position = at; s.World.Bodies[i] = b; }
            }
        }

        static float CooldownAfterTwoSeconds(MatchSession s, Vector2 bossAt)
        {
            PlaceBoss(s, bossAt);
            s.Advance();
            Assert.AreEqual(BossSkillResult.Used, s.UseBossSkill(BossSkills.Shockwave, 0));
            float start = s.World.BossSkills.CooldownLeft(BossSkills.Shockwave);
            for (int i = 0; i < Mathf.RoundToInt(2f / Dt); i++) s.Advance();
            return start - s.World.BossSkills.CooldownLeft(BossSkills.Shockwave);
        }

        [Test]
        public void ABossHoldingTheOpenPointHasItsCooldownsRunOneAndAHalfTimesAsFast()
        {
            MatchSession s = OpenPointSession();
            Vector2 centre = s.World.RescuePoint.Center;

            float run = CooldownAfterTwoSeconds(s, centre);

            Assert.IsTrue(s.World.RescuePoint.Contested);
            Assert.AreEqual(2f * RescueConfig.ContestCooldownRate, run, 0.1f);
        }

        [Test]
        public void NextToThePointOrWithTheContestOffTheCooldownsRunAtTheNormalRate()
        {
            MatchSession outside = OpenPointSession();
            Vector2 next = outside.World.RescuePoint.Center + new Vector2(RescueConfig.Radius + outside.World.Boss.Radius + 1f, 0f);
            Assert.AreEqual(2f, CooldownAfterTwoSeconds(outside, next), 0.1f, "next to the circle");
            Assert.IsFalse(outside.World.RescuePoint.Contested);

            MatchSession off = OpenPointSession(("rescueContest", 0f));
            Assert.AreEqual(2f, CooldownAfterTwoSeconds(off, off.World.RescuePoint.Center), 0.1f, "the contest is off");
        }

        [Test]
        public void WhenTheBossLeavesTheCirclTheRateIsNormalAgainAndNoPointMeansNoReward()
        {
            MatchSession s = OpenPointSession();
            Vector2 centre = s.World.RescuePoint.Center;
            PlaceBoss(s, centre);
            s.Advance();
            Assert.AreEqual(RescueConfig.ContestCooldownRate, s.World.BossSkills.CooldownRate);

            PlaceBoss(s, centre + new Vector2(20f, 0f));
            s.Advance();
            Assert.AreEqual(1f, s.World.BossSkills.CooldownRate);

            BossCatalog.SetCurrentKind(BossCatalog.Standard);
            MatchSession early = MatchSession.Start(new HarnessSettings(), 4);
            for (int i = 0; i < 50; i++) early.Advance();
            Assert.AreEqual(1f, early.World.BossSkills.CooldownRate, "no point is open at the start");
        }
    }
}
