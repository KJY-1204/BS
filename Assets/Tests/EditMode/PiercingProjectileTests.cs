using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class PiercingProjectileTests
    {
        const float Dt = 0.02f;
        const ulong Owner = 1;

        static ProjectileShape Shape(float speed = 14f, float lifetime = 0.9f, float radius = 0.15f, int pierce = 1) =>
            new ProjectileShape { Speed = speed, Lifetime = lifetime, Radius = radius, Pierce = pierce };

        static ushort Fire(ProjectileSim sim, ProjectileShape shape, float degrees = 0f, int damage = 10, Vector2? origin = null)
        {
            var spec = new ProjectileSpec
            {
                Owner = Owner, Origin = origin ?? Vector2.zero, Damage = damage, Shape = shape,
                Angle = AimMath.QuantizeAngle(degrees * Mathf.Deg2Rad),
            };
            Assert.IsTrue(sim.TryFire(spec, out ushort id));
            return id;
        }

        static ushort Spawn(MobSim mobs, float x, float y)
        {
            Assert.IsTrue(mobs.TrySpawn(new Vector2(x, y), 0, out ushort id));
            return id;
        }

        static List<CombatEvent> Run(ProjectileSim sim, MobSim mobs, float seconds)
        {
            var all = new List<CombatEvent>();
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                sim.Step(Dt, mobs);
                sim.DrainEvents(all);
            }
            return all;
        }

        static MobSim Mobs(int health = 1000) => new MobSim(targetCount: 0) { MaxHealth = health };

        // ---- Per-projectile shape ----

        [Test]
        public void EachProjectileFliesAtItsOwnSpeedForItsOwnLifetime()
        {
            var sim = new ProjectileSim();
            ushort slow = Fire(sim, Shape(speed: 5f, lifetime: 1f), 0f, origin: new Vector2(-14, 5));
            ushort fast = Fire(sim, Shape(speed: 20f, lifetime: 0.5f), 0f, origin: new Vector2(-14, -5));

            sim.Step(0.5f, null);
            var events = new List<CombatEvent>();
            sim.DrainEvents(events);

            int slowIndex = Enumerable.Range(0, sim.Count).First(i => sim.IdAt(i) == slow);
            Assert.AreEqual(-14f + 5f * 0.5f, sim.PositionAt(slowIndex).x, 1e-3f);
            Assert.IsFalse(Enumerable.Range(0, sim.Count).Any(i => sim.IdAt(i) == fast), "the fast one ended after its short lifetime");
            CombatEvent end = events.Single(e => e.Kind == CombatEventKind.End && e.ProjectileId == fast);
            Assert.AreEqual(-14f + 20f * 0.5f, end.Position.x, 1e-3f);
        }

        [Test]
        public void ALargeProjectileTouchesAMobASmallOneMisses()
        {
            var sim = new ProjectileSim();
            MobSim mobs = Mobs();
            float offset = mobs.Radius + 0.3f;
            Spawn(mobs, 5, offset);

            Fire(sim, Shape(radius: 0.1f), 0f);
            Assert.AreEqual(0, Run(sim, mobs, 0.6f).Count(e => e.Kind == CombatEventKind.Hit), "the small one passes beside it");

            Fire(sim, Shape(radius: 0.45f), 0f);
            Assert.AreEqual(1, Run(sim, mobs, 0.6f).Count(e => e.Kind == CombatEventKind.Hit), "the large one touches it");
        }

        [Test]
        public void TheOldOverloadKeepsTheDefaultShape()
        {
            var sim = new ProjectileSim();
            Assert.IsTrue(sim.TryFire(Owner, Vector2.zero, 0, out _));
            sim.Step(0.1f, null);
            Assert.AreEqual(CombatConfig.ProjectileSpeed * 0.1f, sim.PositionAt(0).x, 1e-3f);
        }

        // ---- Piercing ----

        [Test]
        public void APiercingShotHitsEachMobOnItsLineNearestFirst_AndKeepsFlying()
        {
            var sim = new ProjectileSim();
            MobSim mobs = Mobs();
            ushort c = Spawn(mobs, 9, 0);
            ushort a = Spawn(mobs, 3, 0);
            ushort b = Spawn(mobs, 6, 0);

            ushort id = Fire(sim, Shape(speed: 16f, lifetime: 1f, pierce: 4));
            List<CombatEvent> events = Run(sim, mobs, 1.2f);

            List<CombatEvent> hits = events.Where(e => e.Kind == CombatEventKind.Hit).ToList();
            CollectionAssert.AreEqual(new[] { a, b, c }, hits.Select(h => h.MobId).ToArray());
            Assert.IsTrue(hits.All(h => h.ProjectileId == id));
            Assert.IsTrue(hits.All(h => h.Continues), "none was the last allowed hit");
            Assert.AreEqual(1, events.Count(e => e.Kind == CombatEventKind.End), "it ends once, at its lifetime");
        }

        [Test]
        public void TheLastAllowedHitEndsTheProjectileAndCannotHitAFourthMob()
        {
            var sim = new ProjectileSim();
            MobSim mobs = Mobs();
            ushort fourth = 0;
            for (int i = 0; i < 4; i++)
            {
                ushort id = Spawn(mobs, 3 + 2 * i, 0);
                if (i == 3) fourth = id;
            }

            Fire(sim, Shape(speed: 16f, lifetime: 1f, pierce: 3));
            List<CombatEvent> events = Run(sim, mobs, 1.2f);

            List<CombatEvent> hits = events.Where(e => e.Kind == CombatEventKind.Hit).ToList();
            Assert.AreEqual(3, hits.Count);
            Assert.IsTrue(hits[0].Continues);
            Assert.IsTrue(hits[1].Continues);
            Assert.IsFalse(hits[2].Continues, "the third hit stops it");
            Assert.AreEqual(0, events.Count(e => e.Kind == CombatEventKind.End), "a stopping hit is the ending");
            Assert.AreEqual(0, sim.Count);
            Assert.IsFalse(hits.Any(h => h.MobId == fourth));
            Assert.IsTrue(mobs.TryGetHealth(fourth, out int health));
            Assert.AreEqual(1000, health, "the fourth mob took no damage");
        }

        [Test]
        public void FewerMobsThanHits_TheShotFliesOnToItsLifetime()
        {
            var sim = new ProjectileSim();
            MobSim mobs = Mobs();
            Spawn(mobs, 4, 0);

            Fire(sim, Shape(speed: 10f, lifetime: 1f, pierce: 3));
            List<CombatEvent> events = Run(sim, mobs, 1.2f);

            Assert.AreEqual(1, events.Count(e => e.Kind == CombatEventKind.Hit));
            CombatEvent end = events.Single(e => e.Kind == CombatEventKind.End);
            Assert.AreEqual(10f, end.Position.x, 0.01f, "it flew past the mob to its full range");
        }

        [Test]
        public void AMobIsHitOnlyOnce_EvenWhenTheShotOverlapsItForSeveralSteps()
        {
            var sim = new ProjectileSim();
            MobSim mobs = Mobs();
            ushort id = Spawn(mobs, 5, 0);

            Fire(sim, Shape(speed: 4f, lifetime: 2f, radius: 0.3f, pierce: 4));
            List<CombatEvent> events = Run(sim, mobs, 2.4f);

            Assert.AreEqual(1, events.Count(e => e.Kind == CombatEventKind.Hit && e.MobId == id));
            Assert.IsTrue(mobs.TryGetHealth(id, out int health));
            Assert.AreEqual(990, health, "the mob took the damage once");
        }

        [Test]
        public void AFastShotCrossingThreeMobsInOneStepHitsAllThree()
        {
            var sim = new ProjectileSim();
            MobSim mobs = Mobs();
            Spawn(mobs, 2, 0);
            Spawn(mobs, 4, 0);
            Spawn(mobs, 6, 0);

            Fire(sim, Shape(speed: 300f, lifetime: 0.05f, pierce: 5));
            sim.Step(0.05f, mobs);
            var events = new List<CombatEvent>();
            sim.DrainEvents(events);

            Assert.AreEqual(3, events.Count(e => e.Kind == CombatEventKind.Hit));
        }

        [Test]
        public void TwoProjectilesDoNotShareTheirHitMemory()
        {
            var sim = new ProjectileSim();
            MobSim mobs = Mobs();
            ushort id = Spawn(mobs, 5, 0);

            Fire(sim, Shape(speed: 16f, lifetime: 1f, pierce: 2));
            Fire(sim, Shape(speed: 16f, lifetime: 1f, pierce: 2));
            List<CombatEvent> events = Run(sim, mobs, 1.2f);

            Assert.AreEqual(2, events.Count(e => e.Kind == CombatEventKind.Hit && e.MobId == id), "each projectile hit the mob once");
        }

        [Test]
        public void ARemovedProjectileDoesNotLeaveItsHitMemoryToTheOneMovedIntoItsPlace()
        {
            var sim = new ProjectileSim();
            MobSim mobs = Mobs();
            ushort target = Spawn(mobs, 5, 0);

            Fire(sim, Shape(speed: 16f, lifetime: 0.1f, pierce: 2), 90f, origin: new Vector2(0, -8)); // ends at once, up
            ushort second = Fire(sim, Shape(speed: 16f, lifetime: 1f, pierce: 2));
            List<CombatEvent> events = Run(sim, mobs, 1.2f);

            Assert.AreEqual(1, events.Count(e => e.Kind == CombatEventKind.Hit && e.ProjectileId == second && e.MobId == target));
        }

        [Test]
        public void APierceOfOneMakesTheSameEventsAsBefore_NoContinueMark()
        {
            var sim = new ProjectileSim();
            MobSim mobs = Mobs(30);
            Spawn(mobs, 4, 0);
            Spawn(mobs, 7, 0);

            Fire(sim, Shape(pierce: 1));
            List<CombatEvent> events = Run(sim, mobs, 1f);

            CombatEvent hit = events.Single(e => e.Kind == CombatEventKind.Hit);
            Assert.IsFalse(hit.Continues);
            Assert.AreEqual(0, events.Count(e => e.Kind == CombatEventKind.End));
        }

        [Test]
        public void AKillByAPiercingShotGivesAKilledHit_AndTheShotFliesOn()
        {
            var sim = new ProjectileSim();
            MobSim mobs = Mobs(10);
            Spawn(mobs, 4, 0);
            ushort behind = Spawn(mobs, 7, 0);

            Fire(sim, Shape(speed: 16f, lifetime: 1f, pierce: 3), damage: 10);
            List<CombatEvent> events = Run(sim, mobs, 1.2f);

            List<CombatEvent> hits = events.Where(e => e.Kind == CombatEventKind.Hit).ToList();
            Assert.AreEqual(2, hits.Count);
            Assert.IsTrue(hits.All(h => h.Killed));
            Assert.IsTrue(hits.Any(h => h.MobId == behind));
            Assert.AreEqual(0, mobs.Count);
        }

        [Test]
        public void ADeadMobIsNotHitTwice_ByTheSameStep()
        {
            var sim = new ProjectileSim();
            MobSim mobs = Mobs(5);
            Spawn(mobs, 4, 0);

            Fire(sim, Shape(pierce: 3), damage: 100);
            List<CombatEvent> events = Run(sim, mobs, 1f);

            Assert.AreEqual(1, events.Count(e => e.Kind == CombatEventKind.Hit));
        }

        [Test]
        public void ManyPiercingShotsAndMobs_NeverBreakTheCountsOrTheCap()
        {
            var rng = new System.Random(5);
            var sim = new ProjectileSim(64);
            MobSim mobs = Mobs(40);
            for (int i = 0; i < 50; i++) mobs.TrySpawn(new Vector2((float)(rng.NextDouble() * 28 - 14), (float)(rng.NextDouble() * 16 - 8)), 0, out _);

            var perProjectile = new Dictionary<ushort, List<ushort>>();
            for (int step = 0; step < 300; step++)
            {
                if (step % 3 == 0)
                    sim.TryFire(new ProjectileSpec
                    {
                        Owner = Owner, Origin = new Vector2((float)(rng.NextDouble() * 10 - 5), 0), Angle = (ushort)rng.Next(65536), Damage = 5,
                        Shape = Shape(speed: 10f + rng.Next(10), lifetime: 0.6f, pierce: rng.Next(1, 6)),
                    }, out _);

                sim.Step(Dt, mobs);
                var events = new List<CombatEvent>();
                sim.DrainEvents(events);
                foreach (CombatEvent e in events.Where(e => e.Kind == CombatEventKind.Hit))
                {
                    if (!perProjectile.TryGetValue(e.ProjectileId, out List<ushort> list)) perProjectile[e.ProjectileId] = list = new List<ushort>();
                    Assert.IsFalse(list.Contains(e.MobId), "a projectile never hits the same mob twice");
                    list.Add(e.MobId);
                    Assert.LessOrEqual(list.Count, 5);
                }
                Assert.LessOrEqual(sim.Count, 64);
            }
        }
    }
}
