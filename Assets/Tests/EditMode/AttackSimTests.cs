using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class AttackSimTests
    {
        const float Dt = 0.02f;
        const ulong A = 1;

        static List<ShooterBody> Shooters(bool canFire = true, ulong id = A, float x = 0f, float y = 0f) =>
            new List<ShooterBody> { new ShooterBody { ClientId = id, Position = new Vector2(x, y), CanFire = canFire } };

        /// <summary>Runs attack and projectile steps for the given time and returns how many shots were fired.</summary>
        static int CountShots(AttackSim attack, ProjectileSim projectiles, List<ShooterBody> bodies, float seconds,
                              System.Action<int> beforeStep = null)
        {
            int shots = 0;
            var events = new List<CombatEvent>();
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                beforeStep?.Invoke(i);
                attack.Step(Dt, bodies, projectiles);
                projectiles.Step(Dt, null);
                events.Clear();
                projectiles.DrainEvents(events);
                foreach (CombatEvent e in events) if (e.Kind == CombatEventKind.Fire) shots++;
            }
            return shots;
        }

        [Test]
        public void HoldingFire_FiresOnceEveryCooldown()
        {
            var attack = new AttackSim();
            attack.Register(A);
            attack.SetAim(A, 0, true);

            int shots = CountShots(attack, new ProjectileSim(), Shooters(), 1f);

            Assert.AreEqual(4, shots, "one second with a 0.25 s cooldown");
        }

        [Test]
        public void TheFirstShotLeavesAsSoonAsFireIsHeld()
        {
            var attack = new AttackSim();
            attack.Register(A);
            attack.SetAim(A, 0, true);
            var projectiles = new ProjectileSim();

            attack.Step(Dt, Shooters(), projectiles);

            Assert.AreEqual(1, projectiles.Count);
        }

        [Test]
        public void ReleasingFire_StopsTheShots()
        {
            var attack = new AttackSim();
            attack.Register(A);
            attack.SetAim(A, 0, true);
            var projectiles = new ProjectileSim();
            CountShots(attack, projectiles, Shooters(), 0.5f);

            attack.SetAim(A, 0, false);
            int more = CountShots(attack, projectiles, Shooters(), 1f);

            Assert.AreEqual(0, more);
        }

        [Test]
        public void PressingAgainDuringTheCooldownDoesNotFireAtOnce()
        {
            var attack = new AttackSim();
            attack.Register(A);
            var projectiles = new ProjectileSim();
            attack.SetAim(A, 0, true);
            attack.Step(Dt, Shooters(), projectiles);
            attack.SetAim(A, 0, false);
            for (int i = 0; i < 5; i++) attack.Step(Dt, Shooters(), projectiles); // 0.1 s later

            attack.SetAim(A, 0, true);
            projectiles.DrainEvents(new List<CombatEvent>());
            attack.Step(Dt, Shooters(), projectiles);

            var events = new List<CombatEvent>();
            projectiles.DrainEvents(events);
            Assert.AreEqual(0, events.Count, "the cooldown kept running while the button was up");
            Assert.Greater(attack.CooldownRemaining(A), 0f);
        }

        [Test]
        public void MashingTheButtonIsNoFasterThanHoldingIt()
        {
            var held = new AttackSim();
            held.Register(A);
            held.SetAim(A, 0, true);
            int holdShots = CountShots(held, new ProjectileSim(), Shooters(), 2f);

            var mashed = new AttackSim();
            mashed.Register(A);
            int mashShots = CountShots(mashed, new ProjectileSim(), Shooters(), 2f,
                beforeStep: i => mashed.SetAim(A, 0, i % 2 == 0));

            Assert.LessOrEqual(mashShots, holdShots);
            Assert.Greater(mashShots, 0);
        }

        [Test]
        public void SendingFireStateFarMoreOftenThanTheCooldownChangesNothing()
        {
            var attack = new AttackSim();
            attack.Register(A);
            int shots = CountShots(attack, new ProjectileSim(), Shooters(), 1f,
                beforeStep: i => { for (int k = 0; k < 20; k++) attack.SetAim(A, 0, true); });

            Assert.AreEqual(4, shots);
        }

        [Test]
        public void DeadSurvivorsAndTheBossCannotFire()
        {
            var attack = new AttackSim();
            attack.Register(A);
            attack.SetAim(A, 0, true);
            var projectiles = new ProjectileSim();

            int shots = CountShots(attack, projectiles, Shooters(canFire: false), 2f);

            Assert.AreEqual(0, shots);
            Assert.AreEqual(0, projectiles.Count);
        }

        [Test]
        public void AFallenSurvivorMayFireAgainOnceAliveButCooldownIsNotBanked()
        {
            var attack = new AttackSim();
            attack.Register(A);
            attack.SetAim(A, 0, true);
            var projectiles = new ProjectileSim();
            CountShots(attack, projectiles, Shooters(canFire: false), 3f);

            int shots = CountShots(attack, projectiles, Shooters(canFire: true), 1f);

            Assert.AreEqual(4, shots, "no extra burst after a long pause");
        }

        [Test]
        public void UnknownClientsAreIgnored()
        {
            var attack = new AttackSim();
            Assert.DoesNotThrow(() => attack.SetAim(99, 0, true));

            var projectiles = new ProjectileSim();
            attack.Step(Dt, Shooters(id: 99), projectiles);

            Assert.AreEqual(0, projectiles.Count);
        }

        [Test]
        public void ShotsFlyInTheAimedDirection()
        {
            var attack = new AttackSim();
            attack.Register(A);
            ushort up = AimMath.QuantizeAngle(Mathf.PI * 0.5f);
            attack.SetAim(A, up, true);
            var projectiles = new ProjectileSim();

            attack.Step(Dt, Shooters(), projectiles);
            for (int i = 0; i < 10; i++) projectiles.Step(Dt, null);

            Assert.AreEqual(1, projectiles.Count);
            Assert.AreEqual(0f, projectiles.PositionAt(0).x, 0.01f);
            Assert.Greater(projectiles.PositionAt(0).y, 2f);
        }

        [Test]
        public void AShotRefusedByTheProjectileCapKeepsTheCooldownFree()
        {
            var attack = new AttackSim();
            attack.Register(A);
            attack.SetAim(A, 0, true);
            var projectiles = new ProjectileSim(cap: 1) { Speed = 0f, Lifetime = 100f };

            attack.Step(Dt, Shooters(), projectiles);
            Assert.AreEqual(1, projectiles.Count);

            // Let the cooldown pass; the cap still refuses, so the shot stays ready.
            for (int i = 0; i < 20; i++) attack.Step(Dt, Shooters(), projectiles);
            Assert.AreEqual(1, projectiles.Count);
            Assert.AreEqual(0f, attack.CooldownRemaining(A));

            projectiles.Clear();
            attack.Step(Dt, Shooters(), projectiles);
            Assert.AreEqual(1, projectiles.Count, "fires at once when there is room again");
        }

        [Test]
        public void AngleCodingRoundTripsAndPointsTheRightWay()
        {
            foreach (float degrees in new[] { 0f, 30f, 90f, 135f, 180f, 270f, 359f, -90f })
            {
                float radians = degrees * Mathf.Deg2Rad;
                Vector2 expected = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                Vector2 actual = AimMath.Direction(AimMath.QuantizeAngle(radians));
                Assert.Less(Vector2.Distance(expected, actual), 0.001f, $"{degrees} degrees");
            }

            Assert.AreEqual(AimMath.QuantizeAngle(0f), AimMath.QuantizeAngle(Mathf.PI * 2f));
            Assert.AreEqual(AimMath.QuantizeAngle(Mathf.PI * 1.5f), AimMath.QuantizeAngle(-Mathf.PI * 0.5f));
            Assert.AreEqual(AimMath.QuantizeAngle(Mathf.PI * 0.5f), AimMath.FromDirection(Vector2.up));
        }
    }
}
