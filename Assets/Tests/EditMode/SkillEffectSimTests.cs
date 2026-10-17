using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class SkillEffectSimTests
    {
        const float Dt = 0.02f;
        const ulong A = 1, B = 2;

        static ushort Spawn(MobSim sim, float x, float y)
        {
            Assert.IsTrue(sim.TrySpawn(new Vector2(x, y), 0, out ushort id));
            return id;
        }

        static MobSim Mobs(int health = 100, int target = 0) => new MobSim(targetCount: target) { MaxHealth = health };

        static List<SurvivorInfo> Survivors(float x = -12f, float y = -7f) =>
            new List<SurvivorInfo> { new SurvivorInfo { ClientId = A, Position = new Vector2(x, y), Alive = true } };

        static void Run(MobSim mobs, List<SurvivorInfo> survivors, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++) mobs.Step(Dt, survivors);
        }

        static int Health(MobSim mobs, ushort id)
        {
            Assert.IsTrue(mobs.TryGetHealth(id, out int health));
            return health;
        }

        // ---- Line damage ----

        [Test]
        public void ALineHitsMobsOnItAndNotBesideItOrBeyondItsEnd()
        {
            var mobs = Mobs();
            ushort onLine1 = Spawn(mobs, 2, 0);
            ushort onLine2 = Spawn(mobs, 6, 0.4f);
            ushort beside = Spawn(mobs, 4, 2f);
            ushort beyond = Spawn(mobs, 13, 0);
            ushort behind = Spawn(mobs, -2, 0);

            int hit = mobs.ApplyLineDamage(Vector2.zero, Vector2.right, 10f, 0.6f, 30, null);

            Assert.AreEqual(2, hit);
            Assert.AreEqual(70, Health(mobs, onLine1));
            Assert.AreEqual(70, Health(mobs, onLine2));
            Assert.AreEqual(100, Health(mobs, beside));
            Assert.AreEqual(100, Health(mobs, beyond));
            Assert.AreEqual(100, Health(mobs, behind));
        }

        [Test]
        public void ALineDoesNotStopAtTheFirstMob()
        {
            var mobs = Mobs();
            var ids = new[] { Spawn(mobs, 1, 0), Spawn(mobs, 3, 0), Spawn(mobs, 5, 0), Spawn(mobs, 7, 0) };

            Assert.AreEqual(4, mobs.ApplyLineDamage(Vector2.zero, Vector2.right, 10f, 0.5f, 10, null));
            foreach (ushort id in ids) Assert.AreEqual(90, Health(mobs, id));
        }

        [Test]
        public void ALineWorksInAnyDirection_AndTheDirectionNeedNotBeNormalised()
        {
            var mobs = Mobs();
            ushort diagonal = Spawn(mobs, 3, 3);
            ushort off = Spawn(mobs, 3, -3);

            int hit = mobs.ApplyLineDamage(Vector2.zero, new Vector2(5, 5), 10f, 0.5f, 20, null);

            Assert.AreEqual(1, hit);
            Assert.AreEqual(80, Health(mobs, diagonal));
            Assert.AreEqual(100, Health(mobs, off));
        }

        [Test]
        public void ALineReportsWhereMobsDied()
        {
            var mobs = Mobs(10);
            Spawn(mobs, 2, 0);
            Spawn(mobs, 5, 0.2f);
            Spawn(mobs, 5, 4f);
            var deaths = new List<Vector2>();

            mobs.ApplyLineDamage(Vector2.zero, Vector2.right, 10f, 0.6f, 10, deaths);

            Assert.AreEqual(2, deaths.Count);
            Assert.AreEqual(1, mobs.Count, "the one off the line lives");
        }

        [Test]
        public void AnEmptyLineDoesNothing()
        {
            var mobs = Mobs();
            Spawn(mobs, 5, 5);
            Assert.AreEqual(0, mobs.ApplyLineDamage(Vector2.zero, Vector2.right, 10f, 0.5f, 10, null));
            Assert.AreEqual(0, mobs.ApplyLineDamage(Vector2.zero, Vector2.zero, 10f, 0.5f, 10, null), "no direction, no line");
        }

        [Test]
        public void TheGridAndABruteForceCheckAgreeOnLines()
        {
            var rng = new System.Random(3);
            for (int scene = 0; scene < 40; scene++)
            {
                var mobs = Mobs(1000);
                int n = rng.Next(1, 60);
                for (int i = 0; i < n; i++) mobs.TrySpawn(new Vector2((float)(rng.NextDouble() * 28 - 14), (float)(rng.NextDouble() * 16 - 8)), 0, out _);

                Vector2 origin = new Vector2((float)(rng.NextDouble() * 20 - 10), (float)(rng.NextDouble() * 10 - 5));
                Vector2 dir = new Vector2((float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1)).normalized;
                float length = 4f + (float)rng.NextDouble() * 10f, half = 0.3f + (float)rng.NextDouble();

                var expected = new List<ushort>();
                for (int i = 0; i < mobs.Count; i++)
                {
                    Vector2 along = mobs.PositionAt(i) - origin;
                    float t = Vector2.Dot(along, dir);
                    if (t >= 0f && t <= length && Mathf.Abs(along.x * dir.y - along.y * dir.x) <= half) expected.Add(mobs.IdAt(i));
                }

                int hit = mobs.ApplyLineDamage(origin, dir, length, half, 1, null);
                Assert.AreEqual(expected.Count, hit, $"scene {scene}");
                foreach (ushort id in expected) Assert.AreEqual(999, Health(mobs, id));
            }
        }

        // ---- Time stop ----

        [Test]
        public void StoppedMobsDoNotMove_AndNoneAppears()
        {
            var mobs = Mobs(target: 30);
            Spawn(mobs, 5, 5);
            Spawn(mobs, -5, -2);
            Vector2[] before = Enumerable.Range(0, mobs.Count).Select(mobs.PositionAt).ToArray();

            mobs.Freeze(2f);
            Run(mobs, Survivors(), 1.9f);

            Assert.AreEqual(2, mobs.Count, "nothing spawned");
            for (int i = 0; i < before.Length; i++) Assert.AreEqual(before[i], mobs.PositionAt(i));
        }

        [Test]
        public void MobsMoveAndSpawnAgainWhenTheStopEnds()
        {
            var mobs = Mobs(target: 30);
            Spawn(mobs, 5, 5);
            mobs.Freeze(1f);

            Run(mobs, Survivors(), 1.2f);
            Vector2 at = mobs.PositionAt(0);
            Run(mobs, Survivors(), 1f);

            Assert.IsFalse(mobs.IsFrozen);
            Assert.AreNotEqual(at, mobs.PositionAt(0), "it chases again");
            Assert.Greater(mobs.Count, 1, "spawning resumed");
        }

        [Test]
        public void AShorterStopNeverShortensARunningOne()
        {
            var mobs = Mobs();
            mobs.Freeze(3f);
            mobs.Freeze(1f);
            Assert.AreEqual(3f, mobs.FrozenSeconds, 1e-4f);
            mobs.Freeze(5f);
            Assert.AreEqual(5f, mobs.FrozenSeconds, 1e-4f);
        }

        [Test]
        public void StoppedMobsStillTakeDamageAndCanDie()
        {
            var mobs = Mobs(10);
            ushort id = Spawn(mobs, 5, 5);
            mobs.Freeze(2f);

            Assert.IsTrue(mobs.ApplyDamage(id, 4, out _, out bool killedFirst));
            Assert.IsFalse(killedFirst);
            Assert.IsTrue(mobs.ApplyDamage(id, 6, out _, out bool killed));
            Assert.IsTrue(killed);
            Assert.AreEqual(0, mobs.Count);
        }

        [Test]
        public void StoppedMobsDoNotHurtOnContact()
        {
            var survivors = new SurvivorSim();
            survivors.Register(A);
            var mobs = Mobs();
            Spawn(mobs, 0.2f, 0);
            var bodies = new List<SurvivorBody> { new SurvivorBody { ClientId = A, Position = Vector2.zero } };

            mobs.Freeze(2f);
            for (int i = 0; i < 50; i++) survivors.Step(Dt, bodies, mobs);
            Assert.AreEqual(CombatConfig.SurvivorMaxHealth, survivors.Health(A), "no damage while stopped");

            Run(mobs, Survivors(0f, 0f), 2.5f); // the stop (only consumed by mob steps) ends, then the mob chases the survivor at the origin
            survivors.Step(Dt, bodies, mobs);
            Assert.Less(survivors.Health(A), CombatConfig.SurvivorMaxHealth, "contact hurts again once time resumes");
        }

        [Test]
        public void ClearingTheSimulationEndsTheStop()
        {
            var mobs = Mobs();
            mobs.Freeze(10f);
            mobs.Clear();
            Assert.IsFalse(mobs.IsFrozen);
        }

        // ---- Push ----

        [Test]
        public void APushMovesMobsAwayAlongTheSameLine()
        {
            var mobs = Mobs();
            ushort id = Spawn(mobs, 1, 0);

            Assert.AreEqual(1, mobs.Push(Vector2.zero, 4f, 3f));

            Vector2 p = mobs.PositionAt(0);
            Assert.AreEqual(4f, p.x, 1e-3f);
            Assert.AreEqual(0f, p.y, 1e-3f);
            Assert.AreEqual(100, Health(mobs, id), "not damaged");
        }

        [Test]
        public void APushWorksInADiagonal()
        {
            var mobs = Mobs();
            Spawn(mobs, 2, 2);

            mobs.Push(Vector2.zero, 5f, 3f);

            Vector2 p = mobs.PositionAt(0);
            Assert.AreEqual(Mathf.Sqrt(8f) + 3f, p.magnitude, 1e-3f);
            Assert.AreEqual(p.x, p.y, 1e-3f, "same direction");
        }

        [Test]
        public void APushStopsAtTheArenaEdge()
        {
            var mobs = Mobs();
            float edge = ArenaConfig.Width * 0.5f - mobs.Radius;
            Spawn(mobs, edge - 1f, 0);

            mobs.Push(Vector2.zero, 100f, 5f);

            Assert.AreEqual(edge, mobs.PositionAt(0).x, 1e-3f);
        }

        [Test]
        public void MobsOutsideTheRadiusDoNotMove()
        {
            var mobs = Mobs();
            Spawn(mobs, 6, 0);

            Assert.AreEqual(0, mobs.Push(Vector2.zero, 4f, 3f));
            Assert.AreEqual(new Vector2(6, 0), mobs.PositionAt(0));
        }

        [Test]
        public void AMobExactlyOnThePointIsPushedWithoutError()
        {
            var mobs = Mobs();
            Spawn(mobs, 0, 0);

            Assert.DoesNotThrow(() => mobs.Push(Vector2.zero, 4f, 3f));

            Vector2 p = mobs.PositionAt(0);
            Assert.AreEqual(3f, p.magnitude, 1e-3f);
            Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y));
        }

        [Test]
        public void EveryMobInRangeIsPushedOnce_NotChainedByEachOther()
        {
            var mobs = Mobs();
            Spawn(mobs, 1, 0);
            Spawn(mobs, 3.5f, 0);

            Assert.AreEqual(2, mobs.Push(Vector2.zero, 4f, 2f));

            var xs = Enumerable.Range(0, mobs.Count).Select(i => mobs.PositionAt(i).x).OrderBy(x => x).ToArray();
            Assert.AreEqual(3f, xs[0], 1e-3f);
            Assert.AreEqual(5.5f, xs[1], 1e-3f);
        }

        // ---- Survivors: heal and protection ----

        [Test]
        public void HealRestoresHealth_NeverAboveTheMaximum()
        {
            var sim = new SurvivorSim();
            sim.Register(A, 100);
            sim.ApplyDamage(A, 60);

            Assert.AreEqual(30, sim.Heal(A, 30));
            Assert.AreEqual(70, sim.Health(A));
            Assert.AreEqual(30, sim.Heal(A, 100), "only what is missing");
            Assert.AreEqual(100, sim.Health(A));
            Assert.AreEqual(0, sim.Heal(A, 10), "already full");
        }

        [Test]
        public void HealUsesTheSurvivorsOwnMaximum()
        {
            var sim = new SurvivorSim();
            sim.Register(A, 120);
            sim.ApplyDamage(A, 90);

            sim.Heal(A, 1000);

            Assert.AreEqual(120, sim.Health(A));
        }

        [Test]
        public void ADeadSurvivorIsNotHealed()
        {
            var sim = new SurvivorSim();
            sim.Register(A);
            sim.Kill(A);

            Assert.AreEqual(0, sim.Heal(A, 50));
            Assert.IsFalse(sim.IsAlive(A));
            Assert.AreEqual(0, sim.Heal(99, 50), "unknown survivors are ignored");
        }

        [Test]
        public void AProtectedSurvivorTakesNoDamage_UntilItEnds()
        {
            var sim = new SurvivorSim();
            sim.Register(A);
            var mobs = Mobs();
            Spawn(mobs, 0.2f, 0);
            var bodies = new List<SurvivorBody> { new SurvivorBody { ClientId = A, Position = Vector2.zero } };

            Assert.IsTrue(sim.GrantInvulnerability(A, 1.5f));
            for (int i = 0; i < 70; i++) sim.Step(Dt, bodies, mobs); // 1.4 s of contact
            Assert.AreEqual(CombatConfig.SurvivorMaxHealth, sim.Health(A));

            for (int i = 0; i < 20; i++) sim.Step(Dt, bodies, mobs);
            Assert.Less(sim.Health(A), CombatConfig.SurvivorMaxHealth, "unprotected again");
        }

        [Test]
        public void ALongerProtectionIsNotShortened()
        {
            var sim = new SurvivorSim();
            sim.Register(A);
            sim.GrantInvulnerability(A, 3f);
            sim.GrantInvulnerability(A, 1f);

            for (int i = 0; i < 100; i++) sim.Step(Dt, new List<SurvivorBody>(), null); // 2 s

            Assert.IsTrue(sim.IsInvulnerable(A));
        }

        [Test]
        public void ProtectionEndsWhenHealthIsSetToZero_AndNeverStartsOnTheDead()
        {
            var sim = new SurvivorSim();
            sim.Register(A);
            sim.GrantInvulnerability(A, 5f);
            sim.Kill(A);

            Assert.IsFalse(sim.IsInvulnerable(A));
            Assert.IsFalse(sim.GrantInvulnerability(A, 5f));
            Assert.IsFalse(sim.IsInvulnerable(A));
        }

        [Test]
        public void ProtectionCannotBeGrantedToAnUnknownSurvivorOrForNoTime()
        {
            var sim = new SurvivorSim();
            sim.Register(A);
            Assert.IsFalse(sim.GrantInvulnerability(B, 2f));
            Assert.IsFalse(sim.GrantInvulnerability(A, 0f));
            Assert.IsFalse(sim.IsInvulnerable(A));
        }
    }
}
