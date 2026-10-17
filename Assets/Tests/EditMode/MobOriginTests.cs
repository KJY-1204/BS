using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class MobOriginTests
    {
        const float Dt = 0.02f;

        static SurvivorInfo Survivor(ulong id, float x, float y, bool alive = true) =>
            new SurvivorInfo { ClientId = id, Position = new Vector2(x, y), Alive = alive };

        static void Run(MobSim sim, List<SurvivorInfo> survivors, int ticks)
        {
            for (int i = 0; i < ticks; i++) sim.Step(Dt, survivors);
        }

        static ushort Spawn(MobSim sim, float x, float y, byte type)
        {
            Assert.IsTrue(sim.TrySpawn(new Vector2(x, y), type, out ushort id));
            return id;
        }

        static Vector2 PositionOf(MobSim sim, ushort id)
        {
            for (int i = 0; i < sim.Count; i++)
                if (sim.IdAt(i) == id) return sim.PositionAt(i);
            Assert.Fail($"mob {id} not found");
            return default;
        }

        // ---- origin counts ----

        [Test]
        public void TheCountsFollowTheOriginOfEachMob()
        {
            var sim = new MobSim(hardCap: 20, targetCount: 0);
            for (int i = 0; i < 3; i++) Spawn(sim, i, 0, MobSim.TypeAuto);
            for (int i = 0; i < 5; i++) Spawn(sim, i, 3, MobSim.TypeSummoned);

            Assert.AreEqual(3, sim.AutoCount);
            Assert.AreEqual(5, sim.SummonedCount);
            Assert.AreEqual(8, sim.Count);
        }

        [Test]
        public void RemovingAMobLowersTheCountOfItsOriginOnly_EvenWhenAnotherMobTakesItsSlot()
        {
            var sim = new MobSim(hardCap: 20, targetCount: 0);
            ushort a = Spawn(sim, 0, 0, MobSim.TypeAuto);
            ushort s1 = Spawn(sim, 1, 0, MobSim.TypeSummoned);
            ushort s2 = Spawn(sim, 2, 0, MobSim.TypeSummoned);

            sim.Remove(a);
            Assert.AreEqual(0, sim.AutoCount);
            Assert.AreEqual(2, sim.SummonedCount);

            sim.Remove(s1);
            Assert.AreEqual(1, sim.SummonedCount);
            Assert.IsTrue(sim.Contains(s2));
        }

        [Test]
        public void AKilledSummonedMobIsCountedOut()
        {
            var sim = new MobSim(hardCap: 20, targetCount: 0);
            ushort s = Spawn(sim, 0, 0, MobSim.TypeSummoned);

            sim.ApplyDamage(s, 1000, out _, out bool killed);

            Assert.IsTrue(killed);
            Assert.AreEqual(0, sim.SummonedCount);
        }

        [Test]
        public void ClearResetsTheCounts()
        {
            var sim = new MobSim(hardCap: 20, targetCount: 0);
            Spawn(sim, 0, 0, MobSim.TypeSummoned);
            Spawn(sim, 1, 0, MobSim.TypeAuto);

            sim.Clear();

            Assert.AreEqual(0, sim.AutoCount);
            Assert.AreEqual(0, sim.SummonedCount);
        }

        [Test]
        public void IsSummonedReportsTheOriginByIndex()
        {
            var sim = new MobSim(hardCap: 20, targetCount: 0);
            Spawn(sim, 0, 0, MobSim.TypeAuto);
            Spawn(sim, 1, 0, MobSim.TypeSummoned);

            Assert.IsFalse(sim.IsSummoned(0));
            Assert.IsTrue(sim.IsSummoned(1));
        }

        // ---- automatic target counts automatic mobs only; the cap is shared ----

        [Test]
        public void AutomaticSpawningIgnoresSummonedMobsWhenCheckingTheTarget()
        {
            var sim = new MobSim(hardCap: 100, targetCount: 3) { SpawnPerSecond = 50f };
            for (int i = 0; i < 10; i++) Spawn(sim, i - 5, 0, MobSim.TypeSummoned);
            var survivors = new List<SurvivorInfo> { Survivor(1, 0, 0) };

            Run(sim, survivors, 200);

            Assert.AreEqual(3, sim.AutoCount, "automatic mobs fill their own target");
            Assert.AreEqual(10, sim.SummonedCount);
        }

        [Test]
        public void AutomaticSpawningPausesAtTheTargetOfAutomaticMobs()
        {
            var sim = new MobSim(hardCap: 100, targetCount: 3) { SpawnPerSecond = 50f };
            var survivors = new List<SurvivorInfo> { Survivor(1, 0, 0) };

            Run(sim, survivors, 200);
            Assert.AreEqual(3, sim.AutoCount);

            Spawn(sim, 0, 5, MobSim.TypeSummoned);
            Run(sim, survivors, 100);
            Assert.AreEqual(3, sim.AutoCount);
        }

        [Test]
        public void SummonedAndAutomaticMobsShareTheHardCap()
        {
            var sim = new MobSim(hardCap: 6, targetCount: 6);
            for (int i = 0; i < 4; i++) Spawn(sim, i, 0, MobSim.TypeSummoned);
            for (int i = 0; i < 2; i++) Spawn(sim, i, 2, MobSim.TypeAuto);

            Assert.IsFalse(sim.TrySpawn(Vector2.zero, MobSim.TypeSummoned, out _));
            Assert.IsFalse(sim.TrySpawn(Vector2.zero, MobSim.TypeAuto, out _));
            Assert.AreEqual(6, sim.Count);
        }

        [Test]
        public void TheBossMatchValuesAreWeakerThanThePracticeOnesButNotZero()
        {
            Assert.Less(MobConfig.BossMatchSpawnPerSecond, MobConfig.SpawnPerSecond);
            Assert.Less(MobConfig.BossMatchAutoTarget, MobConfig.DefaultTarget);
            Assert.Greater(MobConfig.BossMatchSpawnPerSecond, 0f);
            Assert.Greater(MobConfig.BossMatchAutoTarget, 0);
        }

        [Test]
        public void TheBossMatchSpawnsMoreSlowlyThanPractice()
        {
            var practice = new MobSim(hardCap: 100, targetCount: MobConfig.DefaultTarget) { SpawnPerSecond = MobConfig.SpawnPerSecond };
            var boss = new MobSim(hardCap: 100, targetCount: MobConfig.BossMatchAutoTarget) { SpawnPerSecond = MobConfig.BossMatchSpawnPerSecond };
            var survivors = new List<SurvivorInfo> { Survivor(1, 0, 0) };

            Run(practice, survivors, 500); // 10 s
            Run(boss, survivors, 500);

            Assert.Greater(boss.Count, 0, "not silent");
            Assert.Less(boss.Count, practice.Count);
        }

        // ---- position order: a destination ----

        [Test]
        public void APositionOrderEndsOnArrivalAndTheMobChasesTheNearestSurvivor()
        {
            var sim = new MobSim(hardCap: 20, targetCount: 0);
            ushort id = Spawn(sim, 0, 0, MobSim.TypeSummoned);
            var survivors = new List<SurvivorInfo> { Survivor(1, -8, 0) };

            sim.SetTargetPosition(id, new Vector2(2, 0));
            Run(sim, survivors, 100); // 2.5 u/s: the point is two units away, so it arrives and then turns around

            Assert.IsFalse(sim.TryGetOverride(id, out _, out _, out _), "the order ended");
            Assert.Less(PositionOf(sim, id).x, 1.5f, "the mob is on its way to the survivor");
        }

        [Test]
        public void APositionOrderHoldsUntilTheMobArrives()
        {
            var sim = new MobSim(hardCap: 20, targetCount: 0);
            ushort id = Spawn(sim, 0, 0, MobSim.TypeSummoned);
            var survivors = new List<SurvivorInfo> { Survivor(1, -8, 0) };

            sim.SetTargetPosition(id, new Vector2(0, 8));
            Run(sim, survivors, 40); // 2 units of 8

            Assert.IsTrue(sim.TryGetOverride(id, out bool isPlayer, out _, out Vector2 point));
            Assert.IsFalse(isPlayer);
            Assert.AreEqual(8f, point.y, 0.01f);
            Assert.Greater(PositionOf(sim, id).y, 1.5f, "heading for the point, not for the survivor");
        }

        [Test]
        public void APositionOutsideTheArenaIsPulledInside()
        {
            var sim = new MobSim(hardCap: 20, targetCount: 0);
            ushort id = Spawn(sim, 0, 0, MobSim.TypeSummoned);

            sim.SetTargetPosition(id, new Vector2(500, 500));

            sim.TryGetOverride(id, out _, out _, out Vector2 point);
            Assert.LessOrEqual(point.x, ArenaConfig.Width * 0.5f);
            Assert.LessOrEqual(point.y, ArenaConfig.Height * 0.5f);
        }

        [Test]
        public void APlayerOrderDoesNotEndByArrival()
        {
            var sim = new MobSim(hardCap: 20, targetCount: 0);
            ushort id = Spawn(sim, 0, 0, MobSim.TypeSummoned);
            var survivors = new List<SurvivorInfo> { Survivor(1, 1, 0), Survivor(2, -9, 0) };

            sim.SetTarget(id, 1);
            Run(sim, survivors, 200);

            Assert.IsTrue(sim.TryGetOverride(id, out bool isPlayer, out ulong client, out _));
            Assert.IsTrue(isPlayer);
            Assert.AreEqual(1UL, client);
        }

        // ---- the order of the boss reaches summoned mobs only ----

        [Test]
        public void ApplyOrderToSummonedTouchesOnlySummonedMobs()
        {
            var sim = new MobSim(hardCap: 20, targetCount: 0);
            ushort auto = Spawn(sim, 0, 0, MobSim.TypeAuto);
            ushort s1 = Spawn(sim, 1, 0, MobSim.TypeSummoned);
            ushort s2 = Spawn(sim, 2, 0, MobSim.TypeSummoned);

            int touched = sim.ApplyOrderToSummoned(true, 7, false, default);

            Assert.AreEqual(2, touched);
            Assert.IsFalse(sim.TryGetOverride(auto, out _, out _, out _));
            Assert.IsTrue(sim.TryGetOverride(s1, out bool p1, out ulong c1, out _) && p1 && c1 == 7);
            Assert.IsTrue(sim.TryGetOverride(s2, out bool p2, out ulong c2, out _) && p2 && c2 == 7);
        }

        [Test]
        public void ApplyOrderToSummonedCanSetAPointAndClear()
        {
            var sim = new MobSim(hardCap: 20, targetCount: 0);
            ushort s = Spawn(sim, 0, 0, MobSim.TypeSummoned);

            sim.ApplyOrderToSummoned(false, 0, true, new Vector2(5, 5));
            Assert.IsTrue(sim.TryGetOverride(s, out bool isPlayer, out _, out Vector2 point) && !isPlayer);
            Assert.AreEqual(5f, point.x, 0.01f);

            sim.ApplyOrderToSummoned(false, 0, false, default);
            Assert.IsFalse(sim.TryGetOverride(s, out _, out _, out _));
        }

        [Test]
        public void AnOrderedSurvivorWhoFallsIsReplacedByTheNearestLivingOneAndComesBackOnRevival()
        {
            var sim = new MobSim(hardCap: 20, targetCount: 0);
            ushort s = Spawn(sim, 0, 0, MobSim.TypeSummoned);
            sim.SetTarget(s, 2);

            var down = new List<SurvivorInfo> { Survivor(1, 6, 0), Survivor(2, -9, 0, alive: false) };
            Run(sim, down, 25);
            Assert.Greater(PositionOf(sim, s).x, 0f, "chases the living one meanwhile");
            float meanwhile = PositionOf(sim, s).x;

            var up = new List<SurvivorInfo> { Survivor(1, 6, 0), Survivor(2, -9, 0) };
            Run(sim, up, 100);
            Assert.Less(PositionOf(sim, s).x, meanwhile, "back toward the ordered survivor");
        }

        [Test]
        public void ThePlayerLeavingClearsTheOrderOfSummonedMobs()
        {
            var sim = new MobSim(hardCap: 20, targetCount: 0);
            ushort s = Spawn(sim, 0, 0, MobSim.TypeSummoned);
            sim.ApplyOrderToSummoned(true, 3, false, default);

            sim.OnPlayerLeft(3);

            Assert.IsFalse(sim.TryGetOverride(s, out _, out _, out _));
        }
    }
}
