using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class BossSkillSimTests
    {
        const float Dt = 0.02f;
        const ulong Boss = 1;

        sealed class Rig
        {
            public readonly BossSkillSim Sim = new BossSkillSim();
            public readonly ProjectileSim Projectiles;
            public readonly SurvivorSim Survivors = new SurvivorSim();
            public readonly List<SurvivorBody> Bodies = new List<SurvivorBody>();
            public readonly List<CombatEvent> Events = new List<CombatEvent>();
            public bool MatchRunning = true;
            public bool BossStanding = true;
            public Vector2 BossPosition = new Vector2(0f, 6f);
            public ushort Aim;
            public float HealthFraction = 1f;
            public int Released;

            readonly SurvivorTargets targets;

            public Rig(int cap = 256)
            {
                Projectiles = new ProjectileSim(cap);
                targets = new SurvivorTargets(Survivors, id => (byte)id);
            }

            public void AddSurvivor(ulong id, float x, float y, int health = 100)
            {
                Survivors.Register(id, health);
                Bodies.Add(new SurvivorBody { ClientId = id, Position = new Vector2(x, y) });
            }

            public BossSkillState State() => new BossSkillState
            {
                MatchRunning = MatchRunning, BossStanding = BossStanding, BossPosition = BossPosition, Aim = Aim, HealthFraction = HealthFraction,
            };

            public BossSkillResult Use(int slot, bool fromBoss = true) => Sim.TryUse(slot, fromBoss, State(), Boss, Events);

            public void Step(float seconds)
            {
                int steps = Mathf.RoundToInt(seconds / Dt);
                for (int i = 0; i < steps; i++)
                {
                    Released += Sim.Step(Dt, MatchRunning, BossStanding, Projectiles, Survivors, Bodies, Boss, Events);
                    targets.SetBodies(Bodies);
                    Projectiles.Step(Dt, null, null, targets);
                    Projectiles.DrainEvents(Events);
                }
            }
        }

        // ---- the data ----

        [Test]
        public void TheSkillsAreDataWithNamesAndKeys()
        {
            Assert.AreEqual(2, BossSkills.Count);
            Assert.AreEqual("Q", BossSkills.Keys[BossSkills.Barrage]);
            Assert.AreEqual("E", BossSkills.Keys[BossSkills.Shockwave]);
            Assert.IsFalse(string.IsNullOrEmpty(BossSkills.Names[0]));
            Assert.IsFalse(string.IsNullOrEmpty(BossSkills.Names[1]));
            Assert.Greater(BossSkills.WarningSeconds, 0f);
            Assert.AreEqual(12, BossSkills.BarrageShots);
        }

        [Test]
        public void TheWarningNumbersAreAboveEverySurvivorSkill()
        {
            Assert.IsTrue(BossSkills.IsWarningId(BossSkills.WarningIdBarrage));
            Assert.IsTrue(BossSkills.IsWarningId(BossSkills.WarningIdShockwave));
            Assert.AreNotEqual(BossSkills.WarningIdBarrage, BossSkills.WarningIdShockwave);
            foreach (SkillDef def in SkillCatalog.All) Assert.IsFalse(BossSkills.IsWarningId(def.Id), def.Name);
        }

        // ---- using a skill, cooldowns, permissions ----

        [Test]
        public void UsingASkillStartsItsCooldownAtOnce()
        {
            var r = new Rig();

            Assert.AreEqual(BossSkillResult.Used, r.Use(BossSkills.Barrage));

            Assert.AreEqual(BossSkills.BarrageCooldownSeconds, r.Sim.CooldownLeft(BossSkills.Barrage), 1e-4f);
            Assert.AreEqual(0f, r.Sim.CooldownLeft(BossSkills.Shockwave), "the other skill is untouched");
            Assert.AreEqual(1, r.Sim.PendingCount);
        }

        [Test]
        public void ASkillIsReadyExactlyWhenItsCooldownHasPassed()
        {
            var r = new Rig();
            r.Use(BossSkills.Shockwave);

            r.Step(BossSkills.ShockwaveCooldownSeconds - 0.1f);
            Assert.AreEqual(BossSkillResult.Cooldown, r.Use(BossSkills.Shockwave));

            r.Step(0.2f);
            Assert.AreEqual(BossSkillResult.Used, r.Use(BossSkills.Shockwave));
        }

        [Test]
        public void TheTwoCooldownsAreIndependent()
        {
            var r = new Rig();
            Assert.AreEqual(BossSkillResult.Used, r.Use(BossSkills.Barrage));
            Assert.AreEqual(BossSkillResult.Used, r.Use(BossSkills.Shockwave));
            Assert.AreEqual(BossSkillResult.Cooldown, r.Use(BossSkills.Barrage));
        }

        [Test]
        public void ACooldownDoesNotRunWhileTheMatchIsNotRunning()
        {
            var r = new Rig();
            r.Use(BossSkills.Barrage);
            r.MatchRunning = false;

            r.Step(5f);

            Assert.AreEqual(BossSkills.BarrageCooldownSeconds, r.Sim.CooldownLeft(BossSkills.Barrage), 1e-4f);
        }

        [Test]
        public void ANewMatchStartsWithBothSkillsReady()
        {
            var r = new Rig();
            r.Use(BossSkills.Barrage);
            r.Use(BossSkills.Shockwave);

            r.Sim.Reset();

            Assert.AreEqual(0f, r.Sim.CooldownLeft(0));
            Assert.AreEqual(0f, r.Sim.CooldownLeft(1));
            Assert.AreEqual(0, r.Sim.PendingCount);
            Assert.AreEqual(BossSkillResult.Used, r.Use(BossSkills.Barrage));
        }

        [Test]
        public void ARequestNotFromTheBossIsIgnoredWithoutACooldown()
        {
            var r = new Rig();

            Assert.AreEqual(BossSkillResult.Ignored, r.Use(BossSkills.Barrage, fromBoss: false));

            Assert.AreEqual(0f, r.Sim.CooldownLeft(BossSkills.Barrage));
            Assert.AreEqual(0, r.Sim.PendingCount);
            Assert.AreEqual(0, r.Events.Count);
        }

        [Test]
        public void NothingHappensAfterTheMatchOrWhenTheBossHasFallen()
        {
            var r = new Rig();
            r.MatchRunning = false;
            Assert.AreEqual(BossSkillResult.Unavailable, r.Use(BossSkills.Barrage));

            r.MatchRunning = true;
            r.BossStanding = false;
            Assert.AreEqual(BossSkillResult.Unavailable, r.Use(BossSkills.Shockwave));

            Assert.AreEqual(0f, r.Sim.CooldownLeft(0));
            Assert.AreEqual(0f, r.Sim.CooldownLeft(1));
            Assert.AreEqual(0, r.Events.Count);
        }

        [Test]
        public void AnUnknownSlotIsRefused()
        {
            var r = new Rig();
            Assert.AreEqual(BossSkillResult.UnknownSlot, r.Use(BossSkills.SlotCount));
            Assert.AreEqual(BossSkillResult.UnknownSlot, r.Use(-1));
            Assert.AreEqual(0, r.Sim.PendingCount);
        }

        [Test]
        public void ASkillCostsNoSummonEnergy()
        {
            var summon = new SummonSim(SummonTestKit.Config());
            float before = summon.Energy;
            var r = new Rig();

            r.Use(BossSkills.Barrage);
            r.Use(BossSkills.Shockwave);
            r.Step(2f);

            Assert.AreEqual(before, summon.Energy, 1e-5f);
        }

        // ---- the warning ----

        [Test]
        public void UsingASkillAnnouncesItWithTheSkillEvent()
        {
            var r = new Rig { BossPosition = new Vector2(3f, 5f), Aim = 12345 };

            r.Use(BossSkills.Shockwave);

            CombatEvent e = r.Events.Single();
            Assert.AreEqual(CombatEventKind.Skill, e.Kind);
            Assert.AreEqual(BossSkills.WarningIdShockwave, e.SkillId);
            Assert.AreEqual(Boss, e.Owner);
            Assert.AreEqual(3f, e.Position.x, 1e-4f);
            Assert.AreEqual(5f, e.Position.y, 1e-4f);
            Assert.AreEqual(12345, e.Angle);
        }

        [Test]
        public void ASkillTakesEffectOnlyAfterTheWarningTime()
        {
            var r = new Rig();
            r.Use(BossSkills.Barrage);

            r.Step(BossSkills.WarningSeconds - 0.1f);
            Assert.AreEqual(0, r.Released);
            Assert.AreEqual(0, r.Events.Count(e => e.Kind == CombatEventKind.Fire));
            Assert.AreEqual(0, r.Projectiles.Count);

            r.Step(0.2f);
            Assert.AreEqual(1, r.Released);
            Assert.AreEqual(BossSkills.BarrageShots, r.Projectiles.Count);
            Assert.AreEqual(0, r.Sim.PendingCount);
        }

        [Test]
        public void TheCooldownDoesNotWaitForTheWarning()
        {
            var r = new Rig();
            r.Use(BossSkills.Shockwave);
            r.Step(BossSkills.WarningSeconds + 0.1f);

            Assert.AreEqual(BossSkills.ShockwaveCooldownSeconds - BossSkills.WarningSeconds - 0.1f, r.Sim.CooldownLeft(1), 0.05f);
        }

        [Test]
        public void TheWarningIsDroppedWhenTheMatchEndsOrTheBossFalls()
        {
            var r = new Rig();
            r.AddSurvivor(1, 0f, 5f);
            r.Use(BossSkills.Shockwave);
            r.Step(0.3f);
            r.MatchRunning = false;
            r.Step(1f);
            r.MatchRunning = true;
            r.Step(1f);
            Assert.AreEqual(0, r.Released);
            Assert.AreEqual(100, r.Survivors.Health(1));

            var r2 = new Rig();
            r2.AddSurvivor(1, 0f, 5f);
            r2.Use(BossSkills.Barrage);
            r2.Step(0.3f);
            r2.BossStanding = false;
            r2.Step(1f);
            Assert.AreEqual(0, r2.Released);
            Assert.AreEqual(0, r2.Projectiles.Count);
        }

        // ---- the shot ring ----

        [Test]
        public void TheRingLeavesFromTheUseSpotEvenlySpacedAndTheFirstFollowsTheAim()
        {
            var r = new Rig { BossPosition = new Vector2(2f, 3f), Aim = AimMath.QuantizeAngle(Mathf.PI * 0.5f) };
            r.Use(BossSkills.Barrage);
            r.BossPosition = new Vector2(-9f, -4f); // the boss walks away during the warning

            r.Step(BossSkills.WarningSeconds + 0.05f);

            var fires = r.Events.Where(e => e.Kind == CombatEventKind.Fire).OrderBy(e => e.ProjectileId).ToList();
            Assert.AreEqual(12, fires.Count);
            foreach (CombatEvent f in fires)
            {
                Assert.AreEqual(2f, f.Position.x, 1e-3f, "from where it was used");
                Assert.AreEqual(3f, f.Position.y, 1e-3f);
                Assert.AreEqual(Boss, f.Owner);
            }
            Assert.AreEqual(r.Aim, fires[0].Angle);
            var angles = fires.Select(f => (int)f.Angle).OrderBy(a => a).ToList();
            for (int i = 1; i < angles.Count; i++)
                Assert.AreEqual(65536 / 12, angles[i] - angles[i - 1], 2, "evenly spaced");
        }

        [Test]
        public void TheRingWrapsItsAnglesAroundTheCircle()
        {
            var r = new Rig { Aim = 65000 };
            r.Use(BossSkills.Barrage);

            r.Step(BossSkills.WarningSeconds + 0.05f);

            var angles = r.Events.Where(e => e.Kind == CombatEventKind.Fire).Select(e => (int)e.Angle).Distinct().ToList();
            Assert.AreEqual(12, angles.Count);
            Assert.IsTrue(angles.All(a => a >= 0 && a < 65536));
        }

        [Test]
        public void ARingShotHurtsASurvivorUnderTheNormalRules()
        {
            var r = new Rig { BossPosition = new Vector2(0f, 0f), Aim = AimMath.QuantizeAngle(0f) };
            r.AddSurvivor(1, 5f, 0f);
            r.Use(BossSkills.Barrage);

            r.Step(BossSkills.WarningSeconds + 1.5f);

            Assert.AreEqual(100 - BossSkills.BarrageDamage, r.Survivors.Health(1));
            Assert.IsTrue(r.Events.Any(e => e.Kind == CombatEventKind.Hit && e.SurvivorTarget));
        }

        [Test]
        public void ARingShotPassesAProtectedSurvivor()
        {
            var r = new Rig { BossPosition = new Vector2(0f, 0f), Aim = AimMath.QuantizeAngle(0f) };
            r.AddSurvivor(1, 5f, 0f);
            r.Survivors.GrantInvulnerability(1, 100f);
            r.Use(BossSkills.Barrage);

            r.Step(BossSkills.WarningSeconds + 1.5f);

            Assert.AreEqual(100, r.Survivors.Health(1));
        }

        [Test]
        public void TheProjectileCapLetsOnlyThoseThatFitOut()
        {
            var r = new Rig(cap: 5);
            r.Use(BossSkills.Barrage);

            r.Step(BossSkills.WarningSeconds + 0.05f);

            Assert.AreEqual(5, r.Events.Count(e => e.Kind == CombatEventKind.Fire && e.Owner == Boss));
            Assert.AreEqual(1, r.Released);
        }

        [Test]
        public void ARingWithNoRoomAtAllCreatesNothingButTheCooldownStays()
        {
            var r = new Rig(cap: 1);
            Assert.IsTrue(r.Projectiles.TryFire(new ProjectileSpec { Owner = 9, Origin = new Vector2(-15f, 9f), Damage = 1,
                Shape = new ProjectileShape { Speed = 0.001f, Lifetime = 1000f, Radius = 0.1f, Pierce = 1 } }, out _));
            r.Use(BossSkills.Barrage);

            r.Step(BossSkills.WarningSeconds + 0.05f);

            Assert.AreEqual(0, r.Events.Count(e => e.Kind == CombatEventKind.Fire && e.Owner == Boss));
            Assert.Greater(r.Sim.CooldownLeft(BossSkills.Barrage), 5f);
        }

        // ---- the shockwave ----

        [Test]
        public void TheShockwaveHurtsEverySurvivorInRangeOnceAndNobodyOutside()
        {
            var r = new Rig { BossPosition = new Vector2(0f, 0f) };
            r.AddSurvivor(1, 2f, 0f);
            r.AddSurvivor(2, -3f, 1f);
            r.AddSurvivor(3, 8f, 0f);
            r.Use(BossSkills.Shockwave);

            r.Step(BossSkills.WarningSeconds + 0.05f);

            Assert.AreEqual(100 - BossSkills.ShockwaveDamage, r.Survivors.Health(1));
            Assert.AreEqual(100 - BossSkills.ShockwaveDamage, r.Survivors.Health(2));
            Assert.AreEqual(100, r.Survivors.Health(3));
        }

        [Test]
        public void ASurvivorWhoLeavesTheRadiusDuringTheWarningTakesNothing()
        {
            var r = new Rig { BossPosition = new Vector2(0f, 0f) };
            r.AddSurvivor(1, 2f, 0f);
            r.Use(BossSkills.Shockwave);
            r.Step(0.3f);

            r.Bodies[0] = new SurvivorBody { ClientId = 1, Position = new Vector2(7f, 0f) };
            r.Step(BossSkills.WarningSeconds);

            Assert.AreEqual(1, r.Released);
            Assert.AreEqual(100, r.Survivors.Health(1));
        }

        [Test]
        public void TheShockwaveTakesEffectWhereItWasUsedEvenIfTheBossMoved()
        {
            var r = new Rig { BossPosition = new Vector2(0f, 0f) };
            r.AddSurvivor(1, 1f, 0f);
            r.Use(BossSkills.Shockwave);
            r.BossPosition = new Vector2(14f, 8f);

            r.Step(BossSkills.WarningSeconds + 0.05f);

            Assert.AreEqual(100 - BossSkills.ShockwaveDamage, r.Survivors.Health(1));
            CombatEvent blast = r.Events.Single(e => e.Kind == CombatEventKind.Explosion);
            Assert.AreEqual(0f, blast.Position.x, 1e-3f);
            Assert.AreEqual(40, blast.Radius10);
        }

        [Test]
        public void ProtectedAndFallenSurvivorsTakeNoShockwaveDamage()
        {
            var r = new Rig { BossPosition = new Vector2(0f, 0f) };
            r.AddSurvivor(1, 1f, 0f);
            r.AddSurvivor(2, -1f, 0f);
            r.AddSurvivor(3, 0f, 1f);
            r.Survivors.GrantInvulnerability(1, 50f);
            r.Survivors.Kill(2);
            r.Use(BossSkills.Shockwave);

            r.Step(BossSkills.WarningSeconds + 0.05f);

            Assert.AreEqual(100, r.Survivors.Health(1));
            Assert.AreEqual(0, r.Survivors.Health(2));
            Assert.AreEqual(100 - BossSkills.ShockwaveDamage, r.Survivors.Health(3));
        }

        [Test]
        public void TheShockwaveDoesNotMoveAnyone()
        {
            var r = new Rig { BossPosition = new Vector2(0f, 0f) };
            r.AddSurvivor(1, 1f, 0f);
            Vector2 before = r.Bodies[0].Position;
            r.Use(BossSkills.Shockwave);

            r.Step(BossSkills.WarningSeconds + 0.05f);

            Assert.AreEqual(before, r.Bodies[0].Position);
        }

        [Test]
        public void TheShockwaveStartsTheProtectionTime()
        {
            var r = new Rig { BossPosition = new Vector2(0f, 0f) };
            r.AddSurvivor(1, 1f, 0f);
            r.Use(BossSkills.Shockwave);

            r.Step(BossSkills.WarningSeconds + 0.02f);

            Assert.IsTrue(r.Survivors.IsInvulnerable(1));
            Assert.IsFalse(r.Survivors.ApplyDamage(1, 10));
        }

        // ---- the strengthening ----

        [Test]
        public void AFullHealthBossUsesTheNormalDamageAndCooldown()
        {
            var r = new Rig { HealthFraction = 1f };
            r.AddSurvivor(1, 1f, 0f);
            r.BossPosition = Vector2.zero;
            r.Use(BossSkills.Shockwave);

            r.Step(BossSkills.WarningSeconds + 0.05f);

            Assert.AreEqual(100 - 30, r.Survivors.Health(1));
            Assert.AreEqual(BossSkills.ShockwaveCooldownSeconds - BossSkills.WarningSeconds - 0.05f, r.Sim.CooldownLeft(1), 0.05f);
        }

        [Test]
        public void AWeakBossHitsHarderAndSoonerAndTheCooldownIsShorter()
        {
            var r = new Rig { HealthFraction = 0f, BossPosition = Vector2.zero };
            r.AddSurvivor(1, 1f, 0f);
            r.Use(BossSkills.Shockwave);
            float cooldown = r.Sim.CooldownLeft(1);

            r.Step(BossSkills.WarningSeconds + 0.05f);

            Assert.AreEqual(BossSkills.ShockwaveCooldownSeconds * BossAttack.MinCooldownFraction, cooldown, 1e-3f);
            Assert.AreEqual(100 - BossAttack.Damage(BossSkills.ShockwaveDamage, 0f), r.Survivors.Health(1));
            Assert.Greater(BossAttack.Damage(BossSkills.ShockwaveDamage, 0f), BossSkills.ShockwaveDamage);
        }

        [Test]
        public void TheDamageIsDecidedWhenTheSkillIsUsedNotWhenItTakesEffect()
        {
            var r = new Rig { HealthFraction = 1f, BossPosition = Vector2.zero };
            r.AddSurvivor(1, 1f, 0f);
            r.Use(BossSkills.Shockwave);
            r.HealthFraction = 0f; // the boss weakens during the warning

            r.Step(BossSkills.WarningSeconds + 0.05f);

            Assert.AreEqual(100 - BossSkills.ShockwaveDamage, r.Survivors.Health(1));
        }

        [Test]
        public void ARunningCooldownIsNotShortenedByLaterDamage()
        {
            var r = new Rig { HealthFraction = 1f };
            r.Use(BossSkills.Barrage);
            float before = r.Sim.CooldownLeft(BossSkills.Barrage);

            r.HealthFraction = 0f;
            r.Step(0.02f);

            Assert.AreEqual(before - 0.02f, r.Sim.CooldownLeft(BossSkills.Barrage), 1e-3f);
        }

        [Test]
        public void TheRingUsesTheStrengthenedDamageToo()
        {
            var r = new Rig { HealthFraction = 0f, BossPosition = Vector2.zero, Aim = AimMath.QuantizeAngle(0f) };
            r.AddSurvivor(1, 5f, 0f);
            r.Use(BossSkills.Barrage);

            r.Step(BossSkills.WarningSeconds + 1.5f);

            Assert.AreEqual(100 - BossAttack.Damage(BossSkills.BarrageDamage, 0f), r.Survivors.Health(1));
        }

        [Test]
        public void TheDevelopmentDamageOverrideReplacesTheBaseDamage()
        {
            var r = new Rig { HealthFraction = 1f, BossPosition = Vector2.zero };
            r.Sim.BaseDamageOverride = 55;
            r.AddSurvivor(1, 1f, 0f);
            r.Use(BossSkills.Shockwave);

            r.Step(BossSkills.WarningSeconds + 0.05f);

            Assert.AreEqual(100 - 55, r.Survivors.Health(1));
        }

        [Test]
        public void TwoSkillsAnnouncedTogetherBothTakeEffect()
        {
            var r = new Rig { BossPosition = Vector2.zero, Aim = 0 };
            r.AddSurvivor(1, 1f, 0f);
            r.Use(BossSkills.Barrage);
            r.Use(BossSkills.Shockwave);

            r.Step(BossSkills.WarningSeconds + 0.05f);

            Assert.AreEqual(2, r.Released);
            Assert.AreEqual(1, r.Events.Count(e => e.Kind == CombatEventKind.Explosion));
            Assert.AreEqual(12, r.Events.Count(e => e.Kind == CombatEventKind.Fire));
        }
    }
}
