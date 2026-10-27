using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>
    /// Wire format for mob snapshots and events (little endian). Plain byte arrays so it can be tested
    /// without a network. Coordinates are 16-bit integers in 1/1000 world unit.
    /// </summary>
    public static class MobCodec
    {
        public const float Scale = 1000f;

        // Snapshot chunk: tick u32, chunkIndex u8, chunkCount u8, count u16, then count x { id u16, x i16, y i16, flags u8 }
        public const int SnapshotHeaderSize = 8;
        public const int SnapshotEntrySize = 7;

        // Event batch: server time in milliseconds u32, count u16, then count x { kind u8 (spawn, remove, device ready, device produce), id u16, type u8, x i16, y i16 }
        public const int EventHeaderSize = 6;
        public const int EventEntrySize = 8;
        public const byte KindSpawn = 1;
        public const byte KindRemove = 2;
        public const byte KindDeviceReady = 3;
        public const byte KindDeviceProduce = 4;

        /// <summary>No single snapshot message may be larger than this (well under the transport packet size).</summary>
        public const int MaxChunkBytes = 1000;

        public static short Quantize(float value)
        {
            int q = Mathf.RoundToInt(value * Scale);
            return (short)Mathf.Clamp(q, short.MinValue, short.MaxValue);
        }

        public static float Dequantize(short q) => q / Scale;

        public static int ChunkCountFor(int mobCount, int chunkSize = MobConfig.SnapshotChunkSize)
            => mobCount <= 0 ? 0 : (mobCount + chunkSize - 1) / chunkSize;

        public static int SnapshotBytes(int entries) => SnapshotHeaderSize + entries * SnapshotEntrySize;

        public static int EventBytes(int entries) => EventHeaderSize + entries * EventEntrySize;

        // ---- Snapshot ----

        public static int WriteSnapshotChunk(byte[] buffer, uint tick, int chunkIndex, int chunkCount,
                                             IMobSource source, int start, int count)
        {
            int o = 0;
            WriteU32(buffer, ref o, tick);
            buffer[o++] = (byte)chunkIndex;
            buffer[o++] = (byte)chunkCount;
            WriteU16(buffer, ref o, (ushort)count);

            for (int i = 0; i < count; i++)
            {
                Vector2 p = source.PositionAt(start + i);
                WriteU16(buffer, ref o, source.IdAt(start + i));
                WriteI16(buffer, ref o, Quantize(p.x));
                WriteI16(buffer, ref o, Quantize(p.y));
                buffer[o++] = 0; // flags, reserved
            }
            return o;
        }

        public static bool TryReadSnapshotHeader(byte[] buffer, int length, out uint tick, out int chunkIndex,
                                                 out int chunkCount, out int count)
        {
            tick = 0; chunkIndex = 0; chunkCount = 0; count = 0;
            if (length < SnapshotHeaderSize) return false;

            int o = 0;
            tick = ReadU32(buffer, ref o);
            chunkIndex = buffer[o++];
            chunkCount = buffer[o++];
            count = ReadU16(buffer, ref o);
            return length >= SnapshotBytes(count);
        }

        public static void ReadSnapshotEntry(byte[] buffer, int entryIndex, out ushort id, out Vector2 position, out byte flags)
        {
            int o = SnapshotHeaderSize + entryIndex * SnapshotEntrySize;
            id = ReadU16(buffer, ref o);
            float x = Dequantize(ReadI16(buffer, ref o));
            float y = Dequantize(ReadI16(buffer, ref o));
            position = new Vector2(x, y);
            flags = buffer[o];
        }

        // ---- Events ----

        public static int WriteEvents(byte[] buffer, uint timeMs, IReadOnlyList<MobEvent> events, int start, int count)
        {
            int o = 0;
            WriteU32(buffer, ref o, timeMs);
            WriteU16(buffer, ref o, (ushort)count);

            for (int i = 0; i < count; i++)
            {
                MobEvent e = events[start + i];
                buffer[o++] = e.Device == MobEvent.DeviceReady ? KindDeviceReady : e.Device == MobEvent.DeviceProduce ? KindDeviceProduce : e.IsSpawn ? KindSpawn : KindRemove;
                WriteU16(buffer, ref o, e.Id);
                buffer[o++] = e.Type;
                WriteI16(buffer, ref o, Quantize(e.Position.x));
                WriteI16(buffer, ref o, Quantize(e.Position.y));
            }
            return o;
        }

        /// <summary>Every mob as a spawn event; sent to a player who joins a room in progress.</summary>
        public static int WriteFullState(byte[] buffer, uint timeMs, IMobSource source)
        {
            int count = source.Count;
            int o = 0;
            WriteU32(buffer, ref o, timeMs);
            WriteU16(buffer, ref o, (ushort)count);

            for (int i = 0; i < count; i++)
            {
                Vector2 p = source.PositionAt(i);
                buffer[o++] = KindSpawn;
                WriteU16(buffer, ref o, source.IdAt(i));
                buffer[o++] = source.TypeAt(i);
                WriteI16(buffer, ref o, Quantize(p.x));
                WriteI16(buffer, ref o, Quantize(p.y));
            }
            return o;
        }

        public static bool TryReadEventsHeader(byte[] buffer, int length, out uint timeMs, out int count)
        {
            timeMs = 0; count = 0;
            if (length < EventHeaderSize) return false;

            int o = 0;
            timeMs = ReadU32(buffer, ref o);
            count = ReadU16(buffer, ref o);
            return length >= EventBytes(count);
        }

        public static MobEvent ReadEvent(byte[] buffer, int entryIndex)
        {
            int o = EventHeaderSize + entryIndex * EventEntrySize;
            byte kind = buffer[o++];
            ushort id = ReadU16(buffer, ref o);
            byte type = buffer[o++];
            float x = Dequantize(ReadI16(buffer, ref o));
            float y = Dequantize(ReadI16(buffer, ref o));
            byte device = kind == KindDeviceReady ? MobEvent.DeviceReady : kind == KindDeviceProduce ? MobEvent.DeviceProduce : (byte)0;
            return new MobEvent { IsSpawn = kind == KindSpawn, Device = device, Id = id, Type = type, Position = new Vector2(x, y) };
        }

        // ---- Little endian helpers ----

        static void WriteU16(byte[] b, ref int o, ushort v) { b[o++] = (byte)v; b[o++] = (byte)(v >> 8); }
        static void WriteI16(byte[] b, ref int o, short v) => WriteU16(b, ref o, (ushort)v);
        static void WriteU32(byte[] b, ref int o, uint v)
        {
            b[o++] = (byte)v; b[o++] = (byte)(v >> 8); b[o++] = (byte)(v >> 16); b[o++] = (byte)(v >> 24);
        }

        static ushort ReadU16(byte[] b, ref int o) { ushort v = (ushort)(b[o] | (b[o + 1] << 8)); o += 2; return v; }
        static short ReadI16(byte[] b, ref int o) => (short)ReadU16(b, ref o);
        static uint ReadU32(byte[] b, ref int o)
        {
            uint v = (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
            o += 4;
            return v;
        }
    }
}
