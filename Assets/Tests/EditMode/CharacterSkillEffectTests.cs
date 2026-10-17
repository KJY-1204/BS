using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class CharacterSkillEffectTests
    {
        const ulong A = 1, B = 2, C = 3;
        const float Dt = 0.02f;
        const int Q = 0, E = 1;

        static SkillSim NewSkills(params ulong[] clients)
        {
            var sim = new SkillSim();
            foreach (ulong id in clients) sim.Register(id, 1000, 100f);
            return sim;
        }

        static int[] Ranks(byte skill, int rank = 1)
        {
            var ranks = new int[SkillCatalog.MaxSkillId + 1];
            ranks[skill] = rank;
            return ranks;
        }

        /// <summary>Fires the skill through the real activation path and returns its numbers.</summary>
        static SkillActivation Activate(SkillSim sim, byte character, int slot, byte skill, int rank = 1, ulong who = A)
        {
            SkillResult result = sim.TryActivate(who, character, slot, true, Ranks(skill, rank), out SkillActivation activation);
            Assert.AreEqual(SkillResult.Fired, result);
            return activation;
        }

        static MobSim Mobs(int health = 100) => new MobSim(targetCount: 0) { MaxHealth = health };

        static ushort Spawn(MobSim mobs, float x, float y)
        {
            Assert.IsTrue(mobs.TrySpawn(new Vector2(x, y), 0, out ushort id));
            return id;
        }

        static int Health(MobSim mobs, ushort id)
        {
            Assert.IsTrue(mobs.TryGetHealth(id, out int h));
            return h;
        }

        static ushort AngleDegrees(float degrees) => AimMath.QuantizeAngle(degrees * Mathf.Deg2Rad);

        // ---- Area burst (Reimu Q) ----

        [Test]
        public void TheAreaBurstHurtsMobsInsideTheRadiusOnly()
        {
            SkillSim skills = NewSkills(A);
            SkillActivation act = Activate(skills, CharacterIds.Reimu, Q, SkillIds.Burst);
            MobSim mobs = Mobs();
            ushort a = Spawn(mobs, 1, 0), b = Spawn(mobs, 0, 2), c = Spawn(mobs, -2, 0), outside = Spawn(mobs, 6, 0);

            int hit = SkillExecutor.Apply(act, new SkillContext { CasterId = A, Origin = Vector2.zero, Mobs = mobs });

            Assert.AreEqual(3, hit);
            foreach (ushort id in new[] { a, b, c }) Assert.AreEqual(100 - act.Damage, Health(mobs, id));
            Assert.AreEqual(100, Health(mobs, outside));
        }

        [Test]
        public void TheAreaBurstReportsWhereMobsDied()
        {
            SkillSim skills = NewSkills(A);
            SkillActivation act = Activate(skills, CharacterIds.Reimu, Q, SkillIds.Burst, 3);
            MobSim mobs = Mobs(10);
            Spawn(mobs, 1, 0);
            Spawn(mobs, -1, 1);
            var deaths = new List<Vector2>();

            SkillExecutor.Apply(act, new SkillContext { CasterId = A, Mobs = mobs, Deaths = deaths });

            Assert.AreEqual(2, deaths.Count);
            Assert.AreEqual(0, mobs.Count);
        }

        // ---- Barrier (Reimu E) ----

        [Test]
        public void TheBarrierProtectsTheCasterForItsDurationAndThenEnds()
        {
            SkillSim skills = NewSkills(A);
            var survivors = new SurvivorSim();
            survivors.Register(A);
            SkillActivation act = Activate(skills, CharacterIds.Reimu, E, SkillIds.Barrier);
            MobSim mobs = Mobs();
            Spawn(mobs, 0.2f, 0);
            var bodies = new List<SurvivorBody> { new SurvivorBody { ClientId = A, Position = Vector2.zero } };

            Assert.AreEqual(1, SkillExecutor.Apply(act, new SkillContext { CasterId = A, Survivors = survivors }));
            Assert.IsTrue(survivors.IsInvulnerable(A));

            int steps = Mathf.RoundToInt((act.BuffSeconds - 0.1f) / Dt);
            for (int i = 0; i < steps; i++) survivors.Step(Dt, bodies, mobs);
            Assert.AreEqual(CombatConfig.SurvivorMaxHealth, survivors.Health(A), "untouched during the barrier");

            for (int i = 0; i < 20; i++) survivors.Step(Dt, bodies, mobs);
            Assert.Less(survivors.Health(A), CombatConfig.SurvivorMaxHealth, "hurt again after it ends");
        }

        [Test]
        public void TheBarrierLastsLongerAtHigherRanks()
        {
            SkillSim skills = NewSkills(A);
            SkillActivation one = Activate(skills, CharacterIds.Reimu, E, SkillIds.Barrier, 1);
            skills.SetStats(A, 1000, 100f);
            for (int i = 0; i < 1500; i++) skills.Step(Dt);
            SkillActivation three = Activate(skills, CharacterIds.Reimu, E, SkillIds.Barrier, 3);

            Assert.Greater(three.BuffSeconds, one.BuffSeconds);
        }

        // ---- Master Spark (Marisa Q) ----

        [Test]
        public void TheBeamHitsEveryMobOnItsLineAlongTheAim()
        {
            SkillSim skills = NewSkills(A);
            SkillActivation act = Activate(skills, CharacterIds.Marisa, Q, SkillIds.MasterSpark);
            MobSim mobs = Mobs();
            ushort r1 = Spawn(mobs, 0, 2), r2 = Spawn(mobs, 0.2f, 5), r3 = Spawn(mobs, -0.2f, 8), r4 = Spawn(mobs, 0, 11);
            ushort side = Spawn(mobs, 3, 5);
            ushort behind = Spawn(mobs, 0, -3);

            int hit = SkillExecutor.Apply(act, new SkillContext { CasterId = A, Origin = Vector2.zero, AimAngle = AngleDegrees(90), Mobs = mobs });

            Assert.AreEqual(4, hit, "it does not stop at the first mob");
            foreach (ushort id in new[] { r1, r2, r3, r4 }) Assert.AreEqual(100 - act.Damage, Health(mobs, id));
            Assert.AreEqual(100, Health(mobs, side));
            Assert.AreEqual(100, Health(mobs, behind));
        }

        [Test]
        public void TheBeamDoesNotReachBeyondItsLength()
        {
            SkillSim skills = NewSkills(A);
            SkillActivation act = Activate(skills, CharacterIds.Marisa, Q, SkillIds.MasterSpark);
            Assert.AreEqual(12f, act.Length, "the length the test below is built around");
            MobSim mobs = Mobs();
            Vector2 origin = new Vector2(-14, 0);
            ushort inside = Spawn(mobs, origin.x + 11f, 0);
            ushort beyond = Spawn(mobs, origin.x + 14f, 0);

            SkillExecutor.Apply(act, new SkillContext { CasterId = A, Origin = origin, AimAngle = AngleDegrees(0), Mobs = mobs });

            Assert.AreEqual(100 - act.Damage, Health(mobs, inside));
            Assert.AreEqual(100, Health(mobs, beyond));
        }

        [Test]
        public void TheBeamFollowsTheAimInAnyDirection()
        {
            SkillSim skills = NewSkills(A);
            SkillActivation act = Activate(skills, CharacterIds.Marisa, Q, SkillIds.MasterSpark);
            MobSim mobs = Mobs();
            ushort diagonal = Spawn(mobs, 4, 4);
            ushort off = Spawn(mobs, 4, -4);

            SkillExecutor.Apply(act, new SkillContext { CasterId = A, AimAngle = AngleDegrees(45), Mobs = mobs });

            Assert.AreEqual(100 - act.Damage, Health(mobs, diagonal));
            Assert.AreEqual(100, Health(mobs, off));
        }

        [Test]
        public void TheBeamsKillsGiveExperience()
        {
            SkillSim skills = NewSkills(A);
            SkillActivation act = Activate(skills, CharacterIds.Marisa, Q, SkillIds.MasterSpark, 3);
            MobSim mobs = Mobs(10);
            for (int i = 1; i <= 4; i++) Spawn(mobs, 0, 2 * i);
            var deaths = new List<Vector2>();
            var leveling = new LevelingSim(new System.Random(1)) { XpPerKill = 4 };
            leveling.Register(A, CharacterIds.Marisa);

            SkillExecutor.Apply(act, new SkillContext { CasterId = A, AimAngle = AngleDegrees(90), Mobs = mobs, Deaths = deaths });
            var bodies = new List<GrowthBody> { new GrowthBody { ClientId = A, Position = Vector2.zero, Alive = true } };
            foreach (Vector2 death in deaths) leveling.AwardKill(death, bodies);

            Assert.AreEqual(4, deaths.Count);
            Assert.AreEqual(16, leveling.Xp(A));
        }

        // ---- Stardust (Marisa E) ----

        [Test]
        public void StardustStartsTheAttackSpeedBuffAndTouchesNothingElse()
        {
            SkillSim skills = NewSkills(A);
            var ranks = Ranks(SkillIds.RapidFire);
            ranks[SkillIds.Haste] = 1;
            Assert.AreEqual(SkillResult.Fired, skills.TryActivate(A, CharacterIds.Marisa, E, true, ranks, out SkillActivation act));
            MobSim mobs = Mobs();
            Spawn(mobs, 1, 0);

            Assert.Less(skills.AttackMultiplier(A), 1f);
            Assert.AreEqual(0, SkillExecutor.Apply(act, new SkillContext { CasterId = A, Mobs = mobs }));
            Assert.AreEqual(1, mobs.Count);
        }

        // ---- Time Stop (Sakuya Q) ----

        [Test]
        public void TimeStopFreezesEveryMobForItsDuration()
        {
            SkillSim skills = NewSkills(A);
            SkillActivation act = Activate(skills, CharacterIds.Sakuya, Q, SkillIds.TimeStop);
            MobSim mobs = Mobs();
            Spawn(mobs, 5, 5);

            Assert.AreEqual(1, SkillExecutor.Apply(act, new SkillContext { CasterId = A, Mobs = mobs }));

            Assert.IsTrue(mobs.IsFrozen);
            Assert.AreEqual(act.BuffSeconds, mobs.FrozenSeconds, 1e-4f);
        }

        [Test]
        public void UsingTimeStopAgainWhileItRunsDoesNotShortenIt()
        {
            SkillSim skills = NewSkills(A);
            MobSim mobs = Mobs();
            SkillActivation three = Activate(skills, CharacterIds.Sakuya, Q, SkillIds.TimeStop, 3);
            SkillExecutor.Apply(three, new SkillContext { CasterId = A, Mobs = mobs });
            for (int i = 0; i < 1500; i++) skills.Step(Dt);
            SkillActivation one = Activate(skills, CharacterIds.Sakuya, Q, SkillIds.TimeStop, 1);

            SkillExecutor.Apply(one, new SkillContext { CasterId = A, Mobs = mobs });

            Assert.AreEqual(three.BuffSeconds, mobs.FrozenSeconds, 1e-4f);
        }

        // ---- Knife Barrage (Sakuya E) ----

        [Test]
        public void TheKnifeBarrageFiresARingAtEqualAngleSteps()
        {
            SkillSim skills = NewSkills(A);
            SkillActivation act = Activate(skills, CharacterIds.Sakuya, E, SkillIds.KnifeBarrage, 1);
            var projectiles = new ProjectileSim();
            ProjectileShape shape = CharacterCatalog.Get(CharacterIds.Sakuya).Attack.Shape;

            int fired = SkillExecutor.Apply(act, new SkillContext { CasterId = A, Origin = new Vector2(1, 2), AimAngle = 0, Projectiles = projectiles, Shape = shape });

            Assert.AreEqual(act.Count, fired);
            Assert.AreEqual(12, fired);
            var events = new List<CombatEvent>();
            projectiles.DrainEvents(events);
            var angles = events.Where(e => e.Kind == CombatEventKind.Fire).Select(e => (int)e.Angle).OrderBy(a => a).ToArray();
            Assert.AreEqual(12, angles.Length);
            for (int i = 1; i < angles.Length; i++) Assert.AreEqual(65536 / 12.0, angles[i] - angles[i - 1], 1.5, "30 degree steps");
            Assert.IsTrue(events.All(e => e.Position == new Vector2(1, 2) && e.Owner == A));
        }

        [Test]
        public void TheKnivesUseTheCastersShotShapeAndTheSkillsDamage()
        {
            SkillSim skills = NewSkills(A);
            SkillActivation act = Activate(skills, CharacterIds.Sakuya, E, SkillIds.KnifeBarrage, 2);
            var projectiles = new ProjectileSim();
            ProjectileShape shape = CharacterCatalog.Get(CharacterIds.Sakuya).Attack.Shape;
            SkillExecutor.Apply(act, new SkillContext { CasterId = A, AimAngle = 0, Projectiles = projectiles, Shape = shape });

            projectiles.Step(0.1f, null);
            int right = Enumerable.Range(0, projectiles.Count).First(i => Mathf.Abs(projectiles.PositionAt(i).y) < 1e-3f && projectiles.PositionAt(i).x > 0);
            Assert.AreEqual(shape.Speed * 0.1f, projectiles.PositionAt(right).x, 1e-3f);

            MobSim mobs = Mobs(1000);
            Spawn(mobs, 1.5f, 0);
            projectiles.Clear();
            SkillExecutor.Apply(act, new SkillContext { CasterId = A, AimAngle = 0, Projectiles = projectiles, Shape = shape });
            for (int i = 0; i < 20; i++) projectiles.Step(Dt, mobs);
            mobs.TryGetHealth(mobs.IdAt(0), out int health);
            Assert.AreEqual(1000 - act.Damage, health, "one knife hit it for the skill's damage");
        }

        [Test]
        public void WhenTheCapLeavesRoomForFewerKnives_ThoseFireAndTheSkillStillCosts()
        {
            SkillSim skills = NewSkills(A);
            float manaBefore = skills.Mana(A);
            SkillActivation act = Activate(skills, CharacterIds.Sakuya, E, SkillIds.KnifeBarrage, 3);
            var projectiles = new ProjectileSim(5);

            int fired = SkillExecutor.Apply(act, new SkillContext { CasterId = A, Projectiles = projectiles, Shape = ProjectileShape.Default });

            Assert.AreEqual(5, fired);
            Assert.AreEqual(5, projectiles.Count);
            Assert.AreEqual(manaBefore - SkillCatalog.Get(SkillIds.KnifeBarrage).ManaCost, skills.Mana(A), 1e-3f);
            Assert.Greater(skills.CooldownLeft(A, E), 0f);
        }

        // ---- Miracle (Sanae Q) ----

        [Test]
        public void MiracleHealsTheCasterAndNearbyLivingSurvivorsOnly()
        {
            SkillSim skills = NewSkills(A);
            SkillActivation act = Activate(skills, CharacterIds.Sanae, Q, SkillIds.Miracle);
            var survivors = new SurvivorSim();
            foreach (ulong id in new[] { A, B, C }) { survivors.Register(id); survivors.ApplyDamage(id, 70); }
            var bodies = new List<GrowthBody>
            {
                new GrowthBody { ClientId = A, Position = Vector2.zero, Alive = true },
                new GrowthBody { ClientId = B, Position = new Vector2(2, 0), Alive = true },
                new GrowthBody { ClientId = C, Position = new Vector2(act.Radius + 2f, 0), Alive = true },
            };

            int healed = SkillExecutor.Apply(act, new SkillContext { CasterId = A, Origin = Vector2.zero, Survivors = survivors, Bodies = bodies });

            Assert.AreEqual(2, healed);
            Assert.AreEqual(30 + act.Amount, survivors.Health(A));
            Assert.AreEqual(30 + act.Amount, survivors.Health(B));
            Assert.AreEqual(30, survivors.Health(C), "outside the radius");
        }

        [Test]
        public void MiracleNeverHealsAboveTheMaximumOrTheDead()
        {
            SkillSim skills = NewSkills(A);
            SkillActivation act = Activate(skills, CharacterIds.Sanae, Q, SkillIds.Miracle, 3);
            var survivors = new SurvivorSim();
            survivors.Register(A, 120);
            survivors.Register(B);
            survivors.ApplyDamage(A, 20);
            survivors.Kill(B);
            var bodies = new List<GrowthBody>
            {
                new GrowthBody { ClientId = A, Position = Vector2.zero, Alive = true },
                new GrowthBody { ClientId = B, Position = new Vector2(1, 0), Alive = false },
            };

            SkillExecutor.Apply(act, new SkillContext { CasterId = A, Survivors = survivors, Bodies = bodies });

            Assert.AreEqual(120, survivors.Health(A), "capped at her own maximum");
            Assert.IsFalse(survivors.IsAlive(B), "a fallen survivor stays down");
        }

        // ---- Wind Veil (Sanae E) ----

        [Test]
        public void WindVeilPushesMobsInRangeAwayAndLeavesTheRest()
        {
            SkillSim skills = NewSkills(A);
            SkillActivation act = Activate(skills, CharacterIds.Sanae, E, SkillIds.WindVeil);
            MobSim mobs = Mobs();
            Spawn(mobs, 1, 0);
            Spawn(mobs, act.Radius + 3f, 0);

            int pushed = SkillExecutor.Apply(act, new SkillContext { CasterId = A, Origin = Vector2.zero, Mobs = mobs });

            Assert.AreEqual(1, pushed);
            var xs = Enumerable.Range(0, mobs.Count).Select(i => mobs.PositionAt(i).x).OrderBy(x => x).ToArray();
            Assert.AreEqual(1f + act.Distance, xs[0], 1e-3f);
            Assert.AreEqual(act.Radius + 3f, xs[1], 1e-3f);
        }

        [Test]
        public void WindVeilDoesNotDamage()
        {
            SkillSim skills = NewSkills(A);
            SkillActivation act = Activate(skills, CharacterIds.Sanae, E, SkillIds.WindVeil);
            MobSim mobs = Mobs();
            ushort id = Spawn(mobs, 1, 0);

            SkillExecutor.Apply(act, new SkillContext { CasterId = A, Mobs = mobs });

            Assert.AreEqual(100, Health(mobs, id));
        }

        // ---- The shared rules ----

        [Test]
        public void EverySkillSpendsItsManaAndStartsItsCooldown()
        {
            foreach (SkillDef def in SkillCatalog.All.Where(d => d.IsActive))
            {
                SkillSim skills = NewSkills(A);
                var ranks = Ranks(def.Id);
                if (def.RequiresSkill != 0) ranks[def.RequiresSkill] = def.RequiresRank;
                float before = skills.Mana(A);

                SkillResult result = skills.TryActivate(A, def.Character, def.Slot, true, ranks, out SkillActivation act);

                Assert.AreEqual(SkillResult.Fired, result, def.Name);
                Assert.AreEqual(def.Id, act.SkillId, def.Name);
                Assert.AreEqual(def.Effect, act.Effect, def.Name);
                Assert.AreEqual(before - def.ManaCost, skills.Mana(A), 1e-3f, def.Name);
                Assert.AreEqual(def.Cooldown, skills.CooldownLeft(A, def.Slot), 1e-3f, def.Name);
                Assert.AreEqual(def.Cooldown, act.Cooldown, def.Name);
            }
        }

        [Test]
        public void EverySkillIsRefusedWithoutManaOnCooldownWhenDeadOrWhenNotLearned()
        {
            foreach (SkillDef def in SkillCatalog.All.Where(d => d.IsActive))
            {
                var ranks = Ranks(def.Id);

                var poor = NewSkills(A);
                poor.SetStats(A, 1000, 0f);
                poor.Register(A, 1, 0f);
                Assert.AreEqual(SkillResult.NoMana, poor.TryActivate(A, def.Character, def.Slot, true, ranks, out _), def.Name);

                var rich = NewSkills(A);
                Assert.AreEqual(SkillResult.Fired, rich.TryActivate(A, def.Character, def.Slot, true, ranks, out _), def.Name);
                Assert.AreEqual(SkillResult.Cooldown, rich.TryActivate(A, def.Character, def.Slot, true, ranks, out _), def.Name);

                Assert.AreEqual(SkillResult.Dead, NewSkills(A).TryActivate(A, def.Character, def.Slot, false, ranks, out _), def.Name);
                Assert.AreEqual(SkillResult.NotLearned, NewSkills(A).TryActivate(A, def.Character, def.Slot, true, new int[SkillCatalog.MaxSkillId + 1], out _), def.Name);
            }
        }

        [Test]
        public void ASkillOfAnotherCharacterCannotBeFired()
        {
            // Reimu has learned nothing of her own, but the ranks hold Master Spark: her Q slot is Burst, so nothing fires.
            SkillSim skills = NewSkills(A);
            Assert.AreEqual(SkillResult.NotLearned, skills.TryActivate(A, CharacterIds.Reimu, Q, true, Ranks(SkillIds.MasterSpark, 3), out _));
            Assert.AreEqual(SkillResult.Fired, skills.TryActivate(A, CharacterIds.Marisa, Q, true, Ranks(SkillIds.MasterSpark, 3), out _));
        }

        [Test]
        public void NoCharacterMeansNoSkills()
        {
            SkillSim skills = NewSkills(A);
            Assert.AreEqual(SkillResult.UnknownSlot, skills.TryActivate(A, CharacterIds.None, Q, true, Ranks(SkillIds.Burst, 3), out _));
        }

        [Test]
        public void SkillsUsedWithoutTheirTargetsDoNothingAndDoNotThrow()
        {
            SkillSim skills = NewSkills(A);
            foreach (SkillDef def in SkillCatalog.All.Where(d => d.IsActive))
            {
                skills.Register(A, 1000, 100f);
                var ranks = Ranks(def.Id);
                if (def.RequiresSkill != 0) ranks[def.RequiresSkill] = def.RequiresRank;
                skills.TryActivate(A, def.Character, def.Slot, true, ranks, out SkillActivation act);

                Assert.DoesNotThrow(() => SkillExecutor.Apply(act, new SkillContext { CasterId = A }), def.Name);
            }
        }
    }
}
