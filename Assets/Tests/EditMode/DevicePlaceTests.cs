using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class DevicePlaceTests
    {
        static MobSim NewMobs(int hardCap = 100) =>
            new MobSim(hardCap: hardCap, targetCount: 0)
            {
                DeviceSettings = new DeviceConfig { EnergyCost = 30f, PlaceCooldownSeconds = 3f, MaxDevices = 2, PlaceSafeDistance = 3f },
            };

        static SummonContext Ctx(MobSim mobs, Vector2? boss = null, bool standing = true, bool running = true, List<SurvivorInfo> survivors = null) =>
            new SummonContext
            {
                Mobs = mobs, BossPosition = boss ?? new Vector2(1f, 2f), BossStanding = standing, MatchRunning = running,
                Survivors = survivors ?? new List<SurvivorInfo>(),
            };

        static List<SurvivorInfo> SurvivorAt(float x, float y, bool alive = true) =>
            new List<SurvivorInfo> { new SurvivorInfo { ClientId = 1, Position = new Vector2(x, y), Alive = alive } };

        static SummonSim NewSim(float energy = 100f)
        {
            var sim = new SummonSim(new SummonConfig { StartEnergy = energy, MaxEnergy = 100f });
            return sim;
        }

        [Test]
        public void APlacementCreatesADeviceAtTheBossAndTakesEnergyAndStartsTheCooldown()
        {
            var sim = NewSim(100f);
            var mobs = NewMobs();

            SummonResult result = sim.PlaceDevice(Ctx(mobs), true);

            Assert.AreEqual(SummonResult.Created, result);
            Assert.AreEqual(1, mobs.DeviceCount);
            for (int i = 0; i < mobs.Count; i++)
                if (mobs.IsDevice(i)) Assert.AreEqual(new Vector2(1f, 2f), mobs.PositionAt(i));
            Assert.AreEqual(70f, sim.Energy, 1e-3f);
            Assert.AreEqual(3f, sim.PlaceCooldownLeft, 1e-3f);
        }

        [Test]
        public void ASurvivorCannotPlaceOne()
        {
            var sim = NewSim();
            var mobs = NewMobs();

            Assert.AreEqual(SummonResult.Ignored, sim.PlaceDevice(Ctx(mobs), false));

            Assert.AreEqual(0, mobs.DeviceCount);
            Assert.AreEqual(100f, sim.Energy, 1e-3f);
            Assert.AreEqual(0f, sim.PlaceCooldownLeft);
        }

        [Test]
        public void NotEnoughEnergyRefusesAndChangesNothing()
        {
            var sim = NewSim(20f);
            var mobs = NewMobs();

            Assert.AreEqual(SummonResult.NoEnergy, sim.PlaceDevice(Ctx(mobs), true));

            Assert.AreEqual(0, mobs.DeviceCount);
            Assert.AreEqual(20f, sim.Energy, 1e-3f);
            Assert.AreEqual(0f, sim.PlaceCooldownLeft);
        }

        [Test]
        public void ALivingSurvivorTooCloseRefusesButAFallenOrFarOneDoesNot()
        {
            var sim = NewSim();
            var mobs = NewMobs();

            Assert.AreEqual(SummonResult.TooClose, sim.PlaceDevice(Ctx(mobs, survivors: SurvivorAt(2f, 2f)), true));
            Assert.AreEqual(0, mobs.DeviceCount);
            Assert.AreEqual(100f, sim.Energy, 1e-3f);
            Assert.AreEqual(0f, sim.PlaceCooldownLeft);

            Assert.AreEqual(SummonResult.Created, sim.PlaceDevice(Ctx(mobs, survivors: SurvivorAt(2f, 2f, alive: false)), true));
            sim.Step(10f, true);
            Assert.AreEqual(SummonResult.Created, sim.PlaceDevice(Ctx(mobs, survivors: SurvivorAt(9f, 2f)), true));
        }

        [Test]
        public void TheMatchNotRunningOrTheBossFallenRefuses()
        {
            var sim = NewSim();
            var mobs = NewMobs();

            Assert.AreEqual(SummonResult.Unavailable, sim.PlaceDevice(Ctx(mobs, running: false), true));
            Assert.AreEqual(SummonResult.Unavailable, sim.PlaceDevice(Ctx(mobs, standing: false), true));
            Assert.AreEqual(0, mobs.DeviceCount);
            Assert.AreEqual(100f, sim.Energy, 1e-3f);
        }

        [Test]
        public void TheCooldownRefusesAndRunsDownWithTheMatch()
        {
            var sim = NewSim();
            var mobs = NewMobs();
            sim.PlaceDevice(Ctx(mobs), true);
            float energy = sim.Energy;

            Assert.AreEqual(SummonResult.DeviceCooldown, sim.PlaceDevice(Ctx(mobs), true));
            Assert.AreEqual(1, mobs.DeviceCount);
            Assert.AreEqual(energy, sim.Energy, 1e-3f);

            sim.Step(2f, false);
            Assert.AreEqual(3f, sim.PlaceCooldownLeft, 1e-3f); // the cooldown runs only while the match runs

            sim.Step(3.1f, true);
            Assert.AreEqual(0f, sim.PlaceCooldownLeft);
            Assert.AreEqual(SummonResult.Created, sim.PlaceDevice(Ctx(mobs), true));
            Assert.AreEqual(2, mobs.DeviceCount);
        }

        [Test]
        public void TheLimitRefusesAndAFreedSlotCanBeUsedAgain()
        {
            var sim = NewSim();
            var mobs = NewMobs();
            sim.PlaceDevice(Ctx(mobs), true);
            sim.Step(4f, true);
            sim.PlaceDevice(Ctx(mobs), true);
            sim.Step(4f, true);
            float energy = sim.Energy;

            Assert.AreEqual(SummonResult.DeviceLimit, sim.PlaceDevice(Ctx(mobs), true));
            Assert.AreEqual(2, mobs.DeviceCount);
            Assert.AreEqual(energy, sim.Energy, 1e-3f);

            // One is destroyed, another expires: both slots are free again.
            ushort first = 0;
            for (int i = 0; i < mobs.Count; i++) if (mobs.IsDevice(i)) { first = mobs.IdAt(i); break; }
            mobs.ApplyDamage(first, 100000, out _, out _);
            Assert.AreEqual(SummonResult.Created, sim.PlaceDevice(Ctx(mobs), true));
            Assert.AreEqual(2, mobs.DeviceCount);
        }

        [Test]
        public void AFullFieldRefusesWithoutTakingEnergy()
        {
            var sim = NewSim();
            var mobs = NewMobs(hardCap: 3);
            for (int i = 0; i < 3; i++) mobs.TrySpawn(new Vector2(8f + i, 5f), MobSim.TypeSummoned, out _);

            Assert.AreEqual(SummonResult.FieldFull, sim.PlaceDevice(Ctx(mobs), true));

            Assert.AreEqual(0, mobs.DeviceCount);
            Assert.AreEqual(100f, sim.Energy, 1e-3f);
            Assert.AreEqual(0f, sim.PlaceCooldownLeft);
        }

        [Test]
        public void ADeviceCountsAsOneTowardTheHardCap()
        {
            var sim = NewSim();
            var mobs = NewMobs(hardCap: 2);
            sim.PlaceDevice(Ctx(mobs), true);
            mobs.TrySpawn(new Vector2(8f, 5f), MobSim.TypeSummoned, out _);

            Assert.AreEqual(2, mobs.Count);
            Assert.IsFalse(mobs.TrySpawn(new Vector2(9f, 5f), MobSim.TypeSummoned, out _));
        }

        [Test]
        public void SummoningAndPlacingShareEnergyButNotTheCooldown()
        {
            var sim = NewSim(100f);
            var mobs = NewMobs();
            Assert.AreEqual(SummonResult.Created, sim.Summon(Ctx(mobs), true, MobKinds.Basic, out int created));
            Assert.Greater(created, 0);
            float afterSummon = sim.Energy;
            Assert.Greater(sim.CooldownLeft, 0f);

            Assert.AreEqual(SummonResult.Created, sim.PlaceDevice(Ctx(mobs), true));
            Assert.AreEqual(afterSummon - 30f, sim.Energy, 1e-3f);

            // And the other way round: the placing cooldown does not stop a summon.
            sim.Step(1f, true);
            Assert.Greater(sim.PlaceCooldownLeft, 0f);
            Assert.AreEqual(SummonResult.Created, sim.Summon(Ctx(mobs), true, MobKinds.Basic, out _));
        }

        [Test]
        public void ANewMatchStartsReadyWithNoCooldown()
        {
            var sim = NewSim();
            var mobs = NewMobs();
            sim.PlaceDevice(Ctx(mobs), true);

            sim.Reset();
            mobs.Clear();

            Assert.AreEqual(0f, sim.PlaceCooldownLeft);
            Assert.AreEqual(0, mobs.DeviceCount);
            Assert.AreEqual(SummonResult.Created, sim.PlaceDevice(Ctx(mobs), true));
        }

        [Test]
        public void APlacedDeviceMakesMobsWithoutEnergy()
        {
            var sim = NewSim();
            var mobs = NewMobs();
            mobs.DeviceSettings.ProductionIntervalSeconds = 1f;
            mobs.DeviceSettings.MobsPerBatch = 2;
            sim.PlaceDevice(Ctx(mobs), true);
            float energy = sim.Energy;

            var far = SurvivorAt(-12f, -8f);
            for (int i = 0; i < 55; i++) mobs.Step(0.02f, far);

            Assert.AreEqual(2, mobs.SummonedCount);
            Assert.AreEqual(energy, sim.Energy, 1e-3f); // no Step on the summon, so no refill, and nothing taken
        }
    }
}
