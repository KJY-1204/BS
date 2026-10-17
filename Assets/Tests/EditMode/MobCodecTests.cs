using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class MobCodecTests
    {
        static MobSim FilledSim(int count, int hardCap)
        {
            var sim = new MobSim(hardCap, targetCount: 0);
            for (int i = 0; i < count; i++)
            {
                float x = -15f + 30f * (i % 97) / 96f;
                float y = -9f + 18f * (i % 53) / 52f;
                Assert.IsTrue(sim.TrySpawn(new Vector2(x, y), (byte)(i % 3), out _));
            }
            return sim;
        }

        [Test]
        public void Quantize_RoundTripErrorIsWithinOneThousandth()
        {
            for (float x = -16f; x <= 16f; x += 0.0137f)
            {
                float back = MobCodec.Dequantize(MobCodec.Quantize(x));
                Assert.LessOrEqual(Mathf.Abs(back - x), 0.001f, $"x={x}");
            }
        }

        [Test]
        public void Quantize_ClampsOutOfRangeValues()
        {
            Assert.AreEqual(short.MaxValue, MobCodec.Quantize(1000f));
            Assert.AreEqual(short.MinValue, MobCodec.Quantize(-1000f));
        }

        [Test]
        public void Snapshot_RoundTripKeepsIdsAndPositions()
        {
            MobSim sim = FilledSim(10, 100);
            var buffer = new byte[MobCodec.MaxChunkBytes];

            int length = MobCodec.WriteSnapshotChunk(buffer, 77u, 0, 1, sim, 0, sim.Count);

            Assert.IsTrue(MobCodec.TryReadSnapshotHeader(buffer, length, out uint tick, out int index, out int chunks, out int count));
            Assert.AreEqual(77u, tick);
            Assert.AreEqual(0, index);
            Assert.AreEqual(1, chunks);
            Assert.AreEqual(10, count);

            for (int i = 0; i < count; i++)
            {
                MobCodec.ReadSnapshotEntry(buffer, i, out ushort id, out Vector2 position, out _);
                Assert.AreEqual(sim.IdAt(i), id);
                Assert.LessOrEqual(Vector2.Distance(sim.PositionAt(i), position), 0.0015f);
            }
        }

        [Test]
        public void Snapshot_FitsOneChunkAtTheHardCap()
        {
            MobSim sim = FilledSim(MobConfig.HardCap, MobConfig.HardCap);

            Assert.AreEqual(1, MobCodec.ChunkCountFor(sim.Count));
            Assert.LessOrEqual(MobCodec.SnapshotBytes(sim.Count), MobCodec.MaxChunkBytes);
        }

        [TestCase(120)]
        [TestCase(121)]
        [TestCase(500)]
        [TestCase(1000)]
        public void Snapshot_ChunksStayWithinTheSizeLimitAndCoverEveryMob(int mobs)
        {
            MobSim sim = FilledSim(mobs, mobs);
            int chunks = MobCodec.ChunkCountFor(mobs);
            var buffer = new byte[MobCodec.MaxChunkBytes];
            var seen = new HashSet<ushort>();

            for (int c = 0; c < chunks; c++)
            {
                int start = c * MobConfig.SnapshotChunkSize;
                int count = Mathf.Min(MobConfig.SnapshotChunkSize, mobs - start);
                int length = MobCodec.WriteSnapshotChunk(buffer, 1u, c, chunks, sim, start, count);

                Assert.LessOrEqual(length, MobCodec.MaxChunkBytes, $"chunk {c} too large");
                Assert.IsTrue(MobCodec.TryReadSnapshotHeader(buffer, length, out _, out int index, out int total, out int n));
                Assert.AreEqual(c, index);
                Assert.AreEqual(chunks, total);
                for (int i = 0; i < n; i++)
                {
                    MobCodec.ReadSnapshotEntry(buffer, i, out ushort id, out _, out _);
                    Assert.IsTrue(seen.Add(id), $"id {id} sent twice");
                }
            }

            Assert.AreEqual(mobs, seen.Count);
        }

        [Test]
        public void Snapshot_TruncatedBufferIsRejected()
        {
            MobSim sim = FilledSim(5, 100);
            var buffer = new byte[MobCodec.MaxChunkBytes];
            int length = MobCodec.WriteSnapshotChunk(buffer, 1u, 0, 1, sim, 0, 5);

            Assert.IsFalse(MobCodec.TryReadSnapshotHeader(buffer, length - 1, out _, out _, out _, out _));
            Assert.IsFalse(MobCodec.TryReadSnapshotHeader(buffer, 3, out _, out _, out _, out _));
        }

        [Test]
        public void Events_RoundTripSpawnAndRemove()
        {
            var events = new List<MobEvent>
            {
                new MobEvent { IsSpawn = true, Id = 500, Type = 2, Position = new Vector2(3.25f, -4.5f) },
                new MobEvent { IsSpawn = false, Id = 7 },
            };
            var buffer = new byte[256];

            int length = MobCodec.WriteEvents(buffer, 9u, events, 0, events.Count);

            Assert.IsTrue(MobCodec.TryReadEventsHeader(buffer, length, out uint tick, out int count));
            Assert.AreEqual(9u, tick);
            Assert.AreEqual(2, count);

            MobEvent spawn = MobCodec.ReadEvent(buffer, 0);
            Assert.IsTrue(spawn.IsSpawn);
            Assert.AreEqual(500, spawn.Id);
            Assert.AreEqual(2, spawn.Type);
            Assert.LessOrEqual(Vector2.Distance(spawn.Position, new Vector2(3.25f, -4.5f)), 0.0015f);

            MobEvent remove = MobCodec.ReadEvent(buffer, 1);
            Assert.IsFalse(remove.IsSpawn);
            Assert.AreEqual(7, remove.Id);
        }

        [Test]
        public void FullState_ListsEveryMobAsASpawn()
        {
            MobSim sim = FilledSim(MobConfig.HardCap, MobConfig.HardCap);
            var buffer = new byte[MobCodec.EventBytes(MobConfig.HardCap)];

            int length = MobCodec.WriteFullState(buffer, 3u, sim);

            Assert.IsTrue(MobCodec.TryReadEventsHeader(buffer, length, out _, out int count));
            Assert.AreEqual(sim.Count, count);
            for (int i = 0; i < count; i++)
            {
                MobEvent e = MobCodec.ReadEvent(buffer, i);
                Assert.IsTrue(e.IsSpawn);
                Assert.AreEqual(sim.IdAt(i), e.Id);
                Assert.AreEqual(sim.TypeAt(i), e.Type);
            }
        }

        [Test]
        public void ServerTime_EventsAndSnapshotsShareOneTimeline()
        {
            const float interval = 1f / MobConfig.SnapshotHz;

            // At a tick boundary the event time equals the snapshot time.
            Assert.AreEqual(MobMessages.ServerTime(15) * 1000f, MobMessages.ServerTimeMs(15, 0f), 1f);
            Assert.AreEqual(1000u, MobMessages.ServerTimeMs(15, 0f));

            // In between it moves forward, and it never runs ahead of the next tick.
            uint mid = MobMessages.ServerTimeMs(15, interval * 0.5f);
            Assert.Greater(mid, 1000u);
            Assert.LessOrEqual(MobMessages.ServerTimeMs(15, interval * 0.999f), MobMessages.ServerTimeMs(16, 0f) + 1u);

            // Seconds conversion used by the client.
            Assert.AreEqual(1.5f, MobMessages.ServerTimeFromMs(1500), 1e-4f);
        }

        [Test]
        public void MobTraffic_AtTheHardCapIsFarBelowTheBudget()
        {
            // Payload only: one chunk per snapshot at the configured rate.
            float bytesPerSecond = MobCodec.SnapshotBytes(MobConfig.HardCap) * MobConfig.SnapshotHz;
            Assert.Less(bytesPerSecond, 50f * 1024f);
        }
    }
}
