using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class SkillEffectReplayTests
    {
        static SkillEffect Effect(float time, byte skill = SkillIds.Burst, byte slot = 1) =>
            new SkillEffect { SkillId = skill, Slot = slot, Position = new Vector2(time, 0), Rank = 1, ServerTime = time };

        static List<SkillEffect> Due(SkillEffectReplay replay)
        {
            var list = new List<SkillEffect>();
            replay.DrainDue(list);
            return list;
        }

        [Test]
        public void AnEarlyEffect_WaitsUntilTheDisplayClockReachesIt()
        {
            var replay = new SkillEffectReplay();
            replay.OnSkill(Effect(10.5f));

            replay.Advance(10.0f);
            Assert.AreEqual(0, Due(replay).Count);
            Assert.AreEqual(1, replay.PendingCount);

            replay.Advance(10.49f);
            Assert.AreEqual(0, Due(replay).Count);

            replay.Advance(10.5f);
            List<SkillEffect> due = Due(replay);
            Assert.AreEqual(1, due.Count);
            Assert.AreEqual(10.5f, due[0].ServerTime);
            Assert.AreEqual(0, replay.PendingCount);
        }

        [Test]
        public void ALateEffect_IsReleasedAtOnce()
        {
            var replay = new SkillEffectReplay();
            replay.Advance(20f);
            replay.OnSkill(Effect(12f));

            replay.Advance(20.02f);

            Assert.AreEqual(1, Due(replay).Count);
        }

        [Test]
        public void EffectsOfTheSameTick_KeepTheirArrivalOrder()
        {
            var replay = new SkillEffectReplay();
            replay.OnSkill(Effect(5f, SkillIds.Burst, 1));
            replay.OnSkill(Effect(5f, SkillIds.RapidFire, 2));
            replay.OnSkill(Effect(5f, SkillIds.Burst, 3));

            replay.Advance(5f);

            List<SkillEffect> due = Due(replay);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, new[] { due[0].Slot, due[1].Slot, due[2].Slot });
        }

        [Test]
        public void OnlyTheDueOnesAreReleased()
        {
            var replay = new SkillEffectReplay();
            replay.OnSkill(Effect(1f));
            replay.OnSkill(Effect(3f));
            replay.OnSkill(Effect(2f));

            replay.Advance(2f);

            Assert.AreEqual(2, Due(replay).Count);
            Assert.AreEqual(1, replay.PendingCount);
            replay.Advance(3f);
            Assert.AreEqual(1, Due(replay).Count);
        }

        [Test]
        public void ADrainEmptiesTheDueList()
        {
            var replay = new SkillEffectReplay();
            replay.OnSkill(Effect(1f));
            replay.Advance(2f);

            Assert.AreEqual(1, Due(replay).Count);
            Assert.AreEqual(0, Due(replay).Count);
        }

        [Test]
        public void TheQueueCannotGrowWithoutLimitIfTheClockStops()
        {
            var replay = new SkillEffectReplay();
            for (int i = 0; i < 1000; i++) replay.OnSkill(Effect(100f + i));

            Assert.AreEqual(SkillEffectReplay.MaxPending, replay.PendingCount);
        }

        [Test]
        public void Clear_ForgetsEverything()
        {
            var replay = new SkillEffectReplay();
            replay.OnSkill(Effect(1f));
            replay.Advance(2f);
            replay.OnSkill(Effect(9f));

            replay.Clear();

            Assert.AreEqual(0, replay.PendingCount);
            Assert.AreEqual(0, Due(replay).Count);
        }

        // ---- The display clock keeps running while there are no mobs ----

        [Test]
        public void TheDisplayClockFollowsSnapshotTimesEvenWithoutAnyMob()
        {
            var interpolator = new MobInterpolator();
            Assert.AreEqual(0, interpolator.Count);

            for (int i = 0; i < 30; i++)
            {
                interpolator.OnSnapshotTime(10f + i / MobConfig.SnapshotHz);
                for (int frame = 0; frame < 4; frame++) interpolator.Advance(1f / 60f);
            }

            Assert.Greater(interpolator.RenderTime, 11f, "the clock moved although no mob was ever listed");
        }

        [Test]
        public void WithoutAnySnapshotTheClockDoesNotMove_WhichIsWhyEmptySnapshotsAreSent()
        {
            var interpolator = new MobInterpolator();

            for (int frame = 0; frame < 120; frame++) interpolator.Advance(1f / 60f);

            Assert.AreEqual(0f, interpolator.RenderTime);
        }

        [Test]
        public void ShotsAndSkillEffectsKeepMovingWhenTheLastMobDies()
        {
            // The mob clock and a shot share one display time. Feed only empty snapshots and check both advance.
            var interpolator = new MobInterpolator();
            var shots = new ProjectileReplay();
            var skills = new SkillEffectReplay();

            interpolator.OnSnapshotTime(50f);
            shots.OnFire(1, 1, Vector2.zero, AimMath.QuantizeAngle(0f), 50.2f);
            skills.OnSkill(Effect(50.3f));

            for (int i = 0; i < 90; i++)
            {
                interpolator.OnSnapshotTime(50f + (i + 1) / MobConfig.SnapshotHz);
                interpolator.Advance(1f / 15f);
                shots.Advance(interpolator.RenderTime);
                skills.Advance(interpolator.RenderTime);
            }

            Assert.AreEqual(0, skills.PendingCount, "the skill effect came due");
            Assert.AreEqual(0, shots.TrackedCount, "the shot flew its whole life and was removed");
        }
    }
}
