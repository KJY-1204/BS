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
        /// <summary>For the boss: the kind of boss chosen, 0 = none chosen yet (the standard kind).</summary>
        public byte BossKind;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Role);
            serializer.SerializeValue(ref Character);
            serializer.SerializeValue(ref BossKind);
        }

        public bool Equals(PlayerSlot other) => ClientId == other.ClientId && Role == other.Role && Character == other.Character && BossKind == other.BossKind;
    }

    /// <summary>
    /// Scene-placed NetworkObject. The server owns the room: who is here, which character each survivor holds,
    /// and whether the room is waiting or playing. A thin link between <see cref="LobbySim"/> (the rules) and the
    /// network; characters only exist while a match is playing.
    /// </summary>
    [DefaultExecutionOrder(MatchPhases.ClockOrder)]
    public class RoleTable : NetworkBehaviour
    {
        public const byte BossRole = 0;
        public const byte MaxSurvivors = 4;

        public static RoleTable Instance { get; private set; }

        /// <summary>
        /// True while the match advances: the room is playing. Mobs, shots, skills, movement and every timer check this;
        /// after a result they stand still. With no room object (a test scene) things run as they always did.
        /// </summary>
        public static bool MatchRunning => Instance == null || Instance.Phase == RoomPhase.Playing;

        /// <summary>Raised on every machine when the room changes between waiting and playing.</summary>
        public static event Action<RoomPhase> PhaseChanged;

        [SerializeField] GameObject playerPrefab;

        public NetworkList<PlayerSlot> Slots;

        /// <summary>Set by the host UI before the room starts.</summary>
        public static bool HostWantsBoss;

        readonly NetworkVariable<byte> phase = new NetworkVariable<byte>(
            (byte)RoomPhase.Waiting, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // The result and the clock, for everyone's screen
        readonly NetworkVariable<ushort> secondsLeft = new NetworkVariable<ushort>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<byte> winner = new NetworkVariable<byte>(
            (byte)MatchWinner.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<byte> reason = new NetworkVariable<byte>(
            (byte)MatchReason.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>The kind of boss of the match being played; every machine feeds it to <see cref="BossCatalog"/>.</summary>
        readonly NetworkVariable<byte> bossKind = new NetworkVariable<byte>(
            BossCatalog.Standard, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> hasBoss = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        LobbySim lobby;
        MatchSim match => MatchWorld.Current.Clock;
        bool bossLeft;
        readonly List<LobbySlot> spawnList = new List<LobbySlot>();

        string notice = "";
        float noticeUntil;

        public RoomPhase Phase => (RoomPhase)phase.Value;

        /// <summary>Whole seconds left in a match with a boss (0 when there is no clock).</summary>
        public int SecondsLeft => secondsLeft.Value;
        public MatchWinner Winner => (MatchWinner)winner.Value;
        public MatchReason Reason => (MatchReason)reason.Value;

        /// <summary>True when the match has a boss (and so a clock and a result).</summary>
        public bool MatchHasBoss => hasBoss.Value;

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
            bossKind.OnValueChanged += OnBossKindChanged;
            BossCatalog.SetCurrentKind(bossKind.Value);

            if (!IsServer) return;

            // A previous room may have left slots behind; every room starts empty and waiting.
            Slots.Clear();
            lobby = new LobbySim(NetworkManager.ServerClientId);
            phase.Value = (byte)RoomPhase.Waiting;
            ClearMatch();

            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

            // The host connects before this object spawns on some paths, so pick up anyone already connected.
            foreach (ulong id in NetworkManager.ConnectedClientsIds)
                OnClientConnected(id);
        }

        public override void OnNetworkDespawn()
        {
            phase.OnValueChanged -= OnPhaseChanged;
            bossKind.OnValueChanged -= OnBossKindChanged;
            BossCatalog.SetCurrentKind(BossCatalog.Standard);
            if (Instance == this) Instance = null;

            if (!IsServer || NetworkManager == null) return;
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        void OnBossKindChanged(byte previous, byte current) => BossCatalog.SetCurrentKind(current);

        /// <summary>The kind of boss the boss player has chosen in the waiting room (the standard kind when nothing is chosen).</summary>
        public byte ChosenBossKind
        {
            get
            {
                for (int i = 0; i < Slots.Count; i++)
                    if (Slots[i].Role == BossRole && BossCatalog.IsValid(Slots[i].BossKind)) return Slots[i].BossKind;
                return BossCatalog.Standard;
            }
        }

        /// <summary>What the finished match came to, on every machine, while the room is finished. Null otherwise, and for a match without a boss.</summary>
        public MatchSummary Summary { get; private set; }

        void OnPhaseChanged(byte previous, byte current)
        {
            Debug.Log($"[BS] lobby: room is now {(RoomPhase)current} (server={IsServer})");
            if ((RoomPhase)current != RoomPhase.Finished) Summary = null;
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
            if (index >= 0)
            {
                // The boss leaving a running match with a boss forfeits it.
                if (Slots[index].Role == BossRole && lobby.Phase == RoomPhase.Playing && match.Enabled) bossLeft = true;
                Slots.RemoveAt(index);
            }

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

        /// <summary>Asks for a kind of boss (0 releases the choice); only the boss player's request counts.</summary>
        public void RequestBossKind(byte kind) => SelectBossKindRpc(kind);

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
        void SelectBossKindRpc(byte kind, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (lobby == null) return;

            LobbyResult result = lobby.SelectBossKind(sender, kind);
            Debug.Log($"[BS] lobby: client {sender} asked for boss kind {kind}: {result}");

            if (result == LobbyResult.Ok)
            {
                int index = FindSlot(sender);
                if (index >= 0)
                {
                    PlayerSlot slot = Slots[index];
                    slot.BossKind = kind;
                    Slots[index] = slot;
                }
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

            // The boss's kind is fixed before anything spawns, so the boss starts with the right stats and every machine learns it with the spawns.
            byte kind = lobby.BossKindOf();
            BossCatalog.SetCurrentKind(kind);
            bossKind.Value = kind;

            foreach (LobbySlot slot in spawnList)
                SpawnCharacter(slot.ClientId, slot.Role, slot.Character);

            bool withBoss = false;
            foreach (LobbySlot slot in spawnList) if (slot.Role == BossRole) withBoss = true;
            BossDef boss = BossCatalog.ForMatch();
            match.Start(withBoss, boss.MatchSeconds);
            MobServer.Instance?.BeginMatch(match.Enabled);
            SummonServer.Instance?.BeginMatch();
            bossLeft = false;
            hasBoss.Value = match.Enabled;
            secondsLeft.Value = (ushort)Mathf.Clamp(match.SecondsLeft, 0, ushort.MaxValue);
            winner.Value = (byte)MatchWinner.None;
            reason.Value = (byte)MatchReason.None;

            phase.Value = (byte)RoomPhase.Playing;
            Debug.Log($"[BS] lobby: match started with {spawnList.Count} players");
            Debug.Log(match.Enabled
                ? $"[BS] match: started with a boss ({boss.MaxHealth} health), {match.TimeLimit:F0} seconds"
                : "[BS] match: started without a boss (practice, no clock)");
        }

        void FixedUpdate()
        {
            if (!IsServer || !IsSpawned || lobby == null || Phase != RoomPhase.Playing) return;
            StepMatch(Time.fixedDeltaTime);
        }

        /// <summary>Advances the clock and finishes the match when it has a result.</summary>
        void StepMatch(float dt)
        {
            if (!match.Enabled) return;

            MatchWorld world = MatchWorld.Current;
            world.BossLeft = bossLeft;
            world.Run(MatchPhase.Clock, dt);
            bool decided = world.ClockDecided;
            ushort seconds = (ushort)Mathf.Clamp(match.SecondsLeft, 0, ushort.MaxValue);
            if (secondsLeft.Value != seconds) secondsLeft.Value = seconds;

            if (decided) FinishMatch();
        }

        /// <summary>The match has a winner: everything stops, the result is shown, and the room waits for the host.</summary>
        void FinishMatch()
        {
            if (!lobby.Finish()) return;
            MatchWorld world = MatchWorld.Current;

            winner.Value = (byte)match.Winner;
            reason.Value = (byte)match.Reason;
            phase.Value = (byte)RoomPhase.Finished;
            MatchSummary summary = world.BuildSummary();
            if (summary != null) SummaryRpc(MatchSummaryCodec.Write(summary));
            Debug.Log($"[BS] match: finished winner={match.Winner} reason={match.Reason} timeLeft={match.TimeLeft:F1}s " +
                      $"bossHealth={(CombatServer.Instance != null ? CombatServer.Instance.Boss.Health : 0)} " +
                      $"survivorsAlive={(CombatServer.Instance != null ? CombatServer.Instance.Survivors.AliveCount : 0)}");
        }

        /// <summary>The server tells every machine, the host's included, how the match went (once, when it is decided).</summary>
        [Rpc(SendTo.ClientsAndHost)]
        void SummaryRpc(byte[] bytes)
        {
            Summary = MatchSummaryCodec.Read(bytes);
            if (Summary == null) return;
            var sb = new System.Text.StringBuilder();
            sb.Append($"[BS] summary received: length={Summary.LengthSeconds}s boss={Summary.BossHealth}/{Summary.BossMaxHealth} ")
              .Append($"devices placed={Summary.DevicesPlaced} destroyed={Summary.DevicesDestroyed} expired={Summary.DevicesExpired}");
            foreach (SurvivorSummary v in Summary.Survivors)
                sb.Append($" | role={v.Role} char={v.Character} lv={v.Level} falls={v.Falls} revives={v.Revives} alive={v.Alive}");
            Debug.Log(sb.ToString());
        }

        void ClearMatch()
        {
            Summary = null;
            match.Clear();
            bossLeft = false;
            hasBoss.Value = false;
            secondsLeft.Value = 0;
            winner.Value = (byte)MatchWinner.None;
            reason.Value = (byte)MatchReason.None;
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
            ClearMatch();
            phase.Value = (byte)RoomPhase.Waiting;
            Debug.Log("[BS] lobby: match ended, back to waiting");
        }

        /// <summary>Empties every simulation that belongs to a match. Also used when the server stops.</summary>
        public static void ResetMatchState()
        {
            CombatServer.Instance?.ResetMatch();
            MobServer.Instance?.ResetMatch();
            SummonServer.Instance?.ResetMatch();
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
        bool devBossKindDone;
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

            if (DevArgs.BossKind > 0 && !devBossKindDone && since >= 0.5f && Phase == RoomPhase.Waiting && mine.Role == BossRole)
            {
                devBossKindDone = true;
                RequestBossKind((byte)DevArgs.BossKind);
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

        /// <summary>The character the player in a role holds (0 when nobody is in that role or none is chosen).</summary>
        public byte CharacterOfRole(byte role)
        {
            for (int i = 0; i < Slots.Count; i++)
                if (Slots[i].Role == role) return Slots[i].Character;
            return CharacterIds.None;
        }

        public bool TryGetSlot(ulong clientId, out PlayerSlot slot)
        {
            int i = FindSlot(clientId);
            slot = i >= 0 ? Slots[i] : default;
            return i >= 0;
        }
    }
}
