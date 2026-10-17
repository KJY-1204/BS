using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class BossAttackTests
    {
        const float Dt = 0.02f;
        const ulong Boss = 1;

        // ---- Enrage: the strengthening follows the boss's health ----

        [Test]
        public void FullHealth_UsesTheNormalCooldownAndDamage()
        {
            Assert.AreEqual(1f, BossAttack.CooldownMultiplier(1f), 1e-5f);
            Assert.AreEqual(BossAttack.BaseDamage, BossAttack.Damage(BossAttack.BaseDamage, 1f));
        }

        [Test]
        public void ZeroHealth_UsesTheLowestCooldownAndTheStrongestDamage()
        {
            Assert.AreEqual(BossAttack.MinCooldownFraction, BossAttack.CooldownMultiplier(0f), 1e-5f);
            Assert.AreEqual(Mathf.RoundToInt(20 * BossAttack.MaxDamageMultiple), BossAttack.Damage(20, 0f));
        }

        [Test]
        public void HalfHealth_IsHalfwayBetweenNormalAndStrongest()
        {
            float expectedCooldown = (1f + BossAttack.MinCooldownFraction) * 0.5f;
            Assert.AreEqual(expectedCooldown, BossAttack.CooldownMultiplier(0.5f), 1e-5f);

            float expectedDamage = 20f * (1f + BossAttack.MaxDamageMultiple) * 0.5f;
            Assert.AreEqual(Mathf.RoundToInt(expectedDamage), BossAttack.Damage(20, 0.5f));
        }

        [Test]
        public void NearlyFallen_IsCloseToTheLimitsAndNeverPastThem()
        {
            float fraction = 1f / 6000f;
            float cooldown = BossAttack.CooldownMultiplier(fraction);
            Assert.GreaterOrEqual(cooldown, BossAttack.MinCooldownFraction);
            Assert.AreEqual(BossAttack.MinCooldownFraction, cooldown, 0.001f);

            int damage = BossAttack.Damage(20, fraction);
            Assert.LessOrEqual(damage, Mathf.CeilToInt(20 * BossAttack.MaxDamageMultiple));
            Assert.GreaterOrEqual(damage, 29);
        }

        [Test]
        public void ACooldownMultiplierNeverGrowsAsHealthRises()
        {
            float previous = 0f;
            for (int i = 0; i <= 100; i++)
            {
                float m = BossAttack.CooldownMultiplier(i / 100f);
                Assert.GreaterOrEqual(m, previous - 1e-6f);
                previous = m;
            }
        }

        [Test]
        public void DamageNeverFallsAsHealthDrops()
        {
            int previous = int.MaxValue;
            for (int i = 0; i <= 100; i++)
            {
                int damage = BossAttack.Damage(BossAttack.BaseDamage, i / 100f);
                Assert.LessOrEqual(damage, previous);
                previous = damage;
            }
        }

        [Test]
        public void AFractionOutsideZeroToOne_IsBroughtIntoRange()
        {
            Assert.AreEqual(BossAttack.CooldownMultiplier(1f), BossAttack.CooldownMultiplier(5f), 1e-6f);
            Assert.AreEqual(BossAttack.CooldownMultiplier(0f), BossAttack.CooldownMultiplier(-3f), 1e-6f);
            Assert.AreEqual(BossAttack.Damage(10, 1f), BossAttack.Damage(10, 9f));
            Assert.AreEqual(BossAttack.Damage(10, 0f), BossAttack.Damage(10, -1f));
        }

        [Test]
        public void DamageIsAWholeNumberOfAtLeastOne()
        {
            Assert.GreaterOrEqual(BossAttack.Damage(0, 0.3f), 1);
            Assert.GreaterOrEqual(BossAttack.Damage(1, 1f), 1);
        }

        [Test]
        public void TheBossAttackIsOneStraightShotBiggerThanASurvivors()
        {
            AttackDef def = BossAttack.Def;
            Assert.AreEqual(1, def.Shots);
            Assert.AreEqual(BossAttack.CooldownSeconds, def.CooldownSeconds, 1e-6f);
            Assert.AreEqual(1, def.Shape.Pierce);
            Assert.Greater(def.Shape.Radius, ProjectileShape.Default.Radius);
            Assert.Greater(def.Shape.Speed, 0f);
            Assert.Greater(def.Shape.Lifetime, 0f);
        }

        // ---- The boss as a hostile shooter in AttackSim ----

        static List<ShooterBody> BossBody(bool canFire = true) =>
            new List<ShooterBody> { new ShooterBody { ClientId = Boss, Position = Vector2.zero, CanFire = canFire } };

        static AttackSim BossAttackSim()
        {
            var attack = new AttackSim();
            attack.Register(Boss, BossAttack.Def, true);
            return attack;
        }

        static int CountFires(AttackSim attack, ProjectileSim projectiles, List<ShooterBody> bodies, float seconds,
                              System.Action<int> beforeStep = null)
        {
            int shots = 0;
            var events = new List<CombatEvent>();
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                beforeStep?.Invoke(i);
                attack.Step(Dt, bodies, projectiles);
                projectiles.Step(Dt, null);
                events.Clear();
                projectiles.DrainEvents(events);
                foreach (CombatEvent e in events) if (e.Kind == CombatEventKind.Fire) shots++;
            }
            return shots;
        }

        [Test]
        public void HoldingFire_FiresOncePerBossCooldown()
        {
            AttackSim attack = BossAttackSim();
            attack.SetAim(Boss, 0, true);

            int shots = CountFires(attack, new ProjectileSim(), BossBody(), 1.6f);

            Assert.AreEqual(2, shots, "shots at 0 s and 0.8 s");
        }

        [Test]
        public void PressingAndReleasingQuickly_NeverFiresFasterThanHolding()
        {
            AttackSim attack = BossAttackSim();

            int shots = CountFires(attack, new ProjectileSim(), BossBody(), 1.6f, i => attack.SetAim(Boss, 0, i % 4 < 2));

            Assert.LessOrEqual(shots, 2);
        }

        [Test]
        public void AFallenBoss_DoesNotFire()
        {
            AttackSim attack = BossAttackSim();
            attack.SetAim(Boss, 0, true);

            Assert.AreEqual(0, CountFires(attack, new ProjectileSim(), BossBody(canFire: false), 1f));
        }

        [Test]
        public void AShotRefusedByTheCap_DoesNotUseUpTheCooldown()
        {
            AttackSim attack = BossAttackSim();
            attack.SetAim(Boss, 0, true);
            var projectiles = new ProjectileSim(cap: 1);
            Assert.IsTrue(projectiles.TryFire(99, new Vector2(100f, 100f), 0, out _), "the cap is full");

            attack.Step(Dt, BossBody(), projectiles);

            Assert.AreEqual(1, projectiles.Count, "the boss shot was refused");
            Assert.AreEqual(0f, attack.CooldownRemaining(Boss), "and no cooldown started");
        }

        [Test]
        public void ANewCooldownMultiplier_AppliesFromTheNextShotOnly()
        {
            AttackSim attack = BossAttackSim();
            attack.SetAim(Boss, 0, true);
            var projectiles = new ProjectileSim();

            attack.Step(Dt, BossBody(), projectiles); // the first shot starts a 0.8 s cooldown
            Assert.AreEqual(BossAttack.CooldownSeconds, attack.CooldownRemaining(Boss), 1e-4f);

            attack.SetModifiers(Boss, 30, 0.5f);
            Assert.AreEqual(BossAttack.CooldownSeconds, attack.CooldownRemaining(Boss), 1e-4f, "a running cooldown is not cut short");

            // Run until the second shot leaves; its cooldown is the shortened one.
            var bodies = BossBody();
            for (int i = 0; i < 100 && projectiles.Count < 2; i++) attack.Step(Dt, bodies, projectiles);
            Assert.AreEqual(BossAttack.CooldownSeconds * 0.5f, attack.CooldownRemaining(Boss), 1e-4f);
        }

        [Test]
        public void TheBossShotCarriesTheDamageItWasFiredWith()
        {
            AttackSim attack = BossAttackSim();
            attack.SetAim(Boss, 0, true);
            attack.SetModifiers(Boss, 22, 1f);
            var survivors = new SurvivorSim();
            survivors.Register(2, 100);
            var targets = new SurvivorTargets(survivors, id => 1);
            var bodies = new List<SurvivorBody> { new SurvivorBody { ClientId = 2, Position = new Vector2(3f, 0f) } };
            targets.SetBodies(bodies);
            var projectiles = new ProjectileSim();

            attack.Step(Dt, BossBody(), projectiles);
            attack.SetModifiers(Boss, 99, 1f); // a later change must not change the shot in flight
            for (int i = 0; i < 40; i++) projectiles.Step(Dt, null, null, targets);

            Assert.AreEqual(100 - 22, survivors.Health(2));
        }
    }
}
