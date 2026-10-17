using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class SurvivorSimTests
    {
        const float Dt = 0.02f;
        const ulong A = 1, B = 2, C = 3;

        static SurvivorSim NewSim(params ulong[] clients)
        {
            var sim = new SurvivorSim();
            foreach (ulong id in clients) sim.Register(id);
            return sim;
        }

        static SurvivorBody Body(ulong id, float x, float y, bool moving = false) =>
            new SurvivorBody { ClientId = id, Position = new Vector2(x, y), Moving = moving };

        static MobSim NoMobs() => new MobSim(targetCount: 0);

        static void Run(SurvivorSim sim, List<SurvivorBody> bodies, MobSim mobs, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++) sim.Step(Dt, bodies, mobs);
        }

        // ---- 2.1 health, damage, protection, death ----

        [Test]
        public void Survivors_StartWithFullHealth()
        {
            SurvivorSim sim = NewSim(A);
            Assert.AreEqual(CombatConfig.SurvivorMaxHealth, sim.Health(A));
            Assert.IsTrue(sim.IsAlive(A));
        }

        [Test]
        public void Damage_ReducesHealth()
        {
            SurvivorSim sim = NewSim(A);

            Assert.IsTrue(sim.ApplyDamage(A, 10));

            Assert.AreEqual(90, sim.Health(A));
        }

        [Test]
        public void Damage_NeverGoesBelowZeroAndKills()
        {
            SurvivorSim sim = NewSim(A);
            sim.ApplyDamage(A, 95);
            RunProtection(sim);

            Assert.IsTrue(sim.ApplyDamage(A, 10));

            Assert.AreEqual(0, sim.Health(A));
            Assert.IsFalse(sim.IsAlive(A));
        }

        static void RunProtection(SurvivorSim sim)
        {
            Run(sim, new List<SurvivorBody>(), NoMobs(), sim.InvulnerabilitySeconds + 0.1f);
        }

        [Test]
        public void Protection_BlocksDamageUntilItEnds()
        {
            SurvivorSim sim = NewSim(A);
            sim.ApplyDamage(A, 10);
            Assert.IsTrue(sim.IsInvulnerable(A));

            Assert.IsFalse(sim.ApplyDamage(A, 10), "immediately after a hit");
            Run(sim, new List<SurvivorBody>(), NoMobs(), 0.5f);
            Assert.IsFalse(sim.ApplyDamage(A, 10), "still inside the protection time");
            Assert.AreEqual(90, sim.Health(A));

            Run(sim, new List<SurvivorBody>(), NoMobs(), 0.2f);
            Assert.IsFalse(sim.IsInvulnerable(A));
            Assert.IsTrue(sim.ApplyDamage(A, 10));
            Assert.AreEqual(80, sim.Health(A));
        }

        [Test]
        public void Dead_SurvivorsTakeNoDamage()
        {
            SurvivorSim sim = NewSim(A);
            sim.Kill(A);

            Assert.IsFalse(sim.ApplyDamage(A, 10));
            Assert.AreEqual(0, sim.Health(A));
        }

        [Test]
        public void Damage_ZeroOrNegativeAndUnknownSurvivorsAreIgnored()
        {
            SurvivorSim sim = NewSim(A);

            Assert.IsFalse(sim.ApplyDamage(A, 0));
            Assert.IsFalse(sim.ApplyDamage(A, -5));
            Assert.IsFalse(sim.ApplyDamage(99, 10));
            Assert.AreEqual(CombatConfig.SurvivorMaxHealth, sim.Health(A));
            Assert.IsFalse(sim.IsInvulnerable(A), "ignored damage does not start the protection time");
        }

        // ---- 2.2 contact damage ----

        [Test]
        public void Contact_DamagesAtouchedSurvivor()
        {
            SurvivorSim sim = NewSim(A);
            MobSim mobs = NoMobs();
            mobs.TrySpawn(new Vector2(0.5f, 0), 0, out _);
            var bodies = new List<SurvivorBody> { Body(A, 0, 0) };

            sim.Step(Dt, bodies, mobs);

            Assert.AreEqual(CombatConfig.SurvivorMaxHealth - CombatConfig.ContactDamage, sim.Health(A));
        }

        [Test]
        public void Contact_ADistantMobDoesNothing()
        {
            SurvivorSim sim = NewSim(A);
            MobSim mobs = NoMobs();
            mobs.TrySpawn(new Vector2(5, 0), 0, out _);

            sim.Step(Dt, new List<SurvivorBody> { Body(A, 0, 0) }, mobs);

            Assert.AreEqual(CombatConfig.SurvivorMaxHealth, sim.Health(A));
        }

        [Test]
        public void Contact_ACrowdDamagesOncePerProtectionTime()
        {
            SurvivorSim sim = NewSim(A);
            MobSim mobs = NoMobs();
            for (int i = 0; i < 10; i++) mobs.TrySpawn(new Vector2(0.2f, 0.1f * i), 0, out _);
            var bodies = new List<SurvivorBody> { Body(A, 0, 0) };

            sim.Step(Dt, bodies, mobs);
            Assert.AreEqual(90, sim.Health(A), "ten mobs, one hit");

            Run(sim, bodies, mobs, 0.4f);
            Assert.AreEqual(90, sim.Health(A), "still protected");

            Run(sim, bodies, mobs, 0.3f);
            Assert.AreEqual(80, sim.Health(A), "protection ended, next hit");
        }

        [Test]
        public void Contact_DoesNotHurtTheDead()
        {
            SurvivorSim sim = NewSim(A);
            sim.Kill(A);
            MobSim mobs = NoMobs();
            mobs.TrySpawn(new Vector2(0.2f, 0), 0, out _);

            Run(sim, new List<SurvivorBody> { Body(A, 0, 0) }, mobs, 2f);

            Assert.AreEqual(0, sim.Health(A));
            Assert.IsFalse(sim.IsAlive(A));
        }

        [Test]
        public void Contact_KillsASurvivorAfterEnoughHits()
        {
            SurvivorSim sim = NewSim(A);
            MobSim mobs = NoMobs();
            mobs.TrySpawn(new Vector2(0.2f, 0), 0, out _);
            var bodies = new List<SurvivorBody> { Body(A, 0, 0) };

            Run(sim, bodies, mobs, 10f);

            Assert.IsFalse(sim.IsAlive(A));
        }

        // ---- 2.3 revival ----

        static List<SurvivorBody> Pair(bool rescuerMoving = false, float rescuerX = 1f) =>
            new List<SurvivorBody> { Body(A, 0, 0), Body(B, rescuerX, 0, rescuerMoving) };

        [Test]
        public void Revive_FillsLinearlyWhileARescuerStandsStill()
        {
            SurvivorSim sim = NewSim(A, B);
            sim.Kill(A);

            Run(sim, Pair(), NoMobs(), 2f);

            Assert.AreEqual(0.5f, sim.ReviveGauge(A), 0.03f);
            Assert.IsFalse(sim.IsAlive(A));
        }

        [Test]
        public void Revive_CompletesAfterTheReviveTime()
        {
            SurvivorSim sim = NewSim(A, B);
            sim.Kill(A);

            Run(sim, Pair(), NoMobs(), CombatConfig.ReviveSeconds + 0.1f);

            Assert.IsTrue(sim.IsAlive(A));
            Assert.AreEqual(Mathf.CeilToInt(CombatConfig.SurvivorMaxHealth * CombatConfig.ReviveHealthFraction), sim.Health(A));
            Assert.AreEqual(0f, sim.ReviveGauge(A));
            Assert.IsTrue(sim.IsInvulnerable(A));
        }

        [Test]
        public void Revive_ResetsWhenTheRescuerMoves()
        {
            SurvivorSim sim = NewSim(A, B);
            sim.Kill(A);
            Run(sim, Pair(), NoMobs(), 2f);
            Assert.Greater(sim.ReviveGauge(A), 0.4f);

            sim.Step(Dt, Pair(rescuerMoving: true), NoMobs());
            Assert.AreEqual(0f, sim.ReviveGauge(A));

            Run(sim, Pair(), NoMobs(), 2f);
            Assert.IsFalse(sim.IsAlive(A), "must fill from the start again");
            Assert.AreEqual(0.5f, sim.ReviveGauge(A), 0.03f);
        }

        [Test]
        public void Revive_ResetsWhenTheRescuerLeavesTheRange()
        {
            SurvivorSim sim = NewSim(A, B);
            sim.Kill(A);
            Run(sim, Pair(), NoMobs(), 2f);

            sim.Step(Dt, Pair(rescuerX: CombatConfig.ReviveRange + 0.5f), NoMobs());

            Assert.AreEqual(0f, sim.ReviveGauge(A));
        }

        [Test]
        public void Revive_ResetsWhenTheRescuerDies()
        {
            SurvivorSim sim = NewSim(A, B);
            sim.Kill(A);
            Run(sim, Pair(), NoMobs(), 2f);

            sim.Kill(B);
            sim.Step(Dt, Pair(), NoMobs());

            Assert.AreEqual(0f, sim.ReviveGauge(A));
        }

        [Test]
        public void Revive_ResetsWhenTheRescuerDisconnects()
        {
            SurvivorSim sim = NewSim(A, B);
            sim.Kill(A);
            Run(sim, Pair(), NoMobs(), 2f);

            sim.Unregister(B);
            sim.Step(Dt, new List<SurvivorBody> { Body(A, 0, 0) }, NoMobs());

            Assert.AreEqual(0f, sim.ReviveGauge(A));
        }

        [Test]
        public void Revive_NeedsALivingSurvivor_NobodyElseCounts()
        {
            // The boss is never listed as a survivor body, so it cannot revive anyone.
            SurvivorSim sim = NewSim(A);
            sim.Kill(A);

            Run(sim, new List<SurvivorBody> { Body(A, 0, 0) }, NoMobs(), 6f);

            Assert.IsFalse(sim.IsAlive(A));
            Assert.AreEqual(0f, sim.ReviveGauge(A));
        }

        [Test]
        public void Revive_TwoDeadSurvivorsCannotReviveEachOther()
        {
            SurvivorSim sim = NewSim(A, B);
            sim.Kill(A);
            sim.Kill(B);

            Run(sim, Pair(), NoMobs(), 6f);

            Assert.IsFalse(sim.IsAlive(A));
            Assert.IsFalse(sim.IsAlive(B));
            Assert.AreEqual(0f, sim.ReviveGauge(A));
            Assert.AreEqual(0f, sim.ReviveGauge(B));
        }

        [Test]
        public void Revive_AnotherRescuerDoesNotSpeedItUp()
        {
            SurvivorSim sim = NewSim(A, B, C);
            sim.Kill(A);
            var bodies = new List<SurvivorBody> { Body(A, 0, 0), Body(B, 1, 0), Body(C, -1, 0) };

            Run(sim, bodies, NoMobs(), 2f);

            Assert.AreEqual(0.5f, sim.ReviveGauge(A), 0.03f);
        }

        [Test]
        public void Revive_ASecondRescuerKeepsTheGaugeIfTheFirstMoves()
        {
            SurvivorSim sim = NewSim(A, B, C);
            sim.Kill(A);
            var bodies = new List<SurvivorBody> { Body(A, 0, 0), Body(B, 1, 0, moving: true), Body(C, -1, 0) };

            Run(sim, bodies, NoMobs(), 2f);

            Assert.AreEqual(0.5f, sim.ReviveGauge(A), 0.03f, "C is still standing still next to the body");
        }

        [Test]
        public void Revive_ProtectsTheRevivedSurvivorForAWhile()
        {
            SurvivorSim sim = NewSim(A, B);
            sim.Kill(A);
            Run(sim, Pair(), NoMobs(), CombatConfig.ReviveSeconds + 0.1f);
            Assert.IsTrue(sim.IsAlive(A));
            int revivedHealth = sim.Health(A);

            MobSim mobs = NoMobs();
            mobs.TrySpawn(new Vector2(0.2f, 0), 0, out _);
            Run(sim, Pair(), mobs, CombatConfig.ReviveInvulnerabilitySeconds - 0.2f);
            Assert.AreEqual(revivedHealth, sim.Health(A), "protected right after the revive");

            Run(sim, Pair(), mobs, 0.4f);
            Assert.Less(sim.Health(A), revivedHealth, "damage resumes after the protection ends");
        }

        [Test]
        public void Revive_MobsMayChooseTheRevivedSurvivorAgain()
        {
            SurvivorSim sim = NewSim(A, B);
            MobSim mobs = new MobSim(targetCount: 0);
            mobs.TrySpawn(new Vector2(0, 5), 0, out ushort id);
            sim.Kill(A);

            // A lies on the left, B stands on the right; the mob starts in the middle.
            Vector2 positionA = new Vector2(-6, 5);
            Vector2 positionB = new Vector2(8, 5);
            List<SurvivorInfo> Survivors() => new List<SurvivorInfo>
            {
                new SurvivorInfo { ClientId = A, Position = positionA, Alive = sim.IsAlive(A) },
                new SurvivorInfo { ClientId = B, Position = positionB, Alive = sim.IsAlive(B) },
            };

            for (int i = 0; i < 25; i++) mobs.Step(Dt, Survivors());
            Assert.Greater(PositionX(mobs, id), 0f, "dead A is ignored, the mob goes to B");

            // B walks over to A and revives it, then B moves far away. If A counts as a target again it is now
            // the nearest survivor and the mob turns around; if it did not, the mob would keep going to B.
            var reviving = new List<SurvivorBody> { Body(A, positionA.x, positionA.y), Body(B, -5, 5) };
            Run(sim, reviving, NoMobs(), CombatConfig.ReviveSeconds + 0.1f);
            Assert.IsTrue(sim.IsAlive(A));
            positionB = new Vector2(14, 5);

            float before = PositionX(mobs, id);
            for (int i = 0; i < 25; i++) mobs.Step(Dt, Survivors());
            Assert.Less(PositionX(mobs, id), before, "the revived survivor is chosen again");
        }

        static float PositionX(MobSim mobs, ushort id)
        {
            for (int i = 0; i < mobs.Count; i++)
                if (mobs.IdAt(i) == id) return mobs.PositionAt(i).x;
            Assert.Fail("mob missing");
            return 0f;
        }

        [Test]
        public void Revive_DoesNotEndTheRoomWhenEveryoneIsDown()
        {
            SurvivorSim sim = NewSim(A, B);
            sim.Kill(A);
            sim.Kill(B);

            Assert.DoesNotThrow(() => Run(sim, Pair(), NoMobs(), 3f));
            Assert.AreEqual(0f, sim.ReviveGauge(A));
        }
    }
}
