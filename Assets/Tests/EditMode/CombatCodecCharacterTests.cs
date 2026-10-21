using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class CombatCodecCharacterTests
    {
        static List<CombatEvent> RoundTrip(List<CombatEvent> events, uint timeMs = 100u)
        {
            var buffer = new byte[CombatCodec.MaxBatchBytes + 16];
            int length = CombatCodec.WriteBatch(buffer, timeMs, events, 0, events.Count, null);

            Assert.IsTrue(CombatCodec.TryReadHeader(buffer, length, out _, out int count));
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

        static CombatEvent Hit(bool killed, bool continues) =>
            new CombatEvent
            {
                Kind = CombatEventKind.Hit, ProjectileId = 7, MobId = 9, MobHealthPercent = 40, Killed = killed, Continues = continues,
                Position = new Vector2(1.5f, -2.5f),
            };

        [Test]
        public void TheContinueMarkAndTheKilledMarkAreIndependent()
        {
            foreach (bool killed in new[] { false, true })
            {
                foreach (bool continues in new[] { false, true })
                {
                    CombatEvent read = RoundTrip(new List<CombatEvent> { Hit(killed, continues) })[0];
                    Assert.AreEqual(killed, read.Killed, $"killed={killed} continues={continues}");
                    Assert.AreEqual(continues, read.Continues, $"killed={killed} continues={continues}");
                    Assert.AreEqual(9, read.MobId);
                    Assert.AreEqual(40, read.MobHealthPercent);
                }
            }
        }

        [Test]
        public void AHitStaysElevenBytes()
        {
            var buffer = new byte[64];
            int length = CombatCodec.WriteBatch(buffer, 1u, new List<CombatEvent> { Hit(true, true) }, 0, 1, null);
            Assert.AreEqual(CombatCodec.HeaderSize + CombatCodec.HitSize, length);
            Assert.AreEqual(11, CombatCodec.HitSize);
        }

        [Test]
        public void ANormalHitHasNoContinueMark()
        {
            CombatEvent read = RoundTrip(new List<CombatEvent> { Hit(false, false) })[0];
            Assert.IsFalse(read.Continues);
        }

        [Test]
        public void ASkillCarriesItsAimAngle_OverTheWholeRange()
        {
            foreach (ushort angle in new ushort[] { 0, 1, 255, 256, 16384, 32768, 49151, 65535 })
            {
                var skill = new CombatEvent { Kind = CombatEventKind.Skill, SkillId = SkillIds.MasterSpark, Slot = 2, Position = new Vector2(3, 4), Rank = 2, Angle = angle };

                CombatEvent read = RoundTrip(new List<CombatEvent> { skill })[0];

                Assert.AreEqual(angle, read.Angle);
                Assert.AreEqual(SkillIds.MasterSpark, read.SkillId);
                Assert.AreEqual(2, read.Slot);
                Assert.AreEqual(2, read.Rank);
                Assert.AreEqual(3f, read.Position.x, 0.002f);
                Assert.AreEqual(4f, read.Position.y, 0.002f);
            }
        }

        [Test]
        public void EverySkillIdRoundTripsWithItsRankAndAngle()
        {
            foreach (SkillDef def in SkillCatalog.All)
            {
                var skill = new CombatEvent { Kind = CombatEventKind.Skill, SkillId = def.Id, Slot = 1, Rank = (byte)def.MaxRank, Angle = 12345 };
                CombatEvent read = RoundTrip(new List<CombatEvent> { skill })[0];
                Assert.AreEqual(def.Id, read.SkillId, def.Name);
                Assert.AreEqual(def.MaxRank, read.Rank, def.Name);
                Assert.AreEqual(12345, read.Angle, def.Name);
            }
        }

        [Test]
        public void ASkillIsTwelveBytes_AndATruncatedOneIsRejected()
        {
            Assert.AreEqual(12, CombatCodec.SkillSize);
            var buffer = new byte[64];
            var events = new List<CombatEvent> { new CombatEvent { Kind = CombatEventKind.Skill, SkillId = 8, Angle = 999, Rank = 1 } };
            int length = CombatCodec.WriteBatch(buffer, 1u, events, 0, 1, null);
            Assert.AreEqual(CombatCodec.HeaderSize + 12, length);

            int offset = CombatCodec.HeaderSize;
            Assert.IsFalse(CombatCodec.TryReadEvent(buffer, length - 1, ref offset, out _));
        }

        [Test]
        public void AVolleyOfTwentyFiresAndTheirHitsFitInTheMessagesWithoutLoss()
        {
            var events = new List<CombatEvent>();
            for (ushort i = 1; i <= 20; i++)
                events.Add(new CombatEvent { Kind = CombatEventKind.Fire, ProjectileId = i, Slot = 2, Position = new Vector2(1, 1), Angle = (ushort)(i * 3000) });
            for (ushort i = 1; i <= 20; i++) events.Add(Hit(false, i % 2 == 0));
            events.Add(new CombatEvent { Kind = CombatEventKind.Skill, SkillId = SkillIds.KnifeBarrage, Slot = 2, Rank = 3, Angle = 5 });

            int start = 0;
            var all = new List<CombatEvent>();
            var buffer = new byte[CombatCodec.MaxBatchBytes + 16];
            while (start < events.Count)
            {
                int fit = CombatCodec.CountThatFit(events, start);
                Assert.Greater(fit, 0);
                int length = CombatCodec.WriteBatch(buffer, 5u, events, start, fit, null);
                Assert.LessOrEqual(length, CombatCodec.MaxBatchBytes);

                CombatCodec.TryReadHeader(buffer, length, out _, out int count);
                int offset = CombatCodec.HeaderSize;
                for (int i = 0; i < count; i++)
                {
                    Assert.IsTrue(CombatCodec.TryReadEvent(buffer, length, ref offset, out CombatEvent e));
                    all.Add(e);
                }
                start += fit;
            }

            Assert.AreEqual(events.Count, all.Count);
            for (int i = 0; i < events.Count; i++) Assert.AreEqual(events[i].Kind, all[i].Kind, $"order at {i}");
        }

        [Test]
        public void TheWorstCaseTrafficOfAllFourCharactersStaysFarUnderTheBudget()
        {
            // Shots per second and hits per second for each character if every shot hits, one message per event.
            float bytesPerSecond = 0f;
            foreach (CharacterDef c in CharacterCatalog.All)
            {
                float attacksPerSecond = 1f / c.Attack.CooldownSeconds;
                float shots = attacksPerSecond * c.Attack.Shots;
                float hits = shots * c.Attack.Shape.Pierce;
                bytesPerSecond += shots * CombatCodec.FireSize + hits * CombatCodec.HitSize;
            }
            // Every survivor's events go to every other client, so one client receives all four characters' events.
            Assert.Less(bytesPerSecond, 4000f, "well under the 10 KB per second budget even if every shot pierces to the limit");
        }
    }
}
