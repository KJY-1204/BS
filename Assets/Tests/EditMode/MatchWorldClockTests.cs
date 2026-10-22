using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class MatchWorldClockTests
    {
        const float Dt = 0.02f;

        static MatchWorld NewWorld(float timeLimit = 900f, bool withBoss = true)
        {
            var world = new MatchWorld(new MatchWorldOptions { MobTarget = 0 }) { MatchRunning = true };
            world.Clock.Start(withBoss, timeLimit);
            return world;
        }

        static void AddSurvivor(MatchWorld world, ulong id, byte role, float x, float y)
        {
            world.Survivors.Register(id, 100);
            world.Attacks.Register(id, AttackDef.Default);
            world.Slots[id] = role;
            world.Bodies.Add(new MatchBody { ClientId = id, Role = role, Character = CharacterIds.Reimu, Position = new Vector2(x, y), Alive = true });
        }

        static void AddBoss(MatchWorld world, ulong id, float x, float y)
        {
            BossDef def = BossCatalog.Get(BossCatalog.Standard);
            world.Boss.Reset(def, new Vector2(x, y));
            world.Attacks.Register(id, def.Attack.ToAttackDef(), true);
            world.BossSkills.Reset(def);
            world.BossOwnerId = id;
            world.Slots[id] = RoleTable.BossRole;
            world.Bodies.Add(new MatchBody { ClientId = id, Role = RoleTable.BossRole, Position = new Vector2(x, y), Alive = true });
        }

        [Test]
        public void TheBossFallingGivesTheSurvivorsTheMatch()
        {
            MatchWorld world = NewWorld();
            AddBoss(world, 9, 10f, 10f);
            AddSurvivor(world, 1, 1, 0f, 0f);
            world.Boss.TakeDamage(int.MaxValue, out _, out _);

            world.StepClock(Dt);

            Assert.IsTrue(world.ClockDecided);
            Assert.AreEqual(MatchWinner.Survivors, world.Clock.Winner);
            Assert.AreEqual(MatchReason.BossDefeated, world.Clock.Reason);
        }

        [Test]
        public void EverySurvivorFallingGivesTheBossTheMatch()
        {
            MatchWorld world = NewWorld();
            AddBoss(world, 9, 10f, 10f);
            AddSurvivor(world, 1, 1, 0f, 0f);
            AddSurvivor(world, 2, 2, 3f, 0f);
            world.Survivors.Kill(1);
            world.StepClock(Dt);
            Assert.IsFalse(world.ClockDecided);

            world.Survivors.Kill(2);
            world.StepClock(Dt);

            Assert.IsTrue(world.ClockDecided);
            Assert.AreEqual(MatchWinner.Boss, world.Clock.Winner);
            Assert.AreEqual(MatchReason.AllSurvivorsFallen, world.Clock.Reason);
        }

        [Test]
        public void TheTimeRunningOutGivesTheBossTheMatch()
        {
            MatchWorld world = NewWorld(timeLimit: 1f);
            AddBoss(world, 9, 10f, 10f);
            AddSurvivor(world, 1, 1, 0f, 0f);

            for (int i = 0; i < 49; i++) { world.StepClock(Dt); Assert.IsFalse(world.ClockDecided); }
            world.StepClock(Dt);
            world.StepClock(Dt);

            Assert.AreEqual(MatchWinner.Boss, world.Clock.Winner);
            Assert.AreEqual(MatchReason.TimeUp, world.Clock.Reason);
        }

        [Test]
        public void TheBossLeavingGivesTheSurvivorsTheMatchAndTheBossFallingBeatsIt()
        {
            MatchWorld world = NewWorld();
            AddBoss(world, 9, 10f, 10f);
            AddSurvivor(world, 1, 1, 0f, 0f);
            world.BossLeft = true;
            world.StepClock(Dt);
            Assert.AreEqual(MatchReason.BossLeft, world.Clock.Reason);

            MatchWorld both = NewWorld();
            AddBoss(both, 9, 10f, 10f);
            AddSurvivor(both, 1, 1, 0f, 0f);
            both.BossLeft = true;
            both.Boss.TakeDamage(int.MaxValue, out _, out _);
            both.Survivors.Kill(1);
            both.StepClock(Dt);
            Assert.AreEqual(MatchReason.BossDefeated, both.Clock.Reason);
        }

        [Test]
        public void APracticeMatchHasNoClockAndNoResult()
        {
            MatchWorld world = NewWorld(withBoss: false);
            AddSurvivor(world, 1, 1, 0f, 0f);
            world.Survivors.Kill(1);

            for (int i = 0; i < 100; i++) world.StepClock(Dt);

            Assert.IsFalse(world.ClockDecided);
            Assert.AreEqual(MatchWinner.None, world.Clock.Winner);
        }

        [Test]
        public void TheStepStopsTheMatchWhenItIsDecided()
        {
            MatchWorld world = NewWorld();
            AddBoss(world, 9, 10f, 10f);
            AddSurvivor(world, 1, 1, 0f, 0f);
            world.Boss.TakeDamage(int.MaxValue, out _, out _);

            world.Step(Dt);

            Assert.IsTrue(world.ClockDecided);
            Assert.IsFalse(world.MatchRunning);

            world.Trace = new List<MatchPhase>();
            float left = world.Clock.TimeLeft;
            world.Step(Dt);
            Assert.AreEqual(0, world.Trace.Count);
            Assert.AreEqual(left, world.Clock.TimeLeft);
        }

        [Test]
        public void ASurvivorTeamThatShootsTheBossWinsThroughTheWholeStep()
        {
            MatchWorld world = NewWorld();
            AddBoss(world, 9, 5f, 0f);
            world.Boss.Reset(BossCatalog.Get(BossCatalog.Standard), new Vector2(5f, 0f));
            AddSurvivor(world, 1, 1, 0f, 0f);
            world.Boss.TakeDamage(world.Boss.MaxHealth - 30, out _, out _);
            world.SetAim(1, AimMath.FromDirection(Vector2.right), true);

            int steps = 0;
            while (world.MatchRunning && steps < 500) { world.Step(Dt); steps++; }

            Assert.AreEqual(MatchWinner.Survivors, world.Clock.Winner);
            Assert.AreEqual(MatchReason.BossDefeated, world.Clock.Reason);
            Assert.Less(steps, 500);
        }
    }
}
