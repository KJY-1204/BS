using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class MatchWorldPhaseTests
    {
        const float Dt = 0.02f;

        static MatchWorld NewWorld(int mobTarget = 0)
        {
            var world = new MatchWorld(new MatchWorldOptions { MobTarget = mobTarget }) { MatchRunning = true };
            return world;
        }

        static MatchBody Survivor(ulong id, byte role, float x, float y, bool alive = true) =>
            new MatchBody { ClientId = id, Role = role, Character = CharacterIds.Reimu, Position = new Vector2(x, y), Alive = alive };

        static MatchBody BossBody(ulong id, float x, float y) =>
            new MatchBody { ClientId = id, Role = RoleTable.BossRole, Position = new Vector2(x, y), Alive = true };

        // ---- mobs and summon (1.2) ----

        [Test]
        public void TheMobPhaseMovesMobsTowardTheSurvivorsOfTheBodies()
        {
            MatchWorld world = NewWorld();
            Assert.IsTrue(world.Mobs.TrySpawn(new Vector2(10f, 0f), MobSim.TypeAuto, out ushort id));
            world.Bodies.Add(Survivor(1, 1, 0f, 0f));

            for (int i = 0; i < 25; i++) world.StepMobs(Dt);

            Assert.IsTrue(world.Mobs.Contains(id));
            Vector2 p = Vector2.zero;
            for (int i = 0; i < world.Mobs.Count; i++) if (world.Mobs.IdAt(i) == id) p = world.Mobs.PositionAt(i);
            Assert.Less(p.x, 10f);
        }

        [Test]
        public void TheBossIsNeverAMobTarget()
        {
            MatchWorld world = NewWorld();
            Assert.IsTrue(world.Mobs.TrySpawn(new Vector2(10f, 0f), MobSim.TypeAuto, out ushort id));
            world.Bodies.Add(BossBody(9, 0f, 0f));

            world.StepMobs(Dt);

            Assert.AreEqual(0, world.SurvivorInfos.Count);
            Vector2 p = Vector2.zero;
            for (int i = 0; i < world.Mobs.Count; i++) if (world.Mobs.IdAt(i) == id) p = world.Mobs.PositionAt(i);
            Assert.AreEqual(new Vector2(10f, 0f), p); // nobody to chase
        }

        [Test]
        public void AFallenSurvivorIsPassedOnAsNotAlive()
        {
            MatchWorld world = NewWorld();
            world.Bodies.Add(Survivor(1, 1, 3f, 4f, alive: false));
            world.Bodies.Add(Survivor(2, 2, -1f, 2f));

            world.StepMobs(Dt);

            Assert.AreEqual(2, world.SurvivorInfos.Count);
            Assert.IsFalse(world.SurvivorInfos[0].Alive);
            Assert.AreEqual(new Vector2(3f, 4f), world.SurvivorInfos[0].Position);
            Assert.IsTrue(world.SurvivorInfos[1].Alive);
        }

        [Test]
        public void TheSummonPhaseRefillsEnergyOnlyWhileTheMatchRunsWithABoss()
        {
            MatchWorld world = NewWorld();
            world.Boss.Reset(BossCatalog.Get(BossCatalog.Standard), Vector2.zero);
            float start = world.Summon.Energy;

            world.StepSummon(1f);
            Assert.Greater(world.Summon.Energy, start);

            float running = world.Summon.Energy;
            world.MatchRunning = false;
            world.StepSummon(1f);
            Assert.AreEqual(running, world.Summon.Energy);

            world.MatchRunning = true;
            world.Boss.Clear();
            world.StepSummon(1f);
            Assert.AreEqual(running, world.Summon.Energy);
        }

        [Test]
        public void TheStepDoesNothingWhileTheMatchIsNotRunning()
        {
            MatchWorld world = NewWorld();
            world.MatchRunning = false;
            world.Trace = new List<MatchPhase>();

            world.Step(Dt);

            Assert.AreEqual(0, world.Trace.Count);
        }

        [Test]
        public void ADeviceKeepsWorkingThroughTheWorldStep()
        {
            MatchWorld world = NewWorld();
            world.Mobs.DeviceSettings = new DeviceConfig { ProductionIntervalSeconds = 1f, MobsPerBatch = 2, LifetimeSeconds = 20f };
            byte deviceType = MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Device);
            Assert.IsTrue(world.Mobs.TrySpawn(new Vector2(0f, 0f), deviceType, out _));
            world.Bodies.Add(Survivor(1, 1, -12f, -8f));

            for (int i = 0; i < 55; i++) world.StepMobs(Dt);

            Assert.AreEqual(2, world.Mobs.SummonedCount);
        }

        // ---- combat (1.3) ----

        static void AddSurvivor(MatchWorld world, ulong id, byte role, float x, float y)
        {
            world.Survivors.Register(id, 100);
            world.Attacks.Register(id, AttackDef.Default);
            world.Slots[id] = role;
            world.Bodies.Add(Survivor(id, role, x, y));
        }

        static void AddBoss(MatchWorld world, ulong id, float x, float y)
        {
            BossDef def = BossCatalog.Get(BossCatalog.Standard);
            world.Boss.Reset(def, new Vector2(x, y));
            world.Attacks.Register(id, def.Attack.ToAttackDef(), true);
            world.BossSkills.Reset(def);
            world.BossOwnerId = id;
            world.BossShotDamage = def.Attack.BaseDamage;
            world.Slots[id] = RoleTable.BossRole;
            world.Bodies.Add(BossBody(id, x, y));
        }

        static void RunCombat(MatchWorld world, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++) world.StepCombat(Dt);
        }

        [Test]
        public void AMobTouchingASurvivorHurtsItInTheCombatPhase()
        {
            MatchWorld world = NewWorld();
            AddSurvivor(world, 1, 1, 0f, 0f);
            Assert.IsTrue(world.Mobs.TrySpawn(new Vector2(0.2f, 0f), MobSim.TypeAuto, out _));

            world.StepCombat(Dt);

            Assert.Less(world.Survivors.Health(1), 100);
        }

        [Test]
        public void ASurvivorShotKillsAMobAndTheKillIsInTheEvents()
        {
            MatchWorld world = NewWorld();
            AddSurvivor(world, 1, 1, 0f, 0f);
            Assert.IsTrue(world.Mobs.TrySpawn(new Vector2(4f, 0f), MobSim.TypeAuto, out ushort mob));
            world.Mobs.MaxHealth = 10;
            world.Mobs.ApplyDamage(mob, 0, out _, out _);
            world.SetAim(1, AimMath.FromDirection(Vector2.right), true);

            var events = new List<CombatEvent>();
            for (int i = 0; i < 50; i++)
            {
                world.StepCombat(Dt);
                events.AddRange(world.CombatEvents);
            }

            Assert.IsFalse(world.Mobs.Contains(mob));
            Assert.IsTrue(events.Exists(e => e.Kind == CombatEventKind.Hit && e.Killed && e.MobId == mob));
        }

        [Test]
        public void AFallenSurvivorCannotFireAndNeitherCanAFallenBoss()
        {
            MatchWorld world = NewWorld();
            AddSurvivor(world, 1, 1, 0f, 0f);
            world.Survivors.Kill(1);
            world.SetAim(1, 0, true);
            RunCombat(world, 0.5f);
            Assert.AreEqual(0, world.Projectiles.Count);

            AddBoss(world, 9, 10f, 10f);
            world.Boss.TakeDamage(int.MaxValue, out _, out _);
            world.SetAim(9, 0, true);
            RunCombat(world, 0.5f);
            Assert.AreEqual(0, world.Projectiles.Count);
        }

        [Test]
        public void TheBossShootsAndAShotHurtsASurvivor()
        {
            MatchWorld world = NewWorld();
            AddBoss(world, 9, 0f, 0f);
            AddSurvivor(world, 1, 1, 4f, 0f);
            world.SetAim(9, AimMath.FromDirection(Vector2.right), true);

            RunCombat(world, 1.5f);

            Assert.Less(world.Survivors.Health(1), 100);
        }

        static int BossFires(float healthFraction)
        {
            MatchWorld world = NewWorld();
            AddBoss(world, 9, 0f, 0f);
            AddSurvivor(world, 1, 1, -20f, -20f);
            world.Boss.TakeDamage(Mathf.RoundToInt(world.Boss.MaxHealth * (1f - healthFraction)), out _, out _);
            world.SetAim(9, AimMath.FromDirection(Vector2.right), true);

            int fires = 0;
            for (int i = 0; i < 250; i++)
            {
                world.StepCombat(Dt);
                fires += world.CombatEvents.FindAll(e => e.Kind == CombatEventKind.Fire).Count;
            }
            return fires;
        }

        [Test]
        public void TheBossFiresFasterAsItLosesHealth()
        {
            int full = BossFires(1f);
            int low = BossFires(0.1f);

            Assert.Greater(full, 0);
            Assert.Greater(low, full);
        }

        [Test]
        public void ABossSkillFromTheBossIsAnnouncedAndTakesEffectAfterTheWarning()
        {
            MatchWorld world = NewWorld();
            AddBoss(world, 9, 0f, 0f);
            AddSurvivor(world, 1, 1, 2f, 0f);
            var events = new List<CombatEvent>();

            BossSkillResult result = world.UseBossSkill(9, BossSkills.Shockwave, 0, events);

            Assert.AreEqual(BossSkillResult.Used, result);
            Assert.Greater(events.Count, 0);
            int released = 0;
            for (int i = 0; i < 60; i++)
            {
                world.StepCombat(Dt);
                released += world.LastCombat.BossSkillsReleased;
            }
            Assert.AreEqual(1, released);
            Assert.Less(world.Survivors.Health(1), 100);
        }

        [Test]
        public void ABossSkillFromASurvivorIsIgnored()
        {
            MatchWorld world = NewWorld();
            AddBoss(world, 9, 0f, 0f);
            AddSurvivor(world, 1, 1, 2f, 0f);
            var events = new List<CombatEvent>();

            BossSkillResult result = world.UseBossSkill(1, BossSkills.Shockwave, 0, events);

            Assert.AreNotEqual(BossSkillResult.Used, result);
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void ARangedMobFiresAShotThroughTheCombatPhase()
        {
            MatchWorld world = NewWorld();
            AddSurvivor(world, 1, 1, 0f, 0f);
            Assert.IsTrue(world.Mobs.TrySpawn(new Vector2(6f, 0f), MobKinds.Encode(MobKinds.OriginAuto, MobKinds.Ranged), out _));
            int shots = 0;

            for (int i = 0; i < 300; i++)
            {
                world.StepMobs(Dt);
                world.StepCombat(Dt);
                shots += world.LastCombat.MobActions.ShotsFired;
            }

            Assert.Greater(shots, 0);
        }

        [Test]
        public void TheSlotOfAClientIsItsRoleAndMobShotsHaveTheirOwn()
        {
            MatchWorld world = NewWorld();
            AddBoss(world, 9, 0f, 0f);
            AddSurvivor(world, 1, 3, 1f, 0f);

            Assert.AreEqual(RoleTable.BossRole, world.SlotOf(9));
            Assert.AreEqual(3, world.SlotOf(1));
            Assert.AreEqual(MatchWorld.MobShooterSlot, world.SlotOf(MatchWorld.MobShooterOwner));
            Assert.AreEqual(0, world.SlotOf(77));
            Assert.IsTrue(world.IsBossOwner(9));
            Assert.IsFalse(world.IsBossOwner(1));
            world.BossOwnerId = MatchWorld.NoBoss;
            Assert.IsFalse(world.IsBossOwner(MatchWorld.NoBoss));
        }

        [Test]
        public void TheServerWorldTakesItsStartingValuesFromTheDevelopmentArguments()
        {
            MatchWorld world = MatchWorld.ForServer();

            Assert.AreEqual(MobConfig.HardCap, world.Mobs.HardCap);
            Assert.IsNotNull(world.Summon);
            Assert.IsNotNull(world.Leveling);
            Assert.AreEqual(GrowthConfig.XpPerKill, world.Leveling.XpPerKill);
        }
    }
}
