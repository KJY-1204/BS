using NUnit.Framework;

namespace BS.Tests
{
    public class MatchSimTests
    {
        const float Dt = 0.02f;

        static MatchSim Started(float seconds = 60f, bool withBoss = true)
        {
            var sim = new MatchSim();
            sim.Start(withBoss, seconds);
            return sim;
        }

        static MatchInput Alive(int survivors) => new MatchInput { SurvivorsAlive = survivors };

        static void Run(MatchSim sim, float seconds, MatchInput input)
        {
            int steps = (int)System.Math.Round(seconds / Dt);
            for (int i = 0; i < steps; i++) sim.Step(Dt, input);
        }

        // ---- The clock ----

        [Test]
        public void TheCountdownRunsFromTheLimit()
        {
            MatchSim sim = Started(60f);
            Assert.AreEqual(60f, sim.TimeLeft);
            Assert.AreEqual(60, sim.SecondsLeft);

            Run(sim, 30f, Alive(2));

            Assert.AreEqual(30f, sim.TimeLeft, 0.05f);
            Assert.AreEqual(30, sim.SecondsLeft);
            Assert.IsFalse(sim.IsDecided);
        }

        [Test]
        public void SecondsLeftRoundsUpSoTheClockNeverShowsZeroBeforeTheEnd()
        {
            MatchSim sim = Started(10f);
            sim.Step(0.3f, Alive(1));
            Assert.AreEqual(10, sim.SecondsLeft, "9.7 s left shows as 10");
            Run(sim, 9.5f, Alive(1));
            Assert.AreEqual(1, sim.SecondsLeft, "0.2 s left shows as 1");
        }

        [Test]
        public void AFreshStartRestartsTheClockFromTheFullLimit()
        {
            MatchSim sim = Started(60f);
            Run(sim, 20f, Alive(2));
            sim.Start(true, 60f);
            Assert.AreEqual(60f, sim.TimeLeft);
            Assert.IsFalse(sim.IsDecided);

            sim.Step(Dt, new MatchInput { BossFell = true });
            sim.Start(true, 60f);
            Assert.AreEqual(MatchWinner.None, sim.Winner);
            Assert.AreEqual(MatchReason.None, sim.Reason);
        }

        // ---- A match without a boss ----

        [Test]
        public void APracticeMatchHasNoClockAndNoResult()
        {
            MatchSim sim = Started(60f, withBoss: false);

            Assert.IsFalse(sim.Enabled);
            Run(sim, 120f, Alive(0));

            Assert.IsFalse(sim.IsDecided);
            Assert.AreEqual(0f, sim.TimeLeft);
            Assert.IsFalse(sim.Step(Dt, new MatchInput { BossFell = true, BossLeft = true }));
        }

        [Test]
        public void ANonPositiveLimitIsAPracticeMatchToo()
        {
            MatchSim sim = Started(0f);
            Assert.IsFalse(sim.Enabled);
            Run(sim, 10f, Alive(0));
            Assert.IsFalse(sim.IsDecided);
        }

        // ---- Survivors win ----

        [Test]
        public void TheBossFallingIsASurvivorVictoryWithTimeLeft()
        {
            MatchSim sim = Started(60f);
            Run(sim, 10f, Alive(3));

            bool decided = sim.Step(Dt, new MatchInput { BossFell = true, SurvivorsAlive = 3 });

            Assert.IsTrue(decided);
            Assert.AreEqual(MatchWinner.Survivors, sim.Winner);
            Assert.AreEqual(MatchReason.BossDefeated, sim.Reason);
            Assert.Greater(sim.TimeLeft, 40f);
        }

        [Test]
        public void TheBossLeavingIsASurvivorVictory()
        {
            MatchSim sim = Started(60f);
            sim.Step(Dt, new MatchInput { BossLeft = true, SurvivorsAlive = 2 });

            Assert.AreEqual(MatchWinner.Survivors, sim.Winner);
            Assert.AreEqual(MatchReason.BossLeft, sim.Reason);
        }

        // ---- Boss wins ----

        [Test]
        public void TheLastSurvivorFallingIsABossVictory()
        {
            MatchSim sim = Started(60f);
            sim.Step(Dt, Alive(1));
            Assert.IsFalse(sim.IsDecided);

            sim.Step(Dt, Alive(0));

            Assert.AreEqual(MatchWinner.Boss, sim.Winner);
            Assert.AreEqual(MatchReason.AllSurvivorsFallen, sim.Reason);
        }

