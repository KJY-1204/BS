using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class SurvivorTargetsTests
    {
        static SurvivorTargets Make(SurvivorSim sim, params ulong[] ids)
        {
            var bodies = new List<SurvivorBody>();
            foreach (ulong id in ids) bodies.Add(new SurvivorBody { ClientId = id, Position = new Vector2(id, 0f) });
            var targets = new SurvivorTargets(sim, id => (byte)(id + 10));
            targets.SetBodies(bodies);
            return targets;
        }

        [Test]
        public void ALivingSurvivorWithoutProtection_CanBeHit()
        {
            var sim = new SurvivorSim();
            sim.Register(1, 100);
            SurvivorTargets t = Make(sim, 1);

            Assert.AreEqual(1, t.Count);
            Assert.IsTrue(t.CanBeHit(0));
            Assert.AreEqual(11, t.SlotAt(0));
            Assert.AreEqual(sim.Radius, t.Radius);
            Assert.AreEqual(new Vector2(1f, 0f), t.PositionAt(0));
        }

        [Test]
        public void ADamageReducesHealthAndReportsThePercentage()
        {
            var sim = new SurvivorSim();
            sim.Register(1, 80);
            SurvivorTargets t = Make(sim, 1);

            Assert.IsTrue(t.TakeDamage(0, 20, out int percent, out bool fell));

            Assert.AreEqual(60, sim.Health(1));
            Assert.AreEqual(75, percent);
            Assert.IsFalse(fell);
        }

        [Test]
        public void HealthNeverGoesBelowZeroAndTheFallIsReported()
        {
            var sim = new SurvivorSim();
            sim.Register(1, 10);
            SurvivorTargets t = Make(sim, 1);

            Assert.IsTrue(t.TakeDamage(0, 50, out int percent, out bool fell));

            Assert.AreEqual(0, sim.Health(1));
            Assert.AreEqual(0, percent);
            Assert.IsTrue(fell);
        }

        [Test]
        public void AProtectedSurvivor_CannotBeHitAndTakesNothing()
        {
            var sim = new SurvivorSim();
            sim.Register(1, 100);
            SurvivorTargets t = Make(sim, 1);
            Assert.IsTrue(t.TakeDamage(0, 10, out _, out _));

            Assert.IsFalse(t.CanBeHit(0));
            Assert.IsFalse(t.TakeDamage(0, 10, out _, out bool fell));
            Assert.IsFalse(fell);
            Assert.AreEqual(90, sim.Health(1));
        }

        [Test]
        public void TheProtectionEnds_AndTheSurvivorCanBeHitAgain()
        {
            var sim = new SurvivorSim();
            sim.Register(1, 100);
            SurvivorTargets t = Make(sim, 1);
            Assert.IsTrue(t.TakeDamage(0, 10, out _, out _));

            sim.Step(sim.InvulnerabilitySeconds + 0.01f, new List<SurvivorBody>(), null);

            Assert.IsTrue(t.CanBeHit(0));
        }

        [Test]
        public void AFallenSurvivor_CannotBeHit()
        {
            var sim = new SurvivorSim();
            sim.Register(1, 100);
            sim.Kill(1);
            SurvivorTargets t = Make(sim, 1);

            Assert.IsFalse(t.CanBeHit(0));
            Assert.IsFalse(t.TakeDamage(0, 10, out _, out _));
        }

        [Test]
        public void ASurvivorUsesItsOwnMaximumForThePercentage()
        {
            var sim = new SurvivorSim();
            sim.Register(1, 200);
            SurvivorTargets t = Make(sim, 1);

            t.TakeDamage(0, 50, out int percent, out _);

            Assert.AreEqual(75, percent);
        }
    }
}
