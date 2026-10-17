using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class MobSimTests
    {
        const float Dt = 0.02f;

        static SurvivorInfo Survivor(ulong id, float x, float y, bool alive = true) =>
            new SurvivorInfo { ClientId = id, Position = new Vector2(x, y), Alive = alive };

        static void Run(MobSim sim, List<SurvivorInfo> survivors, int ticks)
        {
            for (int i = 0; i < ticks; i++) sim.Step(Dt, survivors);
        }

        static ushort Spawn(MobSim sim, float x, float y)
        {
            Assert.IsTrue(sim.TrySpawn(new Vector2(x, y), 0, out ushort id));
            return id;
        }

        static Vector2 PositionOf(MobSim sim, ushort id)
        {
            for (int i = 0; i < sim.Count; i++)
                if (sim.IdAt(i) == id) return sim.PositionAt(i);
            Assert.Fail($"mob {id} not found");
            return default;
        }

        // ---- 1.1 cap and ids ----

        [Test]
        public void Spawn_IsRefusedAtHardCap()
        {
            var sim = new MobSim(hardCap: 5, targetCount: 5);
            for (int i = 0; i < 5; i++) Spawn(sim, i, 0);

            Assert.IsFalse(sim.TrySpawn(Vector2.zero, 0, out ushort id));
            Assert.AreEqual(0, id);
            Assert.AreEqual(5, sim.Count);
        }

        [Test]
        public void Ids_AreNotReused()
        {
            var sim = new MobSim();
            ushort first = Spawn(sim, 0, 0);
            Assert.IsTrue(sim.Remove(first));
            ushort second = Spawn(sim, 0, 0);

            Assert.AreNotEqual(first, second);
            Assert.AreNotEqual(0, first);
        }

        [Test]
        public void TargetCount_IsNeverAboveHardCap()
        {
            var sim = new MobSim(hardCap: 10, targetCount: 500);
            Assert.AreEqual(10, sim.TargetCount);
        }

        [Test]
        public void Remove_UnknownIdReturnsFalse()
        {
            var sim = new MobSim();
            Assert.IsFalse(sim.Remove(42));
        }

        [Test]
        public void Events_ReportSpawnAndRemove()
        {
            var sim = new MobSim();
            ushort id = Spawn(sim, 1, 2);
            sim.Remove(id);

            var events = new List<MobEvent>();
            sim.DrainEvents(events);

            Assert.AreEqual(2, events.Count);
            Assert.IsTrue(events[0].IsSpawn);
            Assert.AreEqual(id, events[0].Id);
            Assert.IsFalse(events[1].IsSpawn);

            events.Clear();
            sim.DrainEvents(events);
            Assert.AreEqual(0, events.Count);
        }

        // ---- 1.2 automatic spawning ----

        [Test]
        public void AutoSpawn_StopsAtTarget()
        {
            var sim = new MobSim(hardCap: 100, targetCount: 10);
            var survivors = new List<SurvivorInfo> { Survivor(1, 0, 0) };

            Run(sim, survivors, 50 * 20);

            Assert.AreEqual(10, sim.Count);
        }

        [Test]
        public void AutoSpawn_ResumesWhenBelowTarget()
        {
            var sim = new MobSim(hardCap: 100, targetCount: 10);
            var survivors = new List<SurvivorInfo> { Survivor(1, 0, 0) };
            Run(sim, survivors, 50 * 20);

            sim.Remove(sim.IdAt(0));
            Assert.AreEqual(9, sim.Count);
            Run(sim, survivors, 50 * 5);

            Assert.AreEqual(10, sim.Count);
        }

        [Test]
        public void AutoSpawn_DoesNothingWithoutSurvivors()
        {
            var sim = new MobSim();
            Run(sim, new List<SurvivorInfo>(), 50 * 10);
            Assert.AreEqual(0, sim.Count);
        }

        [Test]
        public void AutoSpawn_DoesNothingWhenAllSurvivorsAreDead()
        {
            var sim = new MobSim();
            Run(sim, new List<SurvivorInfo> { Survivor(1, 0, 0, alive: false) }, 50 * 10);
            Assert.AreEqual(0, sim.Count);
        }

        [Test]
        public void AutoSpawn_UsesTheArenaEdgeAwayFromSurvivors()
        {
            var sim = new MobSim(hardCap: 100, targetCount: 60);
            var survivors = new List<SurvivorInfo> { Survivor(1, 0, 0) };
            var spawned = new List<MobEvent>();

            for (int i = 0; i < 50 * 40; i++)
            {
                sim.Step(Dt, survivors);
                sim.DrainEvents(spawned);
            }

            Assert.Greater(spawned.Count, 0);
            float hx = ArenaConfig.Width * 0.5f - MobConfig.Radius;
            float hy = ArenaConfig.Height * 0.5f - MobConfig.Radius;
            foreach (MobEvent e in spawned)
            {
                bool onEdge = Mathf.Approximately(Mathf.Abs(e.Position.x), hx) || Mathf.Approximately(Mathf.Abs(e.Position.y), hy);
                Assert.IsTrue(onEdge, $"spawn at {e.Position} is not on the edge");
                Assert.GreaterOrEqual(e.Position.magnitude, MobConfig.SpawnMinSurvivorDistance);
            }
        }

        // ---- 1.3 chase ----

        [Test]
        public void Chase_MovesTowardTheOnlySurvivor()
        {
            var sim = new MobSim(targetCount: 0);
            ushort id = Spawn(sim, 10, 0);
            var survivors = new List<SurvivorInfo> { Survivor(1, -5, 0) };

            Run(sim, survivors, 50);

            Vector2 p = PositionOf(sim, id);
            Assert.AreEqual(10f - MobConfig.Speed, p.x, 0.05f);
            Assert.AreEqual(0f, p.y, 0.01f);
        }

        [Test]
        public void Chase_SwitchesToACloserSurvivor()
        {
            var sim = new MobSim(targetCount: 0);
            ushort id = Spawn(sim, 0, 0);
            var survivors = new List<SurvivorInfo> { Survivor(1, -10, 0), Survivor(2, 12, 0) };

            Run(sim, survivors, 25);
            Assert.Less(PositionOf(sim, id).x, 0f, "starts toward the nearer survivor on the left");

            survivors[1] = Survivor(2, 3, 0);
            Run(sim, survivors, 50);
            Assert.Greater(PositionOf(sim, id).x, 0f, "switches to the survivor that became closer");
        }

        [Test]
        public void Chase_IgnoresDeadSurvivors()
        {
            var sim = new MobSim(targetCount: 0);
            ushort id = Spawn(sim, 0, 0);
            var survivors = new List<SurvivorInfo> { Survivor(1, -3, 0, alive: false), Survivor(2, 8, 0) };

            Run(sim, survivors, 25);

            Assert.Greater(PositionOf(sim, id).x, 0f);
        }

        [Test]
        public void Chase_WithoutSurvivorsLeavesMobsInPlace()
        {
            var sim = new MobSim(targetCount: 0);
            ushort id = Spawn(sim, 4, 4);

            Run(sim, new List<SurvivorInfo>(), 50);

            Assert.AreEqual(new Vector2(4, 4), PositionOf(sim, id));
        }

        // ---- 1.4 separation and bounds ----

        [Test]
        public void Separation_SpreadsMobsChasingTheSameSurvivor()
        {
            var sim = new MobSim(targetCount: 0);
            for (int i = 0; i < 20; i++) Spawn(sim, 0, 0);
            var survivors = new List<SurvivorInfo> { Survivor(1, 10, 0) };

            Run(sim, survivors, 50 * 6);

            float closest = float.MaxValue;
            for (int i = 0; i < sim.Count; i++)
                for (int j = i + 1; j < sim.Count; j++)
                    closest = Mathf.Min(closest, Vector2.Distance(sim.PositionAt(i), sim.PositionAt(j)));

            Assert.Greater(closest, 0.3f, "no two mobs may sit on the same spot");
        }

        [Test]
        public void Mobs_StayInsideTheArenaNextToACornerSurvivor()
        {
            var sim = new MobSim(hardCap: 100, targetCount: 100);
            var survivors = new List<SurvivorInfo> { Survivor(1, ArenaConfig.Width * 0.5f, ArenaConfig.Height * 0.5f) };

            Run(sim, survivors, 50 * 60);

            float hx = ArenaConfig.Width * 0.5f - MobConfig.Radius + 1e-3f;
            float hy = ArenaConfig.Height * 0.5f - MobConfig.Radius + 1e-3f;
            Assert.Greater(sim.Count, 0);
            for (int i = 0; i < sim.Count; i++)
            {
                Vector2 p = sim.PositionAt(i);
                Assert.LessOrEqual(Mathf.Abs(p.x), hx);
                Assert.LessOrEqual(Mathf.Abs(p.y), hy);
            }
        }

        [Test]
        public void Spawn_OutsideTheArenaIsClamped()
        {
            var sim = new MobSim();
            ushort id = Spawn(sim, 500, -500);

            Vector2 p = PositionOf(sim, id);
            Assert.LessOrEqual(Mathf.Abs(p.x), ArenaConfig.Width * 0.5f);
            Assert.LessOrEqual(Mathf.Abs(p.y), ArenaConfig.Height * 0.5f);
        }

        // ---- 1.5 target override ----

        [Test]
        public void Override_ToAPlayerBeatsTheNearestSurvivor()
        {
            var sim = new MobSim(targetCount: 0);
            ushort id = Spawn(sim, 0, 0);
            var survivors = new List<SurvivorInfo> { Survivor(1, 3, 0), Survivor(2, -9, 0) };

            Assert.IsTrue(sim.SetTarget(id, 2));
            Run(sim, survivors, 25);

            Assert.Less(PositionOf(sim, id).x, 0f);
        }

        [Test]
        public void Override_ClearedReturnsToTheNearestSurvivor()
        {
            var sim = new MobSim(targetCount: 0);
            ushort id = Spawn(sim, 0, 0);
            var survivors = new List<SurvivorInfo> { Survivor(1, 6, 0), Survivor(2, -9, 0) };
            sim.SetTarget(id, 2);
            Run(sim, survivors, 25);
            float afterOverride = PositionOf(sim, id).x;
            Assert.Less(afterOverride, 0f);

            Assert.IsTrue(sim.ClearTarget(id));
            Run(sim, survivors, 50);

            Assert.Greater(PositionOf(sim, id).x, afterOverride);
        }

        [Test]
        public void Override_ToAPositionMovesThere()
        {
            var sim = new MobSim(targetCount: 0);
            ushort id = Spawn(sim, 0, 0);
            var survivors = new List<SurvivorInfo> { Survivor(1, 8, 0) };

            Assert.IsTrue(sim.SetTargetPosition(id, new Vector2(0, 8)));
            Run(sim, survivors, 25);

            Vector2 p = PositionOf(sim, id);
            Assert.Greater(p.y, 1f);
            Assert.AreEqual(0f, p.x, 0.05f);
        }

        [Test]
        public void Override_IsClearedWhenThePlayerLeaves()
        {
            var sim = new MobSim(targetCount: 0);
            ushort id = Spawn(sim, 0, 0);
            sim.SetTarget(id, 2);

            sim.OnPlayerLeft(2);
            var survivors = new List<SurvivorInfo> { Survivor(1, 6, 0) };
            Run(sim, survivors, 25);

            Assert.Greater(PositionOf(sim, id).x, 0f, "mob goes back to chasing the remaining survivor");
        }

        [Test]
        public void Override_UnknownMobReturnsFalse()
        {
            var sim = new MobSim();
            Assert.IsFalse(sim.SetTarget(99, 1));
            Assert.IsFalse(sim.SetTargetPosition(99, Vector2.zero));
            Assert.IsFalse(sim.ClearTarget(99));
        }

        [Test]
        public void Clear_RemovesEveryMobWithoutEvents()
        {
            var sim = new MobSim();
            Spawn(sim, 1, 1);
            Spawn(sim, 2, 2);

            sim.Clear();

            var events = new List<MobEvent>();
            sim.DrainEvents(events);
            Assert.AreEqual(0, sim.Count);
            Assert.AreEqual(0, events.Count);
        }
    }
}
