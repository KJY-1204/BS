using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class CharacterAttackSimTests
    {
        const float Dt = 0.02f;
        const ulong A = 1, B = 2;

        static List<ShooterBody> Shooters(params ulong[] ids) =>
            ids.Select(id => new ShooterBody { ClientId = id, Position = Vector2.zero, CanFire = true }).ToList();

        static AttackDef Attack(float cooldown = 0.25f, int shots = 1, float spread = 0f, float multiplier = 1f, int pierce = 1) =>
            new AttackDef
            {
                CooldownSeconds = cooldown, Shots = shots, SpreadDegrees = spread, DamageMultiplier = multiplier,
                Shape = new ProjectileShape { Speed = 14f, Lifetime = 0.9f, Radius = 0.15f, Pierce = pierce },
            };

        /// <summary>Runs the attack and returns every Fire event made.</summary>
        static List<CombatEvent> Fires(AttackSim attack, ProjectileSim projectiles, List<ShooterBody> bodies, float seconds)
        {
            var fires = new List<CombatEvent>();
            var events = new List<CombatEvent>();
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                attack.Step(Dt, bodies, projectiles);
                projectiles.Step(Dt, null);
                events.Clear();
                projectiles.DrainEvents(events);
                fires.AddRange(events.Where(e => e.Kind == CombatEventKind.Fire));
            }
            return fires;
        }

        static int Degrees(ushort angle) => Mathf.RoundToInt(angle / 65536f * 360f) % 360;

        [Test]
        public void EachCharacterFiresAtItsOwnInterval()
        {
            var attack = new AttackSim();
            attack.Register(A, Attack(cooldown: 0.1f));
            attack.Register(B, Attack(cooldown: 0.5f));
            attack.SetAim(A, 0, true);
            attack.SetAim(B, 0, true);

            List<CombatEvent> fires = Fires(attack, new ProjectileSim(), Shooters(A, B), 1f);

            Assert.AreEqual(10, fires.Count(f => f.Owner == A));
            Assert.AreEqual(2, fires.Count(f => f.Owner == B));
        }

        [Test]
        public void OneAttackFiresAllItsShotsTogetherAndStartsOneCooldown()
        {
            var attack = new AttackSim();
            attack.Register(A, Attack(cooldown: 0.5f, shots: 5, spread: 40f));
            attack.SetAim(A, AimMath.QuantizeAngle(Mathf.PI / 2f), true);

            var projectiles = new ProjectileSim();
            attack.Step(Dt, Shooters(A), projectiles);
            var events = new List<CombatEvent>();
            projectiles.DrainEvents(events);

            Assert.AreEqual(5, events.Count(e => e.Kind == CombatEventKind.Fire), "five shots at once");
            CollectionAssert.AreEqual(new[] { 70, 80, 90, 100, 110 }, events.Select(e => Degrees(e.Angle)).ToArray());

            var more = Fires(attack, projectiles, Shooters(A), 0.46f);
            Assert.AreEqual(0, more.Count, "nothing more until the cooldown ends");
            Assert.AreEqual(5, Fires(attack, projectiles, Shooters(A), 0.1f).Count, "then the next five");
        }

        [Test]
        public void ASingleShotIgnoresTheSpread()
        {
            var attack = new AttackSim();
            attack.Register(A, Attack(shots: 1, spread: 60f));
            attack.SetAim(A, 1000, true);

            List<CombatEvent> fires = Fires(attack, new ProjectileSim(), Shooters(A), 0.1f);

            Assert.AreEqual(1000, fires[0].Angle);
        }

        [Test]
        public void ShotsPerSecondFollowIntervalTimesShots()
        {
            var attack = new AttackSim();
            attack.Register(A, Attack(cooldown: 0.5f, shots: 5, spread: 40f));
            attack.SetAim(A, 0, true);

            Assert.AreEqual(10, Fires(attack, new ProjectileSim(), Shooters(A), 1f).Count, "two attacks of five in a second");
        }

        [Test]
        public void TheDamageMultiplierIsRoundedWithAMinimumOfOne()
        {
            var attack = new AttackSim();
            attack.Register(A, Attack(multiplier: 1.4f));
            attack.Register(B, Attack(multiplier: 0.05f));
            attack.SetModifiers(A, 10, 1f);
            attack.SetModifiers(B, 10, 1f);
            attack.SetAim(A, 0, true);
            attack.SetAim(B, 0, true);

            var projectiles = new ProjectileSim();
            var mobs = new MobSim(targetCount: 0) { MaxHealth = 1000 };
            mobs.TrySpawn(new Vector2(3, 0), 0, out ushort target);
            attack.Step(Dt, new List<ShooterBody> { new ShooterBody { ClientId = A, Position = new Vector2(0, 0.05f), CanFire = true } }, projectiles);
            for (int i = 0; i < 20; i++) projectiles.Step(Dt, mobs);
            mobs.TryGetHealth(target, out int afterA);
            Assert.AreEqual(1000 - 14, afterA, "10 x 1.4");

            projectiles.Clear();
            attack.Step(Dt, new List<ShooterBody> { new ShooterBody { ClientId = B, Position = new Vector2(0, 0.05f), CanFire = true } }, projectiles);
            for (int i = 0; i < 20; i++) projectiles.Step(Dt, mobs);
            mobs.TryGetHealth(target, out int afterB);
            Assert.AreEqual(afterA - 1, afterB, "10 x 0.05 rounds to zero, so the minimum of 1");
        }

        [Test]
        public void GrowthDamageIsScaledByTheMultiplier()
        {
            var attack = new AttackSim();
            attack.Register(A, Attack(multiplier: 2f));
            attack.SetAim(A, 0, true);
            var mobs = new MobSim(targetCount: 0) { MaxHealth = 1000 };
            mobs.TrySpawn(new Vector2(3, 0), 0, out ushort target);
            var projectiles = new ProjectileSim();

            attack.SetModifiers(A, 13, 1f);
            attack.Step(Dt, Shooters(A), projectiles);
            for (int i = 0; i < 20; i++) projectiles.Step(Dt, mobs);

            mobs.TryGetHealth(target, out int health);
            Assert.AreEqual(1000 - 26, health);
        }

        [Test]
        public void ACooldownReductionShortensTheIntervalButNotTheShotCount()
        {
            var plain = new AttackSim();
            plain.Register(A, Attack(cooldown: 0.5f, shots: 5, spread: 40f));
            plain.SetAim(A, 0, true);
            var hasted = new AttackSim();
            hasted.Register(A, Attack(cooldown: 0.5f, shots: 5, spread: 40f));
            hasted.SetModifiers(A, 10, 0.8f);
            hasted.SetAim(A, 0, true);

            int normal = Fires(plain, new ProjectileSim(), Shooters(A), 4f).Count;
            int faster = Fires(hasted, new ProjectileSim(), Shooters(A), 4f).Count;

            Assert.AreEqual(40, normal);
            Assert.AreEqual(50, faster, "25 percent more attacks, five shots each");
            Assert.AreEqual(0, faster % 5);
        }

        [Test]
        public void WhenTheCapLeavesRoomForSomeShots_ThoseFireAndTheCooldownStarts()
        {
            var attack = new AttackSim();
            attack.Register(A, Attack(cooldown: 0.5f, shots: 5, spread: 40f));
            attack.SetAim(A, 0, true);
            var projectiles = new ProjectileSim(3);

            attack.Step(Dt, Shooters(A), projectiles);

            Assert.AreEqual(3, projectiles.Count, "only three fit");
            Assert.Greater(attack.CooldownRemaining(A), 0.4f, "the cooldown started");
        }

        [Test]
        public void WhenNoShotFits_TheCooldownStaysFree()
        {
            var attack = new AttackSim();
            attack.Register(A, Attack(shots: 3, spread: 20f));
            attack.SetAim(A, 0, true);
            var projectiles = new ProjectileSim(2);
            projectiles.TryFire(9, Vector2.zero, 0, out _);
            projectiles.TryFire(9, Vector2.zero, 0, out _);

            attack.Step(Dt, Shooters(A), projectiles);

            Assert.AreEqual(0f, attack.CooldownRemaining(A));
        }

        [Test]
        public void TheProjectilesCarryTheCharactersShape()
        {
            var attack = new AttackSim();
            AttackDef def = Attack(pierce: 3);
            def.Shape.Speed = 7f;
            attack.Register(A, def);
            attack.SetAim(A, 0, true);
            var projectiles = new ProjectileSim();

            attack.Step(Dt, Shooters(A), projectiles);
            projectiles.Step(0.5f, null);

            Assert.AreEqual(3.5f, projectiles.PositionAt(0).x, 1e-3f, "it flies at the character's speed");
        }

        [Test]
        public void AimOfReportsTheLatestAim()
        {
            var attack = new AttackSim();
            attack.Register(A);
            attack.SetAim(A, 4242, false);
            Assert.AreEqual(4242, attack.AimOf(A));
            Assert.AreEqual(0, attack.AimOf(99));
        }

        [Test]
        public void EveryCatalogCharacterFiresItsOwnPatternThroughTheSim()
        {
            foreach (CharacterDef c in CharacterCatalog.All)
            {
                var attack = new AttackSim();
                attack.Register(A, c.Attack);
                attack.SetAim(A, 0, true);
                var projectiles = new ProjectileSim();

                attack.Step(Dt, Shooters(A), projectiles);

                Assert.AreEqual(c.Attack.Shots, projectiles.Count, c.Name);
            }
        }
    }
}
