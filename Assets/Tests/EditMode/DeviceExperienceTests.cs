using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class DeviceExperienceTests
    {
        static readonly byte DeviceType = MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Device);

        static GrowthBody Body(ulong id, float x, float y, bool alive = true) =>
            new GrowthBody { ClientId = id, Position = new Vector2(x, y), Alive = alive };

        static LevelingSim New(params ulong[] clients)
        {
            var sim = new LevelingSim(new System.Random(1));
            foreach (ulong id in clients) sim.Register(id, CharacterIds.Reimu);
            return sim;
        }

        [Test]
        public void ADeviceGivesMoreThanAMob()
        {
            Assert.Greater(GrowthConfig.XpPerDevice, GrowthConfig.XpPerKill);
        }

        [Test]
        public void ThoseWithinTheShareDistanceGetTheDeviceAmountAndTheOthersNothing()
        {
            LevelingSim sim = New(1, 2, 3);
            sim.XpPerDevice = 20; // below the first level's need, so nobody levels up and the experience can be read

            int recipients = sim.AwardDeviceKill(Vector2.zero, new List<GrowthBody>
            {
                Body(1, 4, 0), Body(2, 6, 0), Body(3, GrowthConfig.XpShareDistance + 1f, 0),
            });

            Assert.AreEqual(2, recipients);
            Assert.AreEqual(20, sim.Xp(1));
            Assert.AreEqual(20, sim.Xp(2));
            Assert.AreEqual(0, sim.Xp(3));
        }

        [Test]
        public void AFallenSurvivorAndAnUnknownBodyGetNothing()
        {
            LevelingSim sim = New(1);

            int recipients = sim.AwardDeviceKill(Vector2.zero, new List<GrowthBody> { Body(1, 1, 0, alive: false), Body(99, 1, 0) });

            Assert.AreEqual(0, recipients);
            Assert.AreEqual(0, sim.Xp(1));
        }

        [Test]
        public void TheExperiencePassiveRaisesTheDeviceAmountToo()
        {
            var sim = new LevelingSim(new System.Random(1));
            sim.Register(1, CharacterIds.Sanae);
            sim.Register(2, CharacterIds.Reimu);
            sim.GrantRank(1, SkillIds.Merit, 3);
            sim.XpPerDevice = 20;

            sim.AwardDeviceKill(Vector2.zero, new List<GrowthBody> { Body(1, 2, 0), Body(2, 0, 3) });

            Assert.AreEqual(26, sim.Xp(1));
            Assert.AreEqual(20, sim.Xp(2));
        }

        [Test]
        public void AMobGivesTheOldAmountStill()
        {
            LevelingSim sim = New(1);

            sim.AwardKill(Vector2.zero, new List<GrowthBody> { Body(1, 1, 0) });

            Assert.AreEqual(GrowthConfig.XpPerKill, sim.Xp(1));
        }

        // ---- where the destruction is reported ----

        static MobSim NewSim()
        {
            var sim = new MobSim(targetCount: 0) { DeviceSettings = new DeviceConfig { LifetimeSeconds = 100f } };
            return sim;
        }

        static ushort Place(MobSim sim, float x, float y)
        {
            Assert.IsTrue(sim.TrySpawn(new Vector2(x, y), DeviceType, out ushort id));
            return id;
        }

        [Test]
        public void ADestroyedDeviceIsReportedOnItsOwnAndNotAsAMobDeath()
        {
            MobSim sim = NewSim();
            ushort device = Place(sim, 2f, 2f);
            sim.TryGetHealth(device, out int full);
            var deaths = new List<Vector2>();

            sim.ApplyAreaDamage(new Vector2(2f, 2f), 1f, full, deaths);

            Assert.AreEqual(0, deaths.Count);
            Assert.IsTrue(sim.WasDeviceKill(device));
            var devices = new List<Vector2>();
            sim.DrainDeviceKills(devices);
            Assert.AreEqual(1, devices.Count);
            Assert.AreEqual(new Vector2(2f, 2f), devices[0]);
        }

        [Test]
        public void AKilledMobAndADestroyedDeviceAreToldApart()
        {
            MobSim sim = NewSim();
            ushort device = Place(sim, 2f, 2f);
            Assert.IsTrue(sim.TrySpawn(new Vector2(2.5f, 2f), MobSim.TypeSummoned, out ushort mob));
            sim.TryGetHealth(device, out int full);
            var deaths = new List<Vector2>();

            sim.ApplyLineDamage(new Vector2(0f, 2f), Vector2.right, 6f, 1f, full, deaths);

            Assert.AreEqual(1, deaths.Count); // the mob
            Assert.IsTrue(sim.WasDeviceKill(device));
            Assert.IsFalse(sim.WasDeviceKill(mob));
        }

        [Test]
        public void AShotThatDestroysADeviceIsReportedAsADeviceKill()
        {
            MobSim sim = NewSim();
            ushort device = Place(sim, 3f, 0f);
            sim.TryGetHealth(device, out int full);
            var shots = new ProjectileSim();
            shots.TryFire(1UL, Vector2.zero, AimMath.FromDirection(Vector2.right), full, out _);

            for (int i = 0; i < 60; i++) shots.Step(0.02f, sim);
            var events = new List<CombatEvent>();
            shots.DrainEvents(events);

            CombatEvent hit = events.Find(e => e.Kind == CombatEventKind.Hit);
            Assert.IsTrue(hit.Killed);
            Assert.IsTrue(sim.WasDeviceKill(hit.MobId));
        }

        [Test]
        public void ExpiryAndClearingReportNothing()
        {
            MobSim sim = NewSim();
            sim.DeviceSettings.LifetimeSeconds = 0.3f;
            Place(sim, 2f, 2f);
            for (int i = 0; i < 30; i++) sim.Step(0.02f, new List<SurvivorInfo>());
            Assert.AreEqual(0, sim.DeviceCount);

            var devices = new List<Vector2>();
            sim.DrainDeviceKills(devices);
            Assert.AreEqual(0, devices.Count);

            ushort device = Place(sim, 2f, 2f);
            sim.ApplyDamage(device, 100000, out _, out _);
            sim.Clear();
            sim.DrainDeviceKills(devices);
            Assert.AreEqual(0, devices.Count);
            Assert.IsFalse(sim.WasDeviceKill(device));
        }

        [Test]
        public void TheReportIsTakenOnce()
        {
            MobSim sim = NewSim();
            ushort device = Place(sim, 2f, 2f);
            sim.ApplyDamage(device, 100000, out _, out _);

            var first = new List<Vector2>();
            var second = new List<Vector2>();
            sim.DrainDeviceKills(first);
            sim.DrainDeviceKills(second);

            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(0, second.Count);
        }

        [Test]
        public void AMobKillStillGoesToTheDeathList()
        {
            MobSim sim = NewSim();
            Assert.IsTrue(sim.TrySpawn(new Vector2(1f, 1f), MobSim.TypeSummoned, out _));
            var deaths = new List<Vector2>();

            sim.ApplyAreaDamage(new Vector2(1f, 1f), 1f, 1000, deaths);

            Assert.AreEqual(1, deaths.Count);
            var devices = new List<Vector2>();
            sim.DrainDeviceKills(devices);
            Assert.AreEqual(0, devices.Count);
        }
    }
}
