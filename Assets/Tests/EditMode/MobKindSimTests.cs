using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class MobKindSimTests
    {
        const float Dt = 0.02f;
        static readonly byte BasicAuto = MobKinds.Encode(MobKinds.OriginAuto, MobKinds.Basic);
        static readonly byte FastSummoned = MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Fast);
        static readonly byte ToughSummoned = MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Tough);
        static readonly byte BasicSummoned = MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Basic);

        static List<SurvivorInfo> Survivors(float x = 0f, float y = 0f) =>
            new List<SurvivorInfo> { new SurvivorInfo { ClientId = 1, Position = new Vector2(x, y), Alive = true } };

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

        // ---- speed ----

        [Test]
        public void TheKindsMoveAtTheirOwnSpeeds()
        {
            var sim = new MobSim(targetCount: 0);
            ushort basic = Spawn(sim, -8f, 8f, BasicAuto);
            ushort fast = Spawn(sim, 8f, 8f, FastSummoned);
            ushort tough = Spawn(sim, 8f, -8f, ToughSummoned);
            Vector2 target = Vector2.zero;
            float basicBefore = Vector2.Distance(PositionOf(sim, basic), target);
            float fastBefore = Vector2.Distance(PositionOf(sim, fast), target);
            float toughBefore = Vector2.Distance(PositionOf(sim, tough), target);

            for (int i = 0; i < 50; i++) sim.Step(Dt, Survivors());

            float basicMoved = basicBefore - Vector2.Distance(PositionOf(sim, basic), target);
            float fastMoved = fastBefore - Vector2.Distance(PositionOf(sim, fast), target);
            float toughMoved = toughBefore - Vector2.Distance(PositionOf(sim, tough), target);
            Assert.AreEqual(sim.Speed * 1f, basicMoved, 0.05f);
            Assert.AreEqual(sim.SpeedOf(MobKinds.Fast), fastMoved, 0.05f);
            Assert.AreEqual(sim.SpeedOf(MobKinds.Tough), toughMoved, 0.05f);
            Assert.Greater(fastMoved, basicMoved);
            Assert.Less(toughMoved, basicMoved);
        }

        [Test]
        public void TheFastMobArrivesBeforeTheBasicMob()
        {
            var sim = new MobSim(targetCount: 0);
            ushort basic = Spawn(sim, -8f, 0f, BasicAuto);
            ushort fast = Spawn(sim, 8f, 0f, FastSummoned);

            int fastArrived = -1, basicArrived = -1;
            for (int i = 0; i < 1000 && (fastArrived < 0 || basicArrived < 0); i++)
            {
                sim.Step(Dt, Survivors());
                if (fastArrived < 0 && PositionOf(sim, fast).magnitude < 1f) fastArrived = i;
                if (basicArrived < 0 && PositionOf(sim, basic).magnitude < 1f) basicArrived = i;
            }
            Assert.GreaterOrEqual(fastArrived, 0);
            Assert.Less(fastArrived, basicArrived);
        }

        [Test]
        public void ChangingTheBasicSpeedChangesEveryKind()
        {
            var sim = new MobSim(targetCount: 0) { Speed = 5f };
            Assert.AreEqual(5f * MobKinds.Get(MobKinds.Fast).SpeedMultiple, sim.SpeedOf(MobKinds.Fast), 1e-5f);
            Assert.AreEqual(5f * MobKinds.Get(MobKinds.Tough).SpeedMultiple, sim.SpeedOf(MobKinds.Tough), 1e-5f);
        }

        // ---- health ----

        [Test]
        public void EveryKindStartsWithItsOwnHealth()
        {
            var sim = new MobSim(targetCount: 0) { MaxHealth = 30 };
            ushort basic = Spawn(sim, 0, 5, BasicAuto);
            ushort fast = Spawn(sim, 1, 5, FastSummoned);
            ushort tough = Spawn(sim, 2, 5, ToughSummoned);

            Assert.IsTrue(sim.TryGetHealth(basic, out int hb));
            Assert.IsTrue(sim.TryGetHealth(fast, out int hf));
            Assert.IsTrue(sim.TryGetHealth(tough, out int ht));
            Assert.AreEqual(30, hb);
            Assert.AreEqual(15, hf);
            Assert.AreEqual(120, ht);
        }

        [Test]
        public void TheHealthPercentIsAgainstTheMobsOwnMaximum()
        {
            var sim = new MobSim(targetCount: 0) { MaxHealth = 30 };
            ushort tough = Spawn(sim, 0, 5, ToughSummoned);
            ushort fast = Spawn(sim, 1, 5, FastSummoned);

            Assert.IsTrue(sim.ApplyDamage(tough, 30, out int percentTough, out bool killedTough));
            Assert.IsFalse(killedTough, "a tough mob survives a basic mob's whole health");
            Assert.AreEqual(75, percentTough);

            Assert.IsTrue(sim.ApplyDamage(fast, 5, out int percentFast, out bool killedFast));
            Assert.IsFalse(killedFast);
            Assert.AreEqual(66, percentFast);
        }

        [Test]
        public void AToughMobIsKilledByEnoughDamageAndRemoved()
        {
            var sim = new MobSim(targetCount: 0) { MaxHealth = 30 };
            ushort tough = Spawn(sim, 0, 5, ToughSummoned);

            Assert.IsTrue(sim.ApplyDamage(tough, 119, out _, out bool k1));
            Assert.IsFalse(k1);
            Assert.IsTrue(sim.ApplyDamage(tough, 1, out int percent, out bool k2));
            Assert.IsTrue(k2);
            Assert.AreEqual(0, percent);
            Assert.IsFalse(sim.Contains(tough));
        }

        [Test]
        public void ChangingTheBasicHealthChangesEveryKind()
        {
            var sim = new MobSim(targetCount: 0) { MaxHealth = 200 };
            Assert.AreEqual(100, sim.MaxHealthOf(MobKinds.Fast));
            Assert.AreEqual(800, sim.MaxHealthOf(MobKinds.Tough));
        }

        // ---- size ----

        [Test]
        public void AProjectileTouchesTheBigMobButNotTheSmallOneAtTheSameDistance()
        {
            var sim = new MobSim(targetCount: 0);
            // Both centers are 0.6 above the line; the shot's own radius is 0.1.
            // Reach: fast 0.1 + 0.32 = 0.42 (miss), tough 0.1 + 0.5 = 0.6 (touch).
            Spawn(sim, 5f, 0.58f, FastSummoned);
            Assert.IsFalse(sim.TryHitSegment(new Vector2(0, 0), new Vector2(10, 0), 0.1f, out _, out _), "the small mob is missed");

            var sim2 = new MobSim(targetCount: 0);
            ushort tough = Spawn(sim2, 5f, 0.58f, ToughSummoned);
            Assert.IsTrue(sim2.TryHitSegment(new Vector2(0, 0), new Vector2(10, 0), 0.1f, out ushort hit, out _));
            Assert.AreEqual(tough, hit);
        }

        [Test]
        public void ThePiercingQueryUsesEachMobsOwnSize()
        {
            var sim = new MobSim(targetCount: 0);
            ushort tough = Spawn(sim, 4f, 0.58f, ToughSummoned);
            Spawn(sim, 6f, 0.58f, FastSummoned);

            var hits = new List<MobSim.SegmentHit>();
            sim.CollectSegmentHits(new Vector2(0, 0), new Vector2(10, 0), 0.1f, null, hits);

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(tough, hits[0].MobId);
        }

        [Test]
        public void AVeryFastShotStillHitsABigMobOnItsPath()
        {
            var sim = new MobSim(targetCount: 0);
            ushort tough = Spawn(sim, 5f, 0f, ToughSummoned);

            Assert.IsTrue(sim.TryHitSegment(new Vector2(0, 0), new Vector2(12, 0), 0.1f, out ushort hit, out _));
            Assert.AreEqual(tough, hit);
        }

        [Test]
        public void MobsOfDifferentSizesArePushedApartBySumOfTheirRadii()
        {
            var sim = new MobSim(targetCount: 0);
            ushort big = Spawn(sim, 0f, 5f, ToughSummoned);
            ushort small = Spawn(sim, 0.1f, 5f, FastSummoned);
            // Survivors far away and out of reach so chasing hardly changes the pair's distance.
            var survivors = Survivors(-15f, -9f);

            for (int i = 0; i < 400; i++) sim.Step(Dt, survivors);

            float wanted = sim.RadiusOf(MobKinds.Tough) + sim.RadiusOf(MobKinds.Fast);
            float distance = Vector2.Distance(PositionOf(sim, big), PositionOf(sim, small));
            Assert.GreaterOrEqual(distance, wanted - 0.05f);
        }

        [Test]
        public void TwoBigMobsKeepApartEvenAtTheLargestRadius()
        {
            var sim = new MobSim(targetCount: 0);
            ushort a = Spawn(sim, 0f, 5f, ToughSummoned);
            ushort b = Spawn(sim, 0.2f, 5f, ToughSummoned);
            var survivors = Survivors(-15f, -9f);

            for (int i = 0; i < 400; i++) sim.Step(Dt, survivors);

            float distance = Vector2.Distance(PositionOf(sim, a), PositionOf(sim, b));
            Assert.GreaterOrEqual(distance, sim.RadiusOf(MobKinds.Tough) * 2f - 0.05f);
        }

        [Test]
        public void ABigMobStaysWhollyInsideTheArena()
        {
            var sim = new MobSim(targetCount: 0);
            ushort tough = Spawn(sim, 100f, 100f, ToughSummoned);
            float radius = sim.RadiusOf(MobKinds.Tough);

            Vector2 p = PositionOf(sim, tough);
            Assert.LessOrEqual(p.x, ArenaConfig.Width * 0.5f - radius + 1e-4f);
            Assert.LessOrEqual(p.y, ArenaConfig.Height * 0.5f - radius + 1e-4f);

            // And chasing a survivor at the corner never carries it outside.
            for (int i = 0; i < 100; i++) sim.Step(Dt, Survivors(15.9f, 9.9f));
            p = PositionOf(sim, tough);
            Assert.LessOrEqual(Mathf.Abs(p.x), ArenaConfig.Width * 0.5f - radius + 1e-4f);
            Assert.LessOrEqual(Mathf.Abs(p.y), ArenaConfig.Height * 0.5f - radius + 1e-4f);
        }

        [Test]
        public void APushKeepsABigMobInsideTheArena()
        {
            var sim = new MobSim(targetCount: 0);
            ushort tough = Spawn(sim, 15f, 0f, ToughSummoned);

            sim.Push(new Vector2(10f, 0f), 6f, 10f);

            float radius = sim.RadiusOf(MobKinds.Tough);
            Assert.LessOrEqual(PositionOf(sim, tough).x, ArenaConfig.Width * 0.5f - radius + 1e-4f);
        }

        // ---- contact damage ----

        [Test]
        public void TheContactMultipleIsTheHighestAmongTheMobsTouching()
        {
            var sim = new MobSim(targetCount: 0);
            Spawn(sim, 0.5f, 0f, BasicSummoned);
            Spawn(sim, -0.6f, 0f, ToughSummoned);

            Assert.IsTrue(sim.MaxContactMultipleWithin(Vector2.zero, 0.5f, out float multiple));
            Assert.AreEqual(MobKinds.Get(MobKinds.Tough).ContactDamageMultiple, multiple, 1e-5f);
        }

        [Test]
        public void ABasicMobAloneGivesAMultipleOfOne()
        {
            var sim = new MobSim(targetCount: 0);
            Spawn(sim, 0.5f, 0f, BasicAuto);

            Assert.IsTrue(sim.MaxContactMultipleWithin(Vector2.zero, 0.5f, out float multiple));
            Assert.AreEqual(1f, multiple);
        }

        [Test]
        public void NoMobTouchingGivesNothing()
        {
            var sim = new MobSim(targetCount: 0);
            Spawn(sim, 5f, 0f, ToughSummoned);

            Assert.IsFalse(sim.MaxContactMultipleWithin(Vector2.zero, 0.5f, out float multiple));
            Assert.AreEqual(0f, multiple);
            Assert.IsFalse(new MobSim(targetCount: 0).MaxContactMultipleWithin(Vector2.zero, 0.5f, out _));
        }

        [Test]
        public void TheBigMobTouchesFromFartherAwayThanTheSmallOne()
        {
            var sim = new MobSim(targetCount: 0);
            // Survivor radius 0.5: the fast mob (0.32) reaches 0.82, the tough mob (0.5) reaches 1.0.
            Spawn(sim, 0.9f, 0f, FastSummoned);
            Assert.IsFalse(sim.MaxContactMultipleWithin(Vector2.zero, 0.5f, out _));

            var sim2 = new MobSim(targetCount: 0);
            Spawn(sim2, 0.9f, 0f, ToughSummoned);
            Assert.IsTrue(sim2.MaxContactMultipleWithin(Vector2.zero, 0.5f, out _));
        }

        [Test]
        public void ASurvivorTakesTheToughDamageOnceFromAMixedCrowd()
        {
            var survivors = new SurvivorSim();
            survivors.Register(1, 100);
            var mobs = new MobSim(targetCount: 0);
            Spawn(mobs, 0.5f, 0f, BasicSummoned);
            Spawn(mobs, -0.6f, 0f, ToughSummoned);
            var bodies = new List<SurvivorBody> { new SurvivorBody { ClientId = 1, Position = Vector2.zero } };

            survivors.Step(Dt, bodies, mobs);

            Assert.AreEqual(100 - 15, survivors.Health(1), "10 x 1.5 for the tough mob, once");
            survivors.Step(Dt, bodies, mobs);
            Assert.AreEqual(100 - 15, survivors.Health(1), "protected for the protection time");
        }

        [Test]
        public void ABasicMobStillDealsTen()
        {
            var survivors = new SurvivorSim();
            survivors.Register(1, 100);
            var mobs = new MobSim(targetCount: 0);
            Spawn(mobs, 0.5f, 0f, BasicAuto);
            var bodies = new List<SurvivorBody> { new SurvivorBody { ClientId = 1, Position = Vector2.zero } };

            survivors.Step(Dt, bodies, mobs);

            Assert.AreEqual(90, survivors.Health(1));
        }

        [Test]
        public void AFastMobDealsLessThanABasicOne()
        {
            var survivors = new SurvivorSim();
            survivors.Register(1, 100);
            var mobs = new MobSim(targetCount: 0);
            Spawn(mobs, 0.5f, 0f, FastSummoned);
            var bodies = new List<SurvivorBody> { new SurvivorBody { ClientId = 1, Position = Vector2.zero } };

            survivors.Step(Dt, bodies, mobs);

            Assert.AreEqual(100 - 8, survivors.Health(1));
        }

        [Test]
        public void FrozenMobsDealNoContactDamageWhateverTheKind()
        {
            var survivors = new SurvivorSim();
            survivors.Register(1, 100);
            var mobs = new MobSim(targetCount: 0);
            Spawn(mobs, 0.5f, 0f, ToughSummoned);
            mobs.Freeze(2f);
            var bodies = new List<SurvivorBody> { new SurvivorBody { ClientId = 1, Position = Vector2.zero } };

            survivors.Step(Dt, bodies, mobs);

            Assert.AreEqual(100, survivors.Health(1));
        }

        [Test]
        public void AFallenSurvivorTakesNoContactDamage()
        {
            var survivors = new SurvivorSim();
            survivors.Register(1, 100);
            survivors.Kill(1);
            var mobs = new MobSim(targetCount: 0);
            Spawn(mobs, 0.5f, 0f, ToughSummoned);
            var bodies = new List<SurvivorBody> { new SurvivorBody { ClientId = 1, Position = Vector2.zero } };

            survivors.Step(Dt, bodies, mobs);

            Assert.AreEqual(0, survivors.Health(1));
        }

        // ---- origin and kind are independent ----

        [Test]
        public void ASummonedToughMobCountsAsSummonedAndObeysOrders()
        {
            var sim = new MobSim(targetCount: 0);
            ushort tough = Spawn(sim, 8f, 8f, ToughSummoned);
            Spawn(sim, -8f, 8f, BasicAuto);

            Assert.AreEqual(1, sim.SummonedCount);
            Assert.AreEqual(1, sim.AutoCount);
            Assert.AreEqual(1, sim.ApplyOrderToSummoned(false, 0, true, new Vector2(-12f, -8f)));
            Assert.IsTrue(sim.TryGetOverride(tough, out bool isPlayer, out _, out Vector2 point));
            Assert.IsFalse(isPlayer);
            Assert.AreEqual(-12f, point.x, 1e-4f);
        }

        [Test]
        public void RemovingASummonedMobOfAnyKindLowersTheSummonedCount()
        {
            var sim = new MobSim(targetCount: 0);
            ushort fast = Spawn(sim, 0, 5, FastSummoned);
            ushort tough = Spawn(sim, 1, 5, ToughSummoned);
            Spawn(sim, 2, 5, BasicSummoned);

            sim.Remove(fast);
            Assert.AreEqual(2, sim.SummonedCount);
            sim.Remove(tough);
            Assert.AreEqual(1, sim.SummonedCount);
            Assert.AreEqual(0, sim.AutoCount);
        }

        [Test]
        public void KindsDoNotChangeTheHardCap()
        {
            var sim = new MobSim(hardCap: 3, targetCount: 0);
            Spawn(sim, 0, 5, ToughSummoned);
            Spawn(sim, 1, 5, FastSummoned);
            Spawn(sim, 2, 5, BasicAuto);

            Assert.IsFalse(sim.TrySpawn(new Vector2(3, 5), ToughSummoned, out _));
        }

        [Test]
        public void TheKindSurvivesSlotSwapsWhenAnEarlierMobIsRemoved()
        {
            var sim = new MobSim(targetCount: 0);
            ushort first = Spawn(sim, 0, 5, BasicAuto);
            ushort tough = Spawn(sim, 1, 5, ToughSummoned);

            sim.Remove(first);

            for (int i = 0; i < sim.Count; i++)
                if (sim.IdAt(i) == tough)
                {
                    Assert.AreEqual(MobKinds.Tough, sim.KindAt(i));
                    Assert.IsTrue(sim.IsSummoned(i));
                    Assert.AreEqual(ToughSummoned, sim.TypeAt(i));
                }
        }

        [Test]
        public void AutomaticSpawningMakesBasicMobsOnly()
        {
            var sim = new MobSim(hardCap: 50, targetCount: 30) { SpawnPerSecond = 50f };
            var survivors = Survivors(0f, 0f);

            for (int i = 0; i < 100; i++) sim.Step(Dt, survivors);

            Assert.Greater(sim.Count, 5);
            for (int i = 0; i < sim.Count; i++)
                Assert.AreEqual(MobSim.TypeAuto, sim.TypeAt(i));
        }
    }
}
