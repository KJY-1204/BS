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

        // The boss skills' cooldowns in tenths of a second, readable only by the boss player.
        readonly NetworkVariable<byte> skillCooldown0 = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<byte> skillCooldown1 = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        readonly bool[] heldSkillKeys = new bool[BossSkills.Count];
        float nextAutoSkill;
        int autoSkillTurn;

        /// <summary>Seconds until a boss skill is ready (0 when ready); only meaningful for the boss player's own copy.</summary>
        public float SkillCooldown(int slot) => slot == 0 ? skillCooldown0.Value / 10f : slot == 1 ? skillCooldown1.Value / 10f : 0f;

        // The devices the boss has standing, the most it may have and the placing cooldown in tenths of a second (boss player only).
        readonly NetworkVariable<byte> deviceCount = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<byte> deviceMax = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<ushort> deviceCooldown = new NetworkVariable<ushort>(0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        float nextAutoDevice;

        /// <summary>How many devices the boss has standing (only meaningful for the boss player's own copy).</summary>
        public int DeviceCount => deviceCount.Value;
        /// <summary>The most devices the boss may have standing.</summary>
        public int DeviceMax => deviceMax.Value;
        /// <summary>Seconds until the boss may place another device (0 when ready).</summary>
        public float DeviceCooldown => deviceCooldown.Value / 10f;

        string notice = "";
        float noticeUntil;
        int autoSummonCount;
        readonly bool[] heldKinds = new bool[MobKinds.Count];

        static UnityEngine.InputSystem.Controls.KeyControl KindKey(Keyboard keyboard, int kind)
        {
            switch (kind)
            {
                case 0: return keyboard.digit1Key;
                case 1: return keyboard.digit2Key;
                case 2: return keyboard.digit3Key;
                case 3: return keyboard.digit4Key;
                default: return keyboard.digit5Key;
            }
        }

        /// <summary>The mob kind the boss player has chosen to summon. Kept on the owner's machine and sent with each request.</summary>
        public byte SelectedKind { get; private set; } = MobKinds.Basic;

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
            nextAutoSkill = DevArgs.AutoBossSkillSeconds;
            nextAutoDevice = DevArgs.AutoDeviceSeconds;
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

        /// <summary>Server only: copies the boss skills' cooldowns (in seconds) to the boss player's replicated values.</summary>
        public void ServerWriteSkills(float cooldown0, float cooldown1)
        {
            byte a = (byte)Mathf.Clamp(Mathf.CeilToInt(cooldown0 * 10f), 0, 255);
            byte b = (byte)Mathf.Clamp(Mathf.CeilToInt(cooldown1 * 10f), 0, 255);
            if (skillCooldown0.Value != a) skillCooldown0.Value = a;
            if (skillCooldown1.Value != b) skillCooldown1.Value = b;
        }

        /// <summary>Server only: copies the standing devices, their limit and the placing cooldown (in seconds) to the boss player.</summary>
        public void ServerWriteDevices(int count, int max, float cooldownSeconds)
        {
            byte c = (byte)Mathf.Clamp(count, 0, 255);
            byte m = (byte)Mathf.Clamp(max, 0, 255);
            ushort cd = (ushort)Mathf.Clamp(Mathf.CeilToInt(cooldownSeconds * 10f), 0, ushort.MaxValue);
            if (deviceCount.Value != c) deviceCount.Value = c;
            if (deviceMax.Value != m) deviceMax.Value = m;
            if (deviceCooldown.Value != cd) deviceCooldown.Value = cd;
        }

        [Rpc(SendTo.Server)]
        void PlaceDeviceRpc()
        {
            SummonServer.Instance?.OnPlaceDeviceRequest(OwnerClientId);
        }

        void SendPlaceDevice()
        {
            if (IsServer) SummonServer.Instance?.OnPlaceDeviceRequest(OwnerClientId);
            else PlaceDeviceRpc();
        }

        [Rpc(SendTo.Server)]
        void SkillRpc(byte slot, ushort aim)
        {
            CombatServer.Instance?.OnBossSkillRequest(OwnerClientId, slot, aim);
        }

        /// <summary>
        /// Asks for a boss skill. The request carries the boss's aim of this moment: the server is told the aim only while the boss fires,
        /// so a skill used without firing (the beam, the first shot of the ring) would otherwise point wherever the boss last fired.
        /// </summary>
        void SendSkill(int slot)
        {
            PlayerCombat combat = GetComponent<PlayerCombat>();
            ushort aim = combat != null ? combat.AimAngle : (ushort)0;
            if (IsServer) CombatServer.Instance?.OnBossSkillRequest(OwnerClientId, slot, aim);
            else SkillRpc((byte)slot, aim);
        }

        /// <summary>Server only: tells the boss player a summon did not happen (or only partly).</summary>
        public void ServerSendResult(SummonResult result) => ResultRpc((byte)result);

        [Rpc(SendTo.Server)]
        void SummonRpc(byte kind)
        {
            SummonServer.Instance?.OnSummonRequest(OwnerClientId, kind);
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
                case SummonResult.UnknownKind: Say("알 수 없는 종류입니다"); break;
                case SummonResult.DeviceCooldown: Say("아직 장치를 놓을 수 없습니다"); break;
                case SummonResult.DeviceLimit: Say("장치를 더 놓을 수 없습니다"); break;
                case SummonResult.TooClose: Say("생존자가 너무 가까워 장치를 놓을 수 없습니다"); break;
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
                    Debug.Log($"[BS] summon seen: isOwner={IsOwner} energy={energy.Value}/{maxEnergy.Value} order={(OrderKind)orderKind.Value} skillCooldowns={skillCooldown0.Value}/{skillCooldown1.Value} devices={deviceCount.Value}/{deviceMax.Value} deviceCooldown={deviceCooldown.Value}");
                }
            }

            if (!IsSpawned || !IsOwner) return;

            // Dev only: a survivor sends boss commands, which the server must ignore.
            if (avatar != null && avatar.Role != RoleTable.BossRole && !tryDone && DevArgs.TrySummonSeconds >= 0f &&
                RoleTable.MatchRunning && Time.unscaledTime - spawnedAt >= DevArgs.TrySummonSeconds)
            {
                tryDone = true;
                SummonRpc(MobKinds.Tough);
                OrderRpc(Vector2.zero);
                ClearOrderRpc();
            }

            if (avatar == null || avatar.Role != RoleTable.BossRole) return;
            if (!RoleTable.MatchRunning)
            {
                // The match is over: a player cannot summon or order. The dev automation keeps sending summon requests anyway,
                // so a test can see the server refuse them.
                RunAutoSummon();
                RunAutoSkill();
                RunAutoDevice();
                return;
            }

            ReadInput();
            RunDevAutomation();
            RunAutoSkill();
            RunAutoDevice();
        }

        void ReadInput()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            // The left mouse button fires the boss's shots (see PlayerCombat); summoning is on Space.
            // A key held down picks its kind once, by comparing with the last frame's state (this also works when the window
            // is not focused and input arrives between frames).
            for (int k = 0; k < MobKinds.Count && keyboard != null; k++)
            {
                bool down = KindKey(keyboard, k).isPressed;
                if (down && !heldKinds[k]) SelectedKind = (byte)k;
                heldKinds[k] = down;
            }

            bool summon = keyboard != null && keyboard.spaceKey.isPressed;
            if (summon && Time.unscaledTime - lastSendTime >= MinSendInterval) SendSummon();

            if (mouse != null && mouse.rightButton.wasPressedThisFrame && Camera.main != null)
            {
                Vector3 pointer = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
                SendOrder(pointer);
            }

            if (keyboard != null && keyboard.cKey.wasPressedThisFrame) SendClear();

            // R places a device at the boss's position.
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) SendPlaceDevice();

            // Q and E are the boss's skills. A held key counts once, by comparing with the last frame's state.
            for (int slot = 0; slot < BossSkills.Count && keyboard != null; slot++)
            {
                bool down = (slot == 0 ? keyboard.qKey : keyboard.eKey).isPressed;
                if (down && !heldSkillKeys[slot]) SendSkill(slot);
                heldSkillKeys[slot] = down;
            }
        }

        /// <summary>Development automation: press the boss skill keys on a timer (it keeps going after the match so the refusal can be seen).</summary>
        void RunAutoSkill()
        {
            if (DevArgs.AutoBossSkillSeconds <= 0f || DevArgs.AutoBossSkillKeys.Length == 0) return;
            float since = Time.unscaledTime - spawnedAt;
            if (since < nextAutoSkill) return;

            nextAutoSkill += DevArgs.AutoBossSkillSeconds;
            string keys = DevArgs.AutoBossSkillKeys;
            if (keys.Contains("q")) SendSkill(BossSkills.Barrage);
            if (keys.Contains("e")) SendSkill(BossSkills.Shockwave);
        }

        /// <summary>Development automation: place a device on a timer (it keeps going after the match so the refusal can be seen).</summary>
        void RunAutoDevice()
        {
            if (DevArgs.AutoDeviceSeconds <= 0f) return;
            float since = Time.unscaledTime - spawnedAt;
            if (since < nextAutoDevice) return;

            nextAutoDevice += DevArgs.AutoDeviceSeconds;
            SendPlaceDevice();
        }

        void RunAutoSummon()
        {
            float since = Time.unscaledTime - spawnedAt;
            if (DevArgs.AutoSummonSeconds > 0f && since >= nextAutoSummon)
            {
                nextAutoSummon += DevArgs.AutoSummonSeconds;
                SendSummon(true);
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

        void SendSummon(bool automatic = false)
        {
            lastSendTime = Time.unscaledTime;

            // The development automation can ask for a list of kinds in turn (even one that does not exist); a player asks for the chosen kind.
            byte kind = SelectedKind;
            int[] kinds = DevArgs.AutoSummonKinds;
            if (automatic && kinds.Length > 0) kind = (byte)Mathf.Clamp(kinds[autoSummonCount++ % kinds.Length], 0, 255);

            if (IsServer) SummonServer.Instance?.OnSummonRequest(OwnerClientId, kind);
            else SummonRpc(kind);
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
