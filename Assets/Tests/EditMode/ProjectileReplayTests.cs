using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class ProjectileReplayTests
    {
        const float Dt = 0.02f;

        static ushort Angle(float degrees) => AimMath.QuantizeAngle(degrees * Mathf.Deg2Rad);

        // ---- Visibility and flight ----

        [Test]
        public void ANewShotIsNotVisibleBeforeItsDisplayTime()
        {
            var replay = new ProjectileReplay();
            replay.OnFire(1, 1, Vector2.zero, Angle(0), 5f);

            replay.Advance(4.9f);

            Assert.AreEqual(0, replay.Count);
            Assert.AreEqual(1, replay.TrackedCount);

            replay.Advance(5.05f);
            Assert.AreEqual(1, replay.Count);
        }

        [Test]
        public void FlightMatchesTheServerWithinHalfAUnit()
        {
            // Run the real server simulation and replay its events on a client clock.
            var sim = new ProjectileSim();
            var replay = new ProjectileReplay();
            var events = new List<CombatEvent>();

            float serverTime = 100f;
            sim.TryFire(1, new Vector2(-5, 2), Angle(20), out ushort id);
            sim.DrainEvents(events);
            replay.OnFire(id, 2, events[0].Position, events[0].Angle, serverTime);

            float worst = 0f;
            for (int step = 1; step <= 40; step++)
            {
                sim.Step(Dt, null);
                serverTime += Dt;

                replay.Advance(serverTime);
                if (sim.Count == 0) break;

                Vector2 server = sim.PositionAt(0);
                Assert.AreEqual(1, replay.Count);
                worst = Mathf.Max(worst, Vector2.Distance(server, replay.PositionAt(0)));
            }

            Assert.Less(worst, 0.5f);
            Assert.Less(worst, 0.01f, "the same rule gives practically the same place");
        }

        [Test]
        public void SeveralShotsAreTrackedIndependently()
        {
            var replay = new ProjectileReplay();
            for (ushort i = 1; i <= 100; i++) replay.OnFire(i, 1, new Vector2(0, i * 0.1f - 5f), Angle(0), 10f + i * 0.001f);

            replay.Advance(10.3f);

            Assert.AreEqual(100, replay.Count);
            for (int i = 0; i < replay.Count; i++)
            {
                ushort id = replay.IdAt(i);
                Assert.AreEqual(id * 0.1f - 5f, replay.PositionAt(i).y, 1e-3f);
                // Shots started between 10.001 and 10.1, so after 10.3 they have flown 0.2 to 0.3 s at speed 14.
                Assert.Greater(replay.PositionAt(i).x, 2.7f);
                Assert.Less(replay.PositionAt(i).x, 4.3f);
            }
        }

        // ---- Hits and endings ----

        [Test]
        public void AHitEndsAtTheHitPositionAtTheHitTime()
        {
            var replay = new ProjectileReplay();
            replay.OnFire(1, 1, Vector2.zero, Angle(0), 1.0f);
            replay.OnHit(1, 77, 66, false, new Vector2(3, 0), 1.2f);

            replay.Advance(1.1f);
            Assert.AreEqual(1, replay.Count);
            Assert.AreEqual(1.5f, replay.PositionAt(0).x, 0.05f, "halfway along the way to the hit");

            replay.Advance(1.199f);
            Assert.AreEqual(1, replay.Count);
            Assert.AreEqual(3f, replay.PositionAt(0).x, 0.05f);

            replay.Advance(1.2f);
            Assert.AreEqual(0, replay.Count);

            var effects = new List<HitEffect>();
            replay.DrainDueEffects(effects);
            Assert.AreEqual(1, effects.Count);
            Assert.AreEqual(77, effects[0].MobId);
            Assert.AreEqual(66, effects[0].HealthPercent);
            Assert.IsFalse(effects[0].Killed);
            Assert.AreEqual(new Vector2(3, 0), effects[0].Position);
        }

        [Test]
        public void TheHitEffectIsNotDueBeforeTheDisplayClockReachesIt()
        {
            var replay = new ProjectileReplay();
            replay.OnFire(1, 1, Vector2.zero, Angle(0), 1.0f);
            replay.OnHit(1, 77, 33, true, new Vector2(3, 0), 1.2f);

            replay.Advance(1.15f);

            var effects = new List<HitEffect>();
            replay.DrainDueEffects(effects);
            Assert.AreEqual(0, effects.Count);

            replay.Advance(1.25f);
            replay.DrainDueEffects(effects);
            Assert.AreEqual(1, effects.Count);
            Assert.IsTrue(effects[0].Killed);
        }

        [Test]
        public void ALateHitRemovesTheShotAtOnce()
        {
            var replay = new ProjectileReplay();
            replay.OnFire(1, 1, Vector2.zero, Angle(0), 1.0f);
            replay.Advance(1.6f);
            Assert.AreEqual(1, replay.Count, "still flying on screen");

            replay.OnHit(1, 5, 10, false, new Vector2(3, 0), 1.2f); // reported after the display clock passed it
            replay.Advance(1.62f);

            Assert.AreEqual(0, replay.Count);
            var effects = new List<HitEffect>();
            replay.DrainDueEffects(effects);
            Assert.AreEqual(1, effects.Count);
        }

        [Test]
        public void AnEndEventRemovesTheShotWithoutAnEffect()
        {
            var replay = new ProjectileReplay();
            replay.OnFire(1, 1, Vector2.zero, Angle(0), 1.0f);
            replay.OnEnd(1, new Vector2(12.6f, 0), 1.9f);

            replay.Advance(1.5f);
            Assert.AreEqual(1, replay.Count);

            replay.Advance(1.95f);
            Assert.AreEqual(0, replay.Count);
            var effects = new List<HitEffect>();
            replay.DrainDueEffects(effects);
            Assert.AreEqual(0, effects.Count);
        }

        [Test]
        public void AShotWhoseEndNeverArrivesIsRemovedAfterItsLifetimeAndAMargin()
        {
            var replay = new ProjectileReplay();
            replay.OnFire(1, 1, Vector2.zero, Angle(0), 1.0f);

            replay.Advance(1.0f + CombatConfig.ProjectileLifetimeSeconds + 0.2f);
            Assert.AreEqual(1, replay.Count, "within the margin it is still drawn, stopped at the end of its range");
            Assert.AreEqual(CombatConfig.ProjectileSpeed * CombatConfig.ProjectileLifetimeSeconds, replay.PositionAt(0).x, 0.05f);

            replay.Advance(1.0f + CombatConfig.ProjectileLifetimeSeconds + CombatConfig.ProjectileStaleMarginSeconds + 0.1f);
            Assert.AreEqual(0, replay.Count);
            Assert.AreEqual(0, replay.TrackedCount);
        }

        [Test]
        public void EventsForUnknownShotsAreIgnored()
        {
            var replay = new ProjectileReplay();

            Assert.DoesNotThrow(() => replay.OnHit(9, 1, 0, true, Vector2.zero, 1f));
            Assert.DoesNotThrow(() => replay.OnEnd(9, Vector2.zero, 1f));

            replay.Advance(2f);
            Assert.AreEqual(0, replay.TrackedCount);
        }

        [Test]
        public void ReusingAnIdReplacesTheOldShot()
        {
            var replay = new ProjectileReplay();
            replay.OnFire(1, 1, Vector2.zero, Angle(0), 1f);
            replay.OnFire(1, 1, new Vector2(0, 5), Angle(90), 3f);

            replay.Advance(3.1f);

            Assert.AreEqual(1, replay.Count);
            Assert.AreEqual(5f + CombatConfig.ProjectileSpeed * 0.1f, replay.PositionAt(0).y, 0.05f);
        }

        [Test]
        public void ReplayedHitsLineUpWithTheServerSimulation()
        {
            // The server hits a mob; the replay, fed the same events, ends at the same place and time.
            var mobs = new MobSim(targetCount: 0);
            mobs.TrySpawn(new Vector2(5, 0), 0, out ushort mobId);
            var sim = new ProjectileSim();
            var replay = new ProjectileReplay();
            var events = new List<CombatEvent>();

            float serverTime = 50f;
            sim.TryFire(1, Vector2.zero, Angle(0), out _);
            sim.DrainEvents(events);
            replay.OnFire(events[0].ProjectileId, 1, events[0].Position, events[0].Angle, serverTime);

            float hitTime = -1f;
            CombatEvent hit = default;
            for (int step = 0; step < 60 && hitTime < 0f; step++)
            {
                sim.Step(Dt, mobs);
                serverTime += Dt;
                events.Clear();
                sim.DrainEvents(events);
                foreach (CombatEvent e in events)
                {
                    if (e.Kind != CombatEventKind.Hit) continue;
                    hit = e;
                    hitTime = serverTime;
                    replay.OnHit(e.ProjectileId, e.MobId, e.MobHealthPercent, e.Killed, e.Position, serverTime);
                }
            }

            Assert.Greater(hitTime, 0f, "the server registered the hit");
            Assert.AreEqual(mobId, hit.MobId);

            replay.Advance(hitTime - 0.001f);
            Assert.AreEqual(1, replay.Count);
            Assert.AreEqual(hit.Position.x, replay.PositionAt(0).x, 0.05f, "on screen right at the mob when the hit happens");

            replay.Advance(hitTime);
            Assert.AreEqual(0, replay.Count);
        }

        [Test]
        public void Clear_ForgetsEverything()
        {
            var replay = new ProjectileReplay();
            replay.OnFire(1, 1, Vector2.zero, Angle(0), 1f);
            replay.Advance(1.1f);

            replay.Clear();

            Assert.AreEqual(0, replay.Count);
            Assert.AreEqual(0, replay.TrackedCount);
        }
    }
}
