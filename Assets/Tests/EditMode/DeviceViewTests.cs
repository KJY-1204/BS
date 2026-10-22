using System.Collections.Generic;
using NUnit.Framework;

namespace BS.Tests
{
    public class DeviceViewTests
    {
        [Test]
        public void ABarShowsFullUntilAHitTellsTheHealth()
        {
            var known = new Dictionary<ushort, byte>();
            Assert.AreEqual(100, DeviceBar.Percent(known, 7));

            known[7] = 40;
            Assert.AreEqual(40, DeviceBar.Percent(known, 7));
            Assert.AreEqual(100, DeviceBar.Percent(known, 8));
            Assert.AreEqual(100, DeviceBar.Percent(null, 7));
        }

        [Test]
        public void TheDeviceBodyIsADifferentSizeFromEveryMobKind()
        {
            var sim = new MobSim(targetCount: 0);
            float device = sim.RadiusOf(MobKinds.Device);
            for (byte k = 0; k < MobKinds.Count; k++)
                Assert.AreNotEqual(sim.RadiusOf(k), device, MobKinds.Get(k).Name);
        }

        [Test]
        public void TheBossLineNamesTheKeyTheCountTheCostAndTheState()
        {
            Assert.AreEqual("R 장치 1/3 (30) 준비", DeviceBar.HudText(1, 3, 0f, 30f));
            Assert.AreEqual("R 장치 1/3 (30) 2.5초", DeviceBar.HudText(1, 3, 2.5f, 30f));
            Assert.AreEqual("R 장치 3/3 (30) 가득 참", DeviceBar.HudText(3, 3, 0f, 30f));
            Assert.AreEqual("R 장치 0/3 (30) 준비", DeviceBar.HudText(0, 3, 0f, 30f));
        }
    }
}