        [Test]
        public void TheClockRunningOutIsABossVictory()
        {
            MatchSim sim = Started(5f);
            Run(sim, 4.9f, Alive(2));
            Assert.IsFalse(sim.IsDecided);

            Run(sim, 0.3f, Alive(2));

            Assert.AreEqual(MatchWinner.Boss, sim.Winner);
            Assert.AreEqual(MatchReason.TimeUp, sim.Reason);
            Assert.AreEqual(0f, sim.TimeLeft);
        }

        [Test]
        public void ARevivalInTimeKeepsTheMatchGoing()
        {
            MatchSim sim = Started(60f);
            Run(sim, 5f, Alive(1)); // one down, one alive
            Run(sim, 5f, Alive(2)); // revived
            Assert.IsFalse(sim.IsDecided);
        }

        // ---- The order when several things happen in one step ----

        [Test]
        public void TheBossFallingBeatsEverySurvivorFalling()
        {
            MatchSim sim = Started(60f);
            sim.Step(Dt, new MatchInput { BossFell = true, SurvivorsAlive = 0 });
            Assert.AreEqual(MatchWinner.Survivors, sim.Winner);
            Assert.AreEqual(MatchReason.BossDefeated, sim.Reason);
        }

        [Test]
        public void TheBossFallingBeatsTheClockRunningOut()
        {
            MatchSim sim = Started(0.01f);
            sim.Step(1f, new MatchInput { BossFell = true, SurvivorsAlive = 2 });
            Assert.AreEqual(MatchWinner.Survivors, sim.Winner);
        }

        [Test]
        public void TheBossLeavingBeatsSurvivorsFallingAndTheClock()
        {
            MatchSim sim = Started(0.01f);
            sim.Step(1f, new MatchInput { BossLeft = true, SurvivorsAlive = 0 });
            Assert.AreEqual(MatchWinner.Survivors, sim.Winner);
            Assert.AreEqual(MatchReason.BossLeft, sim.Reason);
        }

        [Test]
        public void TheBossFallingBeatsTheBossLeaving()
        {
            MatchSim sim = Started(60f);
            sim.Step(Dt, new MatchInput { BossFell = true, BossLeft = true, SurvivorsAlive = 1 });
            Assert.AreEqual(MatchReason.BossDefeated, sim.Reason);
        }

        [Test]
        public void SurvivorsFallingBeatsTheClockInTheSameStep()
        {
            MatchSim sim = Started(0.01f);
            sim.Step(1f, Alive(0));
            Assert.AreEqual(MatchReason.AllSurvivorsFallen, sim.Reason);
            Assert.AreEqual(MatchWinner.Boss, sim.Winner);
        }

        // ---- A decided result stays ----

        [Test]
        public void ADecidedResultNeverChanges_AndTheClockStops()
        {
            MatchSim sim = Started(60f);
            Run(sim, 10f, Alive(2));
            sim.Step(Dt, new MatchInput { BossFell = true, SurvivorsAlive = 2 });
            float left = sim.TimeLeft;

            Assert.IsFalse(sim.Step(Dt, Alive(0)), "it is not decided a second time");
            Run(sim, 100f, Alive(0));

            Assert.AreEqual(MatchWinner.Survivors, sim.Winner);
            Assert.AreEqual(MatchReason.BossDefeated, sim.Reason);
            Assert.AreEqual(left, sim.TimeLeft);
        }

        [Test]
        public void OnlyTheStepThatDecidesReturnsTrue()
        {
            MatchSim sim = Started(60f);
            int decisions = 0;
            for (int i = 0; i < 10; i++)
                if (sim.Step(Dt, new MatchInput { BossFell = i >= 3, SurvivorsAlive = 2 })) decisions++;
            Assert.AreEqual(1, decisions);
        }

        [Test]
        public void ClearForgetsTheMatch()
        {
            MatchSim sim = Started(60f);
            sim.Step(Dt, new MatchInput { BossFell = true });

            sim.Clear();

            Assert.IsFalse(sim.Enabled);
            Assert.IsFalse(sim.IsDecided);
            Assert.AreEqual(0f, sim.TimeLeft);
            Assert.IsFalse(sim.Step(Dt, new MatchInput { BossFell = true }), "nothing to decide when there is no match");
        }

        [Test]
        public void ADecidedMatchCanBeFollowedByANewOne()
        {
            MatchSim sim = Started(60f);
            sim.Step(Dt, Alive(0));
            Assert.IsTrue(sim.IsDecided);

            sim.Start(true, 90f);

            Assert.IsFalse(sim.IsDecided);
            Assert.AreEqual(90f, sim.TimeLeft);
            sim.Step(Dt, new MatchInput { BossFell = true });
            Assert.AreEqual(MatchWinner.Survivors, sim.Winner);
        }
    }
}
