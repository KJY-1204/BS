using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BS
{
    public struct PlayerSlot : INetworkSerializable, IEquatable<PlayerSlot>
    {
        public ulong ClientId;
        /// <summary>0 = boss, 1..4 = survivor slot.</summary>
        public byte Role;
        /// <summary>The character a survivor holds, 0 = none yet (the boss never holds one).</summary>
        public byte Character;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Role);
            serializer.SerializeValue(ref Character);
        }

        public bool Equals(PlayerSlot other) => ClientId == other.ClientId && Role == other.Role && Character == other.Character;
    }

    /// <summary>
    /// Scene-placed NetworkObject. The server owns the room: who is here, which character each survivor holds,
    /// and whether the room is waiting or playing. A thin link between <see cref="LobbySim"/> (the rules) and the
    /// network; characters only exist while a match is playing.
    /// </summary>
    public class RoleTable : NetworkBehaviour
    {
        public const byte BossRole = 0;
        public const byte MaxSurvivors = 4;

        public static RoleTable Instance { get; private set; }

        /// <summary>Raised on every machine when the room changes between waiting and playing.</summary>
        public static event Action<RoomPhase> PhaseChanged;

        [SerializeField] GameObject playerPrefab;

        public NetworkList<PlayerSlot> Slots;

        /// <summary>Set by the host UI before the room starts.</summary>
        public static bool HostWantsBoss;

        readonly NetworkVariable<byte> phase = new NetworkVariable<byte>(
            (byte)RoomPhase.Waiting, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        LobbySim lobby;
        readonly List<LobbySlot> spawnList = new List<LobbySlot>();

        string notice = "";
        float noticeUntil;

        public RoomPhase Phase => (RoomPhase)phase.Value;

        /// <summary>The latest refusal or message for this player (shown in the waiting room).</summary>
        public string Notice => Time.unscaledTime < noticeUntil ? notice : "";

        void Awake()
        {
            Slots = new NetworkList<PlayerSlot>();
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            phase.OnValueChanged += OnPhaseChanged;

            if (!IsServer) return;

            // A previous room may have left slots behind; every room starts empty and waiting.
            Slots.Clear();
            lobby = new LobbySim(NetworkManager.ServerClientId);
            phase.Value = (byte)RoomPhase.Waiting;

            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

            // The host connects before this object spawns on some paths, so pick up anyone already connected.
            foreach (ulong id in NetworkManager.ConnectedClientsIds)
                OnClientConnected(id);
        }

        public override void OnNetworkDespawn()
        {
            phase.OnValueChanged -= OnPhaseChanged;
            if (Instance == this) Instance = null;

            if (!IsServer || NetworkManager == null) return;
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        void OnPhaseChanged(byte previous, byte current)
        {
            Debug.Log($"[BS] lobby: room is now {(RoomPhase)current} (server={IsServer})");
            PhaseChanged?.Invoke((RoomPhase)current);
        }

        // ---- Joining and leaving (server) ----

        void OnClientConnected(ulong clientId)
        {
            if (FindSlot(clientId) >= 0) return;

            bool wantsBoss = clientId == NetworkManager.ServerClientId && HostWantsBoss;
            LobbyResult result = lobby.Join(clientId, wantsBoss, out byte role);
            if (result != LobbyResult.Ok)
            {
                Debug.Log($"[BS] lobby: client {clientId} refused ({result})");
                NetworkManager.DisconnectClient(clientId, result == LobbyResult.MatchRunning ? "이미 게임이 진행 중입니다." : "방이 가득 찼습니다.");
                return;
            }

            Slots.Add(new PlayerSlot { ClientId = clientId, Role = role, Character = CharacterIds.None });
            Debug.Log($"[BS] lobby: client {clientId} joined as role {role} ({lobby.Count} in the room)");
        }

        void OnClientDisconnected(ulong clientId)
        {
            int index = FindSlot(clientId);
            if (index >= 0) Slots.RemoveAt(index);

            byte held = lobby.CharacterOf(clientId);
            if (lobby.Leave(clientId))
                Debug.Log($"[BS] lobby: client {clientId} left{(held != CharacterIds.None ? $", character {held} is free" : "")} ({lobby.Count} in the room)");
        }

        int FindSlot(ulong clientId)
        {
            for (int i = 0; i < Slots.Count; i++)
                if (Slots[i].ClientId == clientId) return i;
            return -1;
        }

        // ---- Requests (every machine sends, the server decides) ----

        /// <summary>Asks for a character (0 releases the choice).</summary>
        public void RequestCharacter(byte character) => SelectCharacterRpc(character);

        public void RequestStart() => StartMatchRpc();

        public void RequestEndMatch() => EndMatchRpc();

        [Rpc(SendTo.Server)]
        void SelectCharacterRpc(byte character, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (lobby == null) return;

            LobbyResult result = lobby.SelectCharacter(sender, character);
            Debug.Log($"[BS] lobby: client {sender} asked for character {character}: {result}");

            if (result == LobbyResult.Ok)
            {
                int index = FindSlot(sender);
                if (index >= 0)
                {
                    PlayerSlot slot = Slots[index];
                    slot.Character = character;
                    Slots[index] = slot;
                }
            }
            else if (result == LobbyResult.CharacterTaken || result == LobbyResult.WrongPhase)
            {
                NoticeRpc((byte)result, RpcTarget.Single(sender, RpcTargetUse.Temp));
            }
        }

        [Rpc(SendTo.Server)]
        void StartMatchRpc(RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (lobby == null) return;

            LobbyResult result = lobby.TryStart(sender);
            Debug.Log($"[BS] lobby: client {sender} asked to start: {result}");
            if (result != LobbyResult.Ok)
            {
                if (result == LobbyResult.NotReady) NoticeRpc((byte)result, RpcTarget.Single(sender, RpcTargetUse.Temp));
                return;
            }

            StartMatch();
        }

        [Rpc(SendTo.Server)]
        void EndMatchRpc(RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (lobby == null) return;

            LobbyResult result = lobby.TryEndMatch(sender);
            Debug.Log($"[BS] lobby: client {sender} asked to end the match: {result}");
            if (result != LobbyResult.Ok) return;

            EndMatch();
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void NoticeRpc(byte code, RpcParams rpcParams = default)
        {
            switch ((LobbyResult)code)
            {
                case LobbyResult.CharacterTaken:
                    notice = "이미 다른 사람이 고른 캐릭터입니다.";
                    if (DevArgs.Character > 0) DevPickFree(); // development: a refused automatic choice takes any free character
                    break;
                case LobbyResult.NotReady: notice = "모든 생존자가 캐릭터를 골라야 시작할 수 있습니다."; break;
                case LobbyResult.WrongPhase: notice = "지금은 캐릭터를 바꿀 수 없습니다."; break;
                default: notice = ""; break;
            }
            noticeUntil = Time.unscaledTime + 3f;
        }

        // ---- Match start and end (server) ----

        void StartMatch()
        {
            spawnList.Clear();
            spawnList.AddRange(lobby.Slots);

            foreach (LobbySlot slot in spawnList)
                SpawnCharacter(slot.ClientId, slot.Role, slot.Character);

            phase.Value = (byte)RoomPhase.Playing;
            Debug.Log($"[BS] lobby: match started with {spawnList.Count} players");
        }

        /// <summary>
        /// Removes everything the match made and puts the room back to waiting with the same players.
        /// Characters go first so each one leaves through the normal despawn path, then every simulation is emptied.
        /// </summary>
        void EndMatch()
        {
            foreach (LobbySlot slot in lobby.Slots)
            {
                NetworkObject player = NetworkManager.SpawnManager.GetPlayerNetworkObject(slot.ClientId);
                if (player != null && player.IsSpawned) player.Despawn(true);
            }

            ResetMatchState();
            phase.Value = (byte)RoomPhase.Waiting;
            Debug.Log("[BS] lobby: match ended, back to waiting");
        }

        /// <summary>Empties every simulation that belongs to a match. Also used when the server stops.</summary>
        public static void ResetMatchState()
        {
            CombatServer.Instance?.ResetMatch();
            MobServer.Instance?.ResetMatch();
        }

        void SpawnCharacter(ulong clientId, byte role, byte character)
        {
            Vector2 spawn = ArenaConfig.SpawnPoint(role);
            GameObject go = Instantiate(playerPrefab, spawn, Quaternion.identity);
            go.GetComponent<PlayerAvatar>().InitServer(role, spawn, character);
            go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
        }

        // ---- Development scenarios (-bsCharacter, -bsAutoStart, -bsAutoEnd, -bsTryStart, -bsTryEnd) ----

        float devJoinedAt = -1f;
        float devReadySince = -1f;
        float devPlayingSince = -1f;
        bool devCharacterDone, devEndDone, devTryStartDone, devTryEndDone;

        void Update()
        {
            if (!IsSpawned) return;
            DevTick();
        }

        void DevPickFree()
        {
            foreach (CharacterDef def in CharacterCatalog.All)
            {
                bool held = false;
                for (int i = 0; i < Slots.Count; i++) if (Slots[i].Character == def.Id) held = true;
                if (held) continue;
                Debug.Log($"[BS] lobby: dev picks free character {def.Id}");
                RequestCharacter(def.Id);
                return;
            }
        }

        void DevTick()
        {
            float now = Time.unscaledTime;
            if (!TryGetSlot(NetworkManager.LocalClientId, out PlayerSlot mine)) { devJoinedAt = -1f; return; }
            if (devJoinedAt < 0f) devJoinedAt = now;
            float since = now - devJoinedAt;

            if (DevArgs.Character > 0 && !devCharacterDone && since >= 0.5f && Phase == RoomPhase.Waiting && mine.Role != BossRole)
            {
                devCharacterDone = true;
                RequestCharacter((byte)DevArgs.Character);
            }

            if (!IsServer)
            {
                if (DevArgs.TryStartSeconds >= 0f && !devTryStartDone && since >= DevArgs.TryStartSeconds) { devTryStartDone = true; RequestStart(); }
                if (DevArgs.TryEndSeconds >= 0f && !devTryEndDone && since >= DevArgs.TryEndSeconds) { devTryEndDone = true; RequestEndMatch(); }
                return;
            }

            if (Phase == RoomPhase.Waiting)
            {
                devPlayingSince = -1f;
                if (DevArgs.AutoStartSeconds < 0f) return;

                bool ready = CanStart && lobby.SurvivorCount >= DevArgs.AutoStartSurvivors;
                if (!ready) { devReadySince = -1f; return; }
                if (devReadySince < 0f) devReadySince = now;
                if (now - devReadySince >= DevArgs.AutoStartSeconds)
                {
                    devReadySince = -1f;
                    RequestStart();
                }
            }
            else
            {
                devReadySince = -1f;
                if (devPlayingSince < 0f) devPlayingSince = now;
                if (DevArgs.AutoEndSeconds >= 0f && !devEndDone && now - devPlayingSince >= DevArgs.AutoEndSeconds)
                {
                    devEndDone = true;
                    RequestEndMatch();
                }
            }
        }

        // ---- Readiness (any machine, from the replicated slots) ----

        /// <summary>At least one survivor and every survivor holds a character.</summary>
        public bool CanStart
        {
            get
            {
                if (Phase != RoomPhase.Waiting) return false;
                int survivors = 0;
                for (int i = 0; i < Slots.Count; i++)
                {
                    if (Slots[i].Role == BossRole) continue;
                    survivors++;
                    if (Slots[i].Character == CharacterIds.None) return false;
                }
                return survivors > 0;
            }
        }

        public bool TryGetSlot(ulong clientId, out PlayerSlot slot)
        {
            int i = FindSlot(clientId);
            slot = i >= 0 ? Slots[i] : default;
            return i >= 0;
        }
    }
}
