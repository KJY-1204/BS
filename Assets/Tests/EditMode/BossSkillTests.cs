using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class BossSkillTests
    {
        const ulong A = 1, B = 2;
        const float Dt = 0.02f;

        static SkillSim NewSkills()
        {
            var sim = new SkillSim();
            sim.Register(A, 1000, 100f);
            return sim;
        }

        static SkillActivation Activate(SkillSim skills, byte character, int slot, byte skill, int rank = 1)
        {
            var ranks = new int[SkillCatalog.MaxSkillId + 1];
            ranks[skill] = rank;
            Assert.AreEqual(SkillResult.Fired, skills.TryActivate(A, character, slot, true, ranks, out SkillActivation act));
            return act;
        }

        static BossSim Boss(float x, float y, int health = 100000, float radius = 0.9f) =>
            new BossSim(new BossDef { Name = "t", MaxHealth = health, MoveSpeed = 6f, Radius = radius, ContactDamage = 25, MatchSeconds = 60f }, new Vector2(x, y));

        static ushort AngleDegrees(float degrees) => AimMath.QuantizeAngle(degrees * Mathf.Deg2Rad);

        // ---- Area burst ----

        [Test]
        public void TheBurstHurtsABossWhoseBodyReachesIntoTheRadius()
        {
            SkillActivation act = Activate(NewSkills(), CharacterIds.Reimu, 0, SkillIds.Burst);
            BossSim boss = Boss(act.Radius + 0.5f, 0f, radius: 0.9f); // the centre is outside the radius but the body overlaps it

            int touched = SkillExecutor.Apply(act, new SkillContext { CasterId = A, Origin = Vector2.zero, Boss = boss });

            Assert.AreEqual(1, touched);
            Assert.AreEqual(100000 - act.Damage, boss.Health);
        }

        [Test]
        public void TheBurstMissesABossOutOfReach()
        {
            SkillActivation act = Activate(NewSkills(), CharacterIds.Reimu, 0, SkillIds.Burst);
            BossSim boss = Boss(act.Radius + 2f, 0f, radius: 0.9f);

            Assert.AreEqual(0, SkillExecutor.Apply(act, new SkillContext { CasterId = A, Boss = boss }));
            Assert.AreEqual(100000, boss.Health);
        }

        [Test]
        public void TheBurstHurtsMobsAndTheBossTogether_AndCountsBoth()
        {
            SkillActivation act = Activate(NewSkills(), CharacterIds.Reimu, 0, SkillIds.Burst);
            var mobs = new MobSim(targetCount: 0) { MaxHealth = 500 };
            mobs.TrySpawn(new Vector2(1, 0), 0, out ushort mob);
            BossSim boss = Boss(2f, 0f);

            int touched = SkillExecutor.Apply(act, new SkillContext { CasterId = A, Mobs = mobs, Boss = boss });

            Assert.AreEqual(2, touched);
            mobs.TryGetHealth(mob, out int mobHealth);
            Assert.AreEqual(500 - act.Damage, mobHealth);
            Assert.AreEqual(100000 - act.Damage, boss.Health);
        }

        [Test]
        public void AFallenBossIsNotHurtAgainByTheBurst()
        {
            SkillActivation act = Activate(NewSkills(), CharacterIds.Reimu, 0, SkillIds.Burst);
            BossSim boss = Boss(1f, 0f, health: 5);
            boss.TakeDamage(5, out _, out _);

            Assert.AreEqual(0, SkillExecutor.Apply(act, new SkillContext { CasterId = A, Boss = boss }));
            Assert.AreEqual(0, boss.Health);
        }

        [Test]
        public void TheBurstThatKillsTheBossMakesItFall()
        {
            SkillActivation act = Activate(NewSkills(), CharacterIds.Reimu, 0, SkillIds.Burst, 3);
            BossSim boss = Boss(1f, 0f, health: act.Damage);

            SkillExecutor.Apply(act, new SkillContext { CasterId = A, Boss = boss });

            Assert.IsTrue(boss.HasFallen);
            Assert.IsTrue(boss.JustFell);
        }

        // ---- Beam ----

        [Test]
        public void TheBeamHurtsABossOnItsLine()
        {
            SkillActivation act = Activate(NewSkills(), CharacterIds.Marisa, 0, SkillIds.MasterSpark);
            BossSim boss = Boss(7f, 0.3f);

            int touched = SkillExecutor.Apply(act, new SkillContext { CasterId = A, Origin = Vector2.zero, AimAngle = AngleDegrees(0), Boss = boss });

            Assert.AreEqual(1, touched);
            Assert.AreEqual(100000 - act.Damage, boss.Health);
        }

        [Test]
        public void TheBeamHurtsABossWhoseBodyOnlyGrazesIt()
        {
            SkillActivation act = Activate(NewSkills(), CharacterIds.Marisa, 0, SkillIds.MasterSpark);
            BossSim boss = Boss(7f, act.HalfWidth + 0.5f, radius: 0.9f); // the centre is beside the beam, the body is on it

            Assert.AreEqual(1, SkillExecutor.Apply(act, new SkillContext { CasterId = A, AimAngle = AngleDegrees(0), Boss = boss }));
        }

        [Test]
        public void TheBeamMissesABossBesideItBehindItOrBeyondItsEnd()
        {
            SkillActivation act = Activate(NewSkills(), CharacterIds.Marisa, 0, SkillIds.MasterSpark);
            var side = Boss(7f, act.HalfWidth + 3f);
            var behind = Boss(-5f, 0f);
            var beyond = Boss(act.Length + 4f, 0f);

            foreach (BossSim boss in new[] { side, behind, beyond })
            {
                Assert.AreEqual(0, SkillExecutor.Apply(act, new SkillContext { CasterId = A, AimAngle = AngleDegrees(0), Boss = boss }));
                Assert.AreEqual(100000, boss.Health);
            }
        }

        [Test]
        public void TheBeamFollowsTheAimToTheBoss()
        {
            SkillActivation act = Activate(NewSkills(), CharacterIds.Marisa, 0, SkillIds.MasterSpark);
            BossSim up = Boss(0f, 6f);

            Assert.AreEqual(0, SkillExecutor.Apply(act, new SkillContext { CasterId = A, AimAngle = AngleDegrees(0), Boss = up }), "aimed the wrong way");
            Assert.AreEqual(1, SkillExecutor.Apply(act, new SkillContext { CasterId = A, AimAngle = AngleDegrees(90), Boss = up }));
        }

        [Test]
        public void TheBeamHurtsMobsAndTheBossTogether()
        {
            SkillActivation act = Activate(NewSkills(), CharacterIds.Marisa, 0, SkillIds.MasterSpark, 2);
            var mobs = new MobSim(targetCount: 0) { MaxHealth = 500 };
            mobs.TrySpawn(new Vector2(3, 0), 0, out _);
            mobs.TrySpawn(new Vector2(5, 0.2f), 0, out _);
            BossSim boss = Boss(8f, 0f);

            int touched = SkillExecutor.Apply(act, new SkillContext { CasterId = A, AimAngle = AngleDegrees(0), Mobs = mobs, Boss = boss });

            Assert.AreEqual(3, touched);
            Assert.AreEqual(100000 - act.Damage, boss.Health);
        }

        // ---- Knives ----

        [Test]
        public void TheKnivesOfARingHurtTheBossBecauseTheyAreOrdinaryProjectiles()
        {
            SkillActivation act = Activate(NewSkills(), CharacterIds.Sakuya, 1, SkillIds.KnifeBarrage);
            var projectiles = new ProjectileSim();
            BossSim boss = Boss(2f, 0f);
            ProjectileShape shape = CharacterCatalog.Get(CharacterIds.Sakuya).Attack.Shape;

            SkillExecutor.Apply(act, new SkillContext { CasterId = A, Origin = Vector2.zero, AimAngle = 0, Projectiles = projectiles, Shape = shape });
            for (int i = 0; i < 30; i++) projectiles.Step(Dt, null, boss);

            Assert.Less(boss.Health, 100000);
            Assert.GreaterOrEqual(boss.Health, 100000 - 3 * act.Damage, "a few knives of the ring reach it, not the whole ring");
        }

        // ---- The skills that do not touch the boss ----

        [Test]
        public void TimeStopStopsMobsButNotTheBoss()
        {
            SkillActivation act = Activate(NewSkills(), CharacterIds.Sakuya, 0, SkillIds.TimeStop);
            var mobs = new MobSim(targetCount: 0);
            BossSim boss = Boss(3f, 0f);

            SkillExecutor.Apply(act, new SkillContext { CasterId = A, Mobs = mobs, Boss = boss });

            Assert.IsTrue(mobs.IsFrozen);
            Assert.IsTrue(boss.CanBeHit);
            boss.SetPosition(new Vector2(4f, 1f));
            Assert.AreEqual(new Vector2(4f, 1f), boss.Position, "the boss still moves where its player takes it");
            Assert.AreEqual(100000, boss.Health);
        }

        [Test]
        public void WindVeilNeverMovesTheBoss()
        {
            SkillActivation act = Activate(NewSkills(), CharacterIds.Sanae, 1, SkillIds.WindVeil);
            var mobs = new MobSim(targetCount: 0);
            mobs.TrySpawn(new Vector2(1, 0), 0, out _);
            BossSim boss = Boss(1.5f, 0f);

            SkillExecutor.Apply(act, new SkillContext { CasterId = A, Origin = Vector2.zero, Mobs = mobs, Boss = boss });

            Assert.AreEqual(new Vector2(1.5f, 0f), boss.Position);
            Assert.AreEqual(100000, boss.Health);
        }

        [Test]
        public void HealAndBarrierDoNothingToTheBoss()
        {
            var survivors = new SurvivorSim();
            survivors.Register(A);
            survivors.ApplyDamage(A, 60);
            BossSim boss = Boss(1f, 0f, health: 50000);
            boss.TakeDamage(20000, out _, out _);

            SkillActivation heal = Activate(NewSkills(), CharacterIds.Sanae, 0, SkillIds.Miracle);
            SkillExecutor.Apply(heal, new SkillContext
            {
                CasterId = A, Survivors = survivors, Boss = boss,
                Bodies = new List<GrowthBody> { new GrowthBody { ClientId = A, Position = Vector2.zero, Alive = true } },
            });
            SkillActivation barrier = Activate(NewSkills(), CharacterIds.Reimu, 1, SkillIds.Barrier);
            SkillExecutor.Apply(barrier, new SkillContext { CasterId = A, Survivors = survivors, Boss = boss });

            Assert.AreEqual(30000, boss.Health, "the boss was not healed");
        }

        // ---- No boss ----

        [Test]
        public void WithNoBossEveryAreaSkillBehavesAsBefore()
        {
            var mobs = new MobSim(targetCount: 0) { MaxHealth = 500 };
            mobs.TrySpawn(new Vector2(1, 0), 0, out ushort mob);
            SkillActivation burst = Activate(NewSkills(), CharacterIds.Reimu, 0, SkillIds.Burst);

            int touched = SkillExecutor.Apply(burst, new SkillContext { CasterId = A, Mobs = mobs });

            Assert.AreEqual(1, touched);
            mobs.TryGetHealth(mob, out int health);
            Assert.AreEqual(500 - burst.Damage, health);
        }

        [Test]
        public void AnAbsentBossObjectDoesNotBreakAnySkill()
        {
            foreach (SkillDef def in SkillCatalog.All.Where(d => d.IsActive))
            {
                var skills = NewSkills();
                var ranks = new int[SkillCatalog.MaxSkillId + 1];
                ranks[def.Id] = 1;
                if (def.RequiresSkill != 0) ranks[def.RequiresSkill] = def.RequiresRank;
                skills.TryActivate(A, def.Character, def.Slot, true, ranks, out SkillActivation act);

                Assert.DoesNotThrow(() => SkillExecutor.Apply(act, new SkillContext { CasterId = A, Boss = new BossSim() }), def.Name);
            }
        }
    }
}
