using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class MobCombatSimTests
    {
        static ushort Spawn(MobSim sim, float x, float y)
        {
            Assert.IsTrue(sim.TrySpawn(new Vector2(x, y), 0, out ushort id));
            return id;
        }

        // ---- Health and damage ----

        [Test]
        public void Damage_ReducesHealthAndKeepsTheMobAlive()
        {
            var sim = new MobSim(targetCount: 0) { MaxHealth = 30 };
            ushort id = Spawn(sim, 0, 0);

            Assert.IsTrue(sim.ApplyDamage(id, 10, out int percent, out bool killed));

            Assert.IsFalse(killed);
            Assert.AreEqual(66, percent);
            Assert.IsTrue(sim.TryGetHealth(id, out int health));
            Assert.AreEqual(20, health);
            Assert.AreEqual(1, sim.Count);
        }

        [Test]
        public void Damage_ToZeroKillsAndRemovesTheMob()
        {
            var sim = new MobSim(targetCount: 0) { MaxHealth = 30 };
            ushort id = Spawn(sim, 0, 0);
            sim.DrainEvents(new List<MobEvent>());

            sim.ApplyDamage(id, 10, out _, out _);
            sim.ApplyDamage(id, 10, out _, out _);
            Assert.IsTrue(sim.ApplyDamage(id, 10, out int percent, out bool killed));

            Assert.IsTrue(killed);
            Assert.AreEqual(0, percent);
            Assert.AreEqual(0, sim.Count);
            Assert.IsFalse(sim.Contains(id));

            var events = new List<MobEvent>();
            sim.DrainEvents(events);
            Assert.AreEqual(1, events.Count);
            Assert.IsFalse(events[0].IsSpawn);
            Assert.AreEqual(id, events[0].Id);
        }

        [Test]
        public void Damage_BeyondHealthStillKillsOnce()
        {
            var sim = new MobSim(targetCount: 0) { MaxHealth = 30 };
            ushort id = Spawn(sim, 0, 0);

            Assert.IsTrue(sim.ApplyDamage(id, 1000, out _, out bool killed));
            Assert.IsTrue(killed);

            var events = new List<MobEvent>();
            sim.DrainEvents(events);
            int removals = 0;
            foreach (MobEvent e in events) if (!e.IsSpawn) removals++;
            Assert.AreEqual(1, removals);
        }

        [Test]
        public void Damage_ToAMobThatIsGoneChangesNothing()
        {
            var sim = new MobSim(targetCount: 0);
            ushort other = Spawn(sim, 3, 3);

            Assert.IsFalse(sim.ApplyDamage(999, 10, out _, out bool killed));

            Assert.IsFalse(killed);
            Assert.AreEqual(1, sim.Count);
            Assert.IsTrue(sim.TryGetHealth(other, out int health));
            Assert.AreEqual(sim.MaxHealth, health);
        }

        [Test]
        public void Damage_NegativeAmountDoesNotHeal()
        {
            var sim = new MobSim(targetCount: 0) { MaxHealth = 30 };
            ushort id = Spawn(sim, 0, 0);
            sim.ApplyDamage(id, 10, out _, out _);

            sim.ApplyDamage(id, -50, out _, out _);

            sim.TryGetHealth(id, out int health);
            Assert.AreEqual(20, health);
        }

        [Test]
        public void Health_FollowsTheMobWhenAnotherIsRemoved()
        {
            var sim = new MobSim(targetCount: 0) { MaxHealth = 30 };
            ushort first = Spawn(sim, 0, 0);
            ushort second = Spawn(sim, 5, 0);
            sim.ApplyDamage(second, 10, out _, out _);

            sim.Remove(first);

            sim.TryGetHealth(second, out int health);
            Assert.AreEqual(20, health, "swap-remove must keep each mob's own health");
        }

        // ---- Segment queries ----

        const float ProjectileRadius = 0.15f;

        [Test]
        public void SegmentHit_PicksTheNearerOfTwoMobsOnALine()
        {
            var sim = new MobSim(targetCount: 0);
            ushort near = Spawn(sim, 5, 0);
            Spawn(sim, 8, 0);

            Assert.IsTrue(sim.TryHitSegment(new Vector2(0, 0), new Vector2(12, 0), ProjectileRadius, out ushort hit, out Vector2 point));

            Assert.AreEqual(near, hit);
            Assert.AreEqual(5f - (ProjectileRadius + MobConfig.Radius), point.x, 0.01f);
            Assert.AreEqual(0f, point.y, 0.01f);
        }

        [Test]
        public void SegmentHit_CatchesAMobThatAFastStepWouldSkipOver()
        {
            var sim = new MobSim(targetCount: 0);
            ushort id = Spawn(sim, 10, 0);

            Assert.IsTrue(sim.TryHitSegment(new Vector2(0, 0), new Vector2(20, 0), ProjectileRadius, out ushort hit, out _));
            Assert.AreEqual(id, hit);
        }

        [Test]
        public void SegmentHit_MissesAMobBesideTheLine()
        {
            var sim = new MobSim(targetCount: 0);
            Spawn(sim, 5, 1f);

            Assert.IsFalse(sim.TryHitSegment(new Vector2(0, 0), new Vector2(12, 0), ProjectileRadius, out _, out _));
        }

        [Test]
        public void SegmentHit_CatchesAMobGrazingTheEdge()
        {
            var sim = new MobSim(targetCount: 0);
            Spawn(sim, 5, 0.5f); // inside radius 0.15 + 0.4

            Assert.IsTrue(sim.TryHitSegment(new Vector2(0, 0), new Vector2(12, 0), ProjectileRadius, out _, out _));
        }

        [Test]
        public void SegmentHit_IgnoresMobsBeyondTheEndOrBehindTheStart()
        {
            var sim = new MobSim(targetCount: 0);
            Spawn(sim, 5, 0);
            Spawn(sim, -5, 0);

            Assert.IsFalse(sim.TryHitSegment(new Vector2(0, 0), new Vector2(3, 0), ProjectileRadius, out _, out _));
            Assert.IsFalse(sim.TryHitSegment(new Vector2(0, 0), new Vector2(-1, 0), ProjectileRadius, out _, out _));
        }

        [Test]
        public void SegmentHit_WithNoMobsReturnsFalse()
        {
            var sim = new MobSim(targetCount: 0);
            Assert.IsFalse(sim.TryHitSegment(Vector2.zero, new Vector2(5, 0), ProjectileRadius, out _, out _));
        }

        [Test]
        public void SegmentHit_FollowsSpawnsAndRemovalsWithoutAStep()
        {
            var sim = new MobSim(targetCount: 0);
            ushort id = Spawn(sim, 5, 0);
            Assert.IsTrue(sim.TryHitSegment(Vector2.zero, new Vector2(10, 0), ProjectileRadius, out _, out _));

            sim.Remove(id);
            Assert.IsFalse(sim.TryHitSegment(Vector2.zero, new Vector2(10, 0), ProjectileRadius, out _, out _));

            ushort later = Spawn(sim, 6, 0);
            Assert.IsTrue(sim.TryHitSegment(Vector2.zero, new Vector2(10, 0), ProjectileRadius, out ushort hit, out _));
            Assert.AreEqual(later, hit);
        }

        [Test]
        public void SegmentHit_MatchesABruteForceSearchOnRandomScenes()
        {
            var random = new System.Random(7);
            for (int scene = 0; scene < 40; scene++)
            {
                var sim = new MobSim(targetCount: 0);
                for (int i = 0; i < 80; i++)
                    Spawn(sim, (float)(random.NextDouble() * 30 - 15), (float)(random.NextDouble() * 18 - 9));

                var survivors = new List<SurvivorInfo> { new SurvivorInfo { ClientId = 1, Position = Vector2.zero, Alive = true } };
                for (int step = 0; step < 10; step++) sim.Step(0.02f, survivors);

                for (int q = 0; q < 25; q++)
                {
                    var a = new Vector2((float)(random.NextDouble() * 30 - 15), (float)(random.NextDouble() * 18 - 9));
                    var b = a + new Vector2((float)(random.NextDouble() * 12 - 6), (float)(random.NextDouble() * 12 - 6));

                    bool fast = sim.TryHitSegment(a, b, ProjectileRadius, out ushort fastId, out _);
                    bool slow = BruteForce(sim, a, b, ProjectileRadius, out ushort slowId);

                    Assert.AreEqual(slow, fast, $"scene {scene} query {q}");
                    if (slow) Assert.AreEqual(slowId, fastId, $"scene {scene} query {q}");
                }
            }
        }

        static bool BruteForce(MobSim sim, Vector2 a, Vector2 b, float radius, out ushort id)
        {
            id = 0;
            float reach = radius + MobConfig.Radius;
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            float length = Mathf.Sqrt(lengthSq);
            float bestEntry = float.MaxValue;
            bool found = false;

            for (int i = 0; i < sim.Count; i++)
            {
                Vector2 c = sim.PositionAt(i);
                float t = lengthSq > 1e-8f ? Mathf.Clamp01(Vector2.Dot(c - a, ab) / lengthSq) : 0f;
                float d = Vector2.Distance(c, a + ab * t);
                if (d > reach) continue;

                float back = length > 1e-5f ? Mathf.Sqrt(Mathf.Max(0f, reach * reach - d * d)) / length : 0f;
                float entry = Mathf.Clamp01(t - back);
                if (entry < bestEntry || (Mathf.Approximately(entry, bestEntry) && found && sim.IdAt(i) < id))
                {
                    bestEntry = entry;
                    id = sim.IdAt(i);
                    found = true;
                }
            }
            return found;
        }

        // ---- Touch query ----

        [Test]
        public void AnyWithin_SeesAMobTouchingThePoint()
        {
            var sim = new MobSim(targetCount: 0);
            Spawn(sim, 2, 2);

            float reach = CombatConfig.SurvivorRadius + MobConfig.Radius;
            Assert.IsTrue(sim.AnyWithin(new Vector2(2.5f, 2f), reach));
            Assert.IsFalse(sim.AnyWithin(new Vector2(4f, 2f), reach));
        }

        [Test]
        public void AnyWithin_FollowsMobsAsTheyMove()
        {
            var sim = new MobSim(targetCount: 0);
            Spawn(sim, 10, 0);
            var survivors = new List<SurvivorInfo> { new SurvivorInfo { ClientId = 1, Position = Vector2.zero, Alive = true } };
            float reach = CombatConfig.SurvivorRadius + MobConfig.Radius;
            Assert.IsFalse(sim.AnyWithin(Vector2.zero, reach));

            for (int i = 0; i < 50 * 5; i++) sim.Step(0.02f, survivors);

            Assert.IsTrue(sim.AnyWithin(Vector2.zero, reach));
        }

        [Test]
        public void AnyWithin_WithNoMobsIsFalse()
        {
            var sim = new MobSim(targetCount: 0);
            Assert.IsFalse(sim.AnyWithin(Vector2.zero, 5f));
        }
    }
}
