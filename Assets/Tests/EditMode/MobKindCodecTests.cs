using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class MobKindCodecTests
    {
        static readonly byte[] Types =
        {
            MobKinds.Encode(MobKinds.OriginAuto, MobKinds.Basic),
            MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Basic),
            MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Fast),
            MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Tough),
        };

        [Test]
        public void SpawnEventsCarryTheKindAndOriginInTheSameSize()
        {
            var events = new List<MobEvent>();
            for (int i = 0; i < Types.Length; i++)
                events.Add(new MobEvent { IsSpawn = true, Id = (ushort)(i + 1), Type = Types[i], Position = new Vector2(i, -i) });
            var buffer = new byte[MobCodec.MaxChunkBytes];

            int length = MobCodec.WriteEvents(buffer, 5000, events, 0, events.Count);

            Assert.AreEqual(MobCodec.EventBytes(events.Count), length, "the entry size is the same as before kinds existed");
            Assert.IsTrue(MobCodec.TryReadEventsHeader(buffer, length, out _, out int count));
            Assert.AreEqual(Types.Length, count);
            for (int i = 0; i < count; i++)
            {
                MobEvent read = MobCodec.ReadEvent(buffer, i);
                Assert.AreEqual(Types[i], read.Type);
                Assert.AreEqual(MobKinds.KindOf(Types[i]), MobKinds.KindOf(read.Type));
                Assert.AreEqual(MobKinds.IsSummoned(Types[i]), MobKinds.IsSummoned(read.Type));
            }
        }

        [Test]
        public void TheFullStateForALateJoinerCarriesEveryMobsKind()
        {
            var sim = new MobSim(targetCount: 0);
            foreach (byte type in Types) Assert.IsTrue(sim.TrySpawn(new Vector2(type, 3f), type, out _));
            var buffer = new byte[MobCodec.MaxChunkBytes];

            int length = MobCodec.WriteFullState(buffer, 1000, sim);

            Assert.IsTrue(MobCodec.TryReadEventsHeader(buffer, length, out _, out int count));
            Assert.AreEqual(Types.Length, count);
            for (int i = 0; i < count; i++)
            {
                MobEvent read = MobCodec.ReadEvent(buffer, i);
                Assert.IsTrue(read.IsSpawn);
                Assert.AreEqual(sim.TypeAt(i), read.Type);
            }
        }

        [Test]
        public void ASnapshotEntryDoesNotChangeSize()
        {
            Assert.AreEqual(7, MobCodec.SnapshotEntrySize);
            Assert.AreEqual(8, MobCodec.EventEntrySize);
        }

        [Test]
        public void TheClientLearnsTheTypeFromTheSpawnAndDrawsItWithoutASnapshotType()
        {
            var interpolator = new MobInterpolator();
            byte tough = MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Tough);

            interpolator.OnSpawn(7, tough, new Vector2(1f, 1f), 1f);
            interpolator.OnSnapshotEntry(7, new Vector2(1.1f, 1f), 1.07f);

            Assert.AreEqual(1, interpolator.Count);
            Assert.AreEqual(tough, interpolator.TypeAt(0));
            Assert.AreEqual(MobKinds.Tough, MobKinds.KindOf(interpolator.TypeAt(0)));
        }

        [Test]
        public void AMobSeenFirstInASnapshotIsShownAsBasicUntilItsSpawnArrives()
        {
            var interpolator = new MobInterpolator();
            byte fast = MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Fast);

            interpolator.OnSnapshotEntry(9, new Vector2(0f, 0f), 1f);
            Assert.AreEqual(MobKinds.Basic, MobKinds.KindOf(interpolator.TypeAt(0)));

            interpolator.OnSpawn(9, fast, new Vector2(0f, 0f), 1f);
            Assert.AreEqual(fast, interpolator.TypeAt(0));
        }
    }
}
