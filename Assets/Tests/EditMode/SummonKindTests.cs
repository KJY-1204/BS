using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class SummonKindTests
    {
        static MobSim NewMobs(int cap = 100) => new MobSim(hardCap: cap, targetCount: 0);

        static SummonContext Ctx(MobSim mobs, bool running = true, bool standing = true, params SurvivorInfo[] survivors) =>
            SummonTestKit.Context(mobs, Vector2.zero, new List<SurvivorInfo>(survivors), running, standing);

        static int CountOfKind(MobSim mobs, byte kind)
        {
            int n = 0;
            for (int i = 0; i < mobs.Count; i++) if (mobs.KindAt(i) == kind && mobs.IsSummoned(i)) n++;
            return n;
        }

        // ---- the kind of the group ----

        [Test]
        public void ASummonOfAKindCreatesMobsOfThatKindWithTheSummonedOrigin()
        {
            foreach (byte kind in new byte[] { MobKinds.Basic, MobKinds.Fast, MobKinds.Tough })
            {
                var sim = new SummonSim(SummonTestKit.Config());
                var mobs = NewMobs();

                SummonResult result = sim.Summon(Ctx(mobs), true, kind, out int created);

                Assert.IsTrue(result == SummonResult.Created, MobKinds.Get(kind).Name);
                Assert.AreEqual(created, CountOfKind(mobs, kind), MobKinds.Get(kind).Name);
                Assert.AreEqual(created, mobs.SummonedCount);
                Assert.AreEqual(0, mobs.AutoCount);
            }
        }

        [Test]
        public void TheOldThreeArgumentSummonIsTheBasicKind()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = NewMobs();

            sim.Summon(Ctx(mobs), true, out int created);

            Assert.AreEqual(4, created);
            Assert.AreEqual(4, CountOfKind(mobs, MobKinds.Basic));
        }

        // ---- cost and group size follow the kind ----

        [Test]
        public void EachKindHasItsOwnCostAndGroupSize()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            Assert.AreEqual(5f, sim.CostOf(MobKinds.Basic), 1e-4f);
            Assert.AreEqual(4f, sim.CostOf(MobKinds.Fast), 1e-4f);
            Assert.AreEqual(15f, sim.CostOf(MobKinds.Tough), 1e-4f);
            Assert.AreEqual(4, sim.GroupSizeOf(MobKinds.Basic));
            Assert.AreEqual(5, sim.GroupSizeOf(MobKinds.Fast));
            Assert.AreEqual(2, sim.GroupSizeOf(MobKinds.Tough));
        }

        [Test]
        public void AToughGroupCostsMoreAndIsSmaller()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = NewMobs();

            SummonResult result = sim.Summon(Ctx(mobs), true, MobKinds.Tough, out int created);

            Assert.AreEqual(SummonResult.Created, result);
            Assert.AreEqual(2, created);
            Assert.AreEqual(100f - 2 * 15f, sim.Energy, 1e-3f);
        }

        [Test]
        public void AFastGroupIsBiggerAndEachMobCostsLess()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = NewMobs();

            SummonResult result = sim.Summon(Ctx(mobs), true, MobKinds.Fast, out int created);

            Assert.AreEqual(SummonResult.Created, result);
            Assert.AreEqual(5, created);
            Assert.AreEqual(100f - 5 * 4f, sim.Energy, 1e-3f);
        }

        [Test]
        public void EnergyForPartOfAToughGroupCreatesOnlyThatMany()
        {
            var sim = new SummonSim(SummonTestKit.Config(start: 20f));
            var mobs = NewMobs();

            SummonResult result = sim.Summon(Ctx(mobs), true, MobKinds.Tough, out int created);

            Assert.AreEqual(SummonResult.Partial, result);
            Assert.AreEqual(1, created);
            Assert.AreEqual(5f, sim.Energy, 1e-3f);
        }

        [Test]
        public void TooLittleEnergyForOneMobOfTheKindIsRefusedWithNothingTaken()
        {
            var sim = new SummonSim(SummonTestKit.Config(start: 14f));
            var mobs = NewMobs();

            // 14 energy pays for a basic mob (5) and a fast mob (4) but not a tough mob (15).
            SummonResult result = sim.Summon(Ctx(mobs), true, MobKinds.Tough, out int created);

            Assert.AreEqual(SummonResult.NoEnergy, result);
            Assert.AreEqual(0, created);
            Assert.AreEqual(0, mobs.Count);
            Assert.AreEqual(14f, sim.Energy, 1e-3f);
            Assert.AreEqual(0f, sim.CooldownLeft, "a refusal does not start the cooldown");
        }

        [Test]
        public void TheSameEnergyBuysDifferentCountsOfEachKind()
        {
            var counts = new List<int>();
            foreach (byte kind in new byte[] { MobKinds.Basic, MobKinds.Fast, MobKinds.Tough })
            {
                var config = SummonTestKit.Config(start: 100f, group: 100); // a huge group so only the energy limits it
                var sim = new SummonSim(config);
                var mobs = NewMobs(cap: 200);
                sim.Summon(Ctx(mobs), true, kind, out int created);
                counts.Add(created);
            }
            Assert.AreEqual(20, counts[0], "100 / 5");
            Assert.AreEqual(25, counts[1], "100 / 4");
            Assert.AreEqual(6, counts[2], "100 / 15 rounded down");
        }

        // ---- refusals are the same for every kind ----

        [Test]
        public void AnUnknownKindIsRefusedWithoutEnergyOrCooldown()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = NewMobs();

            SummonResult result = sim.Summon(Ctx(mobs), true, 9, out int created);

            Assert.AreEqual(SummonResult.UnknownKind, result);
            Assert.AreEqual(0, created);
            Assert.AreEqual(0, mobs.Count);
            Assert.AreEqual(100f, sim.Energy, 1e-3f);
            Assert.AreEqual(0f, sim.CooldownLeft);
        }

        [Test]
        public void AnUnknownKindDoesNotBlockTheNextRealSummon()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = NewMobs();

            sim.Summon(Ctx(mobs), true, 200, out _);
            SummonResult result = sim.Summon(Ctx(mobs), true, MobKinds.Fast, out int created);

            Assert.AreEqual(SummonResult.Created, result);
            Assert.AreEqual(5, created);
        }

        [Test]
        public void ARequestNotFromTheBossIsIgnoredWhateverTheKind()
        {
            foreach (byte kind in new byte[] { MobKinds.Basic, MobKinds.Fast, MobKinds.Tough, 77 })
            {
                var sim = new SummonSim(SummonTestKit.Config());
                var mobs = NewMobs();

                SummonResult result = sim.Summon(Ctx(mobs), false, kind, out int created);

                Assert.AreEqual(SummonResult.Ignored, result);
                Assert.AreEqual(0, created);
                Assert.AreEqual(0, mobs.Count);
                Assert.AreEqual(100f, sim.Energy, 1e-3f);
            }
        }

        [Test]
        public void EveryKindIsRefusedDuringTheCooldown()
        {
            foreach (byte kind in new byte[] { MobKinds.Basic, MobKinds.Fast, MobKinds.Tough })
            {
                var sim = new SummonSim(SummonTestKit.Config());
                var mobs = NewMobs();
                sim.Summon(Ctx(mobs), true, MobKinds.Basic, out _);
                float energy = sim.Energy;

                SummonResult result = sim.Summon(Ctx(mobs), true, kind, out int created);

                Assert.AreEqual(SummonResult.Cooldown, result);
                Assert.AreEqual(0, created);
                Assert.AreEqual(energy, sim.Energy, 1e-4f);
            }
        }

        [Test]
        public void EveryKindIsRefusedWhenTheFieldIsFull()
        {
            foreach (byte kind in new byte[] { MobKinds.Basic, MobKinds.Fast, MobKinds.Tough })
            {
                var sim = new SummonSim(SummonTestKit.Config());
                var mobs = NewMobs(cap: 3);
                for (int i = 0; i < 3; i++) mobs.TrySpawn(new Vector2(i, 8), MobSim.TypeAuto, out _);

                SummonResult result = sim.Summon(Ctx(mobs), true, kind, out int created);

                Assert.AreEqual(SummonResult.FieldFull, result);
                Assert.AreEqual(0, created);
                Assert.AreEqual(100f, sim.Energy, 1e-3f);
            }
        }

        [Test]
        public void EveryKindIsRefusedAfterTheMatchOrAfterTheBossFell()
        {
            foreach (byte kind in new byte[] { MobKinds.Basic, MobKinds.Fast, MobKinds.Tough })
            {
                var sim = new SummonSim(SummonTestKit.Config());
                var mobs = NewMobs();

                Assert.AreEqual(SummonResult.Unavailable, sim.Summon(Ctx(mobs, running: false), true, kind, out _));
                Assert.AreEqual(SummonResult.Unavailable, sim.Summon(Ctx(mobs, standing: false), true, kind, out _));
                Assert.AreEqual(0, mobs.Count);
                Assert.AreEqual(100f, sim.Energy, 1e-3f);
            }
        }

        [Test]
        public void ARoomForFewerMobsThanTheGroupCreatesOnlyThose()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = NewMobs(cap: 3);

            SummonResult result = sim.Summon(Ctx(mobs), true, MobKinds.Fast, out int created);

            Assert.AreEqual(SummonResult.Partial, result);
            Assert.AreEqual(3, created);
            Assert.AreEqual(100f - 3 * 4f, sim.Energy, 1e-3f);
        }

        // ---- placement and orders ----

        [Test]
        public void ABigMobIsPlacedFullyInsideTheArenaAtTheEdge()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = NewMobs();
            var ctx = SummonTestKit.Context(mobs, new Vector2(15.9f, 9.9f), new List<SurvivorInfo>());

            sim.Summon(ctx, true, MobKinds.Tough, out int created);

            Assert.AreEqual(2, created);
            float radius = mobs.RadiusOf(MobKinds.Tough);
            for (int i = 0; i < mobs.Count; i++)
            {
                Vector2 p = mobs.PositionAt(i);
                Assert.LessOrEqual(Mathf.Abs(p.x), ArenaConfig.Width * 0.5f - radius + 1e-4f);
                Assert.LessOrEqual(Mathf.Abs(p.y), ArenaConfig.Height * 0.5f - radius + 1e-4f);
            }
        }

        [Test]
        public void NoKindIsPlacedWithinTheSafeDistanceOfASurvivor()
        {
            foreach (byte kind in new byte[] { MobKinds.Basic, MobKinds.Fast, MobKinds.Tough })
            {
                var sim = new SummonSim(SummonTestKit.Config());
                var mobs = NewMobs();
                var survivor = SummonTestKit.Survivor(1, 2f, 0f);

                sim.Summon(Ctx(mobs, true, true, survivor), true, kind, out _);

                for (int i = 0; i < mobs.Count; i++)
                    Assert.GreaterOrEqual(Vector2.Distance(mobs.PositionAt(i), survivor.Position), sim.Config.SafeDistance - 1e-3f);
            }
        }

        [Test]
        public void AnOrderStandingNowReachesNewMobsOfAnyKind()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = NewMobs();
            var survivors = new List<SurvivorInfo> { SummonTestKit.Survivor(7, 10f, -6f) };
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, survivors);
            Assert.IsTrue(sim.GiveOrder(ctx, true, new Vector2(10f, -6f)));

            sim.Summon(ctx, true, MobKinds.Tough, out int created);

            Assert.AreEqual(2, created);
            for (int i = 0; i < mobs.Count; i++)
            {
                Assert.IsTrue(mobs.TryGetOverride(mobs.IdAt(i), out bool isPlayer, out ulong client, out _));
                Assert.IsTrue(isPlayer);
                Assert.AreEqual(7UL, client);
            }
        }

        [Test]
        public void AnOrderReachesTheMobsAlreadySummonedOfEveryKind()
        {
            var sim = new SummonSim(SummonTestKit.Config(regen: 0f, cooldown: 0f));
            var mobs = NewMobs();
            var survivors = new List<SurvivorInfo> { SummonTestKit.Survivor(7, 10f, -6f) };
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, survivors);
            sim.Summon(ctx, true, MobKinds.Fast, out _);
            sim.Summon(ctx, true, MobKinds.Tough, out _);
            mobs.TrySpawn(new Vector2(-8f, 8f), MobSim.TypeAuto, out ushort auto);

            sim.GiveOrder(ctx, true, new Vector2(10f, -6f));

            for (int i = 0; i < mobs.Count; i++)
            {
                bool ordered = mobs.TryGetOverride(mobs.IdAt(i), out _, out _, out _);
                Assert.AreEqual(mobs.IsSummoned(i), ordered, "summoned mobs of every kind obey and the automatic one does not");
            }
            Assert.IsFalse(mobs.TryGetOverride(auto, out _, out _, out _));
        }

        [Test]
        public void AResetStartsAgainWithoutAnyKindMemory()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = NewMobs();
            sim.Summon(Ctx(mobs), true, MobKinds.Tough, out _);

            sim.Reset();

            Assert.AreEqual(sim.Config.StartEnergy, sim.Energy, 1e-3f);
            Assert.AreEqual(0f, sim.CooldownLeft);
        }
    }
}
