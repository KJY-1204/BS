using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class CharacterProjectileReplayTests
    {
        const float Dt = 0.02f;

        static ProjectileShape Shape(float speed, float lifetime, float radius = 0.15f, int pierce = 1) =>
            new ProjectileShape { Speed = speed, Lifetime = lifetime, Radius = radius, Pierce = pierce };

        static ProjectileReplay Replay(params (byte slot, ProjectileShape shape)[] map)
        {
            var dictionary = map.ToDictionary(m => m.slot, m => m.shape);
            return new ProjectileReplay
            {
                ShapeOfSlot = slot => dictionary.TryGetValue(slot, out ProjectileShape s) ? s : ProjectileShape.Default,
            };
        }

        static Vector2 PositionOf(ProjectileReplay replay, ushort id)
        {
            for (int i = 0; i < replay.Count; i++)
                if (replay.IdAt(i) == id) return replay.PositionAt(i);
            Assert.Fail($"projectile {id} is not on screen");
            return default;
        }

        static bool IsVisible(ProjectileReplay replay, ushort id)
        {
            for (int i = 0; i < replay.Count; i++) if (replay.IdAt(i) == id) return true;
            return false;
        }

        [Test]
        public void ShotsOfDifferentCharactersFlyAtTheirOwnSpeed()
        {
            ProjectileReplay replay = Replay((1, Shape(7f, 1.6f)), (2, Shape(18f, 0.45f)));
            replay.OnFire(1, 1, new Vector2(0, 0), 0, 0f);
            replay.OnFire(2, 2, new Vector2(0, 5), 0, 0f);

            replay.Advance(0.3f);

            Assert.AreEqual(7f * 0.3f, PositionOf(replay, 1).x, 1e-3f);
            Assert.AreEqual(18f * 0.3f, PositionOf(replay, 2).x, 1e-3f);
        }

        [Test]
        public void TheShapeOfTheShootersCharacterIsKeptOnTheTrack()
        {
            ProjectileReplay replay = Replay((3, Shape(10f, 1f, radius: 0.45f)));
            replay.OnFire(5, 3, Vector2.zero, 0, 0f);
            replay.Advance(0.1f);

            Assert.AreEqual(0.45f, replay.ShapeAt(0).Radius);
            Assert.AreEqual(3, replay.SlotAt(0));
        }

        [Test]
        public void AnUnknownShooterIsDrawnWithTheDefaultShape()
        {
            ProjectileReplay replay = Replay();
            replay.OnFire(1, 4, Vector2.zero, 0, 0f);

            replay.Advance(0.1f);

            Assert.AreEqual(CombatConfig.ProjectileSpeed * 0.1f, PositionOf(replay, 1).x, 1e-3f);
        }

        [Test]
        public void WithNoLookupTheOldFixedShapeIsUsed()
        {
            var replay = new ProjectileReplay();
            replay.OnFire(1, 1, Vector2.zero, 0, 0f);
            replay.Advance(0.1f);
            Assert.AreEqual(CombatConfig.ProjectileSpeed * 0.1f, PositionOf(replay, 1).x, 1e-3f);
        }

        // ---- Piercing ----

        [Test]
        public void APiercingHitShowsItsEffectAtItsTimeAndTheShotKeepsFlying()
        {
            ProjectileReplay replay = Replay((1, Shape(10f, 1f, pierce: 3)));
            replay.OnFire(1, 1, Vector2.zero, 0, 0f);
            replay.OnHit(1, 77, 60, false, new Vector2(2, 0), 0.2f, continues: true);

            var effects = new List<HitEffect>();
            replay.Advance(0.1f);
            replay.DrainDueEffects(effects);
            Assert.AreEqual(0, effects.Count, "not yet");

            replay.Advance(0.25f);
            replay.DrainDueEffects(effects);
            Assert.AreEqual(1, effects.Count);
            Assert.AreEqual(77, effects[0].MobId);
            Assert.AreEqual(60, effects[0].HealthPercent);
            Assert.IsTrue(IsVisible(replay, 1), "the shot is still flying");
            Assert.AreEqual(2.5f, PositionOf(replay, 1).x, 1e-3f);
        }

        [Test]
        public void TheLastHitEndsTheShotAndGivesItsEffectAtTheEndTime()
        {
            ProjectileReplay replay = Replay((1, Shape(10f, 1f, pierce: 2)));
            replay.OnFire(1, 1, Vector2.zero, 0, 0f);
            replay.OnHit(1, 1, 50, false, new Vector2(2, 0), 0.2f, continues: true);
            replay.OnHit(1, 2, 20, true, new Vector2(4, 0), 0.4f);

            var effects = new List<HitEffect>();
            replay.Advance(0.3f);
            replay.DrainDueEffects(effects);
            Assert.AreEqual(1, effects.Count, "the first hit only");
            Assert.IsTrue(IsVisible(replay, 1));

            replay.Advance(0.45f);
            replay.DrainDueEffects(effects);
            Assert.AreEqual(2, effects.Count);
            Assert.IsTrue(effects[1].Killed);
            Assert.IsFalse(IsVisible(replay, 1), "gone after the last hit");
        }

        [Test]
        public void APiercingShotThatPassedMobsStillEndsWithAnEndEvent()
        {
            ProjectileReplay replay = Replay((1, Shape(10f, 1f, pierce: 3)));
            replay.OnFire(1, 1, Vector2.zero, 0, 0f);
            replay.OnHit(1, 1, 50, false, new Vector2(2, 0), 0.2f, continues: true);
            replay.OnEnd(1, new Vector2(10, 0), 1f);

            var effects = new List<HitEffect>();
            replay.Advance(0.5f);
            Assert.IsTrue(IsVisible(replay, 1));
            replay.Advance(1.05f);
            replay.DrainDueEffects(effects);

            Assert.IsFalse(IsVisible(replay, 1));
            Assert.AreEqual(1, effects.Count, "the end event adds no effect of its own");
        }

        [Test]
        public void AContinuingHitForAnUnknownShotIsIgnored()
        {
            ProjectileReplay replay = Replay();
            replay.OnHit(99, 1, 50, false, Vector2.zero, 0.1f, continues: true);
            var effects = new List<HitEffect>();
            replay.Advance(1f);
            replay.DrainDueEffects(effects);
            Assert.AreEqual(0, effects.Count);
        }

        [Test]
        public void TheLifetimeSafetyNetFollowsEachShapesOwnLifetime()
        {
            ProjectileReplay replay = Replay((1, Shape(18f, 0.45f)), (2, Shape(7f, 1.6f)));
            replay.OnFire(1, 1, Vector2.zero, 0, 0f);
            replay.OnFire(2, 2, new Vector2(0, 5), 0, 0f);

            replay.Advance(0.45f + replay.StaleMargin + 0.1f);

            Assert.IsFalse(IsVisible(replay, 1), "the short-lived one is removed");
            Assert.IsTrue(IsVisible(replay, 2), "the long-lived one is still flying");
        }

        [Test]
        public void ClearForgetsWaitingEffectsToo()
        {
            ProjectileReplay replay = Replay((1, Shape(10f, 1f, pierce: 3)));
            replay.OnFire(1, 1, Vector2.zero, 0, 0f);
            replay.OnHit(1, 1, 50, false, new Vector2(2, 0), 0.2f, continues: true);

            replay.Clear();
            var effects = new List<HitEffect>();
            replay.Advance(1f);
            replay.DrainDueEffects(effects);

            Assert.AreEqual(0, effects.Count);
        }

        [Test]
        public void WaitingEffectsAreCapped()
        {
            ProjectileReplay replay = Replay((1, Shape(10f, 1f, pierce: 8)));
            replay.OnFire(1, 1, Vector2.zero, 0, 0f);
            for (int i = 0; i < ProjectileReplay.MaxPendingEffects + 50; i++)
                replay.OnHit(1, (ushort)i, 50, false, Vector2.zero, 100f, continues: true);

            var effects = new List<HitEffect>();
            replay.Advance(200f);
            replay.DrainDueEffects(effects);

            Assert.AreEqual(ProjectileReplay.MaxPendingEffects, effects.Count);
        }

        // ---- The client replay against the server simulation, for the four real characters ----

        [Test]
        public void EveryCharactersAttackIsReplayedWithinHalfAUnitOfTheServer()
        {
            var rng = new System.Random(21);
            foreach (CharacterDef character in CharacterCatalog.All)
            {
                byte slot = (byte)character.Id;
                var attack = new AttackSim();
                attack.Register(1, character.Attack);
                attack.SetModifiers(1, 10, 1f);
                var projectiles = new ProjectileSim();
                var mobs = new MobSim(targetCount: 0) { MaxHealth = 100000 };
                for (int i = 0; i < 40; i++)
                    mobs.TrySpawn(new Vector2((float)(rng.NextDouble() * 24 - 12), (float)(rng.NextDouble() * 14 - 7)), 0, out _);

                ProjectileReplay replay = Replay((slot, character.Attack.Shape));
                var shooters = new List<ShooterBody> { new ShooterBody { ClientId = 1, Position = new Vector2(-10, 0), CanFire = true } };

                float maxError = 0f;
                int hits = 0, ends = 0, effectsShown = 0, fires = 0;
                int steps = Mathf.RoundToInt(4f / Dt);
                for (int step = 1; step <= steps; step++)
                {
                    attack.SetAim(1, AimMath.QuantizeAngle(0.4f * Mathf.Sin(step * 0.02f)), step < steps - 60);
                    float time = step * Dt;
                    attack.Step(Dt, shooters, projectiles);
                    projectiles.Step(Dt, mobs);

                    var events = new List<CombatEvent>();
                    projectiles.DrainEvents(events);
                    foreach (CombatEvent e in events)
                    {
                        switch (e.Kind)
                        {
                            case CombatEventKind.Fire: fires++; replay.OnFire(e.ProjectileId, slot, e.Position, e.Angle, time); break;
                            case CombatEventKind.Hit: hits++; replay.OnHit(e.ProjectileId, e.MobId, e.MobHealthPercent, e.Killed, e.Position, time, e.Continues); break;
                            case CombatEventKind.End: ends++; replay.OnEnd(e.ProjectileId, e.Position, time); break;
                        }
                    }

                    replay.Advance(time);
                    var effects = new List<HitEffect>();
                    replay.DrainDueEffects(effects);
                    effectsShown += effects.Count;

                    for (int i = 0; i < projectiles.Count; i++)
                    {
                        ushort id = projectiles.IdAt(i);
                        for (int v = 0; v < replay.Count; v++)
                        {
                            if (replay.IdAt(v) != id) continue;
                            maxError = Mathf.Max(maxError, Vector2.Distance(replay.PositionAt(v), projectiles.PositionAt(i)));
                        }
                    }
                }

                Assert.Greater(fires, 0, character.Name);
                Assert.LessOrEqual(maxError, 0.5f, $"{character.Name}: client within half a unit of the server");
                Assert.AreEqual(hits, effectsShown, $"{character.Name}: every hit shows its effect exactly once");
                replay.Advance(100f);
                Assert.AreEqual(0, replay.Count, $"{character.Name}: nothing is left on screen");
            }
        }

        [Test]
        public void MarisaPassingThroughMobsShowsOneEffectPerMobPerShot()
        {
            CharacterDef marisa = CharacterCatalog.Get(CharacterIds.Marisa);
            var attack = new AttackSim();
            attack.Register(1, marisa.Attack);
            attack.SetAim(1, 0, true);
            var projectiles = new ProjectileSim();
            var mobs = new MobSim(targetCount: 0) { MaxHealth = 100000 };
            for (int i = 0; i < 4; i++) mobs.TrySpawn(new Vector2(-8 + 3 * i, 0), 0, out _);
            ProjectileReplay replay = Replay((2, marisa.Attack.Shape));
            var shooters = new List<ShooterBody> { new ShooterBody { ClientId = 1, Position = new Vector2(-12, 0), CanFire = true } };

            attack.Step(Dt, shooters, projectiles);
            attack.SetAim(1, 0, false);
            var all = new List<CombatEvent>();
            for (int step = 1; step <= 100; step++)
            {
                projectiles.Step(Dt, mobs);
                projectiles.DrainEvents(all);
            }

            var hits = all.Where(e => e.Kind == CombatEventKind.Hit).ToList();
            Assert.AreEqual(4, hits.Count, "one shot, four mobs");
            Assert.AreEqual(3, hits.Count(h => h.Continues));
            Assert.IsFalse(hits.Last().Continues, "the fourth allowed hit stops it");
            Assert.AreEqual(4, hits.Select(h => h.MobId).Distinct().Count());
        }
    }
}
