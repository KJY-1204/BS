using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class ExplosionEventTests
    {
        static byte[] Write(IReadOnlyList<CombatEvent> events, out int length)
        {
            var buffer = new byte[CombatCodec.MaxBatchBytes + 64];
            length = CombatCodec.WriteBatch(buffer, 4242, events, 0, events.Count, null);
            return buffer;
        }

        [Test]
        public void AnExplosionRoundTripsInSixBytes()
        {
            var e = new CombatEvent { Kind = CombatEventKind.Explosion, Position = new Vector2(3.25f, -7.5f), Radius10 = 22 };
            byte[] buffer = Write(new List<CombatEvent> { e }, out int length);

            Assert.AreEqual(CombatCodec.HeaderSize + 6, length);
            Assert.AreEqual(6, CombatCodec.ExplosionSize);
            Assert.AreEqual(6, CombatCodec.EventSize(CombatEventKind.Explosion));
            int offset = CombatCodec.HeaderSize;
            Assert.IsTrue(CombatCodec.TryReadEvent(buffer, length, ref offset, out CombatEvent read));
            Assert.AreEqual(CombatEventKind.Explosion, read.Kind);
            Assert.AreEqual(3.25f, read.Position.x, 0.001f);
            Assert.AreEqual(-7.5f, read.Position.y, 0.001f);
            Assert.AreEqual(22, read.Radius10);
            Assert.AreEqual(length, offset);
        }

        [Test]
        public void ExplosionsMixWithTheOtherEventsInOneBatch()
        {
            var events = new List<CombatEvent>
            {
                new CombatEvent { Kind = CombatEventKind.Fire, ProjectileId = 4, Owner = 0, Position = new Vector2(1f, 1f), Angle = 100 },
                new CombatEvent { Kind = CombatEventKind.Explosion, Position = new Vector2(-2f, 2f), Radius10 = 30 },
                new CombatEvent { Kind = CombatEventKind.Hit, ProjectileId = 4, MobId = 2, MobHealthPercent = 80, Position = new Vector2(1f, 1f), SurvivorTarget = true },
                new CombatEvent { Kind = CombatEventKind.End, ProjectileId = 5, Position = new Vector2(0f, 0f) },
                new CombatEvent { Kind = CombatEventKind.Explosion, Position = new Vector2(9f, -9f), Radius10 = 5 },
            };
            byte[] buffer = Write(events, out int length);

            Assert.IsTrue(CombatCodec.TryReadHeader(buffer, length, out _, out int count));
            Assert.AreEqual(5, count);
            int offset = CombatCodec.HeaderSize;
            var kinds = new List<CombatEventKind>();
            for (int i = 0; i < count; i++)
            {
                Assert.IsTrue(CombatCodec.TryReadEvent(buffer, length, ref offset, out CombatEvent read));
                kinds.Add(read.Kind);
                if (read.Kind == CombatEventKind.Explosion && i == 4) Assert.AreEqual(5, read.Radius10);
            }
            CollectionAssert.AreEqual(new[] { CombatEventKind.Fire, CombatEventKind.Explosion, CombatEventKind.Hit, CombatEventKind.End, CombatEventKind.Explosion }, kinds);
            Assert.AreEqual(length, offset);
        }

        [Test]
        public void ATruncatedExplosionIsRefused()
        {
            var e = new CombatEvent { Kind = CombatEventKind.Explosion, Position = Vector2.zero, Radius10 = 10 };
            byte[] buffer = Write(new List<CombatEvent> { e }, out int length);
            int offset = CombatCodec.HeaderSize;

            Assert.IsFalse(CombatCodec.TryReadEvent(buffer, length - 1, ref offset, out _));
        }

        [Test]
        public void TheRadiusIsSentInTenthsAndClamped()
        {
            Assert.AreEqual(22, CombatCodec.QuantizeRadius(2.2f));
            Assert.AreEqual(0, CombatCodec.QuantizeRadius(0f));
            Assert.AreEqual(0, CombatCodec.QuantizeRadius(-3f));
            Assert.AreEqual(255, CombatCodec.QuantizeRadius(99f));
        }

        [Test]
        public void AnUnknownEventKindIsStillRefused()
        {
            var buffer = new byte[16];
            buffer[CombatCodec.HeaderSize] = 9;
            int offset = CombatCodec.HeaderSize;
            Assert.IsFalse(CombatCodec.TryReadEvent(buffer, 16, ref offset, out _));
        }

        [Test]
        public void TheOldEventSizesDidNotChange()
        {
            Assert.AreEqual(10, CombatCodec.FireSize);
            Assert.AreEqual(11, CombatCodec.HitSize);
            Assert.AreEqual(7, CombatCodec.EndSize);
            Assert.AreEqual(10, CombatCodec.SkillSize);
        }

        [Test]
        public void ManyExplosionsSplitIntoMessagesThatFit()
        {
            var events = new List<CombatEvent>();
            for (int i = 0; i < 400; i++) events.Add(new CombatEvent { Kind = CombatEventKind.Explosion, Position = new Vector2(i * 0.01f, 0f), Radius10 = 22 });

            int total = 0;
            int start = 0;
            while (start < events.Count)
            {
                int count = CombatCodec.CountThatFit(events, start);
                Assert.Greater(count, 0);
                Assert.LessOrEqual(CombatCodec.HeaderSize + count * CombatCodec.ExplosionSize, CombatCodec.MaxBatchBytes);
                total += count;
                start += count;
            }
            Assert.AreEqual(400, total);
        }

        // ---- the replay ----

        [Test]
        public void ABlastIsHeldUntilTheDisplayClockReachesIt()
        {
            var replay = new ExplosionReplay();
            replay.OnExplosion(new ExplosionEffect { Position = new Vector2(1f, 2f), Radius = 2.2f, ServerTime = 10f });
            var due = new List<ExplosionEffect>();

            replay.Advance(9.9f);
            replay.DrainDue(due);
            Assert.AreEqual(0, due.Count);
            Assert.AreEqual(1, replay.PendingCount);

            replay.Advance(10.0f);
            replay.DrainDue(due);
            Assert.AreEqual(1, due.Count);
            Assert.AreEqual(2.2f, due[0].Radius, 1e-5f);
            Assert.AreEqual(0, replay.PendingCount);
        }

        [Test]
        public void ABlastThatArrivesLateIsShownAtOnce()
        {
            var replay = new ExplosionReplay();
            replay.OnExplosion(new ExplosionEffect { ServerTime = 3f });
            var due = new List<ExplosionEffect>();

            replay.Advance(8f);
            replay.DrainDue(due);

            Assert.AreEqual(1, due.Count);
        }

        [Test]
        public void ABlastIsShownOnce()
        {
            var replay = new ExplosionReplay();
            replay.OnExplosion(new ExplosionEffect { ServerTime = 1f });
            var due = new List<ExplosionEffect>();

            replay.Advance(2f);
            replay.DrainDue(due);
            replay.Advance(3f);
            replay.DrainDue(due);

            Assert.AreEqual(1, due.Count);
        }

        [Test]
        public void TheReplayForgetsEverythingWhenCleared()
        {
            var replay = new ExplosionReplay();
            replay.OnExplosion(new ExplosionEffect { ServerTime = 5f });
            replay.OnExplosion(new ExplosionEffect { ServerTime = 1f });
            replay.Advance(2f);

            replay.Clear();
            var due = new List<ExplosionEffect>();
            replay.Advance(100f);
            replay.DrainDue(due);

            Assert.AreEqual(0, due.Count);
            Assert.AreEqual(0, replay.PendingCount);
        }

        [Test]
        public void TheQueueCannotGrowWithoutLimit()
        {
            var replay = new ExplosionReplay();
            for (int i = 0; i < ExplosionReplay.MaxPending + 50; i++) replay.OnExplosion(new ExplosionEffect { ServerTime = 1000f + i });

            Assert.AreEqual(ExplosionReplay.MaxPending, replay.PendingCount);
        }

        [Test]
        public void ABlastsKindOfShooterSlotGivesTheMobShotShape()
        {
            var replay = new ProjectileReplay();
            replay.ShapeOfSlot = slot => slot == CombatServer.MobShooterSlot ? MobShot.Shape : ProjectileShape.Default;

            replay.OnFire(1, CombatServer.MobShooterSlot, Vector2.zero, 0, 0f);
            replay.Advance(0.5f);

            Assert.AreEqual(1, replay.Count);
            Assert.AreEqual(MobShot.Shape.Radius, replay.ShapeAt(0).Radius, 1e-6f);
            Assert.AreEqual(MobShot.Shape.Speed * 0.5f, replay.PositionAt(0).x, 1e-3f);
        }
    }
}
