using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class ProjectileSimTests
    {
        const float Dt = 0.02f;
        const ulong Owner = 1;

        static ushort Fire(ProjectileSim sim, float x, float y, float degrees = 0f)
        {
            Assert.IsTrue(sim.TryFire(Owner, new Vector2(x, y), AimMath.QuantizeAngle(degrees * Mathf.Deg2Rad), out ushort id));
            return id;
        }

        static void Run(ProjectileSim sim, MobSim mobs, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++) sim.Step(Dt, mobs);
        }

        static List<CombatEvent> Drain(ProjectileSim sim)
        {
            var events = new List<CombatEvent>();
            sim.DrainEvents(events);
            return events;
        }

        static MobSim Mobs() => new MobSim(targetCount: 0);

        // ---- 3.2 flight, lifetime, bounds, cap ----

        [Test]
        public void Flight_IsStraightAtTheFixedSpeed()
        {
            var sim = new ProjectileSim();
            Fire(sim, 0, 0);

            Run(sim, null, 0.5f);

            Assert.AreEqual(CombatConfig.ProjectileSpeed * 0.5f, sim.PositionAt(0).x, 0.05f);
            Assert.AreEqual(0f, sim.PositionAt(0).y, 0.01f);
        }

        [Test]
        public void Lifetime_EndsAfterSpeedTimesLifetimeAndReportsTheEnd()
        {
            var sim = new ProjectileSim();
            Fire(sim, 0, 0);
            Drain(sim);

            Run(sim, null, CombatConfig.ProjectileLifetimeSeconds + 0.1f);

            Assert.AreEqual(0, sim.Count);
            List<CombatEvent> events = Drain(sim);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(CombatEventKind.End, events[0].Kind);
            Assert.AreEqual(CombatConfig.ProjectileSpeed * CombatConfig.ProjectileLifetimeSeconds, events[0].Position.x, 0.05f);
        }

        [Test]
        public void ArenaEdge_EndsTheProjectileAtTheBoundary()
        {
            var sim = new ProjectileSim();
            Fire(sim, 10, 0);
            Drain(sim);

            Run(sim, null, 1f);

            List<CombatEvent> events = Drain(sim);
            Assert.AreEqual(0, sim.Count);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(CombatEventKind.End, events[0].Kind);
            Assert.AreEqual(ArenaConfig.Width * 0.5f, events[0].Position.x, 0.01f);
        }

        [Test]
        public void ArenaEdge_AlsoStopsDiagonalAndVerticalShots()
        {
            var sim = new ProjectileSim();
            Fire(sim, 0, 8, 90f);
            Fire(sim, 14, 8, 45f);
            Drain(sim);

            Run(sim, null, 1f);

            Assert.AreEqual(0, sim.Count);
            foreach (CombatEvent e in Drain(sim))
            {
                Assert.LessOrEqual(Mathf.Abs(e.Position.x), ArenaConfig.Width * 0.5f + 0.01f);
                Assert.LessOrEqual(Mathf.Abs(e.Position.y), ArenaConfig.Height * 0.5f + 0.01f);
            }
        }

        [Test]
        public void Cap_RefusesNewShots()
        {
            var sim = new ProjectileSim(cap: 3);
            for (int i = 0; i < 3; i++) Fire(sim, 0, i);

            Assert.IsFalse(sim.TryFire(Owner, Vector2.zero, 0, out ushort id));

            Assert.AreEqual(0, id);
            Assert.AreEqual(3, sim.Count);
        }

        [Test]
        public void Cap_FreesUpWhenProjectilesEnd()
        {
            var sim = new ProjectileSim(cap: 1);
            Fire(sim, 0, 0);
            Run(sim, null, 1.2f);

            Assert.IsTrue(sim.TryFire(Owner, Vector2.zero, 0, out _));
        }

        [Test]
        public void Ids_AreUniqueAndNeverZero()
        {
            var sim = new ProjectileSim();
            var seen = new HashSet<ushort>();
            for (int i = 0; i < 100; i++)
            {
                ushort id = Fire(sim, 0, 0);
                Assert.AreNotEqual(0, id);
                Assert.IsTrue(seen.Add(id));
            }
        }

        [Test]
        public void FireEvent_CarriesTheStartAndTheAim()
        {
            var sim = new ProjectileSim();
            ushort angle = AimMath.QuantizeAngle(1f);
            sim.TryFire(7, new Vector2(2, 3), angle, out ushort id);

            CombatEvent e = Drain(sim)[0];

            Assert.AreEqual(CombatEventKind.Fire, e.Kind);
            Assert.AreEqual(id, e.ProjectileId);
            Assert.AreEqual(7UL, e.Owner);
            Assert.AreEqual(new Vector2(2, 3), e.Position);
            Assert.AreEqual(angle, e.Angle);
        }

        // ---- 3.3 hits ----

        [Test]
        public void Hit_OnlyTheNearerOfTwoMobsInALineTakesDamage()
        {
            MobSim mobs = Mobs();
            mobs.TrySpawn(new Vector2(5, 0), 0, out ushort near);
            mobs.TrySpawn(new Vector2(8, 0), 0, out ushort far);
            var sim = new ProjectileSim();
            Fire(sim, 0, 0);
            Drain(sim);

            Run(sim, mobs, 1f);

            mobs.TryGetHealth(near, out int nearHealth);
            mobs.TryGetHealth(far, out int farHealth);
            Assert.AreEqual(mobs.MaxHealth - CombatConfig.ProjectileDamage, nearHealth);
            Assert.AreEqual(mobs.MaxHealth, farHealth);
            Assert.AreEqual(0, sim.Count, "the projectile is used up");

            List<CombatEvent> events = Drain(sim);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(CombatEventKind.Hit, events[0].Kind);
            Assert.AreEqual(near, events[0].MobId);
            Assert.AreEqual(66, events[0].MobHealthPercent);
            Assert.IsFalse(events[0].Killed);
            Assert.Less(events[0].Position.x, 5f);
            Assert.Greater(events[0].Position.x, 4f);
        }

        [Test]
        public void Hit_ThreeShotsKillAndRemoveTheMob()
        {
            MobSim mobs = Mobs();
            mobs.TrySpawn(new Vector2(6, 0), 0, out ushort id);
            mobs.DrainEvents(new List<MobEvent>());
            var sim = new ProjectileSim();

            CombatEvent last = default;
            for (int shot = 0; shot < 3; shot++)
            {
                Fire(sim, 0, 0);
                Run(sim, mobs, 1f);
                List<CombatEvent> events = Drain(sim);
                last = events[events.Count - 1];
            }

            Assert.AreEqual(CombatEventKind.Hit, last.Kind);
            Assert.IsTrue(last.Killed);
            Assert.AreEqual(0, last.MobHealthPercent);
            Assert.AreEqual(0, mobs.Count);

            var mobEvents = new List<MobEvent>();
            mobs.DrainEvents(mobEvents);
            Assert.AreEqual(1, mobEvents.Count);
            Assert.IsFalse(mobEvents[0].IsSpawn);
            Assert.AreEqual(id, mobEvents[0].Id);
        }

        [Test]
        public void Hit_AFastProjectileDoesNotSkipOverAMob()
        {
            MobSim mobs = Mobs();
            mobs.TrySpawn(new Vector2(6, 0), 0, out ushort id);
            var sim = new ProjectileSim { Speed = 400f };
            Fire(sim, 0, 0);
            Drain(sim);

            sim.Step(Dt, mobs); // 8 units in one step, the mob is 0.8 wide

            mobs.TryGetHealth(id, out int health);
            Assert.Less(health, mobs.MaxHealth);
            Assert.AreEqual(0, sim.Count);
        }

        [Test]
        public void Hit_ProjectilesMissMobsOffToTheSide()
        {
            MobSim mobs = Mobs();
            mobs.TrySpawn(new Vector2(5, 2), 0, out ushort id);
            var sim = new ProjectileSim();
            Fire(sim, 0, 0);
            Drain(sim);

            Run(sim, mobs, 1.2f);

            mobs.TryGetHealth(id, out int health);
            Assert.AreEqual(mobs.MaxHealth, health);
            List<CombatEvent> events = Drain(sim);
            Assert.AreEqual(CombatEventKind.End, events[events.Count - 1].Kind);
        }

        [Test]
        public void Hit_AMobRightAtTheMuzzleIsHitImmediately()
        {
            MobSim mobs = Mobs();
            mobs.TrySpawn(new Vector2(0.3f, 0), 0, out ushort id);
            var sim = new ProjectileSim();
            Fire(sim, 0, 0);

            sim.Step(Dt, mobs);

            mobs.TryGetHealth(id, out int health);
            Assert.Less(health, mobs.MaxHealth);
        }

        [Test]
        public void Hit_UsesTheConfiguredDamage()
        {
            MobSim mobs = Mobs();
            mobs.TrySpawn(new Vector2(4, 0), 0, out ushort id);
            var sim = new ProjectileSim { Damage = 25 };
            Fire(sim, 0, 0);

            Run(sim, mobs, 0.8f);

            mobs.TryGetHealth(id, out int health);
            Assert.AreEqual(mobs.MaxHealth - 25, health);
        }

        [Test]
        public void ManyProjectilesAndMobs_EveryProjectileEndsExactlyOnce()
        {
            var random = new System.Random(11);
            MobSim mobs = Mobs();
            for (int i = 0; i < 60; i++)
                mobs.TrySpawn(new Vector2((float)(random.NextDouble() * 28 - 14), (float)(random.NextDouble() * 16 - 8)), 0, out _);
            var sim = new ProjectileSim();
            var fired = new HashSet<ushort>();
            var ended = new HashSet<ushort>();

            for (int step = 0; step < 250; step++)
            {
                if (step % 5 == 0)
                {
                    sim.TryFire(Owner, new Vector2((float)(random.NextDouble() * 20 - 10), (float)(random.NextDouble() * 12 - 6)),
                                AimMath.QuantizeAngle((float)(random.NextDouble() * Mathf.PI * 2)), out ushort id);
                    fired.Add(id);
                }
                sim.Step(Dt, mobs);
                foreach (CombatEvent e in Drain(sim))
                    if (e.Kind != CombatEventKind.Fire) Assert.IsTrue(ended.Add(e.ProjectileId), $"projectile {e.ProjectileId} ended twice");
            }

            // Let everything finish.
            Run(sim, mobs, 2f);
            foreach (CombatEvent e in Drain(sim))
                if (e.Kind != CombatEventKind.Fire) Assert.IsTrue(ended.Add(e.ProjectileId));

            Assert.AreEqual(0, sim.Count);
            Assert.AreEqual(fired.Count, ended.Count);
        }
    }
}
