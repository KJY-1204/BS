using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace BS
{
    /// <summary>
    /// Receives mob messages on a client (not the host) and feeds the interpolator that the view draws from.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class MobClient : MonoBehaviour
    {
        const int DumpEveryTicks = 75;
        const float StatsSeconds = 5f;

        public MobInterpolator Interpolator { get; } = new MobInterpolator();

        NetworkManager nm;
        bool registered;
        byte[] scratch = new byte[4096];

        // Measurement (dev logging)
        float statsTimer;
        long bytesReceived;
        int snapshotsReceived;
        int framesCounted;
        float frameSecondsTotal;
        float frameSecondsMax;
        uint latestTick;
        uint nextDumpTick = DumpEveryTicks;
        float startedAt;
        bool dumpInitialized;

        void Start()
        {
            nm = NetworkManager.Singleton;
            nm.OnClientStarted += HandleClientStarted;
            nm.OnClientStopped += HandleClientStopped;
        }

        void OnDestroy()
        {
            if (nm == null) return;
            nm.OnClientStarted -= HandleClientStarted;
            nm.OnClientStopped -= HandleClientStopped;
            Unregister();
        }

        void HandleClientStarted()
        {
            // The host reads the simulation directly and does not need these messages.
            if (nm.IsServer) return;

            Interpolator.Clear();
            latestTick = 0;
            nextDumpTick = DumpEveryTicks;
            dumpInitialized = false;
            ResetStats();

            nm.CustomMessagingManager.RegisterNamedMessageHandler(MobMessages.Snapshot, OnSnapshot);
            nm.CustomMessagingManager.RegisterNamedMessageHandler(MobMessages.Events, OnEvents);
            nm.CustomMessagingManager.RegisterNamedMessageHandler(MobMessages.Full, OnFull);
            registered = true;
            startedAt = Time.realtimeSinceStartup;
        }

        void HandleClientStopped(bool wasHost)
        {
            if (DevArgs.MobLog && registered)
                Debug.Log($"[BS] mobs client: room ended, clearing {Interpolator.Count} mobs");

            Unregister();
            Interpolator.Clear();

            if (DevArgs.MobLog)
                Debug.Log($"[BS] mobs client: after room end mobs={Interpolator.Count}");
        }

        void Unregister()
        {
            if (!registered) return;
            registered = false;

            var messaging = nm != null ? nm.CustomMessagingManager : null;
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(MobMessages.Snapshot);
            messaging.UnregisterNamedMessageHandler(MobMessages.Events);
            messaging.UnregisterNamedMessageHandler(MobMessages.Full);
        }

        void Update()
        {
            if (!registered) return;

            Interpolator.Advance(Time.deltaTime);

            if (DevArgs.MobDump) MaybeLogDump();
            if (DevArgs.MobLog) UpdateStats();
        }

        // ---- Receiving ----

        bool ReadPayload(FastBufferReader reader, out int length)
        {
            // Length covers the whole buffer including what NGO already read; only the rest is our payload.
            length = reader.Length - reader.Position;
            if (length > scratch.Length)
            {
                Debug.LogWarning($"[BS] mob message of {length} bytes ignored (buffer is {scratch.Length})");
                return false;
            }
            reader.ReadBytesSafe(ref scratch, length);
            bytesReceived += length;
            return true;
        }

        void OnSnapshot(ulong senderClientId, FastBufferReader reader)
        {
            if (!ReadPayload(reader, out int length)) return;
            if (!MobCodec.TryReadSnapshotHeader(scratch, length, out uint tick, out _, out _, out int count)) return;

            snapshotsReceived++;
            if (tick > latestTick) latestTick = tick;

            if (!dumpInitialized)
            {
                // Only dump ticks that happen after joining, so a late joiner's lines compare with the server's.
                dumpInitialized = true;
                nextDumpTick = (tick / DumpEveryTicks + 1) * DumpEveryTicks;
            }

            float serverTime = MobMessages.ServerTime(tick);
            for (int i = 0; i < count; i++)
            {
                MobCodec.ReadSnapshotEntry(scratch, i, out ushort id, out Vector2 position, out _);
                Interpolator.OnSnapshotEntry(id, position, serverTime);
            }
        }

        void OnEvents(ulong senderClientId, FastBufferReader reader) => ApplyEvents(reader, isFullState: false);

        void OnFull(ulong senderClientId, FastBufferReader reader) => ApplyEvents(reader, isFullState: true);

        void ApplyEvents(FastBufferReader reader, bool isFullState)
        {
            if (!isFullState && DevArgs.MobDropEvents) return; // dev only: pretend spawn/remove events were lost
            if (!ReadPayload(reader, out int length)) return;
            if (!MobCodec.TryReadEventsHeader(scratch, length, out uint tick, out int count)) return;

            float serverTime = MobMessages.ServerTime(tick);
            for (int i = 0; i < count; i++)
            {
                MobEvent e = MobCodec.ReadEvent(scratch, i);
                if (e.IsSpawn) Interpolator.OnSpawn(e.Id, e.Type, e.Position, serverTime);
                else Interpolator.OnRemove(e.Id);
            }

            if (isFullState && DevArgs.MobLog)
                Debug.Log($"[BS] mobs client: full state received n={count} displayed={Interpolator.Count} " +
                          $"{Time.realtimeSinceStartup - startedAt:F2}s after the client started");
        }

        // ---- Measurement ----

        void UpdateStats()
        {
            float dt = Time.unscaledDeltaTime;
            statsTimer += dt;
            framesCounted++;
            frameSecondsTotal += dt;
            if (dt > frameSecondsMax) frameSecondsMax = dt;

            if (statsTimer < StatsSeconds) return;

            Debug.Log($"[BS] mobs client: mobs={Interpolator.Count} recv={bytesReceived / statsTimer / 1024.0:F2}KB/s " +
                      $"snapshots/s={snapshotsReceived / statsTimer:F1} staleRemovals={Interpolator.StaleRemovals} " +
                      $"provisional={Interpolator.ProvisionalCreations} " +
                      $"frameMs avg={frameSecondsTotal / framesCounted * 1000f:F2} max={frameSecondsMax * 1000f:F2}");
            ResetStats();
        }

        void ResetStats()
        {
            statsTimer = 0f;
            bytesReceived = 0;
            snapshotsReceived = 0;
            framesCounted = 0;
            frameSecondsTotal = 0f;
            frameSecondsMax = 0f;
        }

        /// <summary>
        /// When the display clock reaches a dump tick, logs what is on screen. The server logs its state at
        /// that same tick, so the two lines show how closely the display follows the server.
        /// </summary>
        void MaybeLogDump()
        {
            if (!dumpInitialized) return;
            if (Interpolator.Count == 0 && latestTick < nextDumpTick) return;
            if (Interpolator.RenderTime * MobConfig.SnapshotHz < nextDumpTick) return;

            var order = new System.Collections.Generic.List<int>(Interpolator.Count);
            for (int i = 0; i < Interpolator.Count; i++) order.Add(i);
            order.Sort((a, b) => Interpolator.IdAt(a).CompareTo(Interpolator.IdAt(b)));

            var sb = new StringBuilder(order.Count * 16);
            foreach (int i in order)
            {
                Vector2 p = Interpolator.PositionAt(i);
                sb.Append(Interpolator.IdAt(i)).Append('=').Append(p.x.ToString("F3")).Append(',').Append(p.y.ToString("F3")).Append(';');
            }
            Debug.Log($"[BS] mobdump client tick={nextDumpTick} n={Interpolator.Count} {sb}");
            nextDumpTick += DumpEveryTicks;
        }
    }
}
