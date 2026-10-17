using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BS
{
    /// <summary>
    /// The boss player's summoning, as seen from its character: the owner reads the summon and order input and sends it to the
    /// server, and the server's energy and standing order come back as values only the owner receives. Survivors' copies of this
    /// component do nothing.
    /// </summary>
    public class PlayerSummon : NetworkBehaviour
    {
        const float MinSendInterval = 0.1f;

        readonly NetworkVariable<ushort> energy = new NetworkVariable<ushort>(
            0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<ushort> maxEnergy = new NetworkVariable<ushort>(
            0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<byte> orderKind = new NetworkVariable<byte>(
            0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<ulong> orderClient = new NetworkVariable<ulong>(
            0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<Vector2> orderPoint = new NetworkVariable<Vector2>(
            Vector2.zero, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

        /// <summary>The boss player's own copy on this machine (null for everyone else).</summary>
        public static PlayerSummon Local { get; private set; }

        PlayerAvatar avatar;
        float lastSendTime = float.NegativeInfinity;
        float spawnedAt;
        float nextAutoSummon;
        bool autoOrderPlayerDone;
        bool autoOrderPointDone;
        bool autoClearDone;

        string notice = "";
        float noticeUntil;

        public float Energy => energy.Value;
        public float MaxEnergy => maxEnergy.Value;
        public OrderKind Order => (OrderKind)orderKind.Value;
        public ulong OrderClient => orderClient.Value;
        public Vector2 OrderPoint => orderPoint.Value;
        public string Notice => Time.unscaledTime < noticeUntil ? notice : "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Local = null;

        void Awake()
        {
            avatar = GetComponent<PlayerAvatar>();
        }

        public override void OnNetworkSpawn()
        {
            spawnedAt = Time.unscaledTime;
            nextAutoSummon = DevArgs.AutoSummonSeconds;
            if (IsOwner) Local = this;
        }

        public override void OnNetworkDespawn()
        {
            if (Local == this) Local = null;
        }

        // ---- Server side ----

        /// <summary>Server only: copies the simulation's values to the owner's replicated variables when they change.</summary>
        public void ServerWrite(float energyValue, float maxValue, OrderKind kind, ulong client, Vector2 point)
        {
            ushort e = (ushort)Mathf.Clamp(Mathf.FloorToInt(energyValue), 0, ushort.MaxValue);
            ushort m = (ushort)Mathf.Clamp(Mathf.CeilToInt(maxValue), 0, ushort.MaxValue);
            if (maxEnergy.Value != m) maxEnergy.Value = m;
            if (energy.Value != e) energy.Value = e;
            if (orderKind.Value != (byte)kind) orderKind.Value = (byte)kind;
            if (orderClient.Value != client) orderClient.Value = client;
            if (orderPoint.Value != point) orderPoint.Value = point;
        }

        /// <summary>Server only: tells the boss player a summon did not happen (or only partly).</summary>
        public void ServerSendResult(SummonResult result) => ResultRpc((byte)result);

        [Rpc(SendTo.Server)]
        void SummonRpc()
        {
            SummonServer.Instance?.OnSummonRequest(OwnerClientId);
        }

        [Rpc(SendTo.Server)]
        void OrderRpc(Vector2 pointer)
        {
            SummonServer.Instance?.OnOrderRequest(OwnerClientId, pointer);
        }

        [Rpc(SendTo.Server)]
        void ClearOrderRpc()
        {
            SummonServer.Instance?.OnClearRequest(OwnerClientId);
        }

        // ---- Owner side ----

        [Rpc(SendTo.Owner)]
        void ResultRpc(byte result)
        {
            switch ((SummonResult)result)
            {
                case SummonResult.NoEnergy: Say("소환 기운이 부족합니다"); break;
                case SummonResult.Cooldown: Say("아직 소환할 수 없습니다"); break;
                case SummonResult.FieldFull: Say("몹이 가득 찼습니다"); break;
                case SummonResult.NoRoom: Say("소환할 자리가 없습니다"); break;
                case SummonResult.Partial: Say("일부만 소환했습니다"); break;
            }
            if (DevArgs.MobLog) Debug.Log($"[BS] summon result (boss): {(SummonResult)result}");
        }

        void Say(string text)
        {
            notice = text;
            noticeUntil = Time.unscaledTime + 1.5f;
        }

        float seenTimer;
        bool tryDone;

        void Update()
        {
            // Dev log on every machine: what this machine knows of the boss's summon state. Only the boss player receives real values.
            if (DevArgs.MobLog && IsSpawned && avatar != null && avatar.Role == RoleTable.BossRole && RoleTable.MatchRunning)
            {
                seenTimer += Time.unscaledDeltaTime;
                if (seenTimer >= 5f)
                {
                    seenTimer = 0f;
                    Debug.Log($"[BS] summon seen: isOwner={IsOwner} energy={energy.Value}/{maxEnergy.Value} order={(OrderKind)orderKind.Value}");
                }
            }

            if (!IsSpawned || !IsOwner) return;

            // Dev only: a survivor sends boss commands, which the server must ignore.
            if (avatar != null && avatar.Role != RoleTable.BossRole && !tryDone && DevArgs.TrySummonSeconds >= 0f &&
                RoleTable.MatchRunning && Time.unscaledTime - spawnedAt >= DevArgs.TrySummonSeconds)
            {
                tryDone = true;
                SummonRpc();
                OrderRpc(Vector2.zero);
                ClearOrderRpc();
            }

            if (avatar == null || avatar.Role != RoleTable.BossRole) return;
            if (!RoleTable.MatchRunning)
            {
                // The match is over: a player cannot summon or order. The dev automation keeps sending summon requests anyway,
                // so a test can see the server refuse them.
                RunAutoSummon();
                return;
            }

            ReadInput();
            RunDevAutomation();
        }

        void ReadInput()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            // The left mouse button fires the boss's shots (see PlayerCombat); summoning is on Space.
            bool summon = keyboard != null && keyboard.spaceKey.isPressed;
            if (summon && Time.unscaledTime - lastSendTime >= MinSendInterval) SendSummon();

            if (mouse != null && mouse.rightButton.wasPressedThisFrame && Camera.main != null)
            {
                Vector3 pointer = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
                SendOrder(pointer);
            }

            if (keyboard != null && keyboard.cKey.wasPressedThisFrame) SendClear();
        }

        void RunAutoSummon()
        {
            float since = Time.unscaledTime - spawnedAt;
            if (DevArgs.AutoSummonSeconds > 0f && since >= nextAutoSummon)
            {
                nextAutoSummon += DevArgs.AutoSummonSeconds;
                SendSummon();
            }
        }

        void RunDevAutomation()
        {
            float since = Time.unscaledTime - spawnedAt;

            RunAutoSummon();

            if (!autoOrderPlayerDone && DevArgs.AutoOrderPlayerSeconds >= 0f && since >= DevArgs.AutoOrderPlayerSeconds)
            {
                autoOrderPlayerDone = true;
                // The same path a real command takes: point at where that survivor stands.
                for (int i = 0; i < PlayerAvatar.All.Count; i++)
                {
                    PlayerAvatar a = PlayerAvatar.All[i];
                    if (a != null && a.Role == DevArgs.AutoOrderPlayerRole) { SendOrder(a.transform.position); break; }
                }
            }

            if (!autoOrderPointDone && DevArgs.AutoOrderPoint.HasValue && DevArgs.AutoOrderPointSeconds >= 0f && since >= DevArgs.AutoOrderPointSeconds)
            {
                autoOrderPointDone = true;
                SendOrder(DevArgs.AutoOrderPoint.Value);
            }

            if (!autoClearDone && DevArgs.AutoClearSeconds >= 0f && since >= DevArgs.AutoClearSeconds)
            {
                autoClearDone = true;
                SendClear();
            }
        }

        void SendSummon()
        {
            lastSendTime = Time.unscaledTime;
            if (IsServer) SummonServer.Instance?.OnSummonRequest(OwnerClientId);
            else SummonRpc();
        }

        void SendOrder(Vector2 pointer)
        {
            if (IsServer) SummonServer.Instance?.OnOrderRequest(OwnerClientId, pointer);
            else OrderRpc(pointer);
        }

        void SendClear()
        {
            if (IsServer) SummonServer.Instance?.OnClearRequest(OwnerClientId);
            else ClearOrderRpc();
        }
    }
}
