using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class HostileProjectileTests
    {
        const float Dt = 0.02f;
        const ulong BossOwner = 1;

        sealed class Field
        {
            public readonly SurvivorSim Survivors = new SurvivorSim();
            public readonly List<SurvivorBody> Bodies = new List<SurvivorBody>();
            public readonly SurvivorTargets Targets;
            public readonly ProjectileSim Projectiles = new ProjectileSim();
            public MobSim Mobs = new MobSim(targetCount: 0) { MaxHealth = 1000 };
            public BossSim Boss;

            public Field() { Targets = new SurvivorTargets(Survivors, id => (byte)id); }

            /// <summary>Client ids double as slots here. Positions are fixed.</summary>
            public void Add(ulong id, float x, float y, int health = 100)
            {
                Survivors.Register(id, health);
                Bodies.Add(new SurvivorBody { ClientId = id, Position = new Vector2(x, y) });
                Targets.SetBodies(Bodies);
            }

            public void Fire(bool hostile, float speed = 12f, float lifetime = 2f, float radius = 0.3f, int damage = 15, float degrees = 0f, Vector2? origin = null)
            {
                Assert.IsTrue(Projectiles.TryFire(new ProjectileSpec
                {
                    Owner = BossOwner, Origin = origin ?? Vector2.zero, Damage = damage, Hostile = hostile,
                    Shape = new ProjectileShape { Speed = speed, Lifetime = lifetime, Radius = radius, Pierce = 1 },
                    Angle = AimMath.QuantizeAngle(degrees * Mathf.Deg2Rad),
                }, out _));
            }

            public List<CombatEvent> Run(float seconds)
            {
                var all = new List<CombatEvent>();
                int steps = Mathf.RoundToInt(seconds / Dt);
                for (int i = 0; i < steps; i++)
                {
                    Survivors.Step(Dt, Bodies, null);
                    Projectiles.Step(Dt, Mobs, Boss, Targets);
                    Projectiles.DrainEvents(all);
                }
                return all;
            }
        }

        static IEnumerable<CombatEvent> Hits(List<CombatEvent> events) => events.Where(e => e.Kind == CombatEventKind.Hit);

        [Test]
        public void AHostileShotHitsASurvivorAndEnds()
        {
            var f = new Field();
            f.Add(2, 5f, 0f);
            f.Fire(true);

            List<CombatEvent> events = f.Run(1f);

            CombatEvent hit = Hits(events).Single();
            Assert.IsTrue(hit.SurvivorTarget);
            Assert.AreEqual(2, hit.MobId, "the survivor's slot");
            Assert.AreEqual(85, hit.MobHealthPercent);
            Assert.IsFalse(hit.Killed);
            Assert.IsFalse(hit.Continues);
            Assert.AreEqual(85, f.Survivors.Health(2));
            Assert.AreEqual(0, f.Projectiles.Count);
            Assert.AreEqual(0, events.Count(e => e.Kind == CombatEventKind.End), "a hit ends it, there is no second ending");
        }

        [Test]
        public void AHitThatKills_SaysSo()
        {
            var f = new Field();
            f.Add(2, 5f, 0f, health: 10);
            f.Fire(true, damage: 15);

            CombatEvent hit = Hits(f.Run(1f)).Single();

            Assert.IsTrue(hit.Killed);
            Assert.AreEqual(0, hit.MobHealthPercent);
            Assert.IsFalse(f.Survivors.IsAlive(2));
        }

        [Test]
        public void AHostileShotPassesThroughMobsAndTheBoss()
        {
            var f = new Field();
            f.Boss = new BossSim(new BossDef { Name = "t", MaxHealth = 1000, MoveSpeed = 6f, Radius = 0.9f, ContactDamage = 25, MatchSeconds = 60f }, new Vector2(2f, 0f));
            Assert.IsTrue(f.Mobs.TrySpawn(new Vector2(3.5f, 0f), 0, out ushort mob));
            f.Add(2, 6f, 0f);
            f.Fire(true);

            List<CombatEvent> events = f.Run(1f);

            CombatEvent hit = Hits(events).Single();
            Assert.IsTrue(hit.SurvivorTarget);
            Assert.AreEqual(1000, f.Boss.Health, "the boss is not hurt by its own shot");
            Assert.IsTrue(f.Mobs.TryGetHealth(mob, out int mobHealth));
            Assert.AreEqual(1000, mobHealth, "mobs are not hurt either");
        }

        [Test]
        public void AFriendlyShotPassesThroughSurvivors()
        {
            var f = new Field();
            f.Add(2, 4f, 0f);
            f.Fire(false);

            List<CombatEvent> events = f.Run(1f);

            Assert.AreEqual(0, Hits(events).Count());
            Assert.AreEqual(100, f.Survivors.Health(2));
        }

        [Test]
        public void AProtectedSurvivorDoesNotStopTheShot()
        {
            var f = new Field();
            f.Add(2, 4f, 0f);
            f.Add(3, 8f, 0f);
            Assert.IsTrue(f.Survivors.ApplyDamage(2, 1), "survivor 2 is now within the protection time");
            f.Fire(true);

            List<CombatEvent> events = f.Run(1f);

            CombatEvent hit = Hits(events).Single();
            Assert.AreEqual(3, hit.MobId, "the shot went on to the next survivor");
            Assert.AreEqual(99, f.Survivors.Health(2));
        }

        [Test]
        public void AFallenSurvivorDoesNotStopTheShot()
        {
            var f = new Field();
            f.Add(2, 4f, 0f);
            f.Survivors.Kill(2);
            f.Fire(true);

            List<CombatEvent> events = f.Run(1.5f);

            Assert.AreEqual(0, Hits(events).Count());
            Assert.AreEqual(1, events.Count(e => e.Kind == CombatEventKind.End), "it just ends with its lifetime");
        }

        [Test]
        public void OnlyTheNearerOfTwoSurvivorsInLineTakesDamage()
        {
            var f = new Field();
            f.Add(2, 8f, 0f);
            f.Add(3, 4f, 0f);
            f.Fire(true);

            CombatEvent hit = Hits(f.Run(1f)).Single();

            Assert.AreEqual(3, hit.MobId);
            Assert.AreEqual(100, f.Survivors.Health(2));
            Assert.AreEqual(85, f.Survivors.Health(3));
        }

        [Test]
        public void AVeryFastShotStillHitsASurvivorOnItsPath()
        {
            var f = new Field();
            f.Add(2, 10f, 0f);
            f.Fire(true, speed: 300f, lifetime: 1f); // 6 units each step, far more than a survivor's width

            Assert.AreEqual(1, Hits(f.Run(0.5f)).Count());
            Assert.AreEqual(85, f.Survivors.Health(2));
        }

        [Test]
        public void AShotThatMissesFliesOnAndEndsAtTheArenaEdge()
        {
            var f = new Field();
            f.Add(2, 4f, 5f);
            f.Fire(true, speed: 30f, lifetime: 5f);

            List<CombatEvent> events = f.Run(2f);

            Assert.AreEqual(0, Hits(events).Count());
            Assert.AreEqual(1, events.Count(e => e.Kind == CombatEventKind.End));
            Assert.AreEqual(100, f.Survivors.Health(2));
        }

        [Test]
        public void AHostileHitStartsTheProtectionTime()
        {
            var f = new Field();
            f.Add(2, 5f, 0f);
            f.Fire(true);
            f.Run(0.5f);

            Assert.IsTrue(f.Survivors.IsInvulnerable(2), "the hit protects the survivor");
            Assert.IsFalse(f.Survivors.ApplyDamage(2, 10), "so a mob touching right afterwards deals nothing");
            Assert.AreEqual(85, f.Survivors.Health(2));
        }

        [Test]
        public void ARevivedSurvivorWithProtection_CannotBeHurt()
        {
            var f = new Field();
            f.Add(2, 5f, 0f);
            Assert.IsTrue(f.Survivors.GrantInvulnerability(2, 2f));
            f.Fire(true);

            Assert.AreEqual(0, Hits(f.Run(1f)).Count());
            Assert.AreEqual(100, f.Survivors.Health(2));
        }

        [Test]
        public void HostileAndFriendlyShotsDoNotCollide()
        {
            var f = new Field();
            f.Add(2, 8f, 0f);
            Assert.IsTrue(f.Mobs.TrySpawn(new Vector2(3f, 0f), 0, out ushort mob));
            f.Fire(true);                                       // from the boss toward the survivor
            f.Fire(false, origin: new Vector2(8f, 0f), degrees: 180f); // back along the same line toward the mob

            List<CombatEvent> events = f.Run(1f);

            Assert.AreEqual(2, Hits(events).Count());
            Assert.AreEqual(1, Hits(events).Count(e => e.SurvivorTarget));
            Assert.IsTrue(f.Mobs.TryGetHealth(mob, out int mobHealth));
            Assert.Less(mobHealth, 1000, "the friendly shot hurt the mob");
        }

        [Test]
        public void AHostileShotFromTheBossHurtsOnlyViaTheAttackSim()
        {
            var f = new Field();
            f.Add(2, 6f, 0f);
            var attack = new AttackSim();
            attack.Register(BossOwner, BossAttack.Def, true);
            attack.SetAim(BossOwner, 0, true);
            attack.SetModifiers(BossOwner, 20, 1f);
            var bodies = new List<ShooterBody> { new ShooterBody { ClientId = BossOwner, Position = Vector2.zero, CanFire = true } };

            attack.Step(Dt, bodies, f.Projectiles);
            f.Run(1f);

            Assert.AreEqual(80, f.Survivors.Health(2));
        }

        [Test]
        public void ASurvivorsOwnAttack_IsNotHostile()
        {
            var f = new Field();
            f.Add(2, 6f, 0f);
            var attack = new AttackSim();
            attack.Register(BossOwner, AttackDef.Default); // the plain survivor registration
            attack.SetAim(BossOwner, 0, true);
            var bodies = new List<ShooterBody> { new ShooterBody { ClientId = BossOwner, Position = Vector2.zero, CanFire = true } };

            attack.Step(Dt, bodies, f.Projectiles);
            f.Run(1f);

            Assert.AreEqual(100, f.Survivors.Health(2));
        }

        [Test]
        public void TheCapCountsHostileShotsToo()
        {
            var f = new Field();
            var small = new ProjectileSim(cap: 2);
            Assert.IsTrue(small.TryFire(new ProjectileSpec { Owner = BossOwner, Hostile = true, Damage = 1, Shape = ProjectileShape.Default }, out _));
            Assert.IsTrue(small.TryFire(new ProjectileSpec { Owner = 5, Damage = 1, Shape = ProjectileShape.Default }, out _));
            Assert.IsFalse(small.TryFire(new ProjectileSpec { Owner = BossOwner, Hostile = true, Damage = 1, Shape = ProjectileShape.Default }, out _));
            Assert.AreEqual(0, f.Projectiles.Count);
        }

        [Test]
        public void ASwappedProjectileKeepsItsHostileFlag()
        {
            // The first shot ends early, so the last projectile is moved into its slot; both must keep their own kind.
            var f = new Field();
            f.Add(2, 6f, 3f);
            f.Fire(false, speed: 30f, lifetime: 0.03f, degrees: 90f); // a friendly shot that ends at once
            f.Fire(true, degrees: 0f, origin: new Vector2(0f, 3f));   // a hostile shot toward the survivor

            List<CombatEvent> events = f.Run(1f);

            Assert.AreEqual(1, Hits(events).Count(e => e.SurvivorTarget));
            Assert.AreEqual(85, f.Survivors.Health(2));
        }

        // ---- The wire format ----

        [Test]
        public void TheSurvivorBitSurvivesTheWireAndTheSizeIsUnchanged()
        {
            var e = new CombatEvent
            {
                Kind = CombatEventKind.Hit, ProjectileId = 7, Position = new Vector2(1.5f, -2f),
                MobId = 3, MobHealthPercent = 40, Killed = true, Continues = false, SurvivorTarget = true,
            };
            var buffer = new byte[CombatCodec.MaxBatchBytes];
            int length = CombatCodec.WriteBatch(buffer, 1234, new List<CombatEvent> { e }, 0, 1, null);
            Assert.AreEqual(CombatCodec.HeaderSize + CombatCodec.HitSize, length);

            Assert.IsTrue(CombatCodec.TryReadHeader(buffer, length, out _, out int count));
            Assert.AreEqual(1, count);
            int offset = CombatCodec.HeaderSize;
            Assert.IsTrue(CombatCodec.TryReadEvent(buffer, length, ref offset, out CombatEvent read));

            Assert.IsTrue(read.SurvivorTarget);
            Assert.IsTrue(read.Killed);
            Assert.IsFalse(read.Continues);
            Assert.AreEqual(3, read.MobId);
            Assert.AreEqual(40, read.MobHealthPercent);
        }

        [Test]
        public void TheOtherHitFlagsAreIndependentOfTheSurvivorBit()
        {
            foreach (bool killed in new[] { false, true })
            foreach (bool continues in new[] { false, true })
            foreach (bool survivor in new[] { false, true })
            {
                var e = new CombatEvent
                {
                    Kind = CombatEventKind.Hit, ProjectileId = 1, MobId = 9, MobHealthPercent = 10,
                    Killed = killed, Continues = continues, SurvivorTarget = survivor,
                };
                var buffer = new byte[CombatCodec.MaxBatchBytes];
                int length = CombatCodec.WriteBatch(buffer, 0, new List<CombatEvent> { e }, 0, 1, null);
                int offset = CombatCodec.HeaderSize;
                Assert.IsTrue(CombatCodec.TryReadEvent(buffer, length, ref offset, out CombatEvent read));
                Assert.AreEqual(killed, read.Killed);
                Assert.AreEqual(continues, read.Continues);
                Assert.AreEqual(survivor, read.SurvivorTarget);
            }
        }

        [Test]
        public void AMobHitDoesNotCarryTheSurvivorBit()
        {
            var f = new Field();
            Assert.IsTrue(f.Mobs.TrySpawn(new Vector2(3f, 0f), 0, out _));
            f.Fire(false);

            CombatEvent hit = Hits(f.Run(1f)).Single();

            Assert.IsFalse(hit.SurvivorTarget);
        }
    }
}
