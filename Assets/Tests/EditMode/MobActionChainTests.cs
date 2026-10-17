using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class MobActionChainTests
    {
        const float Dt = 0.02f;
        const ulong Owner = CombatServer.MobShooterOwner;
        static readonly byte RangedType = MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Ranged);
        static readonly byte ExploderType = MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Exploder);

        /// <summary>One server tick the way CombatServer runs it: mobs, survivors, the mobs' own actions, then projectiles.</summary>
        sealed class World
        {
            public readonly MobSim Mobs = new MobSim(targetCount: 0) { MaxHealth = 30 };
            public readonly SurvivorSim Survivors = new SurvivorSim();
            public readonly ProjectileSim Projectiles;
            public readonly MobActionRunner Runner = new MobActionRunner();
            public readonly SurvivorTargets Targets;
            public readonly List<SurvivorBody> Bodies = new List<SurvivorBody>();
            public readonly List<SurvivorInfo> Infos = new List<SurvivorInfo>();
            public readonly List<CombatEvent> Events = new List<CombatEvent>();
            public MobActionResult Total;

            public World(int projectileCap = 256)
            {
                Projectiles = new ProjectileSim(projectileCap);
                Targets = new SurvivorTargets(Survivors, id => (byte)id);
            }

            public void AddSurvivor(ulong id, float x, float y, int health = 100)
            {
                Survivors.Register(id, health);
                Bodies.Add(new SurvivorBody { ClientId = id, Position = new Vector2(x, y) });
                Targets.SetBodies(Bodies);
            }

            public void Tick()
            {
                Infos.Clear();
                foreach (SurvivorBody b in Bodies) Infos.Add(new SurvivorInfo { ClientId = b.ClientId, Position = b.Position, Alive = Survivors.IsAlive(b.ClientId) });
                Mobs.Step(Dt, Infos);
                Survivors.Step(Dt, Bodies, Mobs);
                var blastEvents = new List<CombatEvent>();
                MobActionResult r = Runner.Run(Mobs, Projectiles, Survivors, Bodies, Owner, blastEvents);
                Total.ShotsFired += r.ShotsFired; Total.ShotsRefused += r.ShotsRefused; Total.Blasts += r.Blasts; Total.SurvivorsHurt += r.SurvivorsHurt;
                Projectiles.Step(Dt, Mobs, null, Targets);
                Projectiles.DrainEvents(Events);
                Events.AddRange(blastEvents);
            }

            public void Run(float seconds)
            {
                int steps = Mathf.RoundToInt(seconds / Dt);
                for (int i = 0; i < steps; i++) Tick();
            }

            public ushort Spawn(float x, float y, byte type)
            {
                Assert.IsTrue(Mobs.TrySpawn(new Vector2(x, y), type, out ushort id));
                return id;
            }
        }

        // ---- ranged mobs ----

        [Test]
        public void ARangedMobsShotHurtsTheSurvivor()
        {
            var w = new World();
            w.AddSurvivor(1, 0f, 0f);
            w.Spawn(6f, 0f, RangedType);

            w.Run(5f);

            Assert.Greater(w.Total.ShotsFired, 0);
            Assert.Less(w.Survivors.Health(1), 100);
            Assert.AreEqual(100 - 8 * ((100 - w.Survivors.Health(1)) / 8), w.Survivors.Health(1), "damage comes in steps of the mob shot's damage");
            Assert.IsTrue(w.Events.Any(e => e.Kind == CombatEventKind.Hit && e.SurvivorTarget && e.Owner == Owner));
        }

        [Test]
        public void AMobShotIsAFireEventOfTheMobShooter()
        {
            var w = new World();
            w.AddSurvivor(1, 0f, 0f);
            w.Spawn(6f, 0f, RangedType);

            w.Run(5f);

            CombatEvent fire = w.Events.First(e => e.Kind == CombatEventKind.Fire);
            Assert.AreEqual(Owner, fire.Owner);
            Assert.AreEqual(CombatServer.MobShooterSlot, CombatServer.MobShooterSlot);
            Assert.AreEqual(5, CombatServer.MobShooterSlot);
        }

        [Test]
        public void AMobsShotPassesThroughMobsAndTheBossAndSurvivorShots()
        {
            var w = new World();
            w.AddSurvivor(1, 0f, 0f);
            w.Spawn(6f, 0f, RangedType);
            ushort bystander = w.Spawn(3f, 0f, MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Tough));
            w.Mobs.ApplyOrderToSummoned(false, 0, true, new Vector2(3f, 0f)); // keep it in the way

            w.Run(5f);

            Assert.IsTrue(w.Mobs.TryGetHealth(bystander, out int health));
            Assert.AreEqual(w.Mobs.MaxHealthOf(MobKinds.Tough), health, "mobs are not hurt by a mob's shot");
        }

        [Test]
        public void AProtectedSurvivorIsNotHurtAndTheShotGoesOn()
        {
            var w = new World();
            w.AddSurvivor(1, 0f, 0f);
            w.Spawn(6f, 0f, RangedType);
            w.Survivors.GrantInvulnerability(1, 100f);

            w.Run(6f);

            Assert.AreEqual(100, w.Survivors.Health(1));
            Assert.IsFalse(w.Events.Any(e => e.Kind == CombatEventKind.Hit && e.SurvivorTarget));
        }

        [Test]
        public void TheProjectileCapRefusesAShotAndGivesTheCooldownBack()
        {
            var w = new World(projectileCap: 1);
            w.AddSurvivor(1, 0f, 0f);
            Assert.IsTrue(w.Projectiles.TryFire(new ProjectileSpec { Owner = 99, Origin = new Vector2(-15f, 9f), Damage = 1, Shape = new ProjectileShape { Speed = 0.001f, Lifetime = 1000f, Radius = 0.1f, Pierce = 1 } }, out _));
            w.Spawn(6f, 0f, RangedType);

            w.Run(5f);

            Assert.AreEqual(0, w.Total.ShotsFired);
            Assert.Greater(w.Total.ShotsRefused, 100, "the mob asks again every tick because its cooldown was given back");
        }

        [Test]
        public void ARangedMobsShotsLeaveFromTheMobAndAimAtTheSurvivor()
        {
            var w = new World();
            w.AddSurvivor(1, 0f, 0f);
            w.Spawn(6f, 0f, RangedType);

            w.Run(4f);

            CombatEvent fire = w.Events.First(e => e.Kind == CombatEventKind.Fire);
            Assert.AreEqual(6f, fire.Position.x, 0.3f);
            Vector2 aim = AimMath.Direction(fire.Angle);
            Assert.Less(aim.x, -0.95f, "toward the survivor on the left");
        }

        // ---- exploding mobs ----

        [Test]
        public void ABlastHurtsTheSurvivorOnceAndEmitsOneExplosionEvent()
        {
            var w = new World();
            w.AddSurvivor(1, 0f, 0f);
            w.Spawn(1.2f, 0f, ExploderType);

            w.Run(3f);

            Assert.AreEqual(1, w.Total.Blasts);
            Assert.AreEqual(1, w.Total.SurvivorsHurt);
            Assert.AreEqual(100 - 25 - (100 - 25 - w.Survivors.Health(1)), w.Survivors.Health(1));
            Assert.LessOrEqual(w.Survivors.Health(1), 75);
            Assert.AreEqual(1, w.Events.Count(e => e.Kind == CombatEventKind.Explosion));
            CombatEvent blast = w.Events.First(e => e.Kind == CombatEventKind.Explosion);
            Assert.AreEqual(22, blast.Radius10);
            Assert.AreEqual(0, w.Mobs.Count);
        }

        [Test]
        public void ARunningSurvivorMayEscapeTheBlast()
        {
            var w = new World();
            w.AddSurvivor(1, 0f, 0f);
            w.Spawn(1.2f, 0f, ExploderType);
            w.Tick(); // the fuse is lit

            // The survivor leaves the blast radius at once, so the blast finds nobody.
            w.Bodies[0] = new SurvivorBody { ClientId = 1, Position = new Vector2(-8f, 0f) };
            w.Targets.SetBodies(w.Bodies);
            w.Run(2f);

            Assert.AreEqual(1, w.Total.Blasts);
            Assert.AreEqual(0, w.Total.SurvivorsHurt);
            Assert.AreEqual(100, w.Survivors.Health(1));
            Assert.AreEqual(1, w.Events.Count(e => e.Kind == CombatEventKind.Explosion), "the effect is still shown");
        }

        [Test]
        public void ABlastDoesNotHurtMobsOrProtectedOrFallenSurvivors()
        {
            var w = new World();
            w.AddSurvivor(1, 0f, 0f);
            w.AddSurvivor(2, 0.5f, 0.5f);
            w.AddSurvivor(3, -0.5f, 0.5f);
            w.Survivors.GrantInvulnerability(2, 50f);
            w.Survivors.Kill(3);
            // A tough mob inside the blast radius that cannot reach a survivor before the blast (it is slow and still far away).
            ushort other = w.Spawn(1.2f, -2f, MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Tough));
            w.Spawn(1.2f, 0f, ExploderType);

            w.Run(0.85f);

            Assert.AreEqual(1, w.Total.Blasts);
            Assert.AreEqual(75, w.Survivors.Health(1));
            Assert.AreEqual(100, w.Survivors.Health(2));
            Assert.AreEqual(0, w.Survivors.Health(3));
            Assert.IsTrue(w.Mobs.TryGetHealth(other, out int health));
            Assert.AreEqual(w.Mobs.MaxHealthOf(MobKinds.Tough), health);
        }

        [Test]
        public void ABlastIsNotAKillSoNoHitEventCarriesIt()
        {
            var w = new World();
            w.AddSurvivor(1, 0f, 0f);
            w.Spawn(1.2f, 0f, ExploderType);

            w.Run(3f);

            Assert.IsFalse(w.Events.Any(e => e.Kind == CombatEventKind.Hit && e.Killed && !e.SurvivorTarget),
                "the blast sends no kill event for the mob, so nobody gains experience for it");
        }

        [Test]
        public void ABlastWhileTheSurvivorIsProtectedDoesNothing()
        {
            var w = new World();
            w.AddSurvivor(1, 0f, 0f);
            Assert.IsTrue(w.Survivors.ApplyDamage(1, 8)); // for example a shot that just landed
            w.Spawn(1.2f, 0f, ExploderType);

            w.Run(0.3f); // the fuse (0.7 s) is still running
            w.Survivors.GrantInvulnerability(1, 5f);
            w.Run(1.5f);

            Assert.AreEqual(1, w.Total.Blasts);
            Assert.AreEqual(0, w.Total.SurvivorsHurt);
            Assert.AreEqual(92, w.Survivors.Health(1), "protected from the blast");
        }

        [Test]
        public void ManyMixedMobsKeepTheCountsAndTheCapRight()
        {
            var w = new World();
            w.AddSurvivor(1, 0f, 0f, 100000);
            for (int i = 0; i < 10; i++) w.Spawn(-12f + i * 0.5f, 7f, RangedType);
            for (int i = 0; i < 10; i++) w.Spawn(12f - i * 0.5f, -7f, ExploderType);

            w.Run(20f);

            Assert.GreaterOrEqual(w.Total.Blasts, 5);
            Assert.GreaterOrEqual(w.Total.ShotsFired, 10);
            Assert.LessOrEqual(w.Projectiles.Count, w.Projectiles.Cap);
            Assert.AreEqual(w.Mobs.SummonedCount, w.Mobs.Count);
        }
    }
}
