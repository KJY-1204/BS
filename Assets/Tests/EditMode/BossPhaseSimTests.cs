using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class BossPhaseSimTests
    {
        const float Dt = 0.02f;
        const ulong Boss = 1;
        const int Phase = BossSkills.PhaseSlot;

        sealed class Rig
        {
            public readonly BossSkillSim Sim = new BossSkillSim();
            public readonly ProjectileSim Projectiles = new ProjectileSim();
            public readonly SurvivorSim Survivors = new SurvivorSim();
            public readonly List<SurvivorBody> Bodies = new List<SurvivorBody>();
            public readonly List<CombatEvent> Events = new List<CombatEvent>();
            public MobSim Mobs = new MobSim(targetCount: 0);
            public bool MatchRunning = true;
            public bool BossStanding = true;
            public Vector2 BossPosition = new Vector2(0f, 6f);
            public float HealthFraction = 1f;

            public void AddSurvivor(ulong id, float x, float y, int health = 1000)
            {
                Survivors.Register(id, health);
                Bodies.Add(new SurvivorBody { ClientId = id, Position = new Vector2(x, y) });
            }

            BossSkillState State() => new BossSkillState
            {
                MatchRunning = MatchRunning, BossStanding = BossStanding, BossPosition = BossPosition, Aim = AimMath.FromDirection(Vector2.down), HealthFraction = HealthFraction,
            };

            public BossSkillResult Use(int slot, bool fromBoss = true) => Sim.TryUse(slot, fromBoss, State(), Boss, Events);

            public bool Update(float fraction, bool enabled = true)
            {
                HealthFraction = fraction;
                return Sim.UpdatePhase(fraction, enabled, MatchRunning, BossPosition, Boss, Events);
            }

            public void Step(float seconds)
            {
                int steps = Mathf.RoundToInt(seconds / Dt);
                for (int i = 0; i < steps; i++) Sim.Step(Dt, MatchRunning, BossStanding, Projectiles, Survivors, Bodies, Boss, Events, Mobs);
            }
        }

        // ---- the phase ----

        [Test]
        public void AMatchStartsInPhaseOneAndTheHealthMovesItUp()
        {
            var r = new Rig();
            Assert.AreEqual(1, r.Sim.Phase);

            Assert.IsFalse(r.Update(0.9f));
            Assert.AreEqual(1, r.Sim.Phase);
            Assert.IsTrue(r.Update(0.66f));
            Assert.AreEqual(2, r.Sim.Phase);
            Assert.IsFalse(r.Update(0.5f), "still phase 2");
            Assert.IsTrue(r.Update(0.33f));
            Assert.AreEqual(3, r.Sim.Phase);
        }

        [Test]
        public void APhaseNeverGoesBack()
        {
            var r = new Rig();
            r.Update(0.5f);
            Assert.AreEqual(2, r.Sim.Phase);

            Assert.IsFalse(r.Update(1f));
            Assert.AreEqual(2, r.Sim.Phase);
            r.Update(0.1f);
            Assert.IsFalse(r.Update(0.9f));
            Assert.AreEqual(3, r.Sim.Phase);
        }

        [Test]
        public void ANewMatchStartsAtPhaseOneAgain()
        {
            var r = new Rig();
            r.Update(0.1f);
            Assert.AreEqual(3, r.Sim.Phase);

            r.Sim.Reset(BossCatalog.Get(BossCatalog.Giant));

            Assert.AreEqual(1, r.Sim.Phase);
            Assert.AreEqual(0, r.Sim.BurstMobs);
        }

        [Test]
        public void SwitchedOffThePhaseStaysOneAndNothingIsAnnounced()
        {
            var r = new Rig();
            Assert.IsFalse(r.Update(0.05f, enabled: false));
            Assert.AreEqual(1, r.Sim.Phase);
            Assert.AreEqual(0, r.Sim.PendingCount);
            Assert.AreEqual(0, r.Events.Count);
            Assert.AreEqual(BossSkillResult.Locked, r.Use(Phase));
        }

        // ---- the third skill ----

        [Test]
        public void TheThirdSkillIsLockedInPhaseOneAndStartsNoCooldown()
        {
            var r = new Rig();

            Assert.AreEqual(BossSkillResult.Locked, r.Use(Phase));

            Assert.AreEqual(0f, r.Sim.CooldownLeft(Phase));
            Assert.AreEqual(0, r.Sim.PendingCount);
            Assert.AreEqual(0, r.Events.Count);
        }

        [Test]
        public void TheThirdSkillIsReadyAtOnceInPhaseTwoAndIsAnnouncedLikeTheOthers()
        {
            var r = new Rig();
            r.Update(0.6f);
            r.Events.Clear();
            r.Sim.Step(Dt, true, true, r.Projectiles, r.Survivors, r.Bodies, Boss, r.Events, r.Mobs); // let the burst's warning run a little
            r.Events.Clear();

            Assert.AreEqual(0f, r.Sim.CooldownLeft(Phase));
            Assert.AreEqual(BossSkillResult.Used, r.Use(Phase));

            BossSkillDef beam = BossCatalog.Get(BossCatalog.Standard).PhaseSkill;
            Assert.AreEqual(beam.CooldownSeconds * BossAttack.CooldownMultiplier(0.6f), r.Sim.CooldownLeft(Phase), 1e-3f);
            Assert.AreEqual(1, r.Events.Count(e => e.Kind == CombatEventKind.Skill && e.SkillId == BossSkills.WarningIdBeam));
        }

        [Test]
        public void TheThirdSkillTakesEffectAfterItsWarning()
        {
            var r = new Rig();
            r.Update(0.6f);
            r.BossPosition = new Vector2(0f, 6f);
            r.AddSurvivor(10, 0f, 0f);
            r.AddSurvivor(11, 6f, 0f);
            r.Use(Phase);

            r.Step(BossCatalog.Get(BossCatalog.Standard).PhaseSkill.WarningSeconds + 0.1f);

            Assert.Less(r.Survivors.Health(10), 1000, "on the beam's line straight down from the boss");
            Assert.AreEqual(1000, r.Survivors.Health(11), "off the line");
        }

        [Test]
        public void TheThirdSkillOfEachKindHasItsKindsEffect()
        {
            var expected = new Dictionary<byte, BossSkillEffect>
            {
                { BossCatalog.Standard, BossSkillEffect.Beam }, { BossCatalog.Giant, BossSkillEffect.ShotRing }, { BossCatalog.Sniper, BossSkillEffect.Shockwave },
            };
            foreach (var pair in expected)
            {
                var r = new Rig();
                r.Sim.Reset(BossCatalog.Get(pair.Key));
                r.Update(0.5f);
                r.Events.Clear();

                Assert.AreEqual(BossSkillResult.Used, r.Use(Phase));
                Assert.AreEqual(BossSkills.WarningIdOf(pair.Value), r.Events.Single(e => e.Kind == CombatEventKind.Skill).SkillId, $"kind {pair.Key}");
            }
        }

        [Test]
        public void TheThirdSkillFollowsTheRulesOfTheOthers()
        {
            var r = new Rig();
            r.Update(0.5f);

            Assert.AreEqual(BossSkillResult.Ignored, r.Use(Phase, fromBoss: false));
            r.MatchRunning = false;
            Assert.AreEqual(BossSkillResult.Unavailable, r.Use(Phase));
            r.MatchRunning = true;
            r.BossStanding = false;
            Assert.AreEqual(BossSkillResult.Unavailable, r.Use(Phase));
            r.BossStanding = true;

            Assert.AreEqual(BossSkillResult.Used, r.Use(Phase));
            Assert.AreEqual(BossSkillResult.Cooldown, r.Use(Phase));
        }

        [Test]
        public void TheThirdSkillHasItsOwnCooldown()
        {
            var r = new Rig();
            r.Update(0.5f);

            Assert.AreEqual(BossSkillResult.Used, r.Use(Phase));
            Assert.AreEqual(BossSkillResult.Used, r.Use(BossSkills.Barrage));
            Assert.AreEqual(BossSkillResult.Used, r.Use(BossSkills.Shockwave));
            Assert.AreEqual(BossSkillResult.Cooldown, r.Use(Phase));
        }

        [Test]
        public void AWeakerBossUsesTheThirdSkillHarderAndSooner()
        {
            var full = new Rig();
            full.Update(0.6f);
            full.HealthFraction = 1f;
            full.Use(Phase);

            var weak = new Rig();
            weak.Update(0.6f);
            weak.HealthFraction = 0.4f;
            weak.Use(Phase);

            Assert.Less(weak.Sim.CooldownLeft(Phase), full.Sim.CooldownLeft(Phase));
        }

        // ---- phase 3 cooldowns ----

        [Test]
        public void PhaseThreeShortensEveryCooldownThatStartsInIt()
        {
            var phase2 = new Rig();
            phase2.Update(0.5f);
            phase2.HealthFraction = 0.5f;
            var phase3 = new Rig();
            phase3.Update(0.2f);
            phase3.HealthFraction = 0.5f; // the same health for both, so only the phase differs

            foreach (int slot in new[] { BossSkills.Barrage, BossSkills.Shockwave, Phase })
            {
                phase2.Use(slot);
                phase3.Use(slot);
                Assert.AreEqual(phase2.Sim.CooldownLeft(slot) * BossPhases.ThirdPhaseCooldownFactor, phase3.Sim.CooldownLeft(slot), 1e-3f, $"slot {slot}");
            }
        }

        [Test]
        public void ARunningCooldownIsNotShortenedWhenTheMatchEntersPhaseThree()
        {
            var r = new Rig();
            r.Update(0.5f);
            r.Use(BossSkills.Shockwave);
            float before = r.Sim.CooldownLeft(BossSkills.Shockwave);

            r.Update(0.2f);

            Assert.AreEqual(before, r.Sim.CooldownLeft(BossSkills.Shockwave), 1e-6f);
        }

        [Test]
        public void PhaseTwoDoesNotShortenTheCooldownsOfTheBaseSkills()
        {
            var one = new Rig();
            one.HealthFraction = 0.5f;
            one.Use(BossSkills.Shockwave);
            var two = new Rig();
            two.Update(0.5f);
            two.Use(BossSkills.Shockwave);

            Assert.AreEqual(one.Sim.CooldownLeft(BossSkills.Shockwave), two.Sim.CooldownLeft(BossSkills.Shockwave), 1e-6f);
        }

        // ---- the burst ----

        [Test]
        public void APhaseChangeIsAnnouncedWithARoarWarningAtTheBoss()
        {
            var r = new Rig();
            r.BossPosition = new Vector2(3f, 4f);

            r.Update(0.6f);

            CombatEvent e = r.Events.Single();
            Assert.AreEqual(CombatEventKind.Skill, e.Kind);
            Assert.AreEqual(BossSkills.WarningIdRoar, e.SkillId);
            Assert.AreEqual(new Vector2(3f, 4f), e.Position);
            Assert.AreEqual(1, r.Sim.PendingCount);
        }

        [Test]
        public void TheBurstMakesItsMobsAfterTheWarningAndMoreInPhaseThree()
        {
            var r = new Rig();
            r.Update(0.6f);
            Assert.AreEqual(0, r.Mobs.Count, "nothing yet");
            r.Step(BossPhases.BurstWarningSeconds - 0.2f);
            Assert.AreEqual(0, r.Mobs.Count, "still warning");
            r.Step(0.4f);
            Assert.AreEqual(BossPhases.BurstMobs, r.Mobs.Count);
            Assert.AreEqual(BossPhases.BurstMobs, r.Sim.BurstMobs);
            Assert.AreEqual(0, r.Sim.RoarMobs, "not counted as the roar skill's mobs");

            r.Update(0.2f);
            r.Step(BossPhases.BurstWarningSeconds + 0.1f);
            Assert.AreEqual(BossPhases.BurstMobs + BossPhases.BurstMobsOf(BossPhases.BurstMobs, 3), r.Mobs.Count);
        }

        [Test]
        public void TheBurstMobsAreSummonedOnesNearTheBossAndCostNoEnergy()
        {
            var r = new Rig();
            r.BossPosition = new Vector2(0f, 6f);
            r.Update(0.6f);
            r.Step(BossPhases.BurstWarningSeconds + 0.1f);

            for (int i = 0; i < r.Mobs.Count; i++)
            {
                Assert.IsTrue(r.Mobs.IsSummoned(i));
                Assert.AreEqual(BossSkillSim.RoarDistance, Vector2.Distance(r.Mobs.PositionAt(i), r.BossPosition), 0.01f);
            }
        }

        [Test]
        public void SkippingAPhaseGivesOneBurstOfTheLastPhase()
        {
            var r = new Rig();
            r.Update(0.1f);

            Assert.AreEqual(3, r.Sim.Phase);
            Assert.AreEqual(1, r.Sim.PendingCount);
            r.Step(BossPhases.BurstWarningSeconds + 0.1f);
            Assert.AreEqual(BossPhases.BurstMobsOf(BossPhases.BurstMobs, 3), r.Mobs.Count);
        }

        [Test]
        public void TheBurstIsDroppedWhenTheMatchEndsOrTheBossFallsBeforeItsWarningEnds()
        {
            var ended = new Rig();
            ended.Update(0.6f);
            ended.MatchRunning = false;
            ended.Step(BossPhases.BurstWarningSeconds + 0.1f);
            Assert.AreEqual(0, ended.Mobs.Count);

            var fallen = new Rig();
            fallen.Update(0.6f);
            fallen.BossStanding = false;
            fallen.Step(BossPhases.BurstWarningSeconds + 0.1f);
            Assert.AreEqual(0, fallen.Mobs.Count);
        }

        [Test]
        public void NothingIsAnnouncedWhenTheMatchIsNotRunning()
        {
            var r = new Rig();
            r.MatchRunning = false;

            Assert.IsTrue(r.Update(0.5f), "the phase still moves");
            Assert.AreEqual(0, r.Sim.PendingCount);
            Assert.AreEqual(0, r.Events.Count);
        }

        [Test]
        public void TheBurstRespectsTheMobCap()
        {
            var r = new Rig { Mobs = new MobSim(5, 0, 1) };
            r.Update(0.6f);
            r.Step(BossPhases.BurstWarningSeconds + 0.1f);

            Assert.AreEqual(5, r.Mobs.Count);
        }

        [Test]
        public void ABossKindWithoutBurstDataStillChangesPhase()
        {
            var r = new Rig();
            BossDef bare = BossCatalog.Get(BossCatalog.Standard).CloneForTest();
            bare.PhaseBurst = null;
            r.Sim.Reset(bare);

            Assert.IsTrue(r.Update(0.5f));
            Assert.AreEqual(2, r.Sim.Phase);
            Assert.AreEqual(0, r.Sim.PendingCount);
        }
    }

    static class BossDefTestExtensions
    {
        public static BossDef CloneForTest(this BossDef d) => new BossDef
        {
            Id = d.Id, Name = d.Name, MaxHealth = d.MaxHealth, MoveSpeed = d.MoveSpeed, Radius = d.Radius, ContactDamage = d.ContactDamage,
            Color = d.Color, MatchSeconds = d.MatchSeconds, Attack = d.Attack, Skills = d.Skills, PhaseSkill = d.PhaseSkill, PhaseBurst = d.PhaseBurst,
        };
    }
}
