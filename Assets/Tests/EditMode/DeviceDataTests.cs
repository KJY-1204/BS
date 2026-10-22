using NUnit.Framework;

namespace BS.Tests
{
    public class DeviceDataTests
    {
        [Test]
        public void TheDeviceIsNoMobKindTheBossCanPick()
        {
            Assert.AreEqual(5, MobKinds.Count);
            Assert.AreEqual(MobKinds.Count, MobKinds.Device);
            for (byte k = 0; k < MobKinds.Count; k++) Assert.IsTrue(MobKinds.IsSummonable(k));
            Assert.IsFalse(MobKinds.IsSummonable(MobKinds.Device));
            Assert.IsTrue(MobKinds.IsValid(MobKinds.Device));
            Assert.IsTrue(MobKinds.IsDevice(MobKinds.Device));
        }

        [Test]
        public void TheDeviceLooksDifferentFromEveryMobKind()
        {
            MobKindDef device = MobKinds.Get(MobKinds.Device);
            Assert.IsFalse(string.IsNullOrEmpty(device.Name));
            for (byte k = 0; k < MobKinds.Count; k++)
            {
                Assert.AreNotEqual(MobKinds.Get(k).Name, device.Name);
                Assert.AreNotEqual(MobKinds.Get(k).Color, device.Color, MobKinds.Get(k).Name);
            }
            Assert.AreNotEqual(MobKinds.SummonedBasicColor, device.Color);
        }

        [Test]
        public void TheDeviceStandsStillAndHurtsNobodyByTouch()
        {
            MobKindDef device = MobKinds.Get(MobKinds.Device);
            Assert.AreEqual(MobBehavior.Device, device.Behavior);
            Assert.AreEqual(0f, device.SpeedMultiple);
            Assert.AreEqual(0f, device.ContactDamageMultiple);
            Assert.Greater(device.HealthMultiple, MobKinds.Get(MobKinds.Basic).HealthMultiple);
        }

        [Test]
        public void TheDeviceBodyFitsTheGrid()
        {
            MobSim sim = new MobSim(targetCount: 0);
            Assert.LessOrEqual(sim.RadiusOf(MobKinds.Device), MobKinds.MaxRadius + 1e-6f);
        }

        [Test]
        public void ASummonOfTheDeviceKindIsRefusedLikeAnUnknownKind()
        {
            var summon = new SummonSim();
            var mobs = new MobSim(targetCount: 0);
            var ctx = new SummonContext
            {
                Mobs = mobs, BossPosition = UnityEngine.Vector2.zero, BossStanding = true, MatchRunning = true,
                Survivors = new System.Collections.Generic.List<SurvivorInfo>(),
            };
            float energy = summon.Energy;
            SummonResult result = summon.Summon(ctx, true, MobKinds.Device, out int created);
            Assert.AreEqual(SummonResult.UnknownKind, result);
            Assert.AreEqual(0, created);
            Assert.AreEqual(energy, summon.Energy);
            Assert.AreEqual(0, mobs.Count);
        }

        [Test]
        public void TheDeviceValuesAreOneAdjustableSet()
        {
            DeviceConfig c = DeviceConfig.Default;
            Assert.Greater(c.EnergyCost, 0f);
            Assert.Greater(c.PlaceCooldownSeconds, 0f);
            Assert.Greater(c.MaxDevices, 0);
            Assert.Greater(c.LifetimeSeconds, c.ProductionIntervalSeconds);
            Assert.Greater(c.MobsPerBatch, 0);
            Assert.Greater(c.RingMax, c.RingMin);
        }
    }
}
