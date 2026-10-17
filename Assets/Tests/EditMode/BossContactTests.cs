using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class BossContactTests
    {
        const float Dt = 0.02f;
        const ulong A = 1, B = 2;

        static BossSim Boss(float x, float y, int contact = 25, float radius = 0.9f, int health = 1000) =>
            new BossSim(new BossDef { Name = "t", MaxHealth = health, MoveSpeed = 6f, Radius = radius, ContactDamage = contact, MatchSeconds = 60f }, new Vector2(x, y));

        static SurvivorBody Body(ulong id, float x, float y, bool moving = false) =>
            new SurvivorBody { ClientId = id, Position = new Vector2(x, y), Moving = moving };

        static SurvivorSim Survivors(params ulong[] ids)
        {
            var sim = new SurvivorSim();
            foreach (ulong id in ids) sim.Register(id);
            return sim;
        }

        static void Run(SurvivorSim sim, List<SurvivorBody> bodies, MobSim mobs, BossSim boss, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++) sim.Step(Dt, bodies, mobs, boss);
        }

        [Test]
        public void TouchingTheBossCostsItsContactDamage_AndStartsTheProtection()
        {
            SurvivorSim sim = Survivors(A);
            BossSim boss = Boss(0.5f, 0f, contact: 25);
            var bodies = new List<SurvivorBody> { Body(A, 0f, 0f) };

            sim.Step(Dt, bodies, null, boss);

            Assert.AreEqual(CombatConfig.SurvivorMaxHealth - 25, sim.Health(A));
            Assert.IsTrue(sim.IsInvulnerable(A));
        }

        [Test]
        public void TheBossHurtsMoreThanAMob()
        {
            var withBoss = Survivors(A);
            var withMob = Survivors(A);
            var mobs = new MobSim(targetCount: 0);
            mobs.TrySpawn(new Vector2(0.2f, 0), 0, out _);
            var bodies = new List<SurvivorBody> { Body(A, 0f, 0f) };

            withBoss.Step(Dt, bodies, null, Boss(0.5f, 0f));
            withMob.Step(Dt, bodies, mobs, null);

            Assert.Less(withBoss.Health(A), withMob.Health(A));
        }

        [Test]
        public void StayingInContactRepeatsNoFasterThanTheProtectionAllows()
        {
            SurvivorSim sim = Survivors(A);
            sim.MaxHealth = 100000;
            sim.Register(A);
            BossSim boss = Boss(0.5f, 0f, contact: 25);
            var bodies = new List<SurvivorBody> { Body(A, 0f, 0f) };

            Run(sim, bodies, null, boss, 3f);

            int hits = (100000 - sim.Health(A)) / 25;
            int most = Mathf.CeilToInt(3f / sim.InvulnerabilitySeconds) + 1;
            Assert.GreaterOrEqual(hits, 3);
            Assert.LessOrEqual(hits, most);
        }

        [Test]
        public void AFarSurvivorIsNotHurt()
        {
            SurvivorSim sim = Survivors(A);
            BossSim boss = Boss(5f, 0f);
            var bodies = new List<SurvivorBody> { Body(A, 0f, 0f) };

            Run(sim, bodies, null, boss, 1f);

            Assert.AreEqual(CombatConfig.SurvivorMaxHealth, sim.Health(A));
        }

        [Test]
        public void TheBossBodyIsBigSoItTouchesFromFartherThanAMob()
        {
            SurvivorSim sim = Survivors(A);
            BossSim boss = Boss(1.3f, 0f, radius: 0.9f); // beyond a mob's reach (0.9) but within the boss's (1.4)
            var mobs = new MobSim(targetCount: 0);
            var bodies = new List<SurvivorBody> { Body(A, 0f, 0f) };

            sim.Step(Dt, bodies, null, boss);

            Assert.Less(sim.Health(A), CombatConfig.SurvivorMaxHealth);
        }

        [Test]
        public void AFallenBossDealsNoDamage()
        {
            SurvivorSim sim = Survivors(A);
            BossSim boss = Boss(0.5f, 0f, health: 10);
            boss.TakeDamage(10, out _, out _);
            var bodies = new List<SurvivorBody> { Body(A, 0f, 0f) };

            Run(sim, bodies, null, boss, 2f);

            Assert.AreEqual(CombatConfig.SurvivorMaxHealth, sim.Health(A));
        }

        [Test]
        public void TimeStopDoesNotStopTheBossHurtingSurvivors()
        {
            SurvivorSim sim = Survivors(A);
            var mobs = new MobSim(targetCount: 0);
            mobs.Freeze(5f);
            BossSim boss = Boss(0.5f, 0f);
            var bodies = new List<SurvivorBody> { Body(A, 0f, 0f) };

            sim.Step(Dt, bodies, mobs, boss);

            Assert.Less(sim.Health(A), CombatConfig.SurvivorMaxHealth);
        }

        [Test]
        public void AMobAndTheBossTouchingTogetherCostOnlyOneHit()
        {
            SurvivorSim sim = Survivors(A);
            var mobs = new MobSim(targetCount: 0);
            mobs.TrySpawn(new Vector2(0.1f, 0), 0, out _);
            BossSim boss = Boss(0.5f, 0f, contact: 25);
            var bodies = new List<SurvivorBody> { Body(A, 0f, 0f) };

            sim.Step(Dt, bodies, mobs, boss);

            Assert.AreEqual(CombatConfig.SurvivorMaxHealth - 25, sim.Health(A), "one protection time, one hit, and the bigger one");
        }

        [Test]
        public void AProtectedSurvivorIsNotHurtByTheBoss()
        {
            SurvivorSim sim = Survivors(A);
            sim.GrantInvulnerability(A, 2f);
            BossSim boss = Boss(0.5f, 0f);
            var bodies = new List<SurvivorBody> { Body(A, 0f, 0f) };

            Run(sim, bodies, null, boss, 1.5f);

            Assert.AreEqual(CombatConfig.SurvivorMaxHealth, sim.Health(A));
        }

        [Test]
        public void ADeadSurvivorIsNotHurtAgain_AndNoBossMeansNoChange()
        {
            SurvivorSim sim = Survivors(A);
            sim.Kill(A);
            var bodies = new List<SurvivorBody> { Body(A, 0f, 0f) };

            Run(sim, bodies, null, Boss(0.5f, 0f), 1f);
            Assert.AreEqual(0, sim.Health(A));

            SurvivorSim other = Survivors(B);
            Run(other, new List<SurvivorBody> { Body(B, 0f, 0f) }, null, null, 1f);
            Assert.AreEqual(CombatConfig.SurvivorMaxHealth, other.Health(B));
        }

        [Test]
        public void TheBossDoesNotInterruptARevival()
        {
            SurvivorSim sim = Survivors(A, B);
            sim.Kill(A);
            BossSim boss = Boss(3f, 0f); // near the rescue but not touching
            var bodies = new List<SurvivorBody> { Body(A, 0f, 0f), Body(B, 0.5f, 0f, moving: false) };

            Run(sim, bodies, null, boss, 1f);
            float gauge = sim.ReviveGauge(A);
            Run(sim, bodies, null, boss, 1f);

            Assert.Greater(sim.ReviveGauge(A), gauge, "the gauge keeps filling with the boss standing near");
        }

        [Test]
        public void ARescuerTouchedByTheBossStillRevives_ButTakesTheDamage()
        {
            SurvivorSim sim = Survivors(A, B);
            sim.MaxHealth = 100000;
            sim.Register(B);
            sim.Kill(A);
            BossSim boss = Boss(1.2f, 0f, contact: 25); // reaches B but not the fallen A at 1.5 away on the other side
            var bodies = new List<SurvivorBody> { Body(A, -0.2f, 0f), Body(B, 0.4f, 0f, moving: false) };

            Run(sim, bodies, null, boss, 5f);

            Assert.IsTrue(sim.IsAlive(A), "revived despite the boss next to the rescuer");
            Assert.Less(sim.Health(B), 100000);
        }
    }
}
