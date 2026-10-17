using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class SkillSimTests
    {
        const ulong A = 1, B = 2;
        const float Dt = 0.02f;
        const int Q = 0, E = 1;
        const byte Reimu = CharacterIds.Reimu, Marisa = CharacterIds.Marisa;

        static SkillSim New(params ulong[] clients)
        {
            var sim = new SkillSim();
            foreach (ulong id in clients) sim.Register(id, GrowthConfig.BaseMaxMana, GrowthConfig.BaseManaRegenPerSecond);
            return sim;
        }

        static int[] Ranks(int burst = 0, int rapid = 0, int haste = 0, int barrier = 0)
        {
            var ranks = new int[SkillCatalog.MaxSkillId + 1];
            ranks[SkillIds.Burst] = burst;
            ranks[SkillIds.RapidFire] = rapid;
            ranks[SkillIds.Haste] = haste;
            ranks[SkillIds.Barrier] = barrier;
            return ranks;
        }

        static void Run(SkillSim sim, float seconds)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds / Dt); i++) sim.Step(Dt);
        }

        // ---- 4.1 mana ----

        [Test]
        public void ManaStartsFull()
        {
            SkillSim sim = New(A);
            Assert.AreEqual(GrowthConfig.BaseMaxMana, sim.Mana(A));
            Assert.AreEqual(GrowthConfig.BaseMaxMana, sim.MaxMana(A));
        }

        [Test]
        public void ManaRefillsAtTheRateOverTime()
        {
            SkillSim sim = New(A);
            sim.TryActivate(A, Reimu, Q, true, Ranks(burst: 1), out _);
            float after = sim.Mana(A);

            Run(sim, 1f);

            Assert.AreEqual(after + GrowthConfig.BaseManaRegenPerSecond, sim.Mana(A), 0.1f);
        }

        [Test]
        public void ManaStopsAtTheMaximum()
        {
            SkillSim sim = New(A);
            Run(sim, 30f);
            Assert.AreEqual(GrowthConfig.BaseMaxMana, sim.Mana(A));

            sim.TryActivate(A, Reimu, Q, true, Ranks(burst: 1), out _);
            Run(sim, 60f);
            Assert.AreEqual(GrowthConfig.BaseMaxMana, sim.Mana(A));
        }

        [Test]
        public void RaisingTheMaximum_DoesNotRefill()
        {
            SkillSim sim = New(A);
            sim.TryActivate(A, Reimu, Q, true, Ranks(burst: 1), out _);
            float before = sim.Mana(A);

            sim.SetStats(A, GrowthConfig.BaseMaxMana + 30, GrowthConfig.BaseManaRegenPerSecond);

            Assert.AreEqual(before, sim.Mana(A));
            Assert.AreEqual(GrowthConfig.BaseMaxMana + 30, sim.MaxMana(A));
        }

        [Test]
        public void LoweringTheMaximumBelowMana_CutsItDown()
        {
            SkillSim sim = New(A);
            sim.SetStats(A, 40, 1f);
            Assert.AreEqual(40, sim.Mana(A));
        }

        [Test]
        public void ManaRefillFollowsTheNewRate()
        {
            SkillSim sim = New(A);
            sim.TryActivate(A, Reimu, Q, true, Ranks(burst: 1), out _);
            float before = sim.Mana(A);
            sim.SetStats(A, GrowthConfig.BaseMaxMana, 20f);

            Run(sim, 1f);

            Assert.AreEqual(before + 20f, sim.Mana(A), 0.2f);
        }

        // ---- 4.2 activation ----

        [Test]
        public void ALearnedReadySkillFires_SpendsManaAndStartsTheCooldown()
        {
            SkillSim sim = New(A);
            SkillDef burst = SkillCatalog.Get(SkillIds.Burst);

            SkillResult result = sim.TryActivate(A, Reimu, Q, true, Ranks(burst: 1), out SkillActivation act);

            Assert.AreEqual(SkillResult.Fired, result);
            Assert.AreEqual(GrowthConfig.BaseMaxMana - burst.ManaCost, sim.Mana(A), 1e-4f);
            Assert.AreEqual(burst.Cooldown, sim.CooldownLeft(A, Q), 1e-4f);
            Assert.AreEqual(SkillIds.Burst, act.SkillId);
            Assert.AreEqual(burst.Radius[0], act.Radius);
            Assert.AreEqual(burst.Damage[0], act.Damage);
            Assert.AreEqual(burst.Cooldown, act.Cooldown);
        }

        [Test]
        public void AnUnlearnedSlotDoesNothing()
        {
            SkillSim sim = New(A);

            Assert.AreEqual(SkillResult.NotLearned, sim.TryActivate(A, Reimu, Q, true, Ranks(), out _));
            Assert.AreEqual(SkillResult.NotLearned, sim.TryActivate(A, Reimu, E, true, Ranks(), out _));
            Assert.AreEqual(GrowthConfig.BaseMaxMana, sim.Mana(A));
            Assert.AreEqual(0f, sim.CooldownLeft(A, Q));
        }

        [Test]
        public void NotEnoughMana_DoesNotFireAndSpendsNothing()
        {
            SkillSim sim = New(A);
            sim.SetStats(A, 20, 0f);
            SkillDef burst = SkillCatalog.Get(SkillIds.Burst);
            Assert.Less(20, burst.ManaCost);

            SkillResult result = sim.TryActivate(A, Reimu, Q, true, Ranks(burst: 1), out _);

            Assert.AreEqual(SkillResult.NoMana, result);
            Assert.AreEqual(20f, sim.Mana(A));
            Assert.AreEqual(0f, sim.CooldownLeft(A, Q), "a refused request does not start the cooldown");
        }

        [Test]
        public void DuringTheCooldown_ItDoesNotFire()
        {
            SkillSim sim = New(A);
            sim.TryActivate(A, Reimu, Q, true, Ranks(burst: 1), out _);
            Run(sim, 2f);

            Assert.AreEqual(SkillResult.Cooldown, sim.TryActivate(A, Reimu, Q, true, Ranks(burst: 1), out _));

            Run(sim, SkillCatalog.Get(SkillIds.Burst).Cooldown);
            Assert.AreEqual(SkillResult.Fired, sim.TryActivate(A, Reimu, Q, true, Ranks(burst: 1), out _));
        }

        [Test]
        public void ADeadSurvivorCannotFire()
        {
            SkillSim sim = New(A);

            Assert.AreEqual(SkillResult.Dead, sim.TryActivate(A, Reimu, Q, false, Ranks(burst: 1), out _));
            Assert.AreEqual(GrowthConfig.BaseMaxMana, sim.Mana(A));
        }

        [Test]
        public void ManyRepeatedRequests_FireOncePerCooldown()
        {
            SkillSim sim = New(A);
            sim.SetStats(A, 1000, 100f);
            int fired = 0;

            // A request on every step for 20 seconds.
            for (int i = 0; i < 1000; i++)
            {
                sim.Step(Dt);
                if (sim.TryActivate(A, Reimu, Q, true, Ranks(burst: 1), out _) == SkillResult.Fired) fired++;
            }

            float cooldown = SkillCatalog.Get(SkillIds.Burst).Cooldown;
            Assert.AreEqual(Mathf.CeilToInt(20f / cooldown), fired, 1);
        }

        [Test]
        public void UnknownSlotsAndSurvivorsAreRefused()
        {
            SkillSim sim = New(A);

            Assert.AreEqual(SkillResult.UnknownSlot, sim.TryActivate(A, Reimu, 7, true, Ranks(burst: 1), out _));
            Assert.AreEqual(SkillResult.UnknownSlot, sim.TryActivate(A, Reimu, -1, true, Ranks(burst: 1), out _));
            Assert.AreEqual(SkillResult.UnknownSlot, sim.TryActivate(99, Reimu, Q, true, Ranks(burst: 1), out _));
        }

        [Test]
        public void TheSlotsHaveTheirOwnCooldowns()
        {
            SkillSim sim = New(A);
            int[] ranks = Ranks(burst: 1, barrier: 1);

            Assert.AreEqual(SkillResult.Fired, sim.TryActivate(A, Reimu, Q, true, ranks, out _));
            Assert.AreEqual(SkillResult.Fired, sim.TryActivate(A, Reimu, E, true, ranks, out _));
            Assert.Greater(sim.CooldownLeft(A, Q), 0f);
            Assert.Greater(sim.CooldownLeft(A, E), 0f);
        }

        [Test]
        public void HigherRanksGiveStrongerValues()
        {
            SkillSim sim = New(A);
            sim.SetStats(A, 1000, 0f);

            sim.TryActivate(A, Reimu, Q, true, Ranks(burst: 1), out SkillActivation one);
            Run(sim, 20f);
            sim.TryActivate(A, Reimu, Q, true, Ranks(burst: 3), out SkillActivation three);

            Assert.Greater(three.Radius, one.Radius);
            Assert.Greater(three.Damage, one.Damage);
        }

        // ---- Rapid Fire ----

        [Test]
        public void RapidFire_SpeedsUpTheBasicAttackThenEndsByItself()
        {
            SkillSim sim = New(A);
            int[] ranks = Ranks(rapid: 1, haste: 1);
            Assert.AreEqual(1f, sim.AttackMultiplier(A));

            SkillResult result = sim.TryActivate(A, Marisa, E, true, ranks, out SkillActivation act);

            Assert.AreEqual(SkillResult.Fired, result);
            SkillDef rapid = SkillCatalog.Get(SkillIds.RapidFire);
            Assert.AreEqual(rapid.Duration[0], sim.BuffLeft(A), 1e-4f);
            Assert.AreEqual(rapid.CooldownMultiplier[0], sim.AttackMultiplier(A));
            Assert.Less(sim.AttackMultiplier(A), 1f);

            Run(sim, rapid.Duration[0] - 0.5f);
            Assert.Less(sim.AttackMultiplier(A), 1f, "still active just before the end");

            Run(sim, 1f);
            Assert.AreEqual(1f, sim.AttackMultiplier(A));
            Assert.AreEqual(0f, sim.BuffLeft(A));
        }

        [Test]
        public void RapidFire_EndsWhenTheSurvivorDies()
        {
            SkillSim sim = New(A);
            sim.TryActivate(A, Marisa, E, true, Ranks(rapid: 1, haste: 1), out _);
            Assert.Less(sim.AttackMultiplier(A), 1f);

            sim.EndBuff(A);

            Assert.AreEqual(1f, sim.AttackMultiplier(A));
        }

        [Test]
        public void ARefusedRapidFire_StartsNoEffect()
        {
            SkillSim sim = New(A);
            sim.SetStats(A, 10, 0f);

            sim.TryActivate(A, Marisa, E, true, Ranks(rapid: 1, haste: 1), out _);

            Assert.AreEqual(1f, sim.AttackMultiplier(A));
        }

        [Test]
        public void ApplyingRapidFireTwice_RestartsTheTimer()
        {
            SkillSim sim = New(A);
            sim.SetStats(A, 1000, 100f);
            int[] ranks = Ranks(rapid: 1, haste: 1);
            sim.TryActivate(A, Marisa, E, true, ranks, out _);
            Run(sim, SkillCatalog.Get(SkillIds.RapidFire).Cooldown);

            sim.TryActivate(A, Marisa, E, true, ranks, out _);

            Assert.AreEqual(SkillCatalog.Get(SkillIds.RapidFire).Duration[0], sim.BuffLeft(A), 1e-3f);
        }

        // ---- 4.3 executing effects, and experience from the kills ----

        [Test]
        public void Burst_HitsMobsAroundTheSurvivorAndSparesTheRest()
        {
            SkillSim sim = New(A);
            sim.TryActivate(A, Reimu, Q, true, Ranks(burst: 1), out SkillActivation act);
            var mobs = new MobSim(targetCount: 0) { MaxHealth = 100 };
            Vector2 origin = new Vector2(2, 1);
            mobs.TrySpawn(origin + new Vector2(1, 0), 0, out ushort a);
            mobs.TrySpawn(origin + new Vector2(0, -2), 0, out ushort b);
            mobs.TrySpawn(origin + new Vector2(-2, 0), 0, out ushort c);
            mobs.TrySpawn(origin + new Vector2(6, 0), 0, out ushort outside);

            int hit = SkillExecutor.Apply(act, origin, mobs, new List<Vector2>());

            Assert.AreEqual(3, hit);
            foreach (ushort id in new[] { a, b, c })
            {
                mobs.TryGetHealth(id, out int health);
                Assert.AreEqual(100 - act.Damage, health);
            }
            mobs.TryGetHealth(outside, out int far);
            Assert.AreEqual(100, far);
        }

        [Test]
        public void Burst_KillsGiveExperienceToNearbyLivingSurvivors()
        {
            var skills = New(A);
            // Four per kill keeps three kills (12) below the first level threshold, so the amount can be read directly.
            var leveling = new LevelingSim(new System.Random(1)) { XpPerKill = 4 };
            leveling.Register(A, Reimu);
            leveling.Register(B, Reimu);
            skills.TryActivate(A, Reimu, Q, true, Ranks(burst: 3), out SkillActivation act);

            var mobs = new MobSim(targetCount: 0) { MaxHealth = 30 };
            Vector2 origin = Vector2.zero;
            mobs.TrySpawn(new Vector2(1, 0), 0, out _);
            mobs.TrySpawn(new Vector2(-1, 1), 0, out _);
            mobs.TrySpawn(new Vector2(0, 2), 0, out _);
            var deaths = new List<Vector2>();

            SkillExecutor.Apply(act, origin, mobs, deaths);

            Assert.AreEqual(3, deaths.Count);
            Assert.AreEqual(0, mobs.Count);

            var bodies = new List<GrowthBody>
            {
                new GrowthBody { ClientId = A, Position = origin, Alive = true },
                new GrowthBody { ClientId = B, Position = new Vector2(20, 0), Alive = true }, // far away
            };
            foreach (Vector2 death in deaths) leveling.AwardKill(death, bodies);

            Assert.AreEqual(3 * 4, leveling.Xp(A));
            Assert.AreEqual(1, leveling.Level(A));
            Assert.AreEqual(0, leveling.Xp(B));
        }

        [Test]
        public void RapidFire_HasNoAreaEffect()
        {
            SkillSim sim = New(A);
            sim.TryActivate(A, Marisa, E, true, Ranks(rapid: 1, haste: 1), out SkillActivation act);
            var mobs = new MobSim(targetCount: 0) { MaxHealth = 30 };
            mobs.TrySpawn(new Vector2(1, 0), 0, out _);

            Assert.AreEqual(0, SkillExecutor.Apply(act, Vector2.zero, mobs, new List<Vector2>()));
            Assert.AreEqual(1, mobs.Count);
            Assert.AreEqual(0, SkillExecutor.Apply(act, Vector2.zero, null, null));
        }

        [Test]
        public void UnregisterAndClear_DropTheState()
        {
            SkillSim sim = New(A, B);

            sim.Unregister(A);
            Assert.IsFalse(sim.Contains(A));
            Assert.AreEqual(0f, sim.Mana(A));
            Assert.IsTrue(sim.Contains(B));

            sim.Clear();
            Assert.AreEqual(0, sim.Count);
        }
    }
}
