using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BS
{
    /// <summary>
    /// Receives combat events on a client and feeds the projectile replay. On the host the server feeds the
    /// same replay directly. The display clock is the one mobs use, so shots and mobs stay in step.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class CombatClient : MonoBehaviour
    {
        const float StatsSeconds = 5f;

        public static CombatClient Instance { get; private set; }

        public ProjectileReplay Replay { get; } = new ProjectileReplay();

        /// <summary>Hits that have become due on the display clock; the view drains this every frame.</summary>
        public List<HitEffect> DueEffects { get; } = new List<HitEffect>();

        MobServer mobServer;
        MobClient mobClient;
        NetworkManager nm;
        bool registered;
        byte[] scratch = new byte[CombatCodec.MaxBatchBytes + 64];

        // Measurement (dev logging)
        float statsTimer;
        long bytesReceived;
        int fires, hits, kills, ends;

        void Awake()
        {
            Instance = this;
            mobServer = GetComponent<MobServer>();
            mobClient = GetComponent<MobClient>();
        }

        void Start()
        {
            nm = NetworkManager.Singleton;
            nm.OnClientStarted += HandleClientStarted;
            nm.OnClientStopped += HandleClientStopped;
            nm.OnServerStopped += HandleServerStopped;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (nm == null) return;
            nm.OnClientStarted -= HandleClientStarted;
            nm.OnClientStopped -= HandleClientStopped;
            nm.OnServerStopped -= HandleServerStopped;
            Unregister();
        }

        void HandleClientStarted()
        {
            Replay.Clear();
            ResetStats();
            if (nm.IsServer) return; // the host is fed by the server directly

            nm.CustomMessagingManager.RegisterNamedMessageHandler(CombatMessages.Events, OnEvents);
            registered = true;
        }

        void HandleClientStopped(bool wasHost)
        {
            Unregister();
            Replay.Clear();
            DueEffects.Clear();
        }

        void HandleServerStopped(bool wasHost)
        {
            Replay.Clear();
            DueEffects.Clear();
        }

        void Unregister()
        {
            if (!registered) return;
            registered = false;

            var messaging = nm != null ? nm.CustomMessagingManager : null;
            if (messaging != null) messaging.UnregisterNamedMessageHandler(CombatMessages.Events);
        }

        /// <summary>Host only: shows events the server just produced, at the server's own time.</summary>
        public void ApplyLocal(IReadOnlyList<CombatEvent> events, float serverTime, System.Func<ulong, byte> slotOf)
        {
            for (int i = 0; i < events.Count; i++)
            {
                CombatEvent e = events[i];
                e.Slot = slotOf != null ? slotOf(e.Owner) : e.Slot;
                Apply(e, serverTime);
            }
        }

        void Update()
        {
            bool host = nm != null && nm.IsServer && nm.IsListening;
            if (!host && !registered) return;

            float displayTime;
            if (host)
            {
                if (mobServer == null) return;
                displayTime = MobMessages.ServerTimeFromMs(mobServer.ServerTimeMs);
            }
            else
            {
                displayTime = mobClient != null ? mobClient.Interpolator.RenderTime : 0f;
            }

            Replay.Advance(displayTime);
            Replay.DrainDueEffects(DueEffects);

            if (DevArgs.CombatLog && registered) UpdateStats();
        }

        // ---- Receiving ----

        void OnEvents(ulong senderClientId, FastBufferReader reader)
        {
            int length = reader.Length - reader.Position;
            if (length > scratch.Length)
            {
                Debug.LogWarning($"[BS] combat message of {length} bytes ignored");
                return;
            }
            reader.ReadBytesSafe(ref scratch, length);
            bytesReceived += length;

            if (!CombatCodec.TryReadHeader(scratch, length, out uint timeMs, out int count)) return;

            float serverTime = MobMessages.ServerTimeFromMs(timeMs);
            int offset = CombatCodec.HeaderSize;
            for (int i = 0; i < count; i++)
            {
                if (!CombatCodec.TryReadEvent(scratch, length, ref offset, out CombatEvent e)) break;
                Apply(e, serverTime);
            }
        }

        void Apply(CombatEvent e, float serverTime)
        {
            if (DevArgs.CombatDump && registered)
                Debug.Log($"[BS] combatdump client t={Mathf.RoundToInt(serverTime * 1000f)} {e.Kind} id={e.ProjectileId} mob={e.MobId} kill={(e.Killed ? 1 : 0)}");

            switch (e.Kind)
            {
                case CombatEventKind.Fire:
                    fires++;
                    Replay.OnFire(e.ProjectileId, e.Slot, e.Position, e.Angle, serverTime);
                    break;
                case CombatEventKind.Hit:
                    hits++;
                    if (e.Killed) kills++;
                    Replay.OnHit(e.ProjectileId, e.MobId, e.MobHealthPercent, e.Killed, e.Position, serverTime);
                    break;
                default:
                    ends++;
                    Replay.OnEnd(e.ProjectileId, e.Position, serverTime);
                    break;
            }
        }

        // ---- Measurement ----

        void UpdateStats()
        {
            statsTimer += Time.unscaledDeltaTime;
            if (statsTimer < StatsSeconds) return;

            Debug.Log($"[BS] combat client: recv={bytesReceived / statsTimer / 1024.0:F2}KB/s fires={fires} hits={hits} kills={kills} ends={ends} " +
                      $"projectilesOnScreen={Replay.Count} tracked={Replay.TrackedCount}");
            ResetStats();
        }

        void ResetStats()
        {
            statsTimer = 0f;
            bytesReceived = 0;
            fires = hits = kills = ends = 0;
        }
    }
}
