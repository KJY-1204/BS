using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class SummonBehaviorKindTests
    {
        static MobSim NewMobs() => new MobSim(hardCap: 100, targetCount: 0);

        static SummonContext Ctx(MobSim mobs) => SummonTestKit.Context(mobs, Vector2.zero, new List<SurvivorInfo>());

        [Test]
        public void TheNewKindsHaveTheirOwnCostAndGroupSize()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            Assert.AreEqual(10f, sim.CostOf(MobKinds.Ranged), 1e-4f);
            Assert.AreEqual(8f, sim.CostOf(MobKinds.Exploder), 1e-4f);
            Assert.AreEqual(2, sim.GroupSizeOf(MobKinds.Ranged));
            Assert.AreEqual(3, sim.GroupSizeOf(MobKinds.Exploder));
        }

        [Test]
        public void ARangedGroupIsTwoMobsOfTheRangedKind()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = NewMobs();

            SummonResult result = sim.Summon(Ctx(mobs), true, MobKinds.Ranged, out int created);

            Assert.AreEqual(SummonResult.Created, result);
            Assert.AreEqual(2, created);
            Assert.AreEqual(100f - 2 * 10f, sim.Energy, 1e-3f);
            for (int i = 0; i < mobs.Count; i++)
            {
                Assert.AreEqual(MobKinds.Ranged, mobs.KindAt(i));
                Assert.IsTrue(mobs.IsSummoned(i));
            }
        }

        [Test]
        public void AnExploderGroupIsThreeMobsOfTheExplodingKind()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = NewMobs();

            SummonResult result = sim.Summon(Ctx(mobs), true, MobKinds.Exploder, out int created);

            Assert.AreEqual(SummonResult.Created, result);
            Assert.AreEqual(3, created);
            Assert.AreEqual(100f - 3 * 8f, sim.Energy, 1e-3f);
            for (int i = 0; i < mobs.Count; i++) Assert.AreEqual(MobKinds.Exploder, mobs.KindAt(i));
        }

        [Test]
        public void EnergyForPartOfAnExploderGroupCreatesOnlyThatMany()
        {
            var sim = new SummonSim(SummonTestKit.Config(start: 17f));
            var mobs = NewMobs();

            SummonResult result = sim.Summon(Ctx(mobs), true, MobKinds.Exploder, out int created);

            Assert.AreEqual(SummonResult.Partial, result);
            Assert.AreEqual(2, created);
            Assert.AreEqual(1f, sim.Energy, 1e-3f);
        }

        [Test]
        public void NotEnoughEnergyForOneRangedMobIsRefused()
        {
            var sim = new SummonSim(SummonTestKit.Config(start: 9f));
            var mobs = NewMobs();

            SummonResult result = sim.Summon(Ctx(mobs), true, MobKinds.Ranged, out int created);

            Assert.AreEqual(SummonResult.NoEnergy, result);
            Assert.AreEqual(0, created);
            Assert.AreEqual(9f, sim.Energy, 1e-3f);
        }

        [Test]
        public void KindFiveIsStillUnknown()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = NewMobs();

            SummonResult result = sim.Summon(Ctx(mobs), true, (byte)MobKinds.Count, out int created);

            Assert.AreEqual(SummonResult.UnknownKind, result);
            Assert.AreEqual(0, created);
            Assert.AreEqual(100f, sim.Energy, 1e-3f);
        }

        [Test]
        public void ARequestNotFromTheBossIsIgnoredForTheNewKinds()
        {
            foreach (byte kind in new[] { MobKinds.Ranged, MobKinds.Exploder })
            {
                var sim = new SummonSim(SummonTestKit.Config());
                var mobs = NewMobs();
                Assert.AreEqual(SummonResult.Ignored, sim.Summon(Ctx(mobs), false, kind, out int created));
                Assert.AreEqual(0, created);
            }
        }

        [Test]
        public void AnOrderReachesNewSummonedMobsOfTheNewKinds()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = NewMobs();
            var survivors = new List<SurvivorInfo> { SummonTestKit.Survivor(7, 10f, -6f) };
            var ctx = SummonTestKit.Context(mobs, Vector2.zero, survivors);
            sim.GiveOrder(ctx, true, new Vector2(10f, -6f));

            sim.Summon(ctx, true, MobKinds.Exploder, out int created);

            Assert.AreEqual(3, created);
            for (int i = 0; i < mobs.Count; i++)
                Assert.IsTrue(mobs.TryGetOverride(mobs.IdAt(i), out bool isPlayer, out ulong client, out _) && isPlayer && client == 7);
        }

        [Test]
        public void TheHardCapStillCountsEveryKindAsOne()
        {
            var sim = new SummonSim(SummonTestKit.Config());
            var mobs = new MobSim(hardCap: 4, targetCount: 0);

            sim.Summon(Ctx(mobs), true, MobKinds.Exploder, out _);
            SummonResult result = sim.Summon(Ctx(mobs), true, MobKinds.Ranged, out int created);
            // 3 exploders are on the field: one slot is left for the ranged group of two.
            Assert.AreEqual(SummonResult.Cooldown, result, "the cooldown comes first");
            Assert.AreEqual(0, created);

            sim.Step(1f, true);
            result = sim.Summon(Ctx(mobs), true, MobKinds.Ranged, out created);
            Assert.AreEqual(SummonResult.Partial, result);
            Assert.AreEqual(1, created);
            Assert.AreEqual(4, mobs.Count);
        }
    }
}
