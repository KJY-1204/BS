using System;
using System.Collections.Generic;

namespace BS
{
    /// <summary>One survivor's line of the match summary.</summary>
    public struct SurvivorSummary
    {
        public byte Role;
        public byte Character;
        public byte Level;
        public ushort Falls;
        public ushort Revives;
        public bool Alive;
    }

    /// <summary>What the result screen shows about a finished match: the numbers of the moment the clock decided it.</summary>
    public sealed class MatchSummary
    {
        /// <summary>How long the match lasted, in seconds.</summary>
        public int LengthSeconds;
        public int BossHealth;
        public int BossMaxHealth;
        public int DevicesPlaced;
        public int DevicesDestroyed;
        public int DevicesExpired;
        public List<SurvivorSummary> Survivors = new List<SurvivorSummary>();
    }

    /// <summary>The bytes a summary travels in: little-endian, 17 bytes of the match and 8 for each survivor.</summary>
    public static class MatchSummaryCodec
    {
        public const int HeaderBytes = 17;
        public const int SurvivorBytes = 8;

        public static byte[] Write(MatchSummary s)
        {
            int count = Math.Min(s.Survivors.Count, byte.MaxValue);
            var bytes = new byte[HeaderBytes + count * SurvivorBytes];
            int at = 0;
            PutU16(bytes, ref at, Clamp16(s.LengthSeconds));
            PutI32(bytes, ref at, s.BossHealth);
            PutI32(bytes, ref at, s.BossMaxHealth);
            PutU16(bytes, ref at, Clamp16(s.DevicesPlaced));
            PutU16(bytes, ref at, Clamp16(s.DevicesDestroyed));
            PutU16(bytes, ref at, Clamp16(s.DevicesExpired));
            bytes[at++] = (byte)count;
            for (int i = 0; i < count; i++)
            {
                SurvivorSummary v = s.Survivors[i];
                bytes[at++] = v.Role;
                bytes[at++] = v.Character;
                bytes[at++] = v.Level;
                PutU16(bytes, ref at, v.Falls);
                PutU16(bytes, ref at, v.Revives);
                bytes[at++] = (byte)(v.Alive ? 1 : 0);
            }
            return bytes;
        }

        /// <summary>The summary in the bytes, or null when they are empty, too short or do not fit their survivor count.</summary>
        public static MatchSummary Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length < HeaderBytes) return null;
            int count = bytes[HeaderBytes - 1];
            if (bytes.Length != HeaderBytes + count * SurvivorBytes) return null;

            var s = new MatchSummary();
            int at = 0;
            s.LengthSeconds = GetU16(bytes, ref at);
            s.BossHealth = GetI32(bytes, ref at);
            s.BossMaxHealth = GetI32(bytes, ref at);
            s.DevicesPlaced = GetU16(bytes, ref at);
            s.DevicesDestroyed = GetU16(bytes, ref at);
            s.DevicesExpired = GetU16(bytes, ref at);
            at++;
            for (int i = 0; i < count; i++)
            {
                var v = new SurvivorSummary { Role = bytes[at++], Character = bytes[at++], Level = bytes[at++] };
                v.Falls = (ushort)GetU16(bytes, ref at);
                v.Revives = (ushort)GetU16(bytes, ref at);
                v.Alive = bytes[at++] != 0;
                s.Survivors.Add(v);
            }
            return s;
        }

        static int Clamp16(int v) => Math.Max(0, Math.Min(ushort.MaxValue, v));

        static void PutU16(byte[] b, ref int at, int v)
        {
            b[at++] = (byte)(v & 0xFF);
            b[at++] = (byte)((v >> 8) & 0xFF);
        }

        static void PutI32(byte[] b, ref int at, int v)
        {
            for (int i = 0; i < 4; i++) b[at++] = (byte)((v >> (8 * i)) & 0xFF);
        }

        static int GetU16(byte[] b, ref int at)
        {
            int v = b[at] | (b[at + 1] << 8);
            at += 2;
            return v;
        }

        static int GetI32(byte[] b, ref int at)
        {
            int v = b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24);
            at += 4;
            return v;
        }
    }

    public sealed partial class MatchWorld
    {
        /// <summary>
        /// The summary of the match as it stands: its length (the time limit less the time left), the boss and its devices, and a line for every
        /// survivor the world still knows, in the order of their roles. Null for a match without a boss.
        /// </summary>
        public MatchSummary BuildSummary()
        {
            if (!Clock.Enabled) return null;

            var s = new MatchSummary
            {
                LengthSeconds = (int)Math.Round(Clock.TimeLimit - Clock.TimeLeft),
                BossHealth = Boss.Health,
                BossMaxHealth = Boss.MaxHealth,
                DevicesPlaced = Mobs.DevicesPlaced,
                DevicesDestroyed = Mobs.DevicesDestroyed,
                DevicesExpired = Mobs.DevicesExpired,
            };
            for (int i = 0; i < growthIds.Count; i++)
            {
                ulong id = growthIds[i];
                s.Survivors.Add(new SurvivorSummary
                {
                    Role = SlotOf(id),
                    Character = Leveling.CharacterOf(id),
                    Level = (byte)Math.Min(byte.MaxValue, Leveling.Level(id)),
                    Falls = (ushort)Math.Min(ushort.MaxValue, Survivors.FallsOf(id)),
                    Revives = (ushort)Math.Min(ushort.MaxValue, Survivors.RevivesOf(id)),
                    Alive = Survivors.IsAlive(id),
                });
            }
            s.Survivors.Sort((a, b) => a.Role.CompareTo(b.Role));
            return s;
        }
    }
}
