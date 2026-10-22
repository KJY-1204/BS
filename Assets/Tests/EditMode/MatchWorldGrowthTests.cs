using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class MatchWorldGrowthTests
    {
        const float Dt = 0.02f;

        static MatchWorld NewWorld() => new MatchWorld(new MatchWorldOptions { MobTarget = 0 }) { MatchRunning = true };

        static void AddSurvivor(MatchWorld world, ulong id, byte role, float x, float y, byte character = CharacterIds.Reimu)
        {
            world.Survivors.Register(id, 100);
            world.Attacks.Register(id, AttackDef.Default);
            world.Slots[id] = role;
            world.StartGrowth(id, character);
            world.Bodies.Add(new MatchBody { ClientId = id, Role = role, Character = character, Position = new Vector2(x, y), Alive = true });
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
            world.Bodies.Add(new MatchBody { ClientId = id, Role = RoleTable.BossRole, Position = new Vector2(x, y), Alive = true });
        }

        [Test]
        public void AShotKillGivesExperienceThroughTheTwoPhases()
        {
            MatchWorld world = NewWorld();
            AddSurvivor(world, 1, 1, 0f, 0f);
            Assert.IsTrue(world.Mobs.TrySpawn(new Vector2(3f, 0f), MobSim.TypeAuto, out ushort mob));
            world.Mobs.ApplyDamage(mob, world.Mobs.MaxHealth - 1, out _, out _);
            world.SetAim(1, AimMath.FromDirection(Vector2.right), true);

            for (int i = 0; i < 60 && world.Mobs.Contains(mob); i++)
            {
                world.StepCombat(Dt);
                world.StepGrowth(Dt);
            }

            Assert.IsFalse(world.Mobs.Contains(mob));
            Assert.AreEqual(GrowthConfig.XpPerKill, world.Leveling.Xp(1));
        }

        [Test]
        public void ADestroyedDeviceGivesItsOwnExperience()
        {
            MatchWorld world = NewWorld();
            AddSurvivor(world, 1, 1, 0f, 0f);
            world.Leveling.XpPerDevice = 20;
            byte deviceType = MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Device);
            Assert.IsTrue(world.Mobs.TrySpawn(new Vector2(2f, 0f), deviceType, out ushort device));
            world.Mobs.ApplyDamage(device, 1000000, out _, out _);

            world.StepGrowth(Dt);

            Assert.AreEqual(20, world.Leveling.Xp(1));
            Assert.AreEqual(1, world.LastGrowth.KillsHandled);
        }

        [Test]
        public void ALearnedSkillFiresAndAnUnlearnedOneIsRefused()
        {
            MatchWorld world = NewWorld();
            AddSurvivor(world, 1, 1, 0f, 0f);
            SkillDef q = SkillCatalog.ForSlot(0, CharacterIds.Reimu);
            Assert.IsNotNull(q);

            Assert.IsTrue(world.RequestSkill(1, 0));
            world.StepGrowth(Dt);
            Assert.AreEqual(1, world.SkillOutcomes.Count);
            Assert.AreNotEqual(SkillResult.Fired, world.SkillOutcomes[0].Result);
            Assert.AreEqual(0, world.SkillEvents.Count);

            world.Leveling.GrantRank(1, q.Id, 1);
            Assert.IsTrue(world.RequestSkill(1, 0));
            world.StepGrowth(Dt);
            Assert.AreEqual(SkillResult.Fired, world.SkillOutcomes[0].Result);
            Assert.AreEqual(1, world.SkillEvents.Count);
            Assert.AreEqual(CombatEventKind.Skill, world.SkillEvents[0].Kind);
            Assert.AreEqual(q.Id, world.SkillOutcomes[0].Activation.SkillId);
        }

        [Test]
        public void ARequestFromSomeoneWithoutGrowthIsIgnoredAndAStepTakesAtMostTheLimit()
        {
            MatchWorld world = NewWorld();
            AddSurvivor(world, 1, 1, 0f, 0f);

            Assert.IsTrue(world.RequestSkill(77, 0));
            world.StepGrowth(Dt);
            Assert.AreEqual(0, world.SkillOutcomes.Count);

            for (int i = 0; i < MatchWorld.MaxSkillRequestsPerStep; i++) Assert.IsTrue(world.RequestSkill(1, 0));
            Assert.IsFalse(world.RequestSkill(1, 0));
        }

        [Test]
        public void ALevelUpIsReportedAndRaisesTheStatsTheOtherSimulationsUse()
        {
            MatchWorld world = NewWorld();
            AddSurvivor(world, 1, 1, 0f, 0f);
            int healthBefore = world.Survivors.MaxHealthOf(1);
            int damageBefore = world.StatsOf(1).ProjectileDamage;

            world.Leveling.AddXp(1, GrowthConfig.XpNeeded(1));
            world.StepGrowth(Dt);

            Assert.AreEqual(2, world.Leveling.Level(1));
            Assert.IsTrue(world.GrowthEvents.Exists(e => e.Kind == GrowthEventKind.LevelUp && e.ClientId == 1));
            Assert.Greater(world.Survivors.MaxHealthOf(1), healthBefore);
            Assert.Greater(world.StatsOf(1).ProjectileDamage, damageBefore);
        }

        [Test]
        public void AnOfferThatIsNotAnsweredClosesByItself()
        {
            MatchWorld world = NewWorld();
            AddSurvivor(world, 1, 1, 0f, 0f);
            world.Leveling.OfferSeconds = 1f;
            world.Leveling.AddXp(1, GrowthConfig.XpNeeded(1));

            var kinds = new List<GrowthEventKind>();
            for (int i = 0; i < 80; i++)
            {
                world.StepGrowth(Dt);
                foreach (GrowthEvent e in world.GrowthEvents) kinds.Add(e.Kind);
            }

            Assert.Contains(GrowthEventKind.OfferOpened, kinds);
            Assert.Contains(GrowthEventKind.OfferClosed, kinds);
        }

        [Test]
        public void AnAnsweredOfferClosesAndAppliesTheSkill()
        {
            MatchWorld world = NewWorld();
            AddSurvivor(world, 1, 1, 0f, 0f);
            world.Leveling.AddXp(1, GrowthConfig.XpNeeded(1));
            world.StepGrowth(Dt);
            GrowthEvent opened = world.GrowthEvents.Find(e => e.Kind == GrowthEventKind.OfferOpened);
            Assert.AreEqual(1UL, opened.ClientId);
            byte chosen = opened.Choices[0];

            Assert.IsTrue(world.Pick(1, opened.OfferId, 0));
            world.StepGrowth(Dt);

            Assert.IsTrue(world.GrowthEvents.Exists(e => e.Kind == GrowthEventKind.OfferClosed && !e.Auto));
            Assert.AreEqual(1, world.Leveling.RankOf(1, chosen));
        }

        [Test]
        public void TheDevelopmentHealthBonusIsAddedToEveryMaximum()
        {
            MatchWorld world = NewWorld();
            world.SurvivorHealthBonus = 500;
            AddSurvivor(world, 1, 1, 0f, 0f);

            Assert.AreEqual(CharacterCatalog.Get(CharacterIds.Reimu).MaxHealth + 500, world.Survivors.MaxHealthOf(1));
        }

        [Test]
        public void StoppingGrowthForgetsTheSurvivorAndClearingForgetsEveryone()
        {
            MatchWorld world = NewWorld();
            AddSurvivor(world, 1, 1, 0f, 0f);
            AddSurvivor(world, 2, 2, 3f, 0f);
            Assert.IsTrue(world.HasGrowth(1));
            Assert.AreEqual(2, world.GrowthIds.Count);

            world.StopGrowth(1);
            Assert.IsFalse(world.HasGrowth(1));
            Assert.IsFalse(world.Leveling.Contains(1));
            Assert.AreEqual(1, world.GrowthIds.Count);

            world.ClearGrowth();
            Assert.AreEqual(0, world.GrowthIds.Count);
            Assert.IsFalse(world.Leveling.Contains(2));
        }

        [Test]
        public void TheFullStepRunsTheChainFromShotToExperienceWithoutAServer()
        {
            MatchWorld world = NewWorld();
            AddBoss(world, 9, 15f, 15f);
            AddSurvivor(world, 1, 1, 0f, 0f);
            world.Mobs.MaxHealth = 10;
            for (int i = 0; i < 6; i++) Assert.IsTrue(world.Mobs.TrySpawn(new Vector2(3f + 0.1f * i, 0f), MobSim.TypeSummoned, out _));
            world.Clock.Start(true, 900f);
            world.SetAim(1, AimMath.FromDirection(Vector2.right), true);

            for (int i = 0; i < 300; i++) world.Step(Dt);

            int xpEarned = world.Leveling.Xp(1);
            for (int level = 1; level < world.Leveling.Level(1); level++) xpEarned += GrowthConfig.XpNeeded(level);
            Assert.Greater(xpEarned, 0);
            Assert.Less(world.Mobs.Count, 6);
        }
    }
}
