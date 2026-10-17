using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class CombatCodecTests
    {
        static CombatEvent Fire(ushort id, float x, float y, ushort angle, byte slot = 2) =>
            new CombatEvent { Kind = CombatEventKind.Fire, ProjectileId = id, Slot = slot, Position = new Vector2(x, y), Angle = angle };

        static CombatEvent Hit(ushort id, ushort mob, byte percent, bool killed, float x, float y) =>
            new CombatEvent { Kind = CombatEventKind.Hit, ProjectileId = id, MobId = mob, MobHealthPercent = percent, Killed = killed, Position = new Vector2(x, y) };

        static CombatEvent End(ushort id, float x, float y) =>
            new CombatEvent { Kind = CombatEventKind.End, ProjectileId = id, Position = new Vector2(x, y) };

        static List<CombatEvent> RoundTrip(List<CombatEvent> events, uint timeMs, out uint readTime)
        {
            var buffer = new byte[CombatCodec.MaxBatchBytes + 16];
            int length = CombatCodec.WriteBatch(buffer, timeMs, events, 0, events.Count, null);

            Assert.IsTrue(CombatCodec.TryReadHeader(buffer, length, out readTime, out int count));
            Assert.AreEqual(events.Count, count);

            var result = new List<CombatEvent>();
            int offset = CombatCodec.HeaderSize;
            for (int i = 0; i < count; i++)
            {
                Assert.IsTrue(CombatCodec.TryReadEvent(buffer, length, ref offset, out CombatEvent e), $"event {i}");
                result.Add(e);
            }
            Assert.AreEqual(length, offset, "the whole message is consumed");
            return result;
        }

        [Test]
        public void Fire_RoundTrips()
        {
            ushort angle = AimMath.QuantizeAngle(2.2f);
            var read = RoundTrip(new List<CombatEvent> { Fire(1234, -3.25f, 4.5f, angle, slot: 3) }, 987654u, out uint time);

            Assert.AreEqual(987654u, time);
            Assert.AreEqual(CombatEventKind.Fire, read[0].Kind);
            Assert.AreEqual(1234, read[0].ProjectileId);
            Assert.AreEqual(3, read[0].Slot);
            Assert.AreEqual(angle, read[0].Angle);
            Assert.Less(Vector2.Distance(new Vector2(-3.25f, 4.5f), read[0].Position), 0.0015f);
        }

        [Test]
        public void Hit_RoundTrips()
        {
            var read = RoundTrip(new List<CombatEvent> { Hit(7, 4321, 66, false, 12.345f, -8.5f) }, 5u, out _);

            Assert.AreEqual(CombatEventKind.Hit, read[0].Kind);
            Assert.AreEqual(7, read[0].ProjectileId);
            Assert.AreEqual(4321, read[0].MobId);
            Assert.AreEqual(66, read[0].MobHealthPercent);
            Assert.IsFalse(read[0].Killed);
            Assert.Less(Vector2.Distance(new Vector2(12.345f, -8.5f), read[0].Position), 0.0015f);
        }

        [Test]
        public void Hit_KilledFlagRoundTrips()
        {
            var read = RoundTrip(new List<CombatEvent> { Hit(7, 1, 0, true, 0, 0) }, 5u, out _);
            Assert.IsTrue(read[0].Killed);
            Assert.AreEqual(0, read[0].MobHealthPercent);
        }

        [Test]
        public void End_RoundTrips()
        {
            var read = RoundTrip(new List<CombatEvent> { End(99, 16f, -2f) }, 5u, out _);

            Assert.AreEqual(CombatEventKind.End, read[0].Kind);
            Assert.AreEqual(99, read[0].ProjectileId);
            Assert.Less(Vector2.Distance(new Vector2(16f, -2f), read[0].Position), 0.0015f);
        }

        [Test]
        public void MixedBatch_KeepsOrderAndContent()
        {
            var events = new List<CombatEvent>
            {
                Fire(1, 0, 0, 100), Hit(2, 3, 50, false, 1, 1), End(4, 2, 2), Fire(5, 1, 1, 200), Hit(6, 7, 0, true, 3, 3),
            };

            var read = RoundTrip(events, 123u, out _);

            Assert.AreEqual(events.Count, read.Count);
            for (int i = 0; i < events.Count; i++)
            {
                Assert.AreEqual(events[i].Kind, read[i].Kind, $"event {i}");
                Assert.AreEqual(events[i].ProjectileId, read[i].ProjectileId, $"event {i}");
            }
        }

        [Test]
        public void OwnerSlotIsLookedUpWhenEncoding()
        {
            var events = new List<CombatEvent> { new CombatEvent { Kind = CombatEventKind.Fire, ProjectileId = 1, Owner = 42, Angle = 5 } };
            var buffer = new byte[64];

            int length = CombatCodec.WriteBatch(buffer, 1u, events, 0, 1, owner => owner == 42 ? (byte)4 : (byte)0);
            int offset = CombatCodec.HeaderSize;
            CombatCodec.TryReadEvent(buffer, length, ref offset, out CombatEvent e);

            Assert.AreEqual(4, e.Slot);
        }

        [Test]
        public void TruncatedOrUnknownEventsAreRejected()
        {
            var buffer = new byte[64];
            var events = new List<CombatEvent> { Fire(1, 0, 0, 5) };
            int length = CombatCodec.WriteBatch(buffer, 1u, events, 0, 1, null);

            int offset = CombatCodec.HeaderSize;
            Assert.IsFalse(CombatCodec.TryReadEvent(buffer, length - 1, ref offset, out _));

            buffer[CombatCodec.HeaderSize] = 99; // unknown kind
            offset = CombatCodec.HeaderSize;
            Assert.IsFalse(CombatCodec.TryReadEvent(buffer, length, ref offset, out _));

            Assert.IsFalse(CombatCodec.TryReadHeader(buffer, 3, out _, out _));
        }

        [Test]
        public void LargeBatchesAreSplitIntoMessagesThatFit()
        {
            var events = new List<CombatEvent>();
            for (ushort i = 1; i <= 300; i++) events.Add(i % 2 == 0 ? Fire(i, 0, 0, 1) : Hit(i, i, 10, false, 0, 0));

            var buffer = new byte[CombatCodec.MaxBatchBytes + 16];
            int sent = 0;
            while (sent < events.Count)
            {
                int count = CombatCodec.CountThatFit(events, sent);
                Assert.Greater(count, 0);
                int length = CombatCodec.WriteBatch(buffer, 1u, events, sent, count, null);
                Assert.LessOrEqual(length, CombatCodec.MaxBatchBytes);
                sent += count;
            }
            Assert.AreEqual(events.Count, sent);
        }

        static CombatEvent Skill(byte skill, byte slot, float x, float y, byte rank) =>
            new CombatEvent { Kind = CombatEventKind.Skill, SkillId = skill, Slot = slot, Position = new Vector2(x, y), Rank = rank };

        [Test]
        public void Skill_RoundTrips()
        {
            var read = RoundTrip(new List<CombatEvent> { Skill(SkillIds.Burst, 3, -7.125f, 4.5f, 2) }, 4242u, out uint time);

            Assert.AreEqual(4242u, time);
            Assert.AreEqual(CombatEventKind.Skill, read[0].Kind);
            Assert.AreEqual(SkillIds.Burst, read[0].SkillId);
            Assert.AreEqual(3, read[0].Slot);
            Assert.AreEqual(2, read[0].Rank);
            Assert.Less(Vector2.Distance(new Vector2(-7.125f, 4.5f), read[0].Position), 0.0015f);
        }

        [Test]
        public void Skill_TakesTenBytes()
        {
            var buffer = new byte[64];
            var events = new List<CombatEvent> { Skill(SkillIds.RapidFire, 1, 0, 0, 1) };

            int length = CombatCodec.WriteBatch(buffer, 1u, events, 0, 1, null);

            Assert.AreEqual(CombatCodec.HeaderSize + CombatCodec.SkillSize, length);
            Assert.AreEqual(10, CombatCodec.SkillSize);
        }

        [Test]
        public void SkillEventsMixedWithShotsKeepTheirOrder()
        {
            var events = new List<CombatEvent>
            {
                Fire(1, 0, 0, 100), Skill(SkillIds.Burst, 2, 1, 1, 1), Hit(1, 5, 0, true, 3, 3), Skill(SkillIds.RapidFire, 4, 2, 2, 3), End(9, 1, 1),
            };

            var read = RoundTrip(events, 55u, out _);

            Assert.AreEqual(events.Count, read.Count);
            for (int i = 0; i < events.Count; i++) Assert.AreEqual(events[i].Kind, read[i].Kind, $"event {i}");
            Assert.AreEqual(SkillIds.RapidFire, read[3].SkillId);
            Assert.AreEqual(5, read[2].MobId);
        }

        [Test]
        public void SkillOwnerSlotIsLookedUpWhenEncoding()
        {
            var events = new List<CombatEvent> { new CombatEvent { Kind = CombatEventKind.Skill, SkillId = 5, Owner = 42, Rank = 1 } };
            var buffer = new byte[64];

            int length = CombatCodec.WriteBatch(buffer, 1u, events, 0, 1, owner => owner == 42 ? (byte)3 : (byte)0);
            int offset = CombatCodec.HeaderSize;
            CombatCodec.TryReadEvent(buffer, length, ref offset, out CombatEvent e);

            Assert.AreEqual(3, e.Slot);
        }

        [Test]
        public void ATruncatedSkillEventIsRejected()
        {
            var buffer = new byte[64];
            int length = CombatCodec.WriteBatch(buffer, 1u, new List<CombatEvent> { Skill(SkillIds.Burst, 1, 0, 0, 1) }, 0, 1, null);

            int offset = CombatCodec.HeaderSize;
            Assert.IsFalse(CombatCodec.TryReadEvent(buffer, length - 1, ref offset, out _));
        }

        [Test]
        public void FourSurvivorsUsingSkillsAsOftenAsTheyCan_AddAlmostNothing()
        {
            // Two skills per survivor, each on its cooldown, one message per event (worst case).
            float perSecond = 0f;
            foreach (SkillDef def in SkillCatalog.All)
                if (def.IsActive) perSecond += 4f / def.Cooldown * (CombatCodec.HeaderSize + CombatCodec.SkillSize);

            Assert.Less(perSecond, 200f, "well under a quarter of a KB per second");
        }

        [Test]
        public void ShotsPlusSkills_StayWithinTheCombatBudget()
        {
            float shots = 4f / CombatConfig.FireCooldownSeconds;
            float bytes = shots * (CombatCodec.HeaderSize + CombatCodec.FireSize) + shots * (CombatCodec.HeaderSize + CombatCodec.HitSize);
            foreach (SkillDef def in SkillCatalog.All)
                if (def.IsActive) bytes += 4f / def.Cooldown * (CombatCodec.HeaderSize + CombatCodec.SkillSize);

            Assert.Less(bytes, 10f * 1024f / 4f, "less than a quarter of the 10 KB/s budget");
        }

        [Test]
        public void FourSurvivorsFiringNonstop_NeedAFractionOfTheCombatBudget()
        {
            // Worst case: every event in its own message (header every time).
            float shotsPerSecondPerSurvivor = 1f / CombatConfig.FireCooldownSeconds;
            float events = 4f * shotsPerSecondPerSurvivor;
            float bytesPerSecond = events * (CombatCodec.HeaderSize + CombatCodec.FireSize)
                                 + events * (CombatCodec.HeaderSize + CombatCodec.HitSize);

            Assert.Less(bytesPerSecond, 10f * 1024f / 10f, "less than a tenth of the 10 KB/s budget");
        }
    }
}
