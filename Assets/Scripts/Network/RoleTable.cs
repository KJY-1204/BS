using System;
using Unity.Netcode;
using UnityEngine;

namespace BS
{
    public struct PlayerSlot : INetworkSerializable, IEquatable<PlayerSlot>
    {
        public ulong ClientId;
        /// <summary>0 = boss, 1..4 = survivor slot.</summary>
        public byte Role;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Role);
        }

        public bool Equals(PlayerSlot other) => ClientId == other.ClientId && Role == other.Role;
    }

    /// <summary>
    /// Scene-placed NetworkObject. The server owns the slot table, assigns roles on connect,
    /// spawns the placeholder character and frees the slot on disconnect.
    /// </summary>
    public class RoleTable : NetworkBehaviour
    {
        public const byte BossRole = 0;
        public const byte MaxSurvivors = 4;

        [SerializeField] GameObject playerPrefab;

        public NetworkList<PlayerSlot> Slots;

        /// <summary>Set by the host UI before the room starts.</summary>
        public static bool HostWantsBoss;

        void Awake()
        {
            Slots = new NetworkList<PlayerSlot>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            // A previous room may have left slots behind; every room starts empty.
            Slots.Clear();

            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

            // The host connects before this object spawns on some paths, so pick up anyone already connected.
            foreach (ulong id in NetworkManager.ConnectedClientsIds)
                OnClientConnected(id);
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer || NetworkManager == null) return;
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        void OnClientConnected(ulong clientId)
        {
            if (FindSlot(clientId) >= 0) return;

            bool wantsBoss = clientId == NetworkManager.ServerClientId && HostWantsBoss;
            if (!TryPickRole(wantsBoss, out byte role))
            {
                NetworkManager.DisconnectClient(clientId, "방이 가득 찼습니다.");
                return;
            }

            Slots.Add(new PlayerSlot { ClientId = clientId, Role = role });
            SpawnCharacter(clientId, role);
        }

        void OnClientDisconnected(ulong clientId)
        {
            int index = FindSlot(clientId);
            if (index >= 0) Slots.RemoveAt(index);
        }

        bool TryPickRole(bool wantsBoss, out byte role)
        {
            bool bossFree = !IsRoleTaken(BossRole);

            if (wantsBoss && bossFree) { role = BossRole; return true; }

            for (byte r = 1; r <= MaxSurvivors; r++)
            {
                if (!IsRoleTaken(r)) { role = r; return true; }
            }

            if (bossFree) { role = BossRole; return true; }

            role = 0;
            return false;
        }

        bool IsRoleTaken(byte role)
        {
            for (int i = 0; i < Slots.Count; i++)
                if (Slots[i].Role == role) return true;
            return false;
        }

        int FindSlot(ulong clientId)
        {
            for (int i = 0; i < Slots.Count; i++)
                if (Slots[i].ClientId == clientId) return i;
            return -1;
        }

        void SpawnCharacter(ulong clientId, byte role)
        {
            Vector2 spawn = ArenaConfig.SpawnPoint(role);
            GameObject go = Instantiate(playerPrefab, spawn, Quaternion.identity);
            go.GetComponent<PlayerAvatar>().InitServer(role, spawn);
            go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
        }
    }
}
