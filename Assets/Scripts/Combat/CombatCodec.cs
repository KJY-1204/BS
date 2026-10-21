using System;
using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>Name of the custom network message that carries combat events.</summary>
    public static class CombatMessages
    {
        public const string Events = "bs.atk.evt";
    }

    /// <summary>
    /// Wire format for combat events (little endian). A batch is the events of one server step:
    /// server time in milliseconds u32, count u16, then the events one after another.
    /// Fire: kind u8, projectile id u16, slot u8, x i16, y i16, angle u16.
    /// Hit: kind u8, projectile id u16, mob id u16, mob health percent u8, flags u8 (bit 0 killed, bit 1 the
    /// projectile keeps flying, bit 2 the target is a survivor and the id is its slot), x i16, y i16.
    /// End: kind u8, projectile id u16, x i16, y i16.
    /// Skill: kind u8, skill id u8, slot u8, x i16, y i16, rank u8, aim angle u16, range scale u8, duration scale u8 (percent, 100 = as defined).
    /// Coordinates are 1/1000 world unit.
    /// Explosion: kind u8, x i16, y i16, radius u8 (tenths of a world unit).
    /// </summary>
    public static class CombatCodec
    {
        public const int HeaderSize = 6;
        public const int FireSize = 10;
        public const int HitSize = 11;
        public const int EndSize = 7;
        public const int SkillSize = 12;
        public const int ExplosionSize = 6;

        /// <summary>The blast radius as it is sent: tenths of a unit, at most 25.5.</summary>
        public static byte QuantizeRadius(float radius) => (byte)Mathf.Clamp(Mathf.RoundToInt(radius * 10f), 0, 255);

        /// <summary>No single combat message may be larger than this.</summary>
        public const int MaxBatchBytes = 1000;

        public static int EventSize(CombatEventKind kind)
        {
            switch (kind)
            {
                case CombatEventKind.Fire: return FireSize;
                case CombatEventKind.Hit: return HitSize;
                case CombatEventKind.Skill: return SkillSize;
                case CombatEventKind.Explosion: return ExplosionSize;
                default: return EndSize;
            }
        }

        /// <summary>How many events from <paramref name="start"/> fit in one message.</summary>
        public static int CountThatFit(IReadOnlyList<CombatEvent> events, int start)
        {
            int size = HeaderSize;
            int count = 0;
            for (int i = start; i < events.Count; i++)
            {
                size += EventSize(events[i].Kind);
                if (size > MaxBatchBytes) break;
                count++;
            }
            return count;
        }

        public static int WriteBatch(byte[] buffer, uint timeMs, IReadOnlyList<CombatEvent> events, int start, int count,
                                     Func<ulong, byte> slotOf)
        {
            int o = 0;
            WriteU32(buffer, ref o, timeMs);
            WriteU16(buffer, ref o, (ushort)count);

            for (int i = 0; i < count; i++)
            {
                CombatEvent e = events[start + i];
                buffer[o++] = (byte)e.Kind;

                if (e.Kind == CombatEventKind.Explosion)
                {
                    WriteI16(buffer, ref o, MobCodec.Quantize(e.Position.x));
                    WriteI16(buffer, ref o, MobCodec.Quantize(e.Position.y));
                    buffer[o++] = e.Radius10;
                    continue;
                }

                if (e.Kind == CombatEventKind.Skill)
                {
                    buffer[o++] = e.SkillId;
                    buffer[o++] = slotOf != null ? slotOf(e.Owner) : e.Slot;
                    WriteI16(buffer, ref o, MobCodec.Quantize(e.Position.x));
                    WriteI16(buffer, ref o, MobCodec.Quantize(e.Position.y));
                    buffer[o++] = e.Rank;
                    WriteU16(buffer, ref o, e.Angle);
                    buffer[o++] = e.RangeScale;
                    buffer[o++] = e.DurationScale;
                    continue;
                }

                WriteU16(buffer, ref o, e.ProjectileId);

                switch (e.Kind)
                {
                    case CombatEventKind.Fire:
                        buffer[o++] = slotOf != null ? slotOf(e.Owner) : e.Slot;
                        WriteI16(buffer, ref o, MobCodec.Quantize(e.Position.x));
                        WriteI16(buffer, ref o, MobCodec.Quantize(e.Position.y));
                        WriteU16(buffer, ref o, e.Angle);
                        break;
                    case CombatEventKind.Hit:
                        WriteU16(buffer, ref o, e.MobId);
                        buffer[o++] = e.MobHealthPercent;
                        buffer[o++] = (byte)((e.Killed ? 1 : 0) | (e.Continues ? 2 : 0) | (e.SurvivorTarget ? 4 : 0));
                        WriteI16(buffer, ref o, MobCodec.Quantize(e.Position.x));
                        WriteI16(buffer, ref o, MobCodec.Quantize(e.Position.y));
                        break;
                    default:
                        WriteI16(buffer, ref o, MobCodec.Quantize(e.Position.x));
                        WriteI16(buffer, ref o, MobCodec.Quantize(e.Position.y));
                        break;
                }
            }
            return o;
        }

        public static bool TryReadHeader(byte[] buffer, int length, out uint timeMs, out int count)
        {
            timeMs = 0; count = 0;
            if (length < HeaderSize) return false;

            int o = 0;
            timeMs = ReadU32(buffer, ref o);
            count = ReadU16(buffer, ref o);
            return true;
        }

        /// <summary>Reads the next event and advances the offset. Returns false on a truncated or unknown event.</summary>
        public static bool TryReadEvent(byte[] buffer, int length, ref int offset, out CombatEvent e)
        {
            e = default;
            if (offset >= length) return false;

            var kind = (CombatEventKind)buffer[offset];
            if (kind != CombatEventKind.Fire && kind != CombatEventKind.Hit && kind != CombatEventKind.End && kind != CombatEventKind.Skill &&
                kind != CombatEventKind.Explosion)
                return false;
            if (offset + EventSize(kind) > length) return false;

            int o = offset + 1;
            e.Kind = kind;

            if (kind == CombatEventKind.Explosion)
            {
                e.Position = new Vector2(MobCodec.Dequantize(ReadI16(buffer, ref o)), MobCodec.Dequantize(ReadI16(buffer, ref o)));
                e.Radius10 = buffer[o++];
                offset = o;
                return true;
            }

            if (kind == CombatEventKind.Skill)
            {
                e.SkillId = buffer[o++];
                e.Slot = buffer[o++];
                e.Position = new Vector2(MobCodec.Dequantize(ReadI16(buffer, ref o)), MobCodec.Dequantize(ReadI16(buffer, ref o)));
                e.Rank = buffer[o++];
                e.Angle = ReadU16(buffer, ref o);
                e.RangeScale = buffer[o++];
                e.DurationScale = buffer[o++];
                offset = o;
                return true;
            }

            e.ProjectileId = ReadU16(buffer, ref o);

            switch (kind)
            {
                case CombatEventKind.Fire:
                    e.Slot = buffer[o++];
                    e.Position = new Vector2(MobCodec.Dequantize(ReadI16(buffer, ref o)), MobCodec.Dequantize(ReadI16(buffer, ref o)));
                    e.Angle = ReadU16(buffer, ref o);
                    break;
                case CombatEventKind.Hit:
                    e.MobId = ReadU16(buffer, ref o);
                    e.MobHealthPercent = buffer[o++];
                    byte flags = buffer[o++];
                    e.Killed = (flags & 1) != 0;
                    e.Continues = (flags & 2) != 0;
                    e.SurvivorTarget = (flags & 4) != 0;
                    e.Position = new Vector2(MobCodec.Dequantize(ReadI16(buffer, ref o)), MobCodec.Dequantize(ReadI16(buffer, ref o)));
                    break;
                default:
                    e.Position = new Vector2(MobCodec.Dequantize(ReadI16(buffer, ref o)), MobCodec.Dequantize(ReadI16(buffer, ref o)));
                    break;
            }

            offset = o;
            return true;
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
