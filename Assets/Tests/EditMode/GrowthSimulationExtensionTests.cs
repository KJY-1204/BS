using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class GrowthSimulationExtensionTests
    {
        const float Dt = 0.02f;
        const ulong A = 1, B = 2;

        static SurvivorBody Body(ulong id, float x, float y, bool moving = false) =>
            new SurvivorBody { ClientId = id, Position = new Vector2(x, y), Moving = moving };

        // ---- 2.1 per-survivor maximum health ----

        [Test]
        public void MaxHealth_DiffersPerSurvivor()
        {
            var sim = new SurvivorSim();
            sim.Register(A);
            sim.Register(B);

            sim.SetMaxHealth(A, 130);

            Assert.AreEqual(130, sim.MaxHealthOf(A));
            Assert.AreEqual(CombatConfig.SurvivorMaxHealth, sim.MaxHealthOf(B));
        }

        [Test]
        public void RaisingTheMaximum_DoesNotHeal()
        {
            var sim = new SurvivorSim();
            sim.Register(A);
            sim.ApplyDamage(A, 60);
            Assert.AreEqual(40, sim.Health(A));

            sim.SetMaxHealth(A, 110);

            Assert.AreEqual(40, sim.Health(A));
            Assert.AreEqual(110, sim.MaxHealthOf(A));
        }

        [Test]
        public void LoweringTheMaximumBelowHealth_CutsHealthDown()
        {
            var sim = new SurvivorSim();
            sim.Register(A);

            sim.SetMaxHealth(A, 60);

            Assert.AreEqual(60, sim.Health(A));
        }

        [Test]
        public void Revival_UsesTheSurvivorsOwnMaximum()
        {
            var sim = new SurvivorSim();
            sim.Register(A);
            sim.Register(B);
            sim.SetMaxHealth(A, 120);
            sim.Kill(A);
            var bodies = new List<SurvivorBody> { Body(A, 0, 0), Body(B, 1, 0) };
            var noMobs = new MobSim(targetCount: 0);

            for (int i = 0; i < Mathf.RoundToInt((CombatConfig.ReviveSeconds + 0.2f) / Dt); i++) sim.Step(Dt, bodies, noMobs);

            Assert.IsTrue(sim.IsAlive(A));
            Assert.AreEqual(Mathf.CeilToInt(120 * CombatConfig.ReviveHealthFraction), sim.Health(A));
        }

        [Test]
        public void UnknownSurvivors_AreIgnoredByTheNewMethods()
        {
            var sim = new SurvivorSim();

            Assert.DoesNotThrow(() => sim.SetMaxHealth(99, 150));
            Assert.AreEqual(CombatConfig.SurvivorMaxHealth, sim.MaxHealthOf(99));
        }

        // ---- 2.2 per-shooter damage and cooldown ----

        static ShooterBody Shooter(ulong id, float x = 0f, float y = 0f) =>
            new ShooterBody { ClientId = id, Position = new Vector2(x, y), CanFire = true };

        [Test]
        public void TwoShooters_DealTheirOwnDamage()
        {
            var attack = new AttackSim();
            attack.Register(A);
            attack.Register(B);
            attack.SetModifiers(A, 12, 1f);
            attack.SetModifiers(B, 10, 1f);
            attack.SetAim(A, AimMath.QuantizeAngle(0f), true);
            attack.SetAim(B, AimMath.QuantizeAngle(Mathf.PI), true);

            var mobs = new MobSim(targetCount: 0) { MaxHealth = 100 };
            mobs.TrySpawn(new Vector2(5, 0), 0, out ushort right);
            mobs.TrySpawn(new Vector2(-5, 0), 0, out ushort left);
            var projectiles = new ProjectileSim();
            var bodies = new List<ShooterBody> { Shooter(A), Shooter(B) };

            attack.Step(Dt, bodies, projectiles);
            for (int i = 0; i < 40; i++) projectiles.Step(Dt, mobs);

            mobs.TryGetHealth(right, out int rightHealth);
            mobs.TryGetHealth(left, out int leftHealth);
            Assert.AreEqual(100 - 12, rightHealth, "A's shot");
            Assert.AreEqual(100 - 10, leftHealth, "B's shot");
        }

        [Test]
        public void ProjectilesInFlight_KeepTheDamageTheyWereFiredWith()
        {
            var attack = new AttackSim();
            attack.Register(A);
            attack.SetModifiers(A, 10, 1f);
            attack.SetAim(A, AimMath.QuantizeAngle(0f), true);
            var mobs = new MobSim(targetCount: 0) { MaxHealth = 100 };
            mobs.TrySpawn(new Vector2(8, 0), 0, out ushort id);
            var projectiles = new ProjectileSim();

            attack.Step(Dt, new List<ShooterBody> { Shooter(A) }, projectiles);
            attack.SetAim(A, AimMath.QuantizeAngle(0f), false);
            attack.SetModifiers(A, 99, 1f); // levels up while the shot is flying
            for (int i = 0; i < 60; i++) projectiles.Step(Dt, mobs);

            mobs.TryGetHealth(id, out int health);
            Assert.AreEqual(90, health, "the shot already in the air still deals the old damage");
        }

        static int CountShots(AttackSim attack, float seconds)
        {
            var projectiles = new ProjectileSim();
            var bodies = new List<ShooterBody> { Shooter(A) };
            var events = new List<CombatEvent>();
            int shots = 0;
            for (int i = 0; i < Mathf.RoundToInt(seconds / Dt); i++)
            {
                attack.Step(Dt, bodies, projectiles);
                projectiles.Step(Dt, null);
                events.Clear();
                projectiles.DrainEvents(events);
                foreach (CombatEvent e in events) if (e.Kind == CombatEventKind.Fire) shots++;
            }
            return shots;
        }

        [Test]
        public void HalvingTheCooldown_DoublesTheShots()
        {
            var normal = new AttackSim();
            normal.Register(A);
            normal.SetAim(A, 0, true);
            var fast = new AttackSim();
            fast.Register(A);
            fast.SetAim(A, 0, true);
            fast.SetModifiers(A, CombatConfig.ProjectileDamage, 0.5f);

            Assert.AreEqual(4, CountShots(normal, 1f));
            Assert.AreEqual(8, CountShots(fast, 1f));
        }

        [Test]
        public void WhenTheEffectEnds_TheCooldownIsNormalAgain()
        {
            var attack = new AttackSim();
            attack.Register(A);
            attack.SetAim(A, 0, true);
            attack.SetModifiers(A, CombatConfig.ProjectileDamage, 0.5f);
            CountShots(attack, 1f);

            attack.SetModifiers(A, CombatConfig.ProjectileDamage, 1f);

            Assert.AreEqual(4, CountShots(attack, 1f));
        }

        [Test]
        public void DamageOf_ReportsTheConfiguredValue()
        {
            var attack = new AttackSim();
            attack.Register(A);
            Assert.AreEqual(CombatConfig.ProjectileDamage, attack.DamageOf(A));

            attack.SetModifiers(A, 17, 1f);
            Assert.AreEqual(17, attack.DamageOf(A));
            Assert.DoesNotThrow(() => attack.SetModifiers(99, 5, 1f));
        }

        // ---- 2.3 area damage ----

        static ushort Spawn(MobSim sim, float x, float y)
        {
            Assert.IsTrue(sim.TrySpawn(new Vector2(x, y), 0, out ushort id));
            return id;
        }

        [Test]
        public void AreaDamage_HitsInsideAndSparesOutside()
        {
            var sim = new MobSim(targetCount: 0) { MaxHealth = 100 };
            ushort a = Spawn(sim, 1, 0);
            ushort b = Spawn(sim, 0, 2);
            ushort c = Spawn(sim, -2, -1);
            ushort outside = Spawn(sim, 6, 0);

            int hit = sim.ApplyAreaDamage(Vector2.zero, 3f, 25, new List<Vector2>());

            Assert.AreEqual(3, hit);
            foreach (ushort id in new[] { a, b, c })
            {
                sim.TryGetHealth(id, out int health);
                Assert.AreEqual(75, health);
            }
            sim.TryGetHealth(outside, out int far);
            Assert.AreEqual(100, far);
        }

        [Test]
        public void AreaDamage_RemovesKilledMobsAndReportsWhereTheyDied()
        {
            var sim = new MobSim(targetCount: 0) { MaxHealth = 30 };
            Spawn(sim, 1, 1);
            Spawn(sim, -1, 2);
            Spawn(sim, 2, -1);
            sim.DrainEvents(new List<MobEvent>());

            var deaths = new List<Vector2>();
            sim.ApplyAreaDamage(Vector2.zero, 4f, 30, deaths);

            Assert.AreEqual(0, sim.Count);
            Assert.AreEqual(3, deaths.Count);
            CollectionAssert.Contains(deaths, new Vector2(1, 1));
            CollectionAssert.Contains(deaths, new Vector2(-1, 2));

            var events = new List<MobEvent>();
            sim.DrainEvents(events);
            Assert.AreEqual(3, events.Count);
            foreach (MobEvent e in events) Assert.IsFalse(e.IsSpawn);
        }

        [Test]
        public void AreaDamage_WoundedButNotKilledMobsAreNotReportedAsDeaths()
        {
            var sim = new MobSim(targetCount: 0) { MaxHealth = 100 };
            Spawn(sim, 1, 0);
            var deaths = new List<Vector2>();

            sim.ApplyAreaDamage(Vector2.zero, 3f, 40, deaths);

            Assert.AreEqual(1, sim.Count);
            Assert.AreEqual(0, deaths.Count);
        }

        [Test]
        public void AreaDamage_EmptyAreaAndZeroRadiusChangeNothing()
        {
            var sim = new MobSim(targetCount: 0) { MaxHealth = 30 };
            ushort id = Spawn(sim, 8, 8);

            Assert.AreEqual(0, sim.ApplyAreaDamage(Vector2.zero, 3f, 30, new List<Vector2>()));
            Assert.AreEqual(0, sim.ApplyAreaDamage(new Vector2(8, 8), 0f, 30, new List<Vector2>()));
            Assert.AreEqual(0, new MobSim(targetCount: 0).ApplyAreaDamage(Vector2.zero, 5f, 30, null));

            sim.TryGetHealth(id, out int health);
            Assert.AreEqual(30, health);
        }

        [Test]
        public void AreaDamage_RadiusBoundaryIsInclusive()
        {
            var sim = new MobSim(targetCount: 0) { MaxHealth = 100 };
            ushort onEdge = Spawn(sim, 3f, 0);
            ushort justOutside = Spawn(sim, 3.01f, 0);

            sim.ApplyAreaDamage(Vector2.zero, 3f, 10, null);

            sim.TryGetHealth(onEdge, out int edgeHealth);
            sim.TryGetHealth(justOutside, out int outsideHealth);
            Assert.AreEqual(90, edgeHealth);
            Assert.AreEqual(100, outsideHealth);
        }

        [Test]
        public void AreaDamage_MatchesABruteForceCountOnRandomScenes()
        {
            var random = new System.Random(5);
            for (int scene = 0; scene < 40; scene++)
            {
                var sim = new MobSim(targetCount: 0) { MaxHealth = 1000 };
                for (int i = 0; i < 80; i++)
                    Spawn(sim, (float)(random.NextDouble() * 30 - 15), (float)(random.NextDouble() * 18 - 9));
                var survivors = new List<SurvivorInfo> { new SurvivorInfo { ClientId = 1, Position = Vector2.zero, Alive = true } };
                for (int step = 0; step < 10; step++) sim.Step(Dt, survivors);

                var center = new Vector2((float)(random.NextDouble() * 24 - 12), (float)(random.NextDouble() * 14 - 7));
                float radius = 1f + (float)random.NextDouble() * 5f;
                int expected = 0;
                for (int i = 0; i < sim.Count; i++)
                    if ((sim.PositionAt(i) - center).sqrMagnitude <= radius * radius) expected++;

                Assert.AreEqual(expected, sim.ApplyAreaDamage(center, radius, 1, null), $"scene {scene}");
            }
        }
    }
}
