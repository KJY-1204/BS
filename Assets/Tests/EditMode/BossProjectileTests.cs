using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class BossProjectileTests
    {
        const float Dt = 0.02f;
        const ulong Owner = 1;

        static BossSim Boss(float x, float y, int health = 1000, float radius = 0.9f) =>
            new BossSim(new BossDef { Name = "t", MaxHealth = health, MoveSpeed = 6f, Radius = radius, ContactDamage = 25, MatchSeconds = 60f }, new Vector2(x, y));

        static ProjectileShape Shape(float speed = 16f, float lifetime = 1f, float radius = 0.15f, int pierce = 1) =>
            new ProjectileShape { Speed = speed, Lifetime = lifetime, Radius = radius, Pierce = pierce };

        static void Fire(ProjectileSim sim, ProjectileShape shape, int damage = 10, float degrees = 0f, Vector2? origin = null)
        {
            Assert.IsTrue(sim.TryFire(new ProjectileSpec
            {
                Owner = Owner, Origin = origin ?? Vector2.zero, Damage = damage, Shape = shape,
                Angle = AimMath.QuantizeAngle(degrees * Mathf.Deg2Rad),
            }, out _));
        }

        static MobSim Mobs(int health = 1000) => new MobSim(targetCount: 0) { MaxHealth = health };

        static ushort Spawn(MobSim mobs, float x, float y)
        {
            Assert.IsTrue(mobs.TrySpawn(new Vector2(x, y), 0, out ushort id));
            return id;
        }

        static List<CombatEvent> Run(ProjectileSim sim, MobSim mobs, IBossTarget boss, float seconds)
        {
            var all = new List<CombatEvent>();
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                sim.Step(Dt, mobs, boss);
                sim.DrainEvents(all);
            }
            return all;
        }

        [Test]
        public void AShotThatReachesTheBossDamagesItAndEnds()
        {
            var sim = new ProjectileSim();
            BossSim boss = Boss(6, 0);
            Fire(sim, Shape(), 14);

            List<CombatEvent> events = Run(sim, null, boss, 1f);

            CombatEvent hit = events.Single(e => e.Kind == CombatEventKind.Hit);
            Assert.AreEqual(BossSim.MobId, hit.MobId);
            Assert.AreEqual(986, boss.Health);
            Assert.AreEqual(98, hit.MobHealthPercent);
            Assert.IsFalse(hit.Continues);
            Assert.IsFalse(hit.Killed);
            Assert.AreEqual(0, sim.Count);
            Assert.AreEqual(0, events.Count(e => e.Kind == CombatEventKind.End), "a stopping hit is the ending");
        }

        [Test]
        public void TheHitPointIsWhereTheShotFirstTouchesTheBossBody()
        {
            var sim = new ProjectileSim();
            BossSim boss = Boss(6, 0, radius: 0.9f);
            Fire(sim, Shape(radius: 0.15f));

            CombatEvent hit = Run(sim, null, boss, 1f).Single(e => e.Kind == CombatEventKind.Hit);

            // The point is found on the segment the shot swept in that step, so it can lie up to one step before the exact touch.
            float oneStep = 16f * Dt;
            float exact = 6f - 0.9f - 0.15f;
            Assert.LessOrEqual(hit.Position.x, exact + 0.001f, "never past the body");
            Assert.GreaterOrEqual(hit.Position.x, exact - oneStep - 0.001f, "within one step of the first touch");
        }

        [Test]
        public void AShotThatMissesTheBossFliesOn()
        {
            var sim = new ProjectileSim();
            BossSim boss = Boss(6, 3);
            Fire(sim, Shape(lifetime: 0.5f));

            List<CombatEvent> events = Run(sim, null, boss, 1f);

            Assert.AreEqual(0, events.Count(e => e.Kind == CombatEventKind.Hit));
            Assert.AreEqual(1000, boss.Health);
        }

        [Test]
        public void AMobInFrontStopsAShotThatCannotPierce_SoTheBossTakesNothing()
        {
            var sim = new ProjectileSim();
            MobSim mobs = Mobs(1000);
            ushort mob = Spawn(mobs, 3, 0);
            BossSim boss = Boss(7, 0);
            Fire(sim, Shape(pierce: 1));

            List<CombatEvent> events = Run(sim, mobs, boss, 1f);

            Assert.AreEqual(mob, events.Single(e => e.Kind == CombatEventKind.Hit).MobId);
            Assert.AreEqual(1000, boss.Health);
        }

        [Test]
        public void APiercingShotHitsMobsThenTheBoss_AndKeepsFlyingWithOneHitLeft()
        {
            var sim = new ProjectileSim();
            MobSim mobs = Mobs(1000);
            ushort first = Spawn(mobs, 3, 0);
            ushort second = Spawn(mobs, 5, 0);
            BossSim boss = Boss(8, 0);
            Fire(sim, Shape(pierce: 4), 14);

            List<CombatEvent> events = Run(sim, mobs, boss, 1.2f);

            List<CombatEvent> hits = events.Where(e => e.Kind == CombatEventKind.Hit).ToList();
            CollectionAssert.AreEqual(new[] { first, second, BossSim.MobId }, hits.Select(h => h.MobId).ToArray(), "nearest first, the boss among them");
            Assert.IsTrue(hits.All(h => h.Continues), "the fourth hit is still available");
            Assert.AreEqual(986, boss.Health);
            Assert.AreEqual(1, events.Count(e => e.Kind == CombatEventKind.End), "it ends once, at its lifetime");
        }

        [Test]
        public void TheBossCountsAsOneOfTheTargetsAPiercingShotMayHit()
        {
            var sim = new ProjectileSim();
            MobSim mobs = Mobs(1000);
            Spawn(mobs, 3, 0);
            BossSim boss = Boss(6, 0);
            Spawn(mobs, 9, 0);
            Fire(sim, Shape(pierce: 2));

            List<CombatEvent> events = Run(sim, mobs, boss, 1.2f);

            List<CombatEvent> hits = events.Where(e => e.Kind == CombatEventKind.Hit).ToList();
            Assert.AreEqual(2, hits.Count, "the mob and the boss use up both hits");
            Assert.AreEqual(BossSim.MobId, hits[1].MobId);
            Assert.IsFalse(hits[1].Continues, "the boss was the last allowed hit");
        }

        [Test]
        public void APiercingShotHitsTheBossOnlyOnce_EvenIfItOverlapsForSeveralSteps()
        {
            var sim = new ProjectileSim();
            BossSim boss = Boss(5, 0);
            Fire(sim, Shape(speed: 3f, lifetime: 3f, radius: 0.3f, pierce: 5), 10);

            List<CombatEvent> events = Run(sim, null, boss, 3.5f);

            Assert.AreEqual(1, events.Count(e => e.Kind == CombatEventKind.Hit && e.MobId == BossSim.MobId));
            Assert.AreEqual(990, boss.Health);
        }

        [Test]
        public void ADifferentShotMayStillHitTheBoss()
        {
            var sim = new ProjectileSim();
            BossSim boss = Boss(5, 0);
            Fire(sim, Shape(pierce: 3), 10);
            Fire(sim, Shape(pierce: 3), 10);

            List<CombatEvent> events = Run(sim, null, boss, 1.2f);

            Assert.AreEqual(2, events.Count(e => e.Kind == CombatEventKind.Hit && e.MobId == BossSim.MobId));
            Assert.AreEqual(980, boss.Health);
        }

        [Test]
        public void AFallenBossIsNotHitAndTheShotFliesThrough()
        {
            var sim = new ProjectileSim();
            BossSim boss = Boss(5, 0, health: 10);
            boss.TakeDamage(10, out _, out _);
            Fire(sim, Shape(lifetime: 0.6f));

            List<CombatEvent> events = Run(sim, null, boss, 1f);

            Assert.AreEqual(0, events.Count(e => e.Kind == CombatEventKind.Hit));
            Assert.AreEqual(1, events.Count(e => e.Kind == CombatEventKind.End));
        }

        [Test]
        public void TheKillingHitSaysSo_AndTheBossFalls()
        {
            var sim = new ProjectileSim();
            BossSim boss = Boss(5, 0, health: 20);
            Fire(sim, Shape(), 30);

            CombatEvent hit = Run(sim, null, boss, 1f).Single(e => e.Kind == CombatEventKind.Hit);

            Assert.IsTrue(hit.Killed);
            Assert.AreEqual(0, hit.MobHealthPercent);
            Assert.IsTrue(boss.HasFallen);
            Assert.IsTrue(boss.JustFell);
        }

        [Test]
        public void WithNoBossTheShotsBehaveAsBefore()
        {
            var sim = new ProjectileSim();
            MobSim mobs = Mobs(1000);
            ushort mob = Spawn(mobs, 4, 0);
            Fire(sim, Shape());

            List<CombatEvent> viaOldOverload = new List<CombatEvent>();
            for (int i = 0; i < 50; i++) { sim.Step(Dt, mobs); sim.DrainEvents(viaOldOverload); }

            Assert.AreEqual(mob, viaOldOverload.Single(e => e.Kind == CombatEventKind.Hit).MobId);
        }

        [Test]
        public void AnEnemyOnlyBossStepWorksWithoutMobs()
        {
            var sim = new ProjectileSim();
            BossSim boss = Boss(5, 0);
            Fire(sim, Shape());
            Assert.DoesNotThrow(() => Run(sim, null, boss, 1f));
            Assert.Less(boss.Health, 1000);
        }

        [Test]
        public void AFastShotDoesNotSkipOverTheBoss()
        {
            var sim = new ProjectileSim();
            BossSim boss = Boss(6, 0.2f);
            Fire(sim, Shape(speed: 400f, lifetime: 0.05f, pierce: 2));

            List<CombatEvent> events = Run(sim, null, boss, 0.1f);

            Assert.AreEqual(1, events.Count(e => e.Kind == CombatEventKind.Hit && e.MobId == BossSim.MobId));
        }

        [Test]
        public void ManyShotsAndMobsAroundTheBoss_NeverBreakTheRules()
        {
            var rng = new System.Random(8);
            var sim = new ProjectileSim(64);
            MobSim mobs = Mobs(60);
            for (int i = 0; i < 40; i++) mobs.TrySpawn(new Vector2((float)(rng.NextDouble() * 24 - 12), (float)(rng.NextDouble() * 14 - 7)), 0, out _);
            BossSim boss = Boss(2, 1, health: 100000);

            var perShot = new Dictionary<ushort, List<ushort>>();
            for (int step = 0; step < 300; step++)
            {
                if (step % 2 == 0)
                    sim.TryFire(new ProjectileSpec
                    {
                        Owner = Owner, Origin = new Vector2(-6, 0), Angle = (ushort)rng.Next(65536), Damage = 5,
                        Shape = Shape(speed: 10f + rng.Next(10), lifetime: 0.7f, pierce: rng.Next(1, 6)),
                    }, out _);

                sim.Step(Dt, mobs, boss);
                var events = new List<CombatEvent>();
                sim.DrainEvents(events);
                foreach (CombatEvent e in events.Where(e => e.Kind == CombatEventKind.Hit))
                {
                    if (!perShot.TryGetValue(e.ProjectileId, out List<ushort> list)) perShot[e.ProjectileId] = list = new List<ushort>();
                    Assert.IsFalse(list.Contains(e.MobId), "no target is hit twice by one shot");
                    list.Add(e.MobId);
                }
                Assert.LessOrEqual(sim.Count, 64);
            }
            Assert.Less(boss.Health, 100000, "some shots reached the boss");
        }
    }
}
