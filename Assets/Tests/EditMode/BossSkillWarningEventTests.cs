using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class BossSkillWarningEventTests
    {
        [Test]
        public void TheWarningIsASkillEventThatRoundTripsThroughTheCodec()
        {
            var rig = new BossSkillSim();
            var events = new List<CombatEvent>();
            var state = new BossSkillState { MatchRunning = true, BossStanding = true, BossPosition = new Vector2(2.5f, -3f), Aim = 4321, HealthFraction = 1f };
            Assert.AreEqual(BossSkillResult.Used, rig.TryUse(BossSkills.Shockwave, true, state, 0, events));

            var buffer = new byte[CombatCodec.MaxBatchBytes];
            int length = CombatCodec.WriteBatch(buffer, 1000, events, 0, events.Count, id => 0);
            int offset = CombatCodec.HeaderSize;
            Assert.IsTrue(CombatCodec.TryReadEvent(buffer, length, ref offset, out CombatEvent read));

            Assert.AreEqual(CombatEventKind.Skill, read.Kind);
            Assert.AreEqual(BossSkills.WarningIdShockwave, read.SkillId);
            Assert.AreEqual(0, read.Slot);
            Assert.AreEqual(2.5f, read.Position.x, 0.001f);
            Assert.AreEqual(-3f, read.Position.y, 0.001f);
            Assert.AreEqual(4321, read.Angle);
            Assert.AreEqual(CombatCodec.HeaderSize + CombatCodec.SkillSize, length, "a warning is an ordinary skill event");
        }

        [Test]
        public void BothWarningNumbersFitTheOneByteSkillField()
        {
            Assert.LessOrEqual(BossSkills.WarningIdBarrage, byte.MaxValue);
            Assert.LessOrEqual(BossSkills.WarningIdShockwave, byte.MaxValue);
            Assert.AreEqual(BossSkills.WarningIdBarrage, BossSkills.WarningIdOf(BossSkills.Barrage));
            Assert.AreEqual(BossSkills.WarningIdShockwave, BossSkills.WarningIdOf(BossSkills.Shockwave));
        }

        [Test]
        public void TheWarningIsHeldUntilTheDisplayClockReachesIt()
        {
            var replay = new SkillEffectReplay();
            replay.OnSkill(new SkillEffect { SkillId = BossSkills.WarningIdShockwave, Position = Vector2.one, Rank = 1, ServerTime = 12f });
            var due = new List<SkillEffect>();

            replay.Advance(11.9f);
            replay.DrainDue(due);
            Assert.AreEqual(0, due.Count);

            replay.Advance(12f);
            replay.DrainDue(due);
            Assert.AreEqual(1, due.Count);
            Assert.IsTrue(BossSkills.IsWarningId(due[0].SkillId));
        }

        [Test]
        public void TheWarningThenTheEffectComeInOrderOnTheServerTimeline()
        {
            // The warning is published when the skill is used; the explosion event later, when the warning time has passed.
            var sim = new BossSkillSim();
            var events = new List<CombatEvent>();
            var survivors = new SurvivorSim();
            survivors.Register(1, 100);
            var bodies = new List<SurvivorBody> { new SurvivorBody { ClientId = 1, Position = new Vector2(1f, 0f) } };
            var state = new BossSkillState { MatchRunning = true, BossStanding = true, BossPosition = Vector2.zero, HealthFraction = 1f };
            sim.TryUse(BossSkills.Shockwave, true, state, 0, events);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(CombatEventKind.Skill, events[0].Kind);

            var later = new List<CombatEvent>();
            float t = 0f;
            for (; t < 1.2f && later.Count == 0; t += 0.02f)
                sim.Step(0.02f, true, true, new ProjectileSim(), survivors, bodies, 0, later);

            Assert.AreEqual(1, later.Count);
            Assert.AreEqual(CombatEventKind.Explosion, later[0].Kind);
            Assert.AreEqual(BossSkills.WarningSeconds, t, 0.05f);
        }

        [Test]
        public void ARingOfTheShockwaveRadiusIsSentInTenths()
        {
            Assert.AreEqual(40, CombatCodec.QuantizeRadius(BossSkills.ShockwaveRadius));
        }

        [Test]
        public void SurvivorSkillEventsAreNotWarnings()
        {
            foreach (byte id in SkillCatalog.All.Select(d => d.Id)) Assert.IsFalse(BossSkills.IsWarningId(id));
            Assert.IsFalse(BossSkills.IsWarningId(0));
        }
    }
}
