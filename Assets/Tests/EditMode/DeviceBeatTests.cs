using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    /// <summary>A standing device tells every screen when it is getting ready and when it made mobs, so its animation matches the simulation.</summary>
    public class DeviceBeatTests
    {
        const float Dt = 0.02f;
        static readonly byte DeviceType = MobKinds.Encode(MobKinds.OriginSummoned, MobKinds.Device);

        static MobSim NewSim(float interval = 4f, float ready = 0.9f)
        {
            return new MobSim(hardCap: 100, targetCount: 0)
            {
                DeviceSettings = new DeviceConfig { ProductionIntervalSeconds = interval, LifetimeSeconds = 100f, MobsPerBatch = 1, ReadySeconds = ready },
            };
        }

        static List<SurvivorInfo> Far() =>
            new List<SurvivorInfo> { new SurvivorInfo { ClientId = 1, Position = new Vector2(-12, -8), Alive = true } };

        static void Run(MobSim sim, float seconds)
        {
            var far = Far();
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                sim.Step(Dt, far);
                sim.DrainActions(new List<MobShotRequest>(), new List<MobBlast>());
            }
        }

        [Test]
        public void ADeviceGetsReadyBeforeItMakesMobsAndThenMakesThem()
        {
            MobSim sim = NewSim();
            Assert.IsTrue(sim.TrySpawn(Vector2.zero, DeviceType, out ushort id));

            Run(sim, 3.0f); // short of the ready moment (4 - 0.9 = 3.1)
            var beats = new List<MobEvent>();
            sim.DrainDeviceBeats(beats);
            Assert.AreEqual(0, beats.Count);

            Run(sim, 0.3f); // 3.3 s: ready has been announced
            sim.DrainDeviceBeats(beats);
            Assert.AreEqual(1, beats.Count);
            Assert.AreEqual(MobEvent.DeviceReady, beats[0].Device);
            Assert.AreEqual(id, beats[0].Id);

            beats.Clear();
            Run(sim, 0.9f); // past 4 s: it made mobs
            sim.DrainDeviceBeats(beats);
            Assert.AreEqual(1, beats.Count);
            Assert.AreEqual(MobEvent.DeviceProduce, beats[0].Device);
        }

        [Test]
        public void EveryProductionIsPrecededByOneReady()
        {
            MobSim sim = NewSim();
            sim.TrySpawn(Vector2.zero, DeviceType, out _);
            Run(sim, 8.5f);

            var beats = new List<MobEvent>();
            sim.DrainDeviceBeats(beats);
            var kinds = new List<byte>();
            foreach (MobEvent beat in beats) kinds.Add(beat.Device);
            CollectionAssert.AreEqual(new[] { MobEvent.DeviceReady, MobEvent.DeviceProduce, MobEvent.DeviceReady, MobEvent.DeviceProduce }, kinds);
        }

        [Test]
        public void BeatsForTheNetworkAreKeptApartFromBeatsForTheLocalView()
        {
            MobSim sim = NewSim();
            sim.TrySpawn(Vector2.zero, DeviceType, out _);
            Run(sim, 4.2f);

            var view = new List<MobEvent>();
            sim.DrainDeviceBeats(view);
            var network = new List<MobEvent>();
            sim.DrainNetworkBeats(network);
            Assert.AreEqual(2, view.Count);
            Assert.AreEqual(2, network.Count);

            // Spawns and removals still come from DrainEvents alone: a beat is no spawn event.
            var spawns = new List<MobEvent>();
            sim.DrainEvents(spawns);
            foreach (MobEvent e in spawns) Assert.AreEqual(0, e.Device);
        }

        [Test]
        public void ABeatSurvivesTheWire()
        {
            var buffer = new byte[MobCodec.EventBytes(2)];
            var events = new List<MobEvent>
            {
                new MobEvent { Device = MobEvent.DeviceReady, Id = 7, Type = DeviceType, Position = new Vector2(1.5f, -2f) },
                new MobEvent { Device = MobEvent.DeviceProduce, Id = 7, Type = DeviceType, Position = new Vector2(1.5f, -2f) },
            };
            int length = MobCodec.WriteEvents(buffer, 1234, events, 0, 2);

            Assert.IsTrue(MobCodec.TryReadEventsHeader(buffer, length, out uint timeMs, out int count));
            Assert.AreEqual(1234u, timeMs);
            Assert.AreEqual(2, count);

            MobEvent ready = MobCodec.ReadEvent(buffer, 0);
            MobEvent produce = MobCodec.ReadEvent(buffer, 1);
            Assert.AreEqual(MobEvent.DeviceReady, ready.Device);
            Assert.AreEqual(MobEvent.DeviceProduce, produce.Device);
            Assert.IsFalse(ready.IsSpawn);
            Assert.AreEqual(7, produce.Id);
        }

        [Test]
        public void AScreenShowsABeatOnlyWhenItsDisplayClockReachesIt()
        {
            var interpolator = new MobInterpolator();
            interpolator.OnSnapshotTime(1f);
            interpolator.OnDeviceBeat(new MobEvent { Device = MobEvent.DeviceProduce, Id = 3, Type = DeviceType }, 5f);

            var shown = new List<MobEvent>();
            interpolator.DrainDeviceBeats(shown);
            Assert.AreEqual(0, shown.Count, "the beat is in the future of the display clock");

            interpolator.OnSnapshotTime(10f);
            for (int i = 0; i < 400; i++) interpolator.Advance(0.02f);
            interpolator.DrainDeviceBeats(shown);
            Assert.AreEqual(1, shown.Count);
            Assert.AreEqual(3, shown[0].Id);

            shown.Clear();
            interpolator.DrainDeviceBeats(shown);
            Assert.AreEqual(0, shown.Count, "a beat is shown once");
        }
    }
}
