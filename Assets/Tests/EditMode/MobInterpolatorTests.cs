using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class MobInterpolatorTests
    {
        const float SnapshotInterval = 1f / MobConfig.SnapshotHz;
        const float Frame = 1f / 60f;
        const float NetworkDelay = 0.05f;
        const float MobSpeed = 2.5f;

        /// <summary>
        /// Plays one mob moving along +x at constant speed through the interpolator, delivering a snapshot
        /// every 1/15 s after a fixed network delay, and returns the displayed x for every frame.
        /// </summary>
        static List<float> PlayStraightRun(MobInterpolator interp, float seconds, ISet<int> lostSnapshots)
        {
            var shown = new List<float>();
            int nextSnapshot = 0;
            float local = 0f;

            while (local < seconds)
            {
                while (nextSnapshot * SnapshotInterval + NetworkDelay <= local)
                {
                    float serverTime = nextSnapshot * SnapshotInterval;
                    if (lostSnapshots == null || !lostSnapshots.Contains(nextSnapshot))
                        interp.OnSnapshotEntry(1, new Vector2(MobSpeed * serverTime, 0f), serverTime);
                    nextSnapshot++;
                }

                interp.Advance(Frame);
                local += Frame;
                if (interp.Count > 0) shown.Add(interp.PositionAt(0).x);
            }
            return shown;
        }

        static void AssertContinuous(List<float> xs, int skipFrames)
        {
            float maxStep = MobSpeed * Frame * 1.6f;
            for (int i = skipFrames + 1; i < xs.Count; i++)
            {
                float step = xs[i] - xs[i - 1];
                Assert.GreaterOrEqual(step, -1e-4f, $"moved backwards at frame {i}");
                Assert.LessOrEqual(step, maxStep, $"jumped {step} at frame {i}");
            }
        }

        [Test]
        public void NormalMotion_IsContinuousAndKeepsMoving()
        {
            var interp = new MobInterpolator();

            List<float> xs = PlayStraightRun(interp, 4f, null);

            AssertContinuous(xs, skipFrames: 30);
            Assert.Greater(xs[xs.Count - 1] - xs[30], MobSpeed * 2.5f, "keeps advancing at about the mob speed");
        }

        [Test]
        public void OneLostSnapshot_DoesNotJump()
        {
            var interp = new MobInterpolator();

            List<float> xs = PlayStraightRun(interp, 4f, new HashSet<int> { 20 });

            AssertContinuous(xs, skipFrames: 30);
        }

        [Test]
        public void DisplayLagsTheLatestSnapshotByAboutTheDelay()
        {
            var interp = new MobInterpolator();
            PlayStraightRun(interp, 4f, null);

            float shownX = interp.PositionAt(0).x;
            float latestX = MobSpeed * (3.95f - NetworkDelay);
            float lagSeconds = (latestX - shownX) / MobSpeed;

            Assert.That(lagSeconds, Is.InRange(0.08f, 0.25f), "about 130 ms behind the newest data");
        }

        [Test]
        public void MobWithoutPositionsForTwoSecondsIsRemoved()
        {
            var interp = new MobInterpolator();
            interp.OnSnapshotEntry(5, Vector2.zero, 1f);
            Assert.AreEqual(1, interp.Count);

            for (int i = 0; i < 60 * 3; i++) interp.Advance(Frame);

            Assert.AreEqual(0, interp.Count);
            Assert.AreEqual(1, interp.StaleRemovals);
        }

        [Test]
        public void UpdatedMobIsNotRemoved()
        {
            var interp = new MobInterpolator();
            for (int i = 0; i < 60 * 5; i++)
            {
                if (i % 4 == 0) interp.OnSnapshotEntry(5, Vector2.zero, i * Frame);
                interp.Advance(Frame);
            }

            Assert.AreEqual(1, interp.Count);
            Assert.AreEqual(0, interp.StaleRemovals);
        }

        [Test]
        public void PositionBeforeSpawnEvent_CreatesAMobWhoseTypeIsLearnedLater()
        {
            var interp = new MobInterpolator();

            interp.OnSnapshotEntry(9, new Vector2(1, 1), 1f);
            Assert.AreEqual(1, interp.Count);
            Assert.AreEqual(0, interp.TypeAt(0));

            interp.OnSpawn(9, 3, new Vector2(1, 1), 1f);
            Assert.AreEqual(1, interp.Count, "no duplicate mob");
            Assert.AreEqual(3, interp.TypeAt(0));
        }

        [Test]
        public void RemovedMobIsNotRevivedByALateSnapshot()
        {
            var interp = new MobInterpolator();
            interp.OnSpawn(4, 0, Vector2.zero, 1f);
            interp.OnRemove(4);
            Assert.AreEqual(0, interp.Count);

            interp.OnSnapshotEntry(4, new Vector2(2, 0), 0.9f);

            Assert.AreEqual(0, interp.Count);
        }

        [Test]
        public void RemovedMobCanBeSpawnedAgainByANewSpawnEvent()
        {
            var interp = new MobInterpolator();
            interp.OnSpawn(4, 0, Vector2.zero, 1f);
            interp.OnRemove(4);

            interp.OnSpawn(4, 0, Vector2.one, 2f);

            Assert.AreEqual(1, interp.Count);
        }

        [Test]
        public void JoiningMidGame_ShowsAllMobsFromTheFullState()
        {
            var interp = new MobInterpolator();
            for (ushort id = 1; id <= 40; id++)
                interp.OnSpawn(id, 0, new Vector2(id * 0.1f, 0), 5f);

            interp.Advance(Frame);

            Assert.AreEqual(40, interp.Count);
        }

        [Test]
        public void OldSamplesAreIgnored()
        {
            var interp = new MobInterpolator();
            interp.OnSnapshotEntry(1, new Vector2(10, 0), 2f);
            interp.OnSnapshotEntry(1, new Vector2(0, 0), 1f);

            Assert.AreEqual(10f, interp.PositionAt(0).x, 1e-4f);
        }

        // ---- Removal at display time ----

        [Test]
        public void TimedRemoval_KeepsTheMobUntilTheDisplayClockReachesTheKillTime()
        {
            var interp = new MobInterpolator();
            interp.OnSpawn(1, 0, Vector2.zero, 10f);
            interp.OnSpawn(2, 0, Vector2.zero, 10f);

            interp.OnRemove(1, 10.1f);
            Assert.AreEqual(2, interp.Count, "removal is in the display future, so the mob stays for now");

            float serverTime = 10f;
            int removedAtFrame = -1;
            for (int frame = 0; frame < 120 && removedAtFrame < 0; frame++)
            {
                // The server keeps sending positions for the surviving mob.
                if (frame % 4 == 0) { serverTime += SnapshotInterval; interp.OnSnapshotEntry(2, Vector2.zero, serverTime); }
                interp.Advance(Frame);

                if (interp.Count == 1) removedAtFrame = frame;
                else Assert.Less(interp.RenderTime, 10.1f + 0.001f, "must not outlive the kill time");
            }

            Assert.GreaterOrEqual(removedAtFrame, 1, "not removed at once");
            Assert.GreaterOrEqual(interp.RenderTime, 10.1f - 0.03f, "removed when the display clock reached the kill time");
            Assert.AreEqual(2, interp.IdAt(0));
        }

        [Test]
        public void TimedRemoval_ThatIsAlreadyDueHappensImmediately()
        {
            var interp = new MobInterpolator();
            interp.OnSpawn(1, 0, Vector2.zero, 10f);
            interp.OnSnapshotEntry(1, Vector2.zero, 10.5f);
            for (int i = 0; i < 60; i++) interp.Advance(Frame);

            interp.OnRemove(1, 9f);

            Assert.AreEqual(0, interp.Count);
        }

        [Test]
        public void TimedRemoval_StillHappensWhenNoMoreSnapshotsArrive()
        {
            var interp = new MobInterpolator();
            interp.OnSpawn(1, 0, Vector2.zero, 10f);
            interp.OnSpawn(2, 0, Vector2.zero, 10f);
            interp.OnRemove(1, 10.1f);

            for (int i = 0; i < 60; i++) interp.Advance(Frame); // one second, the clock has nothing new to follow

            Assert.AreEqual(1, interp.Count);
            Assert.AreEqual(0, interp.StaleRemovals, "a requested removal is not a stale removal");
        }

        [Test]
        public void TimedRemoval_IsNotReversedByLateSnapshots()
        {
            var interp = new MobInterpolator();
            interp.OnSpawn(1, 0, Vector2.zero, 10f);
            interp.OnSpawn(2, 0, Vector2.zero, 10f);
            interp.OnRemove(1, 10.1f);
            for (int i = 0; i < 60; i++) interp.Advance(Frame);
            Assert.AreEqual(1, interp.Count);

            interp.OnSnapshotEntry(1, new Vector2(1, 1), 10.05f);

            Assert.AreEqual(1, interp.Count);
        }

        [Test]
        public void TimedRemoval_WithoutAClockOrAMobIsHarmless()
        {
            var interp = new MobInterpolator();
            Assert.DoesNotThrow(() => interp.OnRemove(7, 5f));

            interp.OnSpawn(8, 0, Vector2.zero, 10f);
            Assert.DoesNotThrow(() => interp.OnRemove(99, 10.2f));
            Assert.AreEqual(1, interp.Count);
        }

        [Test]
        public void Clear_DropsEverything()
        {
            var interp = new MobInterpolator();
            interp.OnSpawn(1, 0, Vector2.zero, 1f);

            interp.Clear();

            Assert.AreEqual(0, interp.Count);
        }
    }
}
