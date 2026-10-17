using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class BossShotReplayTests
    {
        static ProjectileReplay Make()
        {
            var replay = new ProjectileReplay();
            replay.ShapeOfSlot = slot => slot == RoleTable.BossRole ? BossAttack.Shape : ProjectileShape.Default;
            return replay;
        }

        [Test]
        public void ABossShotIsDrawnWithTheBossShape()
        {
            ProjectileReplay replay = Make();
            replay.OnFire(1, RoleTable.BossRole, Vector2.zero, 0, 0f);

            replay.Advance(0.5f);

            Assert.AreEqual(1, replay.Count);
            Assert.AreEqual(RoleTable.BossRole, replay.SlotAt(0));
            Assert.AreEqual(BossAttack.Shape.Radius, replay.ShapeAt(0).Radius, 1e-6f);
            Assert.AreEqual(BossAttack.Shape.Speed * 0.5f, replay.PositionAt(0).x, 1e-3f);
        }

        [Test]
        public void ABossShotIsLargerThanASurvivorShot()
        {
            ProjectileReplay replay = Make();
            replay.OnFire(1, RoleTable.BossRole, Vector2.zero, 0, 0f);
            replay.OnFire(2, 1, Vector2.zero, 0, 0f);

            replay.Advance(0.1f);

            float boss = 0f, survivor = 0f;
            for (int i = 0; i < replay.Count; i++)
            {
                if (replay.SlotAt(i) == RoleTable.BossRole) boss = replay.ShapeAt(i).Radius;
                else survivor = replay.ShapeAt(i).Radius;
            }
            Assert.Greater(boss, survivor);
        }

        [Test]
        public void AShotThatHitsKeepsFlyingOnScreenUntilTheHitTime()
        {
            ProjectileReplay replay = Make();
            replay.OnFire(1, RoleTable.BossRole, Vector2.zero, 0, 0f);
            replay.OnHit(1, 3, 85, false, new Vector2(4f, 0f), 0.4f);

            replay.Advance(0.2f);
            var due = new List<HitEffect>();
            replay.DrainDueEffects(due);
            Assert.AreEqual(0, due.Count, "not yet");
            Assert.AreEqual(1, replay.Count, "the shot is still flying");

            replay.Advance(0.5f);
            replay.DrainDueEffects(due);

            Assert.AreEqual(0, replay.Count, "it ended at the hit");
            Assert.AreEqual(1, due.Count);
        }

        [Test]
        public void TheSurvivorMarkReachesTheHitEffect()
        {
            ProjectileReplay replay = Make();
            replay.OnFire(1, RoleTable.BossRole, Vector2.zero, 0, 0f);
            replay.OnHit(1, 3, 85, false, new Vector2(4f, 0f), 0.4f, false, true);

            replay.Advance(0.5f);
            var due = new List<HitEffect>();
            replay.DrainDueEffects(due);

            Assert.AreEqual(1, due.Count);
            Assert.IsTrue(due[0].Survivor);
            Assert.AreEqual(3, due[0].MobId);
            Assert.AreEqual(new Vector2(4f, 0f), due[0].Position);
        }

        [Test]
        public void AMobHitIsNotMarkedAsASurvivorHit()
        {
            ProjectileReplay replay = Make();
            replay.OnFire(1, 1, Vector2.zero, 0, 0f);
            replay.OnHit(1, 9, 50, false, new Vector2(2f, 0f), 0.2f);

            replay.Advance(0.5f);
            var due = new List<HitEffect>();
            replay.DrainDueEffects(due);

            Assert.AreEqual(1, due.Count);
            Assert.IsFalse(due[0].Survivor);
        }
    }
}
