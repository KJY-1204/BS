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
        public SkillEffectReplay SkillEffects { get; } = new SkillEffectReplay();

        /// <summary>Skill effects whose display time has come; the view drains this every frame.</summary>
        public List<SkillEffect> DueSkillEffects { get; } = new List<SkillEffect>();

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
        int fires, hits, kills, ends, skills;

        /// <summary>The shape of the projectiles of whoever stands in a shooter slot, from the room's character table.</summary>
        static ProjectileShape ShapeOfShooterSlot(byte slot)
        {
            if (slot == RoleTable.BossRole) return BossAttack.Shape; // a shot from the boss's slot is a boss shot
            RoleTable table = RoleTable.Instance;
            CharacterDef character = table != null ? CharacterCatalog.Get(table.CharacterOfRole(slot)) : null;
            return character != null ? character.Attack.Shape : ProjectileShape.Default;
        }

        void Awake()
        {
            Instance = this;
            Replay.ShapeOfSlot = ShapeOfShooterSlot;
            mobServer = GetComponent<MobServer>();
            mobClient = GetComponent<MobClient>();
        }

        void Start()
        {
            nm = NetworkManager.Singleton;
            nm.OnClientStarted += HandleClientStarted;
            nm.OnClientStopped += HandleClientStopped;
            nm.OnServerStopped += HandleServerStopped;
            RoleTable.PhaseChanged += OnPhaseChanged;
        }

        /// <summary>A match ended and the room is waiting again: no shot or skill effect of it stays on screen.</summary>
        void OnPhaseChanged(RoomPhase phase)
        {
            if (phase != RoomPhase.Waiting) return;
            Replay.Clear();
            SkillEffects.Clear();
            DueEffects.Clear();
            DueSkillEffects.Clear();
        }

        void OnDestroy()
        {
            RoleTable.PhaseChanged -= OnPhaseChanged;
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
            SkillEffects.Clear();
            ResetStats();
            if (nm.IsServer) return; // the host is fed by the server directly

            nm.CustomMessagingManager.RegisterNamedMessageHandler(CombatMessages.Events, OnEvents);
            registered = true;
        }

        void HandleClientStopped(bool wasHost)
        {
            Unregister();
            Replay.Clear();
            SkillEffects.Clear();
            DueEffects.Clear();
            DueSkillEffects.Clear();
        }

        void HandleServerStopped(bool wasHost)
        {
            Replay.Clear();
            SkillEffects.Clear();
            DueEffects.Clear();
            DueSkillEffects.Clear();
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
            SkillEffects.Advance(displayTime);
            SkillEffects.DrainDue(DueSkillEffects);

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
            {
                int dumpId = e.Kind == CombatEventKind.Skill ? e.SkillId : e.ProjectileId;
                Debug.Log($"[BS] combatdump client t={Mathf.RoundToInt(serverTime * 1000f)} {e.Kind} id={dumpId} mob={e.MobId} kill={(e.Killed ? 1 : 0)} cont={(e.Continues ? 1 : 0)} angle={e.Angle} surv={(e.SurvivorTarget ? 1 : 0)}");
            }

            switch (e.Kind)
            {
                case CombatEventKind.Fire:
                    fires++;
                    Replay.OnFire(e.ProjectileId, e.Slot, e.Position, e.Angle, serverTime);
                    break;
                case CombatEventKind.Hit:
                    hits++;
                    if (e.Killed) kills++;
                    Replay.OnHit(e.ProjectileId, e.MobId, e.MobHealthPercent, e.Killed, e.Position, serverTime, e.Continues, e.SurvivorTarget);
                    break;
                case CombatEventKind.Skill:
                    skills++;
                    SkillEffects.OnSkill(new SkillEffect
                    {
                        SkillId = e.SkillId, Slot = e.Slot, Position = e.Position, Rank = e.Rank, Angle = e.Angle, ServerTime = serverTime,
                    });
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

            Debug.Log($"[BS] combat client: recv={bytesReceived / statsTimer / 1024.0:F2}KB/s fires={fires} hits={hits} kills={kills} ends={ends} skills={skills} " +
                      $"projectilesOnScreen={Replay.Count} tracked={Replay.TrackedCount}");
            ResetStats();
        }

        void ResetStats()
        {
            statsTimer = 0f;
            bytesReceived = 0;
            fires = hits = kills = ends = skills = 0;
        }
    }
}
