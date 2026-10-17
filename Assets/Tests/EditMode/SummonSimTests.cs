using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    static class SummonTestKit
    {
        public static SurvivorInfo Survivor(ulong id, float x, float y, bool alive = true) =>
            new SurvivorInfo { ClientId = id, Position = new Vector2(x, y), Alive = alive };

        public static SummonContext Context(MobSim mobs, Vector2 boss, List<SurvivorInfo> survivors,
                                            bool running = true, bool standing = true) =>
            new SummonContext { Mobs = mobs, BossPosition = boss, BossStanding = standing, MatchRunning = running, Survivors = survivors };

        public static SummonConfig Config(float max = 100f, float start = 100f, float regen = 5f, float cost = 5f, int group = 4, float cooldown = 0.6f) =>
            new SummonConfig { MaxEnergy = max, StartEnergy = start, RegenPerSecond = regen, CostPerMob = cost, GroupSize = group, CooldownSeconds = cooldown };
    }

    // ---- 2.1 energy, cooldown and the results of a summon ----

    public class SummonEnergyTests
    {
        static MobSim NewMobs(int cap = 100) => new MobSim(hardCap: cap, targetCount: 0);

        [Test]
        public void AFullGroupIsCreatedAndPaidFor()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = NewMobs();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, new List<SurvivorInfo> { SummonTestKit.Survivor(1, 10, 6) });

            SummonResult result = sim.Summon(ctx, true, out int created);

            Assert.AreEqual(SummonResult.Created, result);
            Assert.AreEqual(4, created);
            Assert.AreEqual(4, mobs.SummonedCount);
            Assert.AreEqual(80f, sim.Energy, 0.001f);
        }

        [Test]
        public void EnergyForPartOfAGroupCreatesOnlyThatMany()
        {
            var sim = new SummonSim(SummonTestKit.Config(start: 12f));
            var mobs = NewMobs();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, new List<SurvivorInfo>());

            SummonResult result = sim.Summon(ctx, true, out int created);

            Assert.AreEqual(SummonResult.Partial, result);
            Assert.AreEqual(2, created);
            Assert.AreEqual(2f, sim.Energy, 0.001f);
        }

        [Test]
        public void WithoutEnergyForOneMobNothingHappensAndNothingIsTaken()
        {
            var sim = new SummonSim(SummonTestKit.Config(start: 4f));
            var mobs = NewMobs();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, new List<SurvivorInfo>());

            SummonResult result = sim.Summon(ctx, true, out int created);

            Assert.AreEqual(SummonResult.NoEnergy, result);
            Assert.AreEqual(0, created);
            Assert.AreEqual(0, mobs.Count);
            Assert.AreEqual(4f, sim.Energy, 0.001f);
        }

        [Test]
        public void EnergyRefillsAtTheRateAndStopsAtTheMaximum()
        {
            var sim = new SummonSim(SummonTestKit.Config(max: 100f, start: 0f, regen: 5f));

            sim.Step(2f, true);
            Assert.AreEqual(10f, sim.Energy, 0.001f);

            sim.Step(100f, true);
            Assert.AreEqual(100f, sim.Energy, 0.001f);
        }

        [Test]
        public void EnergyDoesNotRefillWhileTheMatchIsNotRunning()
        {
            var sim = new SummonSim(SummonTestKit.Config(start: 10f));

            sim.Step(5f, false);

            Assert.AreEqual(10f, sim.Energy, 0.001f);
        }

        [Test]
        public void TheStartingEnergyNeverExceedsTheMaximum()
        {
            var sim = new SummonSim(SummonTestKit.Config(max: 30f, start: 80f));

            Assert.AreEqual(30f, sim.Energy, 0.001f);
        }

        [Test]
        public void ANewMatchStartsFromTheStartingEnergyAndNoCooldown()
        {
            var sim = new SummonSim(SummonTestKit.Config(start: 50f));
            var mobs = NewMobs();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, new List<SurvivorInfo>());
            sim.Summon(ctx, true, out _);
            sim.Step(3f, true);

            sim.Reset();

            Assert.AreEqual(50f, sim.Energy, 0.001f);
            Assert.AreEqual(0f, sim.CooldownLeft, 0.001f);
        }

        [Test]
        public void ASecondSummonBeforeTheCooldownIsRefusedWithoutCost()
        {
            var sim = new SummonSim(SummonTestKit.Config(cooldown: 0.6f));
            var mobs = NewMobs();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, new List<SurvivorInfo>());
            sim.Summon(ctx, true, out _);
            float energy = sim.Energy;

            sim.Step(0.3f, true);
            SummonResult result = sim.Summon(ctx, true, out int created);

            Assert.AreEqual(SummonResult.Cooldown, result);
            Assert.AreEqual(0, created);
            Assert.AreEqual(energy + 5f * 0.3f, sim.Energy, 0.001f, "only the refill changed it");
        }

        [Test]
        public void AfterTheCooldownASummonWorksAgain()
        {
            var sim = new SummonSim(SummonTestKit.Config(cooldown: 0.6f));
            var mobs = NewMobs();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, new List<SurvivorInfo>());
            sim.Summon(ctx, true, out _);

            sim.Step(0.7f, true);

            Assert.AreEqual(SummonResult.Created, sim.Summon(ctx, true, out _));
        }

        [Test]
        public void AFullFieldRefusesAndTakesNothing()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = NewMobs(cap: 3);
            for (int i = 0; i < 3; i++) mobs.TrySpawn(new Vector2(i, 8), MobSim.TypeAuto, out _);
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, new List<SurvivorInfo>());

            SummonResult result = sim.Summon(ctx, true, out int created);

            Assert.AreEqual(SummonResult.FieldFull, result);
            Assert.AreEqual(0, created);
            Assert.AreEqual(100f, sim.Energy, 0.001f);
        }

        [Test]
        public void ARoomForFewerThanAGroupCreatesWhatFitsAndChargesOnlyThose()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = NewMobs(cap: 6);
            for (int i = 0; i < 4; i++) mobs.TrySpawn(new Vector2(i, 8), MobSim.TypeAuto, out _);
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, new List<SurvivorInfo>());

            SummonResult result = sim.Summon(ctx, true, out int created);

            Assert.AreEqual(SummonResult.Partial, result);
            Assert.AreEqual(2, created);
            Assert.AreEqual(90f, sim.Energy, 0.001f);
            Assert.AreEqual(6, mobs.Count);
        }

        [Test]
        public void AFallenBossCannotSummon()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = NewMobs();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, new List<SurvivorInfo>(), standing: false);

            Assert.AreEqual(SummonResult.Unavailable, sim.Summon(ctx, true, out int created));
            Assert.AreEqual(0, created);
            Assert.AreEqual(100f, sim.Energy, 0.001f);
        }

        [Test]
        public void NothingIsSummonedOutsideARunningMatch()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = NewMobs();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, new List<SurvivorInfo>(), running: false);

            Assert.AreEqual(SummonResult.Unavailable, sim.Summon(ctx, true, out _));
            Assert.AreEqual(0, mobs.Count);
        }

        [Test]
        public void ARequestFromSomeoneElseIsIgnored()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = NewMobs();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, new List<SurvivorInfo>());

            Assert.AreEqual(SummonResult.Ignored, sim.Summon(ctx, false, out int created));
            Assert.AreEqual(0, created);
            Assert.AreEqual(0, mobs.Count);
            Assert.AreEqual(100f, sim.Energy, 0.001f);
        }

        [Test]
        public void SummonedMobsAreOfTheSummonedType()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = NewMobs();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, new List<SurvivorInfo>());

            sim.Summon(ctx, true, out _);

            for (int i = 0; i < mobs.Count; i++)
            {
                Assert.AreEqual(MobSim.TypeSummoned, mobs.TypeAt(i));
                Assert.IsTrue(mobs.IsSummoned(i));
            }
        }

        [Test]
        public void ChangingTheValuesNeedsNoRuleChange()
        {
            var sim = new SummonSim(SummonTestKit.Config(group: 2, cost: 10f));
            var mobs = NewMobs();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, new List<SurvivorInfo>());

            sim.Summon(ctx, true, out int created);

            Assert.AreEqual(2, created);
            Assert.AreEqual(80f, sim.Energy, 0.001f);
        }

        [Test]
        public void TheDefaultValuesArePositiveAndConsistent()
        {
            SummonConfig c = SummonConfig.Default;

            Assert.Greater(c.MaxEnergy, 0f);
            Assert.LessOrEqual(c.StartEnergy, c.MaxEnergy);
            Assert.Greater(c.RegenPerSecond, 0f);
            Assert.Greater(c.CostPerMob, 0f);
            Assert.GreaterOrEqual(c.MaxEnergy, c.CostPerMob * c.GroupSize, "a full bar buys at least a group");
            Assert.Greater(c.GroupSize, 0);
            Assert.Less(c.RingMin, c.RingMax);
            Assert.Greater(c.SafeDistance, 0f);
        }
    }

    // ---- 2.2 where summoned mobs appear ----

    public class SummonPlacementTests
    {
        [Test]
        public void MobsAppearOnTheRingAroundTheBoss()
        {
            var cfg = SummonTestKit.Config();
            var sim = new SummonSim(cfg);
            var mobs = new MobSim(hardCap: 100, targetCount: 0);
            var boss = new Vector2(3, -2);
            var ctx = SummonTestKit.Context(mobs, boss, new List<SurvivorInfo>());

            for (int round = 0; round < 5; round++)
            {
                sim.Step(1f, true);
                sim.Summon(ctx, true, out _);
            }

            Assert.Greater(mobs.Count, 0);
            for (int i = 0; i < mobs.Count; i++)
            {
                float d = Vector2.Distance(mobs.PositionAt(i), boss);
                Assert.GreaterOrEqual(d, cfg.RingMin - 0.01f);
                Assert.LessOrEqual(d, cfg.RingMax + 0.01f);
            }
        }

        [Test]
        public void MobsNeverAppearWithinTheSafeDistanceOfALivingSurvivor()
        {
            var cfg = SummonConfig.Default;
            var sim = new SummonSim(SummonTestKit.Config(max: 1000f, start: 1000f, cooldown: 0f));
            var mobs = new MobSim(hardCap: 100, targetCount: 0);
            var survivors = new List<SurvivorInfo> { SummonTestKit.Survivor(1, 2, 0), SummonTestKit.Survivor(2, -2, 0) };
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, survivors);

            for (int i = 0; i < 20; i++) sim.Summon(ctx, true, out _);

            Assert.Greater(mobs.Count, 0, "some positions were allowed");
            for (int i = 0; i < mobs.Count; i++)
                foreach (SurvivorInfo s in survivors)
                    Assert.GreaterOrEqual(Vector2.Distance(mobs.PositionAt(i), s.Position), cfg.SafeDistance - 0.01f);
        }

        [Test]
        public void AFallenSurvivorDoesNotBlockPositions()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = new MobSim(hardCap: 100, targetCount: 0);
            var survivors = new List<SurvivorInfo> { SummonTestKit.Survivor(1, 0, 0, alive: false) };
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, survivors);

            sim.Summon(ctx, true, out int created);

            Assert.AreEqual(4, created);
        }

        [Test]
        public void WhenEveryPositionIsBlockedNothingIsCreatedAndNothingIsCharged()
        {
            var sim = new SummonSim(new SummonConfig { SafeDistance = 50f, GroupSize = 4, StartEnergy = 100f, MaxEnergy = 100f, CostPerMob = 5f });
            var mobs = new MobSim(hardCap: 100, targetCount: 0);
            var survivors = new List<SurvivorInfo> { SummonTestKit.Survivor(1, 0, 0) };
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, survivors);

            SummonResult result = sim.Summon(ctx, true, out int created);

            Assert.AreEqual(SummonResult.NoRoom, result);
            Assert.AreEqual(0, created);
            Assert.AreEqual(100f, sim.Energy, 0.001f);
            Assert.AreEqual(0f, sim.CooldownLeft, 0.001f, "a failed summon does not start the cooldown");
        }

        [Test]
        public void ABossAtTheArenaEdgeStillGetsMobsInsideTheArena()
        {
            var sim = new SummonSim(SummonTestKit.Config(max: 1000f, start: 1000f, cooldown: 0f));
            var mobs = new MobSim(hardCap: 100, targetCount: 0);
            var corner = new Vector2(ArenaConfig.Width * 0.5f, ArenaConfig.Height * 0.5f);
            var ctx = SummonTestKit.Context(mobs, corner, new List<SurvivorInfo>());

            for (int i = 0; i < 10; i++) sim.Summon(ctx, true, out _);

            Assert.Greater(mobs.Count, 0);
            for (int i = 0; i < mobs.Count; i++)
            {
                Assert.LessOrEqual(Mathf.Abs(mobs.PositionAt(i).x), ArenaConfig.Width * 0.5f + 0.001f);
                Assert.LessOrEqual(Mathf.Abs(mobs.PositionAt(i).y), ArenaConfig.Height * 0.5f + 0.001f);
            }
        }

        [Test]
        public void SummonedMobsProduceOrdinarySpawnEvents()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = new MobSim(hardCap: 100, targetCount: 0);
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, new List<SurvivorInfo>());

            sim.Summon(ctx, true, out int created);
            var events = new List<MobEvent>();
            mobs.DrainEvents(events);

            Assert.AreEqual(created, events.Count);
            foreach (MobEvent e in events)
            {
                Assert.IsTrue(e.IsSpawn);
                Assert.AreEqual(MobSim.TypeSummoned, e.Type, "clients tell the origin by the type they already receive");
            }
        }
    }

    // ---- 2.3 orders ----

    public class SummonOrderTests
    {
        static (SummonSim sim, MobSim mobs, List<SurvivorInfo> survivors) Setup()
        {
            var sim = new SummonSim(SummonTestKit.Config(max: 1000f, start: 1000f, cooldown: 0f));
            var mobs = new MobSim(hardCap: 100, targetCount: 0);
            var survivors = new List<SurvivorInfo>
            {
                SummonTestKit.Survivor(1, 10, 6),
                SummonTestKit.Survivor(2, -10, 6),
            };
            return (sim, mobs, survivors);
        }

        static List<ushort> SummonedIds(MobSim mobs)
        {
            var ids = new List<ushort>();
            for (int i = 0; i < mobs.Count; i++) if (mobs.IsSummoned(i)) ids.Add(mobs.IdAt(i));
            return ids;
        }

        [Test]
        public void PointingAtASurvivorOrdersThatSurvivor()
        {
            var (sim, mobs, survivors) = Setup();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, survivors);
            sim.Summon(ctx, true, out _);

            bool ok = sim.GiveOrder(ctx, true, new Vector2(10.5f, 6.5f));

            Assert.IsTrue(ok);
            Assert.AreEqual(OrderKind.Player, sim.Order);
            Assert.AreEqual(1UL, sim.OrderClient);
            foreach (ushort id in SummonedIds(mobs))
            {
                Assert.IsTrue(mobs.TryGetOverride(id, out bool isPlayer, out ulong client, out _));
                Assert.IsTrue(isPlayer);
                Assert.AreEqual(1UL, client);
            }
        }

        [Test]
        public void PointingAtNobodyOrdersThePoint()
        {
            var (sim, mobs, survivors) = Setup();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, survivors);
            sim.Summon(ctx, true, out _);

            sim.GiveOrder(ctx, true, new Vector2(0, -7));

            Assert.AreEqual(OrderKind.Point, sim.Order);
            Assert.AreEqual(-7f, sim.OrderPoint.y, 0.01f);
            foreach (ushort id in SummonedIds(mobs))
            {
                Assert.IsTrue(mobs.TryGetOverride(id, out bool isPlayer, out _, out Vector2 point));
                Assert.IsFalse(isPlayer);
                Assert.AreEqual(-7f, point.y, 0.01f);
            }
        }

        [Test]
        public void ASurvivorBeyondThePickDistanceIsNotChosen()
        {
            var (sim, mobs, survivors) = Setup();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, survivors);

            sim.GiveOrder(ctx, true, new Vector2(10f + sim.Config.PickDistance + 0.5f, 6f));

            Assert.AreEqual(OrderKind.Point, sim.Order);
        }

        [Test]
        public void TheNearestOfTwoSurvivorsInRangeIsChosen()
        {
            var (sim, mobs, _) = Setup();
            var close = new List<SurvivorInfo> { SummonTestKit.Survivor(1, 0, 0), SummonTestKit.Survivor(2, 1.5f, 0) };
            var ctx = SummonTestKit.Context(mobs, new Vector2(8, 8), close);

            sim.GiveOrder(ctx, true, new Vector2(1.2f, 0));

            Assert.AreEqual(2UL, sim.OrderClient);
        }

        [Test]
        public void AFallenSurvivorIsNotChosenAndThePointIsOrderedInstead()
        {
            var (sim, mobs, _) = Setup();
            var down = new List<SurvivorInfo> { SummonTestKit.Survivor(1, 3, 3, alive: false) };
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, down);

            sim.GiveOrder(ctx, true, new Vector2(3, 3));

            Assert.AreEqual(OrderKind.Point, sim.Order);
        }

        [Test]
        public void APointOutsideTheArenaIsPulledInside()
        {
            var (sim, mobs, survivors) = Setup();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, survivors);

            sim.GiveOrder(ctx, true, new Vector2(900, -900));

            Assert.LessOrEqual(Mathf.Abs(sim.OrderPoint.x), ArenaConfig.Width * 0.5f);
            Assert.LessOrEqual(Mathf.Abs(sim.OrderPoint.y), ArenaConfig.Height * 0.5f);
        }

        [Test]
        public void ANewOrderReplacesTheOldOneForAllSummonedMobs()
        {
            var (sim, mobs, survivors) = Setup();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, survivors);
            sim.Summon(ctx, true, out _);
            sim.GiveOrder(ctx, true, new Vector2(10, 6));

            sim.GiveOrder(ctx, true, new Vector2(0, -7));

            Assert.AreEqual(OrderKind.Point, sim.Order);
            foreach (ushort id in SummonedIds(mobs))
            {
                mobs.TryGetOverride(id, out bool isPlayer, out _, out _);
                Assert.IsFalse(isPlayer);
            }
        }

        [Test]
        public void ClearingTheOrderFreesTheMobsAndLaterSummons()
        {
            var (sim, mobs, survivors) = Setup();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, survivors);
            sim.Summon(ctx, true, out _);
            sim.GiveOrder(ctx, true, new Vector2(10, 6));

            Assert.IsTrue(sim.ClearOrder(ctx, true));
            sim.Summon(ctx, true, out _);

            Assert.AreEqual(OrderKind.None, sim.Order);
            foreach (ushort id in SummonedIds(mobs))
                Assert.IsFalse(mobs.TryGetOverride(id, out _, out _, out _));
        }

        [Test]
        public void MobsSummonedAfterAnOrderReceiveIt()
        {
            var (sim, mobs, survivors) = Setup();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, survivors);
            sim.GiveOrder(ctx, true, new Vector2(-10, 6));

            sim.Summon(ctx, true, out int created);

            Assert.AreEqual(4, created);
            foreach (ushort id in SummonedIds(mobs))
            {
                Assert.IsTrue(mobs.TryGetOverride(id, out bool isPlayer, out ulong client, out _));
                Assert.IsTrue(isPlayer);
                Assert.AreEqual(2UL, client);
            }
        }

        [Test]
        public void APointOrderStandsForNewMobsAfterEarlierMobsArrived()
        {
            var (sim, mobs, survivors) = Setup();
            var ctx = SummonTestKit.Context(mobs, new Vector2(0, -6), survivors);
            sim.Summon(ctx, true, out _);
            sim.GiveOrder(ctx, true, new Vector2(0, -7)); // close to the boss: the first group arrives soon
            for (int i = 0; i < 200; i++) mobs.Step(0.02f, survivors);

            sim.Summon(ctx, true, out _);

            Assert.AreEqual(OrderKind.Point, sim.Order, "the order stands until it is cleared or replaced");
            bool anyOrdered = false;
            foreach (ushort id in SummonedIds(mobs))
                if (mobs.TryGetOverride(id, out _, out _, out _)) anyOrdered = true;
            Assert.IsTrue(anyOrdered, "the new mobs got the point");
        }

        [Test]
        public void AutomaticMobsAreNotAffectedByOrders()
        {
            var (sim, mobs, survivors) = Setup();
            mobs.TrySpawn(new Vector2(0, 8), MobSim.TypeAuto, out ushort auto);
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, survivors);

            sim.GiveOrder(ctx, true, new Vector2(10, 6));
            sim.Summon(ctx, true, out _);

            Assert.IsFalse(mobs.TryGetOverride(auto, out _, out _, out _));
        }

        [Test]
        public void TheOrderedSurvivorLeavingEndsTheOrder()
        {
            var (sim, mobs, survivors) = Setup();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, survivors);
            sim.GiveOrder(ctx, true, new Vector2(10, 6));

            sim.OnPlayerLeft(1);

            Assert.AreEqual(OrderKind.None, sim.Order);
        }

        [Test]
        public void AnotherSurvivorLeavingLeavesTheOrderAlone()
        {
            var (sim, mobs, survivors) = Setup();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, survivors);
            sim.GiveOrder(ctx, true, new Vector2(10, 6));

            sim.OnPlayerLeft(2);

            Assert.AreEqual(OrderKind.Player, sim.Order);
        }

        [Test]
        public void OrdersFromSomeoneElseAreIgnored()
        {
            var (sim, mobs, survivors) = Setup();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, survivors);
            sim.Summon(ctx, true, out _);

            Assert.IsFalse(sim.GiveOrder(ctx, false, new Vector2(10, 6)));
            Assert.IsFalse(sim.ClearOrder(ctx, false));

            Assert.AreEqual(OrderKind.None, sim.Order);
            foreach (ushort id in SummonedIds(mobs))
                Assert.IsFalse(mobs.TryGetOverride(id, out _, out _, out _));
        }

        [Test]
        public void OrdersAreIgnoredOutsideARunningMatchAndWhileTheBossIsDown()
        {
            var (sim, mobs, survivors) = Setup();

            Assert.IsFalse(sim.GiveOrder(SummonTestKit.Context(mobs, Vector2.zero, survivors, running: false), true, new Vector2(10, 6)));
            Assert.IsFalse(sim.GiveOrder(SummonTestKit.Context(mobs, Vector2.zero, survivors, standing: false), true, new Vector2(10, 6)));

            Assert.AreEqual(OrderKind.None, sim.Order);
        }

        [Test]
        public void ANewMatchStartsWithoutAnOrder()
        {
            var (sim, mobs, survivors) = Setup();
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, survivors);
            sim.GiveOrder(ctx, true, new Vector2(10, 6));

            sim.Reset();

            Assert.AreEqual(OrderKind.None, sim.Order);
        }
    }
}
