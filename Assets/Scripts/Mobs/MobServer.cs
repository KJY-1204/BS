using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BS
{
    /// <summary>
    /// Runs the mob simulation on the server (the host), sends snapshots and events to the other
    /// players, and clears everything when the room ends. The host draws mobs straight from the simulation.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class MobServer : MonoBehaviour
    {
        const int EventsPerMessage = 100;
        const int DumpEveryTicks = 75; // 5 s at 15 Hz
        const float StatsSeconds = 5f;

        public MobSim Sim { get; private set; }

        readonly List<SurvivorInfo> survivors = new List<SurvivorInfo>();
        readonly List<MobEvent> events = new List<MobEvent>();
        readonly List<ulong> remoteClients = new List<ulong>();

        NetworkManager nm;
        bool running;
        byte[] buffer;
        uint tick;
        float snapshotAccumulator;

        // Measurement (dev logging)
        readonly Stopwatch stepWatch = new Stopwatch();
        float statsTimer;
        long bytesSnapshot, bytesEvents, bytesFull;
        double tickMsTotal;
        double tickMsMax;
        int tickSamples;

        void Awake()
        {
            int target = DevArgs.MobTarget > 0 ? Math.Min(DevArgs.MobTarget, MobConfig.HardCap) : MobConfig.DefaultTarget;
            Sim = new MobSim(MobConfig.HardCap, target);
            buffer = new byte[Math.Max(MobCodec.MaxChunkBytes, MobCodec.EventBytes(Math.Max(MobConfig.HardCap, EventsPerMessage))) + 16];
        }

        void Start()
        {
            nm = NetworkManager.Singleton;
            nm.OnServerStarted += HandleServerStarted;
            nm.OnServerStopped += HandleServerStopped;
        }

        void OnDestroy()
        {
            if (nm == null) return;
            nm.OnServerStarted -= HandleServerStarted;
            nm.OnServerStopped -= HandleServerStopped;
            if (running) Unsubscribe();
        }

        void HandleServerStarted()
        {
            running = true;
            Sim.Clear();
            events.Clear();
            tick = 0;
            snapshotAccumulator = 0f;
            ResetStats();
            nm.OnClientConnectedCallback += HandleClientConnected;
            nm.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        void HandleServerStopped(bool wasHost)
        {
            if (running) Unsubscribe();
            running = false;
            Sim.Clear();
            events.Clear();
        }

        void Unsubscribe()
        {
            nm.OnClientConnectedCallback -= HandleClientConnected;
            nm.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        void HandleClientConnected(ulong clientId)
        {
            if (clientId == NetworkManager.ServerClientId) return;
            SendFullState(clientId);
        }

        void HandleClientDisconnected(ulong clientId) => Sim.OnPlayerLeft(clientId);

        void FixedUpdate()
        {
            if (!running) return;

            stepWatch.Restart();

            PlayerRegistry.CollectSurvivors(survivors);
            Sim.Step(Time.fixedDeltaTime, survivors);

            if (DevArgs.MobChurn > 0f) ChurnForTesting();

            events.Clear();
            Sim.DrainEvents(events);

            CollectRemoteClients();
            if (events.Count > 0) SendEvents();

            snapshotAccumulator += Time.fixedDeltaTime;
            float interval = 1f / MobConfig.SnapshotHz;
            if (snapshotAccumulator >= interval)
            {
                snapshotAccumulator -= interval;
                if (snapshotAccumulator > interval) snapshotAccumulator = 0f;
                tick++;
                SendSnapshot();
                if (DevArgs.MobDump && tick % DumpEveryTicks == 0) LogDump();
            }

            stepWatch.Stop();
            RecordTickTime(stepWatch.Elapsed.TotalMilliseconds);
        }

        void Update()
        {
            if (!running || !DevArgs.MobLog) return;

            statsTimer += Time.unscaledDeltaTime;
            if (statsTimer < StatsSeconds) return;

            int clients = remoteClients.Count;
            double perClientKBps = clients > 0 ? (bytesSnapshot + bytesEvents + bytesFull) / (double)clients / statsTimer / 1024.0 : 0.0;
            Debug.Log($"[BS] mobs server: mobs={Sim.Count} target={Sim.TargetCount} clients={clients} " +
                      $"payloadPerClient={perClientKBps:F2}KB/s (snap={bytesSnapshot / statsTimer / 1024.0:F2} evt={bytesEvents / statsTimer / 1024.0:F2} full={bytesFull / statsTimer / 1024.0:F2} KB/s all clients) " +
                      $"tickMs avg={(tickSamples > 0 ? tickMsTotal / tickSamples : 0):F3} max={tickMsMax:F3}");
            ResetStats();
        }

        // ---- Sending ----

        void CollectRemoteClients()
        {
            remoteClients.Clear();
            foreach (ulong id in nm.ConnectedClientsIds)
                if (id != NetworkManager.ServerClientId) remoteClients.Add(id);
        }

        void SendEvents()
        {
            if (remoteClients.Count == 0) return;

            for (int start = 0; start < events.Count; start += EventsPerMessage)
            {
                int count = Math.Min(EventsPerMessage, events.Count - start);
                int length = MobCodec.WriteEvents(buffer, tick, events, start, count);
                Send(MobMessages.Events, length, NetworkDelivery.ReliableSequenced);
                bytesEvents += (long)length * remoteClients.Count;
            }
        }

        void SendSnapshot()
        {
            int total = Sim.Count;
            int chunks = MobCodec.ChunkCountFor(total);
            if (remoteClients.Count == 0) return;

            for (int c = 0; c < chunks; c++)
            {
                int start = c * MobConfig.SnapshotChunkSize;
                int count = Math.Min(MobConfig.SnapshotChunkSize, total - start);
                int length = MobCodec.WriteSnapshotChunk(buffer, tick, c, chunks, Sim, start, count);
                Send(MobMessages.Snapshot, length, NetworkDelivery.UnreliableSequenced);
                bytesSnapshot += (long)length * remoteClients.Count;
            }
        }

        void SendFullState(ulong clientId)
        {
            int length = MobCodec.WriteFullState(buffer, tick, Sim);
            using (var writer = new FastBufferWriter(length, Allocator.Temp))
            {
                writer.WriteBytesSafe(buffer, length);
                nm.CustomMessagingManager.SendNamedMessage(MobMessages.Full, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
            bytesFull += length;
        }

        void Send(string messageName, int length, NetworkDelivery delivery)
        {
            using (var writer = new FastBufferWriter(length, Allocator.Temp))
            {
                writer.WriteBytesSafe(buffer, length);
                nm.CustomMessagingManager.SendNamedMessage(messageName, remoteClients, writer, delivery);
            }
        }

        // ---- Measurement ----

        float churnTimer;

        /// <summary>Dev only: removes a random mob on a timer so removal events can be tested before combat exists.</summary>
        void ChurnForTesting()
        {
            churnTimer += Time.fixedDeltaTime;
            if (churnTimer < DevArgs.MobChurn || Sim.Count == 0) return;

            churnTimer = 0f;
            Sim.Remove(Sim.IdAt(UnityEngine.Random.Range(0, Sim.Count)));
        }

        void ResetStats()
        {
            statsTimer = 0f;
            bytesSnapshot = bytesEvents = bytesFull = 0;
            tickMsTotal = 0;
            tickMsMax = 0;
            tickSamples = 0;
        }

        void RecordTickTime(double ms)
        {
            tickMsTotal += ms;
            if (ms > tickMsMax) tickMsMax = ms;
            tickSamples++;
        }

        /// <summary>Logs the state that was just sent so a client log can be compared with it.</summary>
        void LogDump()
        {
            var order = new List<int>(Sim.Count);
            for (int i = 0; i < Sim.Count; i++) order.Add(i);
            order.Sort((a, b) => Sim.IdAt(a).CompareTo(Sim.IdAt(b)));

            var sb = new StringBuilder(order.Count * 16);
            foreach (int i in order)
            {
                Vector2 p = Sim.PositionAt(i);
                sb.Append(Sim.IdAt(i)).Append('=')
                  .Append(MobCodec.Dequantize(MobCodec.Quantize(p.x)).ToString("F3")).Append(',')
                  .Append(MobCodec.Dequantize(MobCodec.Quantize(p.y)).ToString("F3")).Append(';');
            }
            Debug.Log($"[BS] mobdump server tick={tick} n={Sim.Count} {sb}");
        }
    }
}
