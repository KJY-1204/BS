using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class BossPhaseWorldTests
    {
        static HarnessSettings Settings(params (string name, float value)[] overrides)
        {
            var s = new HarnessSettings();
            s.overrides.Add(new NamedValue { name = "mobAutoTarget", value = 0f });
            s.overrides.Add(new NamedValue { name = "matchSeconds", value = 60f });
            foreach ((string name, float value) o in overrides) s.overrides.Add(new NamedValue { name = o.name, value = o.value });
            return s;
        }

        static MatchSession Start(params (string name, float value)[] overrides)
        {
            BossCatalog.SetCurrentKind(BossCatalog.Standard);
            return MatchSession.Start(Settings(overrides), 1);
        }

        static void HurtTo(MatchSession s, float fraction)
        {
            BossSim boss = s.World.Boss;
            int target = Mathf.RoundToInt(boss.MaxHealth * fraction);
            boss.TakeDamage(boss.Health - target, out _, out _);
        }

        static BossSkillResult UseSkill(MatchSession s, int slot, ulong who = MatchSession.BossId)
        {
            var events = new List<CombatEvent>();
            return s.World.UseBossSkill(who, slot, 0, events);
        }

        [Test]
        public void TheBossLosingHealthMovesTheWorldIntoPhaseTwoAndAnnouncesTheBurst()
        {
            MatchSession s = Start();
            Assert.AreEqual(1, s.World.BossSkills.Phase);

            HurtTo(s, 0.6f);
            s.Advance();

            Assert.AreEqual(2, s.World.BossSkills.Phase);
            Assert.IsTrue(s.World.CombatEvents.Any(e => e.Kind == CombatEventKind.Skill && e.SkillId == BossSkills.WarningIdRoar), "the announcement is among this step's events");
        }

        [Test]
        public void TheBurstArrivesAfterItsWarningAsMobsInTheWorld()
        {
            MatchSession s = Start();
            int before = s.World.Mobs.Count;
            HurtTo(s, 0.6f);

            for (int i = 0; i < Mathf.RoundToInt((BossPhases.BurstWarningSeconds + 0.2f) / 0.02f); i++) s.Advance();

            Assert.AreEqual(before + BossPhases.BurstMobs, s.World.Mobs.Count);
        }

        [Test]
        public void TheThirdSkillIsLockedUntilPhaseTwoThroughTheWorldRequest()
        {
            MatchSession s = Start();
            Assert.AreEqual(BossSkillResult.Locked, UseSkill(s, BossSkills.PhaseSlot));

            HurtTo(s, 0.6f);
            s.Advance();

            Assert.AreEqual(BossSkillResult.Used, UseSkill(s, BossSkills.PhaseSlot));
            Assert.AreEqual(BossSkillResult.Ignored, UseSkill(s, BossSkills.PhaseSlot, who: 1), "a survivor's request");
        }

        [Test]
        public void WithThePhasesOffTheMatchStaysInPhaseOne()
        {
            MatchSession s = Start(("bossPhases", 0f));
            Assert.IsFalse(s.World.BossPhasesEnabled);

            HurtTo(s, 0.1f);
            for (int i = 0; i < 80; i++) s.Advance();

            Assert.AreEqual(1, s.World.BossSkills.Phase);
            Assert.AreEqual(0, s.World.BossSkills.BurstMobs);
            Assert.AreEqual(0, s.World.Mobs.Count);
            Assert.AreEqual(BossSkillResult.Locked, UseSkill(s, BossSkills.PhaseSlot));
        }

        [Test]
        public void ThePhasesAreOnByDefault()
        {
            Assert.IsTrue(new MatchWorld().BossPhasesEnabled);
            Assert.IsTrue(DevArgs.BossPhases);
        }

        [Test]
        public void ThePhaseOverrideIsKnownAndOnlyTakesZeroOrOne()
        {
            Assert.IsTrue(HarnessOverrides.IsKnown("bossPhases"));
            Assert.IsNull(Settings(("bossPhases", 0f)).Validate());
            Assert.IsNull(Settings(("bossPhases", 1f)).Validate());
            StringAssert.Contains("bossPhases", Settings(("bossPhases", 2f)).Validate());
            StringAssert.Contains("bossPhases", Settings(("bossPhases", 0.5f)).Validate());
        }

        [Test]
        public void ANewMatchStartsInPhaseOneAgain()
        {
            MatchSession first = Start();
            HurtTo(first, 0.1f);
            first.Advance();
            Assert.AreEqual(3, first.World.BossSkills.Phase);

            MatchSession second = Start();

            Assert.AreEqual(1, second.World.BossSkills.Phase);
        }

        [Test]
        public void AMatchThatNeverGetsTheBossBelowTheThresholdPlaysTheSameWithThePhasesOnOrOff()
        {
            MatchRow on = MatchRunner.RunOne(Settings(), 3);
            MatchRow off = MatchRunner.RunOne(Settings(("bossPhases", 0f)), 3);

            Assert.AreEqual(on.stateHash, off.stateHash);
            Assert.AreEqual(on.lengthSeconds, off.lengthSeconds);
        }

        [Test]
        public void APracticeWorldWithoutABossHasNoPhaseChange()
        {
            var world = new MatchWorld(new MatchWorldOptions { MobTarget = 0 }) { MatchRunning = true };
            world.Clock.Start(false, 60f);

            world.StepCombat(0.02f);

            Assert.AreEqual(1, world.BossSkills.Phase);
            Assert.AreEqual(0, world.CombatEvents.Count);
        }
    }
}
