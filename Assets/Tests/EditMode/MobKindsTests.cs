using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class MobKindsTests
    {
        [Test]
        public void TheBasicKindHasEveryMultipleAtOne()
        {
            MobKindDef basic = MobKinds.Get(MobKinds.Basic);
            Assert.AreEqual(1f, basic.SpeedMultiple);
            Assert.AreEqual(1f, basic.HealthMultiple);
            Assert.AreEqual(1f, basic.RadiusMultiple);
            Assert.AreEqual(1f, basic.ContactDamageMultiple);
            Assert.AreEqual(1f, basic.CostMultiple);
            Assert.AreEqual(1f, basic.GroupMultiple);
        }

        [Test]
        public void ThereAreFiveKindsWithNames()
        {
            Assert.AreEqual(5, MobKinds.Count);
            for (byte k = 0; k < MobKinds.Count; k++)
            {
                Assert.IsTrue(MobKinds.IsValid(k));
                Assert.IsFalse(string.IsNullOrEmpty(MobKinds.Get(k).Name));
            }
            Assert.IsFalse(MobKinds.IsValid(MobKinds.Count));
            Assert.IsFalse(MobKinds.IsValid(200));
        }

        [Test]
        public void TheFastKindIsQuickerAndWeakerAndSmaller()
        {
            MobKindDef fast = MobKinds.Get(MobKinds.Fast);
            Assert.Greater(fast.SpeedMultiple, 1f);
            Assert.Less(fast.HealthMultiple, 1f);
            Assert.Less(fast.RadiusMultiple, 1f);
        }

        [Test]
        public void TheToughKindIsSlowerAndStrongerAndBiggerAndHitsHarder()
        {
            MobKindDef tough = MobKinds.Get(MobKinds.Tough);
            Assert.Less(tough.SpeedMultiple, 1f);
            Assert.Greater(tough.HealthMultiple, 1f);
            Assert.Greater(tough.RadiusMultiple, 1f);
            Assert.Greater(tough.ContactDamageMultiple, 1f);
        }

        [Test]
        public void NoBodyIsBiggerThanTheGridAllows()
        {
            MobSim sim = new MobSim(targetCount: 0);
            for (byte k = 0; k < MobKinds.Count; k++)
                Assert.LessOrEqual(sim.RadiusOf(k), MobKinds.MaxRadius + 1e-6f, MobKinds.Get(k).Name);
            Assert.AreEqual(MobKinds.Get(MobKinds.Tough).RadiusMultiple, MobKinds.LargestRadiusMultiple, 1e-6f);
        }

        [Test]
        public void AnUnknownKindIsTreatedAsBasic()
        {
            Assert.AreSame(MobKinds.Get(MobKinds.Basic), MobKinds.Get(99));
        }

        [Test]
        public void TheBasicTypesAreTheOldValues()
        {
            Assert.AreEqual(MobSim.TypeAuto, MobKinds.Encode(MobKinds.OriginAuto, MobKinds.Basic));
            Assert.AreEqual(MobSim.TypeSummoned, MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Basic));
            Assert.AreEqual(0, MobSim.TypeAuto);
            Assert.AreEqual(1, MobSim.TypeSummoned);
        }

        [Test]
        public void EveryKindAndOriginSurvivesTheByte()
        {
            for (byte kind = 0; kind < MobKinds.Count; kind++)
            foreach (byte origin in new byte[] { MobKinds.OriginAuto, MobKinds.OriginSummoned })
            {
                byte type = MobKinds.Encode(origin, kind);
                Assert.AreEqual(kind, MobKinds.KindOf(type));
                Assert.AreEqual(origin, MobKinds.OriginOf(type));
                Assert.AreEqual(origin == MobKinds.OriginSummoned, MobKinds.IsSummoned(type));
            }
        }

        [Test]
        public void DifferentKindsAndOriginsGiveDifferentBytes()
        {
            var seen = new HashSet<byte>();
            for (byte kind = 0; kind < MobKinds.Count; kind++)
            {
                Assert.IsTrue(seen.Add(MobKinds.Encode(MobKinds.OriginAuto, kind)));
                Assert.IsTrue(seen.Add(MobKinds.Encode(MobKinds.OriginSummoned, kind)));
            }
        }

        [Test]
        public void TheColorsTellTheKindsApart()
        {
            Color autoBasic = MobKinds.ColorOf(MobKinds.Encode(MobKinds.OriginAuto, MobKinds.Basic));
            Color summonedBasic = MobKinds.ColorOf(MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Basic));
            Color fast = MobKinds.ColorOf(MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Fast));
            Color tough = MobKinds.ColorOf(MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Tough));

            Assert.AreNotEqual(autoBasic, summonedBasic, "the basic kind keeps its origin colors");
            Assert.AreNotEqual(fast, tough);
            Assert.AreNotEqual(fast, summonedBasic);
            Assert.AreNotEqual(tough, summonedBasic);
            Assert.AreNotEqual(fast, autoBasic);
            Assert.AreNotEqual(tough, autoBasic);
        }

        [Test]
        public void AKindKeepsItsColorWhateverTheOrigin()
        {
            Assert.AreEqual(MobKinds.ColorOf(MobKinds.Encode(MobKinds.OriginAuto, MobKinds.Tough)),
                            MobKinds.ColorOf(MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Tough)));
        }
    }
}
