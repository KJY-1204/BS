using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class SegmentHitTests
    {
        const float ShotRadius = 0.2f;

        static ushort Spawn(MobSim sim, float x, float y)
        {
            Assert.IsTrue(sim.TrySpawn(new Vector2(x, y), 0, out ushort id));
            return id;
        }

        static List<MobSim.SegmentHit> Collect(MobSim sim, Vector2 a, Vector2 b, IReadOnlyList<ushort> exclude = null)
        {
            var hits = new List<MobSim.SegmentHit>();
            sim.CollectSegmentHits(a, b, ShotRadius, exclude, hits);
            return hits;
        }

        [Test]
        public void ASweepOverThreeMobs_FindsAllThreeNearestFirst()
        {
            var sim = new MobSim(targetCount: 0);
            ushort far = Spawn(sim, 9, 0);
            ushort near = Spawn(sim, 3, 0);
            ushort middle = Spawn(sim, 6, 0);

            List<MobSim.SegmentHit> hits = Collect(sim, Vector2.zero, new Vector2(12, 0));

            CollectionAssert.AreEqual(new[] { near, middle, far }, hits.Select(h => h.MobId).ToArray());
            Assert.Less(hits[0].T, hits[1].T);
            Assert.Less(hits[1].T, hits[2].T);
        }

        [Test]
        public void TheReturnedCountMatchesTheAppendedHits()
        {
            var sim = new MobSim(targetCount: 0);
            Spawn(sim, 3, 0);
            Spawn(sim, 6, 0);
            var hits = new List<MobSim.SegmentHit>();

            Assert.AreEqual(2, sim.CollectSegmentHits(Vector2.zero, new Vector2(12, 0), ShotRadius, null, hits));
            Assert.AreEqual(2, hits.Count);
            Assert.AreEqual(2, sim.CollectSegmentHits(Vector2.zero, new Vector2(12, 0), ShotRadius, null, hits), "it appends, so the list now has four");
            Assert.AreEqual(4, hits.Count);
        }

        [Test]
        public void MobsAlreadyHit_AreLeftOut()
        {
            var sim = new MobSim(targetCount: 0);
            ushort a = Spawn(sim, 3, 0);
            ushort b = Spawn(sim, 6, 0);

            List<MobSim.SegmentHit> hits = Collect(sim, Vector2.zero, new Vector2(12, 0), new List<ushort> { a });

            CollectionAssert.AreEqual(new[] { b }, hits.Select(h => h.MobId).ToArray());
        }

        [Test]
        public void MobsOffTheSegment_AreNotFound()
        {
            var sim = new MobSim(targetCount: 0);
            Spawn(sim, 5, 3);
            Spawn(sim, 20, 0);
            Spawn(sim, -4, 0);

            Assert.AreEqual(0, Collect(sim, Vector2.zero, new Vector2(12, 0)).Count);
        }

        [Test]
        public void AMobGrazedByTheEdge_IsFound_AndOneJustBeyondIsNot()
        {
            var sim = new MobSim(targetCount: 0);
            float reach = ShotRadius + sim.Radius;
            ushort grazed = Spawn(sim, 5, reach - 0.01f);
            Spawn(sim, 8, reach + 0.05f);

            List<MobSim.SegmentHit> hits = Collect(sim, Vector2.zero, new Vector2(12, 0));

            CollectionAssert.AreEqual(new[] { grazed }, hits.Select(h => h.MobId).ToArray());
        }

        [Test]
        public void TheFirstHitMatchesTryHitSegment()
        {
            var sim = new MobSim(targetCount: 0);
            Spawn(sim, 4, 0.2f);
            Spawn(sim, 7, -0.1f);

            Assert.IsTrue(sim.TryHitSegment(Vector2.zero, new Vector2(12, 0), ShotRadius, out ushort first, out Vector2 point));
            List<MobSim.SegmentHit> hits = Collect(sim, Vector2.zero, new Vector2(12, 0));

            Assert.AreEqual(first, hits[0].MobId);
            Assert.AreEqual(point.x, hits[0].Point.x, 1e-4f);
            Assert.AreEqual(point.y, hits[0].Point.y, 1e-4f);
        }

        [Test]
        public void AnEmptySimulationFindsNothing()
        {
            var sim = new MobSim(targetCount: 0);
            Assert.AreEqual(0, Collect(sim, Vector2.zero, new Vector2(5, 5)).Count);
        }

        [Test]
        public void TheGridAndABruteForceCheckAgree_OnRandomScenes()
        {
            var rng = new System.Random(11);
            for (int scene = 0; scene < 60; scene++)
            {
                var sim = new MobSim(targetCount: 0);
                int n = rng.Next(1, 60);
                for (int i = 0; i < n; i++) sim.TrySpawn(new Vector2((float)(rng.NextDouble() * 28 - 14), (float)(rng.NextDouble() * 16 - 8)), 0, out _);

                var a = new Vector2((float)(rng.NextDouble() * 20 - 10), (float)(rng.NextDouble() * 12 - 6));
                var b = a + new Vector2((float)(rng.NextDouble() * 24 - 12), (float)(rng.NextDouble() * 24 - 12));

                var expected = new List<ushort>();
                float reach = ShotRadius + sim.Radius;
                for (int i = 0; i < sim.Count; i++)
                {
                    Vector2 c = sim.PositionAt(i);
                    Vector2 ab = b - a;
                    float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(c - a, ab) / ab.sqrMagnitude) : 0f;
                    if (Vector2.Distance(c, a + ab * t) <= reach) expected.Add(sim.IdAt(i));
                }

                var found = Collect(sim, a, b).Select(h => h.MobId).ToList();

                CollectionAssert.AreEquivalent(expected, found, $"scene {scene}");
                for (int i = 1; i < found.Count; i++)
                {
                    var hits = Collect(sim, a, b);
                    Assert.LessOrEqual(hits[i - 1].T, hits[i].T + 1e-6f, "nearest first");
                }
            }
        }
    }
}
