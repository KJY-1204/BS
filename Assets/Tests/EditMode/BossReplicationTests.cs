using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class BossReplicationTests
    {
        const float Dt = 0.02f;

        static BossSim Boss(float x, float health = 1000f) =>
            new BossSim(new BossDef { Name = "t", MaxHealth = (int)health, MoveSpeed = 6f, Radius = 0.9f, ContactDamage = 25, MatchSeconds = 60f }, new Vector2(x, 0));

        [Test]
        public void ABossHitTravelsAsAnOrdinaryHitEventWithTheReservedId()
        {
            var sim = new ProjectileSim();
            var boss = Boss(6f);
            sim.TryFire(new ProjectileSpec
            {
                Owner = 1, Origin = Vector2.zero, Angle = 0, Damage = 100,
                Shape = new ProjectileShape { Speed = 16f, Lifetime = 1f, Radius = 0.15f, Pierce = 1 },
            }, out _);

            var events = new List<CombatEvent>();
            for (int i = 0; i < 40; i++) { sim.Step(Dt, null, boss); sim.DrainEvents(events); }
            CombatEvent hit = events.Single(e => e.Kind == CombatEventKind.Hit);

            var buffer = new byte[CombatCodec.MaxBatchBytes + 16];
            int length = CombatCodec.WriteBatch(buffer, 100u, new List<CombatEvent> { hit }, 0, 1, null);
            Assert.AreEqual(CombatCodec.HeaderSize + CombatCodec.HitSize, length, "no new event format");

            CombatCodec.TryReadHeader(buffer, length, out _, out _);
            int offset = CombatCodec.HeaderSize;
            Assert.IsTrue(CombatCodec.TryReadEvent(buffer, length, ref offset, out CombatEvent read));
            Assert.AreEqual(BossSim.MobId, read.MobId);
            Assert.AreEqual(90, read.MobHealthPercent);
            Assert.IsFalse(read.Killed);
        }

        [Test]
        public void AKillingBossHitRoundTripsWithTheKilledMark()
        {
            var sim = new ProjectileSim();
            var boss = Boss(6f, 50f);
            sim.TryFire(new ProjectileSpec
            {
                Owner = 1, Origin = Vector2.zero, Angle = 0, Damage = 100,
                Shape = new ProjectileShape { Speed = 16f, Lifetime = 1f, Radius = 0.15f, Pierce = 1 },
            }, out _);
            var events = new List<CombatEvent>();
            for (int i = 0; i < 40; i++) { sim.Step(Dt, null, boss); sim.DrainEvents(events); }

            var buffer = new byte[CombatCodec.MaxBatchBytes + 16];
            CombatEvent hit = events.Single(e => e.Kind == CombatEventKind.Hit);
            int length = CombatCodec.WriteBatch(buffer, 100u, new List<CombatEvent> { hit }, 0, 1, null);
            int offset = CombatCodec.HeaderSize;
            CombatCodec.TryReadEvent(buffer, length, ref offset, out CombatEvent read);

            Assert.IsTrue(read.Killed);
            Assert.AreEqual(0, read.MobHealthPercent);
            Assert.AreEqual(BossSim.MobId, read.MobId);
        }

        [Test]
        public void AClientReplayDrawsABossHitLikeAnyOther_WithTheReservedId()
        {
            var replay = new ProjectileReplay();
            replay.OnFire(1, 1, Vector2.zero, 0, 0f);
            replay.OnHit(1, BossSim.MobId, 80, false, new Vector2(5, 0), 0.3f);

            var effects = new List<HitEffect>();
            replay.Advance(0.5f);
            replay.DrainDueEffects(effects);

            Assert.AreEqual(1, effects.Count);
            Assert.AreEqual(BossSim.MobId, effects[0].MobId);
            Assert.AreEqual(80, effects[0].HealthPercent);
        }

        [Test]
        public void NoMobEventEverCarriesTheReservedBossId()
        {
            var sim = new MobSim(hardCap: 100, targetCount: 0);
            var events = new List<MobEvent>();
            for (int i = 0; i < 70000; i++)
            {
                sim.TrySpawn(Vector2.zero, 0, out ushort id);
                sim.Remove(id);
                if (i % 1000 == 999) sim.DrainEvents(events);
                if (i % 1000 == 999) Assert.IsFalse(events.Any(e => e.Id == BossSim.MobId), $"after {i + 1} spawns");
                if (i % 1000 == 999) events.Clear();
            }
        }

        [Test]
        public void TheClientsMobTrackerNeverMakesAMobFromTheBossIdThroughNormalEvents()
        {
            // Mob spawn events come only from MobSim, which never uses the reserved id, so the interpolator never has a mob with it.
            var interpolator = new MobInterpolator();
            var sim = new MobSim(hardCap: 100, targetCount: 0);
            for (int i = 0; i < 300; i++)
            {
                sim.TrySpawn(Vector2.zero, 0, out ushort id);
                interpolator.OnSpawn(id, 0, Vector2.zero, 0f);
                sim.Remove(id);
                interpolator.OnRemove(id, 0f);
            }

            for (int i = 0; i < interpolator.Count; i++) Assert.AreNotEqual(BossSim.MobId, interpolator.IdAt(i));
        }
    }
}
