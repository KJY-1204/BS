using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class BossKindTraitTests
    {
        const float Dt = 0.02f;
        const ulong A = 1;

        static BossSim Boss(byte kind, float x = 0f, float y = 0f)
        {
            BossDef def = BossCatalog.Get(kind);
            return new BossSim(def, new Vector2(x, y));
        }

        static void Move(BossSim boss, float x, float y, float dt = Dt)
        {
            boss.SetPosition(new Vector2(x, y));
            boss.Advance(dt);
        }

        static void Stand(BossSim boss, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++) boss.Advance(Dt);
        }

        static int Touch(BossSim boss)
        {
            var sim = new SurvivorSim();
            sim.Register(A);
            var bodies = new List<SurvivorBody> { new SurvivorBody { ClientId = A, Position = boss.Position } };
            sim.Step(Dt, bodies, null, boss);
            return CombatConfig.SurvivorMaxHealth - sim.Health(A);
        }

        static HarnessSettings Settings(params (string name, float value)[] overrides) => SettingsFor(BossCatalog.Standard, overrides);

        static HarnessSettings SettingsFor(byte kind, params (string name, float value)[] overrides)
        {
            var s = new HarnessSettings { bossKind = kind };
            s.overrides.Add(new NamedValue { name = "mobAutoTarget", value = 0f });
            s.overrides.Add(new NamedValue { name = "matchSeconds", value = 60f });
            foreach ((string name, float value) o in overrides) s.overrides.Add(new NamedValue { name = o.name, value = o.value });
            return s;
        }

        // ---- stillness ----

        [Test]
        public void AMovingBossCountsAsMovingWithNoStillTime()
        {
            BossSim boss = Boss(BossCatalog.Standard);
            Move(boss, 0.1f, 0f);

            Assert.IsTrue(boss.Moving);
            Assert.AreEqual(0f, boss.StillSeconds);
        }

        [Test]
        public void AStandingBossAddsUpItsStillTime()
        {
            BossSim boss = Boss(BossCatalog.Standard);
            Stand(boss, 1.5f);

            Assert.IsFalse(boss.Moving);
            Assert.AreEqual(1.5f, boss.StillSeconds, 0.001f);
        }

        [Test]
        public void MovingAgainStartsTheStillTimeFromZero()
        {
            BossSim boss = Boss(BossCatalog.Standard);
            Stand(boss, 2f);
            Move(boss, 0.5f, 0f);
            Assert.AreEqual(0f, boss.StillSeconds);

            Stand(boss, 0.5f);
            Assert.AreEqual(0.5f, boss.StillSeconds, 0.001f);
        }

        [Test]
        public void ATinyDriftCountsAsStandingStill()
        {
            BossSim boss = Boss(BossCatalog.Standard);
            Move(boss, BossSim.StillDistance * 0.5f, 0f);

            Assert.IsFalse(boss.Moving);
            Assert.Greater(boss.StillSeconds, 0f);
        }

        [Test]
        public void NoBossAndAFallenBossHaveNoStillness()
        {
            var none = new BossSim();
            none.Advance(1f);
            Assert.AreEqual(0f, none.StillSeconds);
            Assert.IsFalse(none.Moving);

            BossSim boss = Boss(BossCatalog.Sniper);
            Stand(boss, 2f);
            boss.TakeDamage(boss.MaxHealth, out _, out _);
            boss.Advance(Dt);
            Assert.AreEqual(0f, boss.StillSeconds);
            Assert.AreEqual(1f, boss.ShotMultiple);
        }

        // ---- data ----

        [Test]
        public void TheStandardKindHasNoShotOrContactTraitButCommand()
        {
            BossDef standard = BossCatalog.Get(BossCatalog.Standard);

            Assert.AreEqual(0f, standard.SteadySeconds);
            Assert.AreEqual(1f, standard.SteadyShotMultiple);
            Assert.AreEqual(1f, standard.MovingContactMultiple);
            Assert.AreEqual(1f, standard.StillContactMultiple);
            Assert.AreEqual(1.25f, standard.SummonedSpeedMultiple, 0.0001f);
            Assert.AreEqual(1f, BossCatalog.Get(BossCatalog.Giant).SummonedSpeedMultiple);
            Assert.AreEqual(1f, BossCatalog.Get(BossCatalog.Sniper).SummonedSpeedMultiple);
        }

        [Test]
        public void EveryTraitValueIsPositiveAndASteadyKindHitsHarder()
        {
            foreach (BossDef def in BossCatalog.All)
            {
                Assert.GreaterOrEqual(def.SteadySeconds, 0f, def.Name);
                Assert.Greater(def.SteadyShotMultiple, 0f, def.Name);
                Assert.Greater(def.MovingContactMultiple, 0f, def.Name);
                Assert.Greater(def.StillContactMultiple, 0f, def.Name);
                if (def.SteadySeconds > 0f) Assert.Greater(def.SteadyShotMultiple, 1f, def.Name);
            }
        }

        [Test]
        public void TheTraitsAreCopiedIntoTheMatchData()
        {
            foreach (BossDef def in BossCatalog.All)
            {
                BossDef copy = BossCatalog.ForMatch(def.Id);
                Assert.AreEqual(def.SteadySeconds, copy.SteadySeconds, def.Name);
                Assert.AreEqual(def.SteadyShotMultiple, copy.SteadyShotMultiple, def.Name);
                Assert.AreEqual(def.MovingContactMultiple, copy.MovingContactMultiple, def.Name);
                Assert.AreEqual(def.StillContactMultiple, copy.StillContactMultiple, def.Name);
            }
        }

        // ---- Steady ----

        [Test]
        public void TheSniperIsSteadyOnceItHasStoodStillForASecond()
        {
            BossSim boss = Boss(BossCatalog.Sniper);
            BossDef def = BossCatalog.Get(BossCatalog.Sniper);

            Stand(boss, def.SteadySeconds * 0.5f);
            Assert.AreEqual(1f, boss.ShotMultiple, "not yet steady");

            Stand(boss, def.SteadySeconds * 0.5f + Dt);
            Assert.AreEqual(def.SteadyShotMultiple, boss.ShotMultiple, 0.0001f);
        }

        [Test]
        public void MovingEndsTheSteadyBonusAtOnce()
        {
            BossSim boss = Boss(BossCatalog.Sniper);
            Stand(boss, 2f);
            Assert.Greater(boss.ShotMultiple, 1f);

            Move(boss, 1f, 0f);

            Assert.AreEqual(1f, boss.ShotMultiple);
        }

        [Test]
        public void TheSteadyMultipleScalesTheDamageAndRoundsAtLeastToOne()
        {
            BossDef sniper = BossCatalog.Get(BossCatalog.Sniper);
            Assert.AreEqual(1f, BossTraits.ShotMultiple(sniper, 0.5f));
            Assert.AreEqual(sniper.SteadyShotMultiple, BossTraits.ShotMultiple(sniper, 1f));
            Assert.AreEqual(1f, BossTraits.ShotMultiple(null, 5f));
            Assert.AreEqual(1f, BossTraits.ShotMultiple(BossCatalog.Get(BossCatalog.Standard), 99f));
            Assert.AreEqual(1f, BossTraits.ShotMultiple(BossCatalog.Get(BossCatalog.Giant), 99f));
        }

        // ---- Momentum ----

        [Test]
        public void TheMovingGiantTouchesHarderAndTheStandingGiantSofter()
        {
            BossDef giant = BossCatalog.Get(BossCatalog.Giant);
            int normal = giant.ContactDamage;

            BossSim moving = Boss(BossCatalog.Giant);
            Move(moving, 0.1f, 0f);
            int movingHit = Touch(moving);
            Assert.AreEqual(Mathf.RoundToInt(normal * giant.MovingContactMultiple), movingHit);

            BossSim standing = Boss(BossCatalog.Giant);
            Stand(standing, 0.5f);
            int standingHit = Touch(standing);
            Assert.AreEqual(Mathf.RoundToInt(normal * giant.StillContactMultiple), standingHit);

            Assert.Greater(movingHit, normal);
            Assert.Less(standingHit, normal);
        }

        [Test]
        public void TheOtherKindsTouchForTheirPlainContactDamageEitherWay()
        {
            foreach (byte kind in new[] { BossCatalog.Standard, BossCatalog.Sniper })
            {
                int normal = BossCatalog.Get(kind).ContactDamage;

                BossSim moving = Boss(kind);
                Move(moving, 0.1f, 0f);
                Assert.AreEqual(normal, Touch(moving), $"kind {kind} moving");

                BossSim standing = Boss(kind);
                Stand(standing, 0.5f);
                Assert.AreEqual(normal, Touch(standing), $"kind {kind} standing");
            }
        }

        [Test]
        public void TheContactDamageIsNeverBelowOne()
        {
            var def = new BossDef { ContactDamage = 1, StillContactMultiple = 0.1f };
            Assert.AreEqual(1, BossTraits.ContactDamage(def, 1, moving: false));
        }

        // ---- the switch and the world ----

        [Test]
        public void TheTraitsCanBeTurnedOffInTheHarness()
        {
            Assert.IsTrue(HarnessOverrides.IsKnown("bossTraits"));
            Assert.IsNull(Settings(("bossTraits", 0f)).Validate());
            Assert.IsNull(Settings(("bossTraits", 1f)).Validate());
            StringAssert.Contains("bossTraits", Settings(("bossTraits", 2f)).Validate());

            MatchSession off = MatchSession.Start(SettingsFor(BossCatalog.Sniper, ("bossTraits", 0f)), 1);
            for (int i = 0; i < 100; i++) off.Advance();
            Assert.IsFalse(off.World.Boss.TraitsEnabled);
            Assert.AreEqual(1f, off.World.Boss.ShotMultiple);

            MatchSession on = MatchSession.Start(SettingsFor(BossCatalog.Sniper), 1);
            for (int i = 0; i < 100; i++) on.Advance();
            Assert.IsTrue(on.World.Boss.TraitsEnabled);
            Assert.Greater(on.World.Boss.ShotMultiple, 1f, "the idle boss of the session stands still");
            BossCatalog.SetCurrentKind(BossCatalog.Standard);
        }

        [Test]
        public void TheWorldStepsTheBossStillTimeAndTheStandardBossStaysPlain()
        {
            MatchSession s = MatchSession.Start(Settings(), 1);
            for (int i = 0; i < 100; i++) s.Advance();

            Assert.Greater(s.World.Boss.StillSeconds, 1.5f);
            Assert.AreEqual(1f, s.World.Boss.ShotMultiple);
        }

        [Test]
        public void AMatchWithTraitsOnIsRepeatableWithTheSameSeed()
        {
            string Run()
            {
                MatchSession s = MatchSession.Start(SettingsFor(BossCatalog.Giant), 7);
                for (int i = 0; i < 400; i++) s.Advance();
                BossCatalog.SetCurrentKind(BossCatalog.Standard);
                return $"{s.World.Boss.Health}/{s.World.Mobs.Count}/{s.World.Survivors.Health(s.SurvivorIds[0])}";
            }

            Assert.AreEqual(Run(), Run());
        }

        // ---- Command (the standard boss) ----

        static float TravelOf(byte kind, byte origin, params (string name, float value)[] overrides)
        {
            MatchSession s = MatchSession.Start(SettingsFor(kind, overrides), 2);
            s.World.Mobs.Clear();
            s.World.Mobs.TrySpawn(new Vector2(10f, 0f), MobKinds.Encode(origin, MobKinds.Basic), out _);
            s.World.StepMobs(0f);
            Vector2 before = s.World.Mobs.PositionAt(0);
            for (int i = 0; i < 20; i++) s.World.StepMobs(Dt);
            return Vector2.Distance(before, s.World.Mobs.PositionAt(0));
        }

        [Test]
        public void TheStandardBossMobsMoveTwentyFivePercentFasterWhenSummonedOnly()
        {
            float automatic = TravelOf(BossCatalog.Standard, MobKinds.OriginAuto);
            float summoned = TravelOf(BossCatalog.Standard, MobKinds.OriginSummoned);

            Assert.Greater(automatic, 0f);
            Assert.AreEqual(automatic * 1.25f, summoned, automatic * 0.02f);
        }

        [Test]
        public void TheOtherKindsAndTheTraitsOffLeaveSummonedMobsAtTheirNormalSpeed()
        {
            float normal = TravelOf(BossCatalog.Standard, MobKinds.OriginAuto);
            Assert.AreEqual(normal, TravelOf(BossCatalog.Giant, MobKinds.OriginSummoned), normal * 0.02f);
            Assert.AreEqual(normal, TravelOf(BossCatalog.Sniper, MobKinds.OriginSummoned), normal * 0.02f);
            Assert.AreEqual(normal, TravelOf(BossCatalog.Standard, MobKinds.OriginSummoned, ("bossTraits", 0f)), normal * 0.02f);
        }
    }
}
