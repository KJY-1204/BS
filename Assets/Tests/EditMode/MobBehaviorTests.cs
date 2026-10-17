using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class MobBehaviorTests
    {
        const float Dt = 0.02f;
        static readonly byte Ranged = MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Ranged);
        static readonly byte Exploder = MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Exploder);
        static readonly byte Basic = MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Basic);

        static List<SurvivorInfo> One(float x = 0f, float y = 0f, bool alive = true, ulong id = 1) =>
            new List<SurvivorInfo> { new SurvivorInfo { ClientId = id, Position = new Vector2(x, y), Alive = alive } };

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

        static void Run(MobSim sim, List<SurvivorInfo> survivors, float seconds, List<MobShotRequest> shots = null, List<MobBlast> blasts = null)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                sim.Step(Dt, survivors);
                sim.DrainActions(shots ?? new List<MobShotRequest>(), blasts ?? new List<MobBlast>());
            }
        }

        static MobKindDef Def(byte kind) => MobKinds.Get(kind);

        // ---- 1.1 the data ----

        [Test]
        public void TheOldKindsChase_AndTheNewOnesHaveTheirOwnBehaviors()
        {
            Assert.AreEqual(MobBehavior.Chase, Def(MobKinds.Basic).Behavior);
            Assert.AreEqual(MobBehavior.Chase, Def(MobKinds.Fast).Behavior);
            Assert.AreEqual(MobBehavior.Chase, Def(MobKinds.Tough).Behavior);
            Assert.AreEqual(MobBehavior.Ranged, Def(MobKinds.Ranged).Behavior);
            Assert.AreEqual(MobBehavior.Exploder, Def(MobKinds.Exploder).Behavior);
        }

        [Test]
        public void TheNewKindsHaveSensibleBehaviorValues()
        {
            MobKindDef r = Def(MobKinds.Ranged);
            Assert.Greater(r.HoldDistance, 0f);
            Assert.Greater(r.ShootRange, r.HoldDistance);
            Assert.Greater(r.ShotCooldownSeconds, 0f);
            Assert.Greater(r.ShotDamage, 0);

            MobKindDef e = Def(MobKinds.Exploder);
            Assert.Greater(e.TriggerDistance, 0f);
            Assert.Greater(e.FuseSeconds, 0f);
            Assert.Greater(e.BlastRadius, 0f);
            Assert.Greater(e.BlastDamage, 0);
        }

        [Test]
        public void EveryKindIsDistinctInColorAndNoneIsTooBig()
        {
            var colors = new HashSet<Color>();
            var sim = new MobSim(targetCount: 0);
            for (byte k = 0; k < MobKinds.Count; k++)
            {
                Assert.IsTrue(colors.Add(MobKinds.Get(k).Color), MobKinds.Get(k).Name);
                Assert.LessOrEqual(sim.RadiusOf(k), MobKinds.MaxRadius + 1e-6f);
            }
            Assert.AreEqual(5, colors.Count);
        }

        [Test]
        public void TheMobShotIsSlowerAndThinnerThanTheBossShot()
        {
            ProjectileShape shot = MobShot.Shape;
            Assert.Greater(shot.Speed, 0f);
            Assert.Greater(shot.Lifetime, 0f);
            Assert.Less(shot.Radius, BossAttack.Shape.Radius);
            Assert.AreEqual(1, shot.Pierce);
        }

        [Test]
        public void TheFiveKindsRoundTripThroughTheTypeByte()
        {
            for (byte kind = 0; kind < MobKinds.Count; kind++)
            foreach (byte origin in new byte[] { MobKinds.OriginAuto, MobKinds.OriginSummoned })
            {
                byte type = MobKinds.Encode(origin, kind);
                Assert.AreEqual(kind, MobKinds.KindOf(type));
                Assert.AreEqual(origin, MobKinds.OriginOf(type));
            }
        }

        // ---- 1.2 ranged mobs ----

        [Test]
        public void ARangedMobStopsAtItsHoldDistance()
        {
            var sim = new MobSim(targetCount: 0);
            ushort mob = Spawn(sim, 12f, 0f, Ranged);

            Run(sim, One(0f, 0f), 8f);

            float distance = PositionOf(sim, mob).magnitude;
            Assert.AreEqual(Def(MobKinds.Ranged).HoldDistance, distance, 0.2f);
        }

        [Test]
        public void ARangedMobKeepsStandingWhileTheSurvivorStaysClose()
        {
            var sim = new MobSim(targetCount: 0);
            ushort mob = Spawn(sim, 12f, 0f, Ranged);
            Run(sim, One(0f, 0f), 8f);
            Vector2 stopped = PositionOf(sim, mob);

            Run(sim, One(0f, 0f), 2f);

            Assert.AreEqual(stopped.x, PositionOf(sim, mob).x, 1e-3f);
        }

        [Test]
        public void ARangedMobMovesAgainWhenTheSurvivorWalksAway()
        {
            var sim = new MobSim(targetCount: 0);
            ushort mob = Spawn(sim, 12f, 0f, Ranged);
            Run(sim, One(0f, 0f), 8f);
            Vector2 stopped = PositionOf(sim, mob);

            Run(sim, One(-10f, 0f), 2f);

            Assert.Less(PositionOf(sim, mob).x, stopped.x - 0.5f);
        }

        [Test]
        public void ARangedMobFiresAtTheNearestSurvivorOnceEveryCooldown()
        {
            var sim = new MobSim(targetCount: 0);
            Spawn(sim, 6f, 0f, Ranged);
            var shots = new List<MobShotRequest>();

            Run(sim, One(0f, 0f), 10f, shots);

            float cooldown = Def(MobKinds.Ranged).ShotCooldownSeconds;
            Assert.GreaterOrEqual(shots.Count, Mathf.FloorToInt(10f / cooldown) - 1);
            Assert.LessOrEqual(shots.Count, Mathf.CeilToInt(10f / cooldown) + 1);
            Assert.AreEqual(MobKinds.Ranged, shots[0].Kind);
        }

        [Test]
        public void TheShotsAimAtTheNearestLivingSurvivor()
        {
            var sim = new MobSim(targetCount: 0);
            Spawn(sim, 6f, 0f, Ranged);
            // A survivor straight left (distance 6) and a nearer one straight up from the mob (distance 3): up wins.
            var survivors = new List<SurvivorInfo>
            {
                new SurvivorInfo { ClientId = 1, Position = new Vector2(0f, 0f), Alive = true },
                new SurvivorInfo { ClientId = 2, Position = new Vector2(6f, 3f), Alive = true },
            };
            var shots = new List<MobShotRequest>();

            Run(sim, survivors, 4f, shots);

            Assert.GreaterOrEqual(shots.Count, 1);
            Vector2 aim = AimMath.Direction(shots[0].Angle);
            Assert.Greater(aim.y, 0.99f, "straight up toward the nearer survivor");
        }

        [Test]
        public void ADeadSurvivorIsNotShotAt()
        {
            var sim = new MobSim(targetCount: 0);
            Spawn(sim, 6f, 0f, Ranged);
            var survivors = new List<SurvivorInfo>
            {
                new SurvivorInfo { ClientId = 1, Position = new Vector2(5f, 0f), Alive = false },
                new SurvivorInfo { ClientId = 2, Position = new Vector2(6f, 8f), Alive = true },
            };
            var shots = new List<MobShotRequest>();

            Run(sim, survivors, 4f, shots);

            Assert.GreaterOrEqual(shots.Count, 1);
            Assert.Greater(AimMath.Direction(shots[0].Angle).y, 0.99f);
        }

        [Test]
        public void NothingIsShotWhenNoSurvivorIsAliveOrInRange()
        {
            var sim = new MobSim(targetCount: 0);
            Spawn(sim, 12f, 0f, Ranged);
            var shots = new List<MobShotRequest>();

            Run(sim, One(-14f, 8f, alive: false), 6f, shots);
            Assert.AreEqual(0, shots.Count, "nobody alive");

            var sim2 = new MobSim(targetCount: 0);
            Spawn(sim2, 15f, 9f, Ranged);
            var shots2 = new List<MobShotRequest>();
            Run(sim2, One(-15f, -9f), 0.5f, shots2); // far outside the shooting range
            Assert.AreEqual(0, shots2.Count);
        }

        [Test]
        public void MobsMadeTogetherDoNotAllFireAtTheSameTime()
        {
            var sim = new MobSim(targetCount: 0);
            for (int i = 0; i < 8; i++) Spawn(sim, 4f + i * 0.05f, i * 0.9f - 3f, Ranged);
            var firstShotTime = new Dictionary<ushort, float>();
            var survivors = One(0f, 0f);

            for (int step = 0; step < 200; step++)
            {
                sim.Step(Dt, survivors);
                var shots = new List<MobShotRequest>();
                sim.DrainActions(shots, new List<MobBlast>());
                foreach (MobShotRequest s in shots) if (!firstShotTime.ContainsKey(s.MobId)) firstShotTime[s.MobId] = step * Dt;
            }

            Assert.GreaterOrEqual(firstShotTime.Count, 6);
            Assert.Greater(firstShotTime.Values.Distinct().Count(), 2, "the first shots leave at different times");
        }

        [Test]
        public void ARangedMobOrderedToAPointKeepsGoingAndStillShoots()
        {
            var sim = new MobSim(targetCount: 0);
            ushort mob = Spawn(sim, 8f, 6f, Ranged);
            sim.ApplyOrderToSummoned(false, 0, true, new Vector2(-8f, 6f));
            var shots = new List<MobShotRequest>();

            Run(sim, One(0f, 3f), 5f, shots);

            Assert.Less(PositionOf(sim, mob).x, 0f, "it did not stop to hold its distance from the survivor");
            Assert.GreaterOrEqual(shots.Count, 1, "but it shoots a survivor within its range on the way");
        }

        [Test]
        public void ARefundedShotCanBeAskedAgainAtOnce()
        {
            var sim = new MobSim(targetCount: 0);
            ushort mob = Spawn(sim, 6f, 0f, Ranged);
            var shots = new List<MobShotRequest>();
            Run(sim, One(0f, 0f), 6f, shots);
            Assert.GreaterOrEqual(shots.Count, 1);

            sim.RefundShot(shots[shots.Count - 1].MobId);
            var again = new List<MobShotRequest>();
            sim.Step(Dt, One(0f, 0f));
            sim.DrainActions(again, new List<MobBlast>());

            Assert.AreEqual(1, again.Count);
            Assert.AreEqual(mob, again[0].MobId);
        }

        [Test]
        public void FrozenRangedMobsDoNotShootAndTheCooldownWaits()
        {
            var sim = new MobSim(targetCount: 0);
            Spawn(sim, 6f, 0f, Ranged);
            sim.Freeze(3f);
            var shots = new List<MobShotRequest>();

            Run(sim, One(0f, 0f), 2.9f, shots);

            Assert.AreEqual(0, shots.Count);
        }

        [Test]
        public void TheBehaviorStateSurvivesTheSlotSwapOfARemoval()
        {
            var sim = new MobSim(targetCount: 0);
            ushort first = Spawn(sim, -10f, 8f, Basic);
            ushort exploder = Spawn(sim, 1f, 0f, Exploder);
            var survivors = One(0f, 0f);
            sim.Step(Dt, survivors); // the exploder lights its fuse
            Assert.IsTrue(sim.IsFuseLit(exploder));

            sim.Remove(first);

            Assert.IsTrue(sim.IsFuseLit(exploder), "the fuse state moved with the mob");
        }

        // ---- 1.3 exploding mobs ----

        [Test]
        public void AnExploderRunsInAndStopsAtTheTriggerDistance()
        {
            var sim = new MobSim(targetCount: 0);
            ushort mob = Spawn(sim, 8f, 0f, Exploder);
            var survivors = One(0f, 0f);

            for (int i = 0; i < 400 && !sim.IsFuseLit(mob); i++) sim.Step(Dt, survivors);

            Assert.IsTrue(sim.IsFuseLit(mob));
            Assert.LessOrEqual(PositionOf(sim, mob).magnitude, Def(MobKinds.Exploder).TriggerDistance + 0.1f);
        }

        [Test]
        public void TheFuseLastsItsLengthThenTheMobBlastsAndIsRemoved()
        {
            var sim = new MobSim(targetCount: 0);
            ushort mob = Spawn(sim, 1.2f, 0f, Exploder);
            var survivors = One(0f, 0f);
            var blasts = new List<MobBlast>();
            float litAt = -1f, blastAt = -1f;

            for (int step = 0; step < 200; step++)
            {
                sim.Step(Dt, survivors);
                if (litAt < 0f && sim.IsFuseLit(mob)) litAt = step * Dt;
                int before = blasts.Count;
                sim.DrainActions(new List<MobShotRequest>(), blasts);
                if (blasts.Count > before) { blastAt = step * Dt; break; }
            }

            Assert.GreaterOrEqual(litAt, 0f);
            Assert.AreEqual(Def(MobKinds.Exploder).FuseSeconds, blastAt - litAt, 0.08f);
            Assert.AreEqual(1, blasts.Count);
            Assert.AreEqual(mob, blasts[0].MobId);
            Assert.AreEqual(Def(MobKinds.Exploder).BlastRadius, blasts[0].Radius);
            Assert.AreEqual(Def(MobKinds.Exploder).BlastDamage, blasts[0].Damage);
            Assert.IsFalse(sim.Contains(mob));
            Assert.AreEqual(0, sim.Count);
        }

        [Test]
        public void ABlastedMobSendsARemovalEventAndLowersTheSummonedCount()
        {
            var sim = new MobSim(targetCount: 0);
            Spawn(sim, 1.2f, 0f, Exploder);
            var events = new List<MobEvent>();
            sim.DrainEvents(events);
            events.Clear();

            Run(sim, One(0f, 0f), 2f);
            sim.DrainEvents(events);

            Assert.IsTrue(events.Any(e => !e.IsSpawn));
            Assert.AreEqual(0, sim.SummonedCount);
        }

        [Test]
        public void ALitExploderStandsStillEvenWhileTheSurvivorRunsAway()
        {
            var sim = new MobSim(targetCount: 0);
            ushort mob = Spawn(sim, 1.2f, 0f, Exploder);
            sim.Step(Dt, One(0f, 0f));
            Assert.IsTrue(sim.IsFuseLit(mob));
            Vector2 where = PositionOf(sim, mob);

            for (int i = 0; i < 20; i++) sim.Step(Dt, One(-10f, 0f)); // 0.4 s, still within the fuse

            Assert.AreEqual(where.x, PositionOf(sim, mob).x, 1e-3f);
            Assert.IsTrue(sim.IsFuseLit(mob), "the fuse goes on although nobody is near any more");
        }

        [Test]
        public void ThePositionsOfTheBlastIsWhereTheMobStood()
        {
            var sim = new MobSim(targetCount: 0);
            ushort mob = Spawn(sim, 1.2f, 0f, Exploder);
            sim.Step(Dt, One(0f, 0f));
            Vector2 where = PositionOf(sim, mob);
            var blasts = new List<MobBlast>();

            Run(sim, One(0f, 0f), 2f, null, blasts);

            Assert.AreEqual(1, blasts.Count);
            Assert.AreEqual(where.x, blasts[0].Position.x, 1e-3f);
            Assert.AreEqual(where.y, blasts[0].Position.y, 1e-3f);
        }

        [Test]
        public void AnExploderKilledDuringTheFuseDoesNotBlast()
        {
            var sim = new MobSim(targetCount: 0) { MaxHealth = 30 };
            ushort mob = Spawn(sim, 1.2f, 0f, Exploder);
            var blasts = new List<MobBlast>();
            sim.Step(Dt, One(0f, 0f));
            Assert.IsTrue(sim.IsFuseLit(mob));

            sim.ApplyDamage(mob, 999, out _, out bool killed);
            Assert.IsTrue(killed);
            Run(sim, One(0f, 0f), 2f, null, blasts);

            Assert.AreEqual(0, blasts.Count);
        }

        [Test]
        public void TheFuseDoesNotRunWhileTheMobsAreFrozenAndGoesOnAfterwards()
        {
            var sim = new MobSim(targetCount: 0);
            ushort mob = Spawn(sim, 1.2f, 0f, Exploder);
            sim.Step(Dt, One(0f, 0f));
            Assert.IsTrue(sim.IsFuseLit(mob));
            sim.Freeze(3f);
            var blasts = new List<MobBlast>();

            Run(sim, One(0f, 0f), 2.9f, null, blasts);
            Assert.AreEqual(0, blasts.Count, "frozen: no blast");
            Assert.IsTrue(sim.Contains(mob));

            Run(sim, One(0f, 0f), 1.5f, null, blasts);
            Assert.AreEqual(1, blasts.Count, "the fuse resumed and ran out after the stop");
        }

        [Test]
        public void AnExploderOrderedToASurvivorStillLightsItsFuseNearAnotherOne()
        {
            var sim = new MobSim(targetCount: 0);
            ushort mob = Spawn(sim, 8f, 0f, Exploder);
            sim.ApplyOrderToSummoned(true, 1, false, default);
            var survivors = new List<SurvivorInfo>
            {
                new SurvivorInfo { ClientId = 1, Position = new Vector2(-14f, 8f), Alive = true },
                new SurvivorInfo { ClientId = 2, Position = new Vector2(8f, 1f), Alive = true },
            };

            sim.Step(Dt, survivors);

            Assert.IsTrue(sim.IsFuseLit(mob));
        }

        [Test]
        public void NoFuseWhenOnlyAFallenSurvivorIsClose()
        {
            var sim = new MobSim(targetCount: 0);
            ushort mob = Spawn(sim, 1f, 0f, Exploder);
            var survivors = new List<SurvivorInfo>
            {
                new SurvivorInfo { ClientId = 1, Position = Vector2.zero, Alive = false },
                new SurvivorInfo { ClientId = 2, Position = new Vector2(-14f, 8f), Alive = true },
            };

            sim.Step(Dt, survivors);

            Assert.IsFalse(sim.IsFuseLit(mob));
        }

        [Test]
        public void TwoExplodersAtOnceBothBlastAndTheCountsStayRight()
        {
            var sim = new MobSim(targetCount: 0);
            Spawn(sim, 1.2f, 0f, Exploder);
            Spawn(sim, -1.2f, 0.4f, Exploder);
            Spawn(sim, 0f, 9f, Basic);
            var blasts = new List<MobBlast>();

            Run(sim, One(0f, 0f), 2f, null, blasts);

            Assert.AreEqual(2, blasts.Count);
            Assert.AreEqual(1, sim.SummonedCount);
            Assert.AreEqual(1, sim.Count);
        }

        [Test]
        public void ClearingDropsActionsAndState()
        {
            var sim = new MobSim(targetCount: 0);
            Spawn(sim, 1.2f, 0f, Exploder);
            Run(sim, One(0f, 0f), 2f);
            sim.Clear();

            var shots = new List<MobShotRequest>();
            var blasts = new List<MobBlast>();
            sim.DrainActions(shots, blasts);

            Assert.AreEqual(0, sim.Count);
            Assert.AreEqual(0, shots.Count);
            Assert.AreEqual(0, blasts.Count);
            ushort again = Spawn(sim, 1.2f, 0f, Exploder);
            Assert.IsFalse(sim.IsFuseLit(again));
        }

        [Test]
        public void TheOldKindsNeverMakeActions()
        {
            var sim = new MobSim(targetCount: 0);
            Spawn(sim, 1f, 0f, Basic);
            Spawn(sim, -1f, 0f, MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Tough));
            var shots = new List<MobShotRequest>();
            var blasts = new List<MobBlast>();

            Run(sim, One(0f, 0f), 5f, shots, blasts);

            Assert.AreEqual(0, shots.Count);
            Assert.AreEqual(0, blasts.Count);
            Assert.AreEqual(2, sim.Count);
        }

        // ---- 1.4 the blast on survivors ----

        static List<SurvivorBody> Bodies(params (ulong id, float x, float y)[] list) =>
            list.Select(t => new SurvivorBody { ClientId = t.id, Position = new Vector2(t.x, t.y) }).ToList();

        [Test]
        public void ABlastHurtsSurvivorsInsideTheRadiusOnly()
        {
            var survivors = new SurvivorSim();
            survivors.Register(1, 100);
            survivors.Register(2, 100);
            var bodies = Bodies((1, 1f, 0f), (2, 5f, 0f));

            int hurt = survivors.ApplyBlast(Vector2.zero, 2.2f, 25, bodies);

            Assert.AreEqual(1, hurt);
            Assert.AreEqual(75, survivors.Health(1));
            Assert.AreEqual(100, survivors.Health(2));
        }

        [Test]
        public void EverySurvivorInsideTakesTheDamageOnce()
        {
            var survivors = new SurvivorSim();
            survivors.Register(1, 100);
            survivors.Register(2, 100);
            var bodies = Bodies((1, 1f, 0f), (2, -1f, 0.5f));

            int hurt = survivors.ApplyBlast(Vector2.zero, 2.2f, 25, bodies);

            Assert.AreEqual(2, hurt);
            Assert.AreEqual(75, survivors.Health(1));
            Assert.AreEqual(75, survivors.Health(2));
        }

        [Test]
        public void AProtectedOrFallenSurvivorTakesNoBlastDamage()
        {
            var survivors = new SurvivorSim();
            survivors.Register(1, 100);
            survivors.Register(2, 100);
            survivors.Register(3, 100);
            Assert.IsTrue(survivors.ApplyDamage(1, 1)); // now protected
            survivors.Kill(2);
            var bodies = Bodies((1, 0.5f, 0f), (2, -0.5f, 0f), (3, 0f, 0.5f));

            int hurt = survivors.ApplyBlast(Vector2.zero, 2.2f, 25, bodies);

            Assert.AreEqual(1, hurt);
            Assert.AreEqual(99, survivors.Health(1));
            Assert.AreEqual(0, survivors.Health(2));
            Assert.AreEqual(75, survivors.Health(3));
        }

        [Test]
        public void ABlastStartsTheProtectionTime()
        {
            var survivors = new SurvivorSim();
            survivors.Register(1, 100);
            var bodies = Bodies((1, 0.5f, 0f));

            survivors.ApplyBlast(Vector2.zero, 2.2f, 25, bodies);

            Assert.IsTrue(survivors.IsInvulnerable(1));
            Assert.IsFalse(survivors.ApplyDamage(1, 10), "a touch right afterwards deals nothing");
            Assert.AreEqual(75, survivors.Health(1));
        }

        [Test]
        public void ABlastCanDropASurvivorToZeroButNotBelow()
        {
            var survivors = new SurvivorSim();
            survivors.Register(1, 20);
            var bodies = Bodies((1, 0.5f, 0f));

            survivors.ApplyBlast(Vector2.zero, 2.2f, 25, bodies);

            Assert.AreEqual(0, survivors.Health(1));
            Assert.IsFalse(survivors.IsAlive(1));
        }

        [Test]
        public void TheRadiusEdgeIsInclusive()
        {
            var survivors = new SurvivorSim();
            survivors.Register(1, 100);
            var bodies = Bodies((1, 2.2f, 0f));

            Assert.AreEqual(1, survivors.ApplyBlast(Vector2.zero, 2.2f, 25, bodies));
        }
    }
}
