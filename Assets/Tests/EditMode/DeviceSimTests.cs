using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class DeviceSimTests
    {
        const float Dt = 0.02f;
        static readonly byte DeviceType = MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Device);

        static MobSim NewSim(float interval = 2f, float lifetime = 10f, int perBatch = 3, int hardCap = 100)
        {
            var sim = new MobSim(hardCap: hardCap, targetCount: 0)
            {
                DeviceSettings = new DeviceConfig { ProductionIntervalSeconds = interval, LifetimeSeconds = lifetime, MobsPerBatch = perBatch },
            };
            return sim;
        }

        static List<SurvivorInfo> Far() =>
            new List<SurvivorInfo> { new SurvivorInfo { ClientId = 1, Position = new Vector2(-12, -8), Alive = true } };

        static ushort PlaceDevice(MobSim sim, float x = 0f, float y = 0f)
        {
            Assert.IsTrue(sim.TrySpawn(new Vector2(x, y), DeviceType, out ushort id));
            return id;
        }

        static ushort Spawn(MobSim sim, float x, float y, byte type = MobSim.TypeSummoned)
        {
            Assert.IsTrue(sim.TrySpawn(new Vector2(x, y), type, out ushort id));
            return id;
        }

        static void Run(MobSim sim, List<SurvivorInfo> survivors, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                sim.Step(Dt, survivors);
                sim.DrainActions(new List<MobShotRequest>(), new List<MobBlast>());
            }
        }

        static Vector2 PositionOf(MobSim sim, ushort id)
        {
            for (int i = 0; i < sim.Count; i++)
                if (sim.IdAt(i) == id) return sim.PositionAt(i);
            Assert.Fail($"mob {id} not found");
            return default;
        }

        // ---- inert where a mob would not be (1.2) ----

        [Test]
        public void ADeviceKeepsItsPlaceAndIsNotMovedByAnythingAroundIt()
        {
            var sim = NewSim(lifetime: 100f);
            ushort device = PlaceDevice(sim, 3f, 3f);
            for (int i = 0; i < 6; i++) Spawn(sim, 3f + 0.1f * i, 3f); // mobs piled on top of it

            Run(sim, new List<SurvivorInfo> { new SurvivorInfo { ClientId = 1, Position = new Vector2(8, 3), Alive = true } }, 1f);

            Assert.AreEqual(new Vector2(3f, 3f), PositionOf(sim, device));
        }

        [Test]
        public void TheWindPushesMobsButNotADevice()
        {
            var sim = NewSim(lifetime: 100f);
            ushort device = PlaceDevice(sim, 1f, 0f);
            ushort mob = Spawn(sim, 1f, 1f);

            int pushed = sim.Push(Vector2.zero, 4f, 2f);

            Assert.AreEqual(1, pushed);
            Assert.AreEqual(new Vector2(1f, 0f), PositionOf(sim, device));
            Assert.AreNotEqual(new Vector2(1f, 1f), PositionOf(sim, mob));
        }

        [Test]
        public void ATouchedDeviceHurtsNobody()
        {
            var sim = NewSim(lifetime: 100f);
            PlaceDevice(sim, 0f, 0f);

            Assert.IsFalse(sim.MaxContactMultipleWithin(Vector2.zero, 0.4f, out _));

            Spawn(sim, 0.2f, 0f);
            Assert.IsTrue(sim.MaxContactMultipleWithin(Vector2.zero, 0.4f, out float multiple));
            Assert.AreEqual(1f, multiple); // only the mob counts
        }

        [Test]
        public void ADeviceTakesNoOrderAndIsNoSummonedOrAutomaticMob()
        {
            var sim = NewSim(lifetime: 100f);
            ushort device = PlaceDevice(sim);
            ushort mob = Spawn(sim, 5f, 5f);

            sim.ApplyOrderToSummoned(true, 7, false, default);

            Assert.IsFalse(sim.TryGetOverride(device, out _, out _, out _));
            Assert.IsTrue(sim.TryGetOverride(mob, out bool isPlayer, out ulong client, out _));
            Assert.IsTrue(isPlayer);
            Assert.AreEqual(7UL, client);
            Assert.IsFalse(sim.SetTarget(device, 7));
            Assert.IsFalse(sim.SetTargetPosition(device, Vector2.one));
            Assert.AreEqual(1, sim.DeviceCount);
            Assert.AreEqual(1, sim.SummonedCount);
            Assert.AreEqual(0, sim.AutoCount);
        }

        [Test]
        public void ADeviceDoesNotCountAgainstTheAutomaticSpawnTarget()
        {
            var sim = new MobSim(targetCount: 2) { SpawnPerSecond = 100f };
            PlaceDevice(sim);
            PlaceDevice(sim, 4f, 4f);

            Run(sim, Far(), 1f);

            Assert.AreEqual(2, sim.AutoCount);
            Assert.AreEqual(2, sim.DeviceCount);
        }

        // ---- production and lifetime (1.3) ----

        [Test]
        public void ADeviceMakesItsGroupEveryInterval()
        {
            var sim = NewSim(interval: 2f, perBatch: 3);
            PlaceDevice(sim);

            Run(sim, Far(), 1.9f);
            Assert.AreEqual(0, sim.SummonedCount);

            Run(sim, Far(), 0.2f);
            Assert.AreEqual(3, sim.SummonedCount);

            Run(sim, Far(), 2f);
            Assert.AreEqual(6, sim.SummonedCount);
        }

        [Test]
        public void TheMobsOfADeviceAreBasicSummonedMobsOnARingAroundIt()
        {
            var sim = NewSim(interval: 1f, perBatch: 3);
            PlaceDevice(sim, 2f, 1f);

            Run(sim, Far(), 1.1f);

            DeviceConfig cfg = sim.DeviceSettings;
            int found = 0;
            for (int i = 0; i < sim.Count; i++)
            {
                if (sim.IsDevice(i)) continue;
                found++;
                Assert.IsTrue(sim.IsSummoned(i));
                Assert.AreEqual(MobKinds.Basic, sim.KindAt(i));
                float d = Vector2.Distance(sim.PositionAt(i), new Vector2(2f, 1f));
                Assert.GreaterOrEqual(d, cfg.RingMin - 0.5f); // a few steps of walking toward the survivor
                Assert.LessOrEqual(d, cfg.RingMax + 0.5f);
            }
            Assert.AreEqual(3, found);
        }

        [Test]
        public void NoMobOfADeviceAppearsNextToALivingSurvivor()
        {
            var sim = NewSim(interval: 1f, perBatch: 4);
            PlaceDevice(sim, 0f, 0f);
            var survivors = new List<SurvivorInfo> { new SurvivorInfo { ClientId = 1, Position = new Vector2(0f, 0f), Alive = true } };

            Run(sim, survivors, 1.02f); // the survivor stands on the device: every ring position is inside the safe distance

            Assert.AreEqual(0, sim.SummonedCount);
        }

        [Test]
        public void ADeadSurvivorDoesNotBlockTheRing()
        {
            var sim = NewSim(interval: 1f, perBatch: 3);
            PlaceDevice(sim, 0f, 0f);
            var survivors = new List<SurvivorInfo> { new SurvivorInfo { ClientId = 1, Position = new Vector2(0f, 0f), Alive = false } };

            Run(sim, survivors, 1.02f);

            Assert.AreEqual(3, sim.SummonedCount);
        }

        [Test]
        public void TheHardCapLimitsWhatADeviceMakes()
        {
            var sim = NewSim(interval: 1f, perBatch: 5, hardCap: 8);
            PlaceDevice(sim);
            for (int i = 0; i < 4; i++) Spawn(sim, 8f + i, 6f);

            Run(sim, Far(), 1.1f);

            Assert.AreEqual(8, sim.Count);
            Assert.AreEqual(1, sim.DeviceCount);
            Assert.AreEqual(4 + 3, sim.SummonedCount);
        }

        [Test]
        public void TheMobsOfADeviceObeyTheStandingOrder()
        {
            var sim = NewSim(interval: 1f, perBatch: 2);
            PlaceDevice(sim);
            sim.ApplyOrderToSummoned(true, 9, false, default);

            Run(sim, Far(), 1.1f);

            int ordered = 0;
            for (int i = 0; i < sim.Count; i++)
            {
                if (sim.IsDevice(i)) continue;
                Assert.IsTrue(sim.TryGetOverride(sim.IdAt(i), out bool isPlayer, out ulong client, out _));
                Assert.IsTrue(isPlayer);
                Assert.AreEqual(9UL, client);
                ordered++;
            }
            Assert.AreEqual(2, ordered);
        }

        [Test]
        public void AClearedOrderIsNotAppliedToTheMobsOfADevice()
        {
            var sim = NewSim(interval: 1f, perBatch: 2);
            PlaceDevice(sim);
            sim.ApplyOrderToSummoned(false, 0, true, new Vector2(4, 4));
            sim.ApplyOrderToSummoned(false, 0, false, default);

            Run(sim, Far(), 1.1f);

            for (int i = 0; i < sim.Count; i++)
                if (!sim.IsDevice(i)) Assert.IsFalse(sim.TryGetOverride(sim.IdAt(i), out _, out _, out _));
        }

        [Test]
        public void ADeviceKeepsMakingMobsWhileTimeIsStopped()
        {
            var sim = NewSim(interval: 1f, perBatch: 2);
            ushort device = PlaceDevice(sim);
            sim.Freeze(30f);

            Run(sim, Far(), 1.1f);

            Assert.IsTrue(sim.IsFrozen);
            Assert.AreEqual(2, sim.SummonedCount);
            Assert.AreEqual(new Vector2(0f, 0f), PositionOf(sim, device));
        }

        [Test]
        public void ADeviceDisappearsWhenItsLifetimeHasPassedAndStopsMakingMobs()
        {
            var sim = NewSim(interval: 1f, lifetime: 3.5f, perBatch: 2);
            ushort device = PlaceDevice(sim);
            sim.DrainEvents(new List<MobEvent>());

            Run(sim, Far(), 3.4f);
            Assert.IsTrue(sim.Contains(device));
            Assert.AreEqual(6, sim.SummonedCount);

            Run(sim, Far(), 0.2f);
            Assert.IsFalse(sim.Contains(device));
            Assert.AreEqual(0, sim.DeviceCount);

            var events = new List<MobEvent>();
            sim.DrainEvents(events);
            Assert.IsTrue(events.Exists(e => !e.IsSpawn && e.Id == device));

            Run(sim, Far(), 3f);
            Assert.AreEqual(6, sim.SummonedCount);
        }

        [Test]
        public void ExpiryLeavesNoDeathBehind()
        {
            var sim = NewSim(lifetime: 0.5f);
            PlaceDevice(sim);
            var deaths = new List<Vector2>();

            Run(sim, Far(), 1f);
            sim.ApplyAreaDamage(Vector2.zero, 20f, 1000, deaths);

            Assert.AreEqual(0, deaths.Count);
            Assert.AreEqual(0, sim.DeviceCount);
        }

        [Test]
        public void SeveralDevicesWorkApartAndAnExpiringOneDoesNotSkipTheOthers()
        {
            var sim = NewSim(interval: 1f, lifetime: 100f, perBatch: 1);
            PlaceDevice(sim, 0f, 0f);
            PlaceDevice(sim, 5f, 5f);
            PlaceDevice(sim, -5f, 5f);
            sim.DeviceSettings.LifetimeSeconds = 0.5f;
            PlaceDevice(sim, 5f, -5f); // the lifetime is read when a device is placed

            Run(sim, Far(), 1.1f);

            Assert.AreEqual(3, sim.DeviceCount);
            Assert.AreEqual(3, sim.SummonedCount);
        }

        [Test]
        public void ClearingTheFieldRemovesDevicesAndTheStandingOrder()
        {
            var sim = NewSim(interval: 1f);
            PlaceDevice(sim);
            sim.ApplyOrderToSummoned(true, 9, false, default);

            sim.Clear();
            Assert.AreEqual(0, sim.DeviceCount);
            Assert.AreEqual(0, sim.Count);

            PlaceDevice(sim);
            Run(sim, Far(), 1.1f);
            for (int i = 0; i < sim.Count; i++)
                if (!sim.IsDevice(i)) Assert.IsFalse(sim.TryGetOverride(sim.IdAt(i), out _, out _, out _));
        }

        [Test]
        public void ADeviceAppearsWithTheUsualMobEvent()
        {
            var sim = NewSim(lifetime: 100f);
            ushort device = PlaceDevice(sim, 1f, 2f);

            var events = new List<MobEvent>();
            sim.DrainEvents(events);
            Assert.AreEqual(1, events.Count);
            Assert.IsTrue(events[0].IsSpawn);
            Assert.AreEqual(device, events[0].Id);
            Assert.AreEqual(DeviceType, events[0].Type);
            Assert.IsTrue(MobKinds.IsDeviceType(events[0].Type));
        }

        // ---- being destroyed (1.4) ----

        [Test]
        public void AShotHurtsADeviceAndStopsAtIt()
        {
            var sim = NewSim(lifetime: 100f);
            ushort device = PlaceDevice(sim, 3f, 0f);
            sim.TryGetHealth(device, out int full);
            var shots = new ProjectileSim();
            shots.TryFire(1UL, Vector2.zero, AimMath.FromDirection(Vector2.right), 14, out _);

            for (int i = 0; i < 100; i++) shots.Step(Dt, sim);

            sim.TryGetHealth(device, out int left);
            Assert.AreEqual(full - 14, left);
            Assert.AreEqual(0, shots.Count);
        }

        [Test]
        public void AShotThatCannotPierceStopsAtADeviceInFrontOfAMob()
        {
            var sim = NewSim(lifetime: 100f);
            PlaceDevice(sim, 3f, 0f);
            ushort behind = Spawn(sim, 6f, 0f);
            sim.TryGetHealth(behind, out int mobHealth);
            var shots = new ProjectileSim();
            shots.TryFire(1UL, Vector2.zero, AimMath.FromDirection(Vector2.right), 14, out _);

            for (int i = 0; i < 100; i++) shots.Step(Dt, sim);

            sim.TryGetHealth(behind, out int after);
            Assert.AreEqual(mobHealth, after);
        }

        [Test]
        public void APiercingShotHurtsAMobAndADeviceOnceEachAndFliesOn()
        {
            var sim = NewSim(lifetime: 100f);
            ushort mob = Spawn(sim, 2f, 0f);
            ushort device = PlaceDevice(sim, 4f, 0f);
            sim.TryGetHealth(mob, out int mobFull);
            sim.TryGetHealth(device, out int deviceFull);
            var shots = new ProjectileSim();
            shots.TryFire(new ProjectileSpec
            {
                Owner = 1UL, Origin = Vector2.zero, Angle = AimMath.FromDirection(Vector2.right), Damage = 6,
                Shape = new ProjectileShape { Speed = 10f, Lifetime = 3f, Radius = 0.2f, Pierce = 4 },
            }, out _);

            for (int i = 0; i < 40; i++) shots.Step(Dt, sim);

            sim.TryGetHealth(mob, out int mobLeft);
            sim.TryGetHealth(device, out int deviceLeft);
            Assert.AreEqual(mobFull - 6, mobLeft);
            Assert.AreEqual(deviceFull - 6, deviceLeft);
            Assert.AreEqual(1, shots.Count); // still flying with two hits left
        }

        [Test]
        public void TheHitEventTellsTheHealthThatIsLeft()
        {
            var sim = NewSim(lifetime: 100f);
            ushort device = PlaceDevice(sim, 2f, 0f);
            sim.TryGetHealth(device, out int full);
            var shots = new ProjectileSim();
            shots.TryFire(1UL, Vector2.zero, AimMath.FromDirection(Vector2.right), full / 4, out _);

            var events = new List<CombatEvent>();
            for (int i = 0; i < 60; i++) shots.Step(Dt, sim);
            shots.DrainEvents(events);

            CombatEvent hit = events.Find(e => e.Kind == CombatEventKind.Hit);
            Assert.AreEqual(device, hit.MobId);
            Assert.AreEqual(75, hit.MobHealthPercent);
            Assert.IsFalse(hit.Killed);
        }

        [Test]
        public void TheAreaBurstAndTheBeamHurtADeviceLikeAMob()
        {
            var sim = NewSim(lifetime: 100f);
            ushort device = PlaceDevice(sim, 2f, 0f);
            sim.TryGetHealth(device, out int full);

            Assert.AreEqual(1, sim.ApplyAreaDamage(Vector2.zero, 3f, 10, new List<Vector2>()));
            Assert.AreEqual(1, sim.ApplyLineDamage(Vector2.zero, Vector2.right, 8f, 0.5f, 10, new List<Vector2>()));

            sim.TryGetHealth(device, out int left);
            Assert.AreEqual(full - 20, left);
        }

        [Test]
        public void ADestroyedDeviceIsRemovedWithAKillAndStopsMakingMobs()
        {
            var sim = NewSim(interval: 1f, lifetime: 100f, perBatch: 2);
            ushort device = PlaceDevice(sim, 2f, 2f);

            sim.TryGetHealth(device, out int full);
            Assert.IsTrue(sim.ApplyDamage(device, full, out int percent, out bool killed));
            Assert.IsTrue(killed);
            Assert.AreEqual(0, percent);
            Assert.AreEqual(0, sim.DeviceCount);

            Run(sim, Far(), 3f);
            Assert.AreEqual(0, sim.SummonedCount);
        }

        [Test]
        public void TheKillOfADeviceIsReportedWhereItStood()
        {
            var sim = NewSim(lifetime: 100f);
            ushort device = PlaceDevice(sim, 2f, 2f);
            sim.TryGetHealth(device, out int full);
            var deaths = new List<Vector2>();

            sim.ApplyAreaDamage(new Vector2(2f, 2f), 1f, full, deaths);

            Assert.AreEqual(0, deaths.Count); // not a mob death: it gives its own experience
            var devices = new List<Vector2>();
            sim.DrainDeviceKills(devices);
            Assert.AreEqual(1, devices.Count);
            Assert.AreEqual(new Vector2(2f, 2f), devices[0]);
        }

        [Test]
        public void ADeviceIsSturdierThanAMobButFallsToAFewDozenShots()
        {
            var sim = NewSim();
            int health = sim.MaxHealthOf(MobKinds.Device);
            Assert.Greater(health, sim.MaxHealthOf(MobKinds.Basic));
            Assert.LessOrEqual(health / CombatConfig.ProjectileDamage, 20);
        }
    }
}
