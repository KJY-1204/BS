using System.Collections.Generic;

namespace BS
{
    public enum RoomPhase : byte
    {
        Waiting = 0,
        Playing = 1,
    }

    public enum LobbyResult
    {
        Ok,
        /// <summary>Nothing changed because the request repeated the current state.</summary>
        NoChange,
        UnknownPlayer,
        WrongPhase,
        NotSurvivor,
        UnknownCharacter,
        CharacterTaken,
        NotHost,
        NotReady,
        RoomFull,
        MatchRunning,
    }

    /// <summary>One connected player as the room sees them.</summary>
    public struct LobbySlot
    {
        public ulong ClientId;
        /// <summary>0 = boss, 1 to 4 = survivor slot.</summary>
        public byte Role;
        /// <summary>0 = none yet.</summary>
        public byte Character;
    }

    /// <summary>
    /// The room's two states and the rules inside them: who is here, which character each survivor holds,
    /// when the host may start and how a match ends. Pure data, no network; the server asks it to judge every
    /// request in the order the requests arrive.
    /// </summary>
    public class LobbySim
    {
        public const byte BossRole = 0;
        public const byte MaxSurvivors = 4;
        public const int MaxPlayers = 5;

        readonly List<LobbySlot> slots = new List<LobbySlot>();

        public RoomPhase Phase { get; private set; } = RoomPhase.Waiting;
        public ulong HostId { get; }

        public LobbySim(ulong hostId)
        {
            HostId = hostId;
        }

        public IReadOnlyList<LobbySlot> Slots => slots;
        public int Count => slots.Count;

        public int IndexOf(ulong clientId)
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].ClientId == clientId) return i;
            return -1;
        }

        public bool Contains(ulong clientId) => IndexOf(clientId) >= 0;

        public byte RoleOf(ulong clientId)
        {
            int i = IndexOf(clientId);
            return i >= 0 ? slots[i].Role : byte.MaxValue;
        }

        public byte CharacterOf(ulong clientId)
        {
            int i = IndexOf(clientId);
            return i >= 0 ? slots[i].Character : CharacterIds.None;
        }

        /// <summary>The player holding this character, or false when it is free.</summary>
        public bool HolderOf(byte character, out ulong clientId)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Character == character && character != CharacterIds.None)
                {
                    clientId = slots[i].ClientId;
                    return true;
                }
            }
            clientId = 0;
            return false;
        }

        public int SurvivorCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < slots.Count; i++) if (slots[i].Role != BossRole) n++;
                return n;
            }
        }

        // ---- Joining and leaving ----

        /// <summary>
        /// Adds a player. The host chooses its own role; everyone else gets the lowest free survivor slot, or the boss
        /// role when it is still free and no survivor slot is. A running match accepts nobody.
        /// </summary>
        public LobbyResult Join(ulong clientId, bool wantsBoss, out byte role)
        {
            role = 0;
            if (Contains(clientId)) { role = RoleOf(clientId); return LobbyResult.NoChange; }
            if (Phase == RoomPhase.Playing) return LobbyResult.MatchRunning;
            if (slots.Count >= MaxPlayers) return LobbyResult.RoomFull;

            bool bossFree = !RoleTaken(BossRole);
            if (wantsBoss && bossFree)
            {
                role = BossRole;
            }
            else
            {
                role = byte.MaxValue;
                for (byte r = 1; r <= MaxSurvivors; r++)
                {
                    if (!RoleTaken(r)) { role = r; break; }
                }

                if (role == byte.MaxValue)
                {
                    if (!bossFree) return LobbyResult.RoomFull;
                    role = BossRole;
                }
            }

            slots.Add(new LobbySlot { ClientId = clientId, Role = role, Character = CharacterIds.None });
            return LobbyResult.Ok;
        }

        /// <summary>Removes a player. The character they held is free again.</summary>
        public bool Leave(ulong clientId)
        {
            int i = IndexOf(clientId);
            if (i < 0) return false;
            slots.RemoveAt(i);
            return true;
        }

        bool RoleTaken(byte role)
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].Role == role) return true;
            return false;
        }

        // ---- Choosing a character ----

        /// <summary>
        /// Chooses a character for a survivor (0 releases the choice). The request that reaches the server first
        /// holds the character; a later request for a held character is refused and changes nothing.
        /// </summary>
        public LobbyResult SelectCharacter(ulong clientId, byte character)
        {
            int i = IndexOf(clientId);
            if (i < 0) return LobbyResult.UnknownPlayer;
            if (Phase != RoomPhase.Waiting) return LobbyResult.WrongPhase;
            if (slots[i].Role == BossRole) return LobbyResult.NotSurvivor;
            if (character != CharacterIds.None && !CharacterCatalog.IsValid(character)) return LobbyResult.UnknownCharacter;
            if (slots[i].Character == character) return LobbyResult.NoChange;

            if (character != CharacterIds.None && HolderOf(character, out _)) return LobbyResult.CharacterTaken;

            LobbySlot slot = slots[i];
            slot.Character = character;
            slots[i] = slot;
            return LobbyResult.Ok;
        }

        // ---- Starting and ending a match ----

        /// <summary>At least one survivor, and every survivor holds a character.</summary>
        public bool IsReadyToStart
        {
            get
            {
                if (Phase != RoomPhase.Waiting) return false;
                int survivors = 0;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i].Role == BossRole) continue;
                    survivors++;
                    if (slots[i].Character == CharacterIds.None) return false;
                }
                return survivors > 0;
            }
        }

        public LobbyResult TryStart(ulong requester)
        {
            if (requester != HostId) return LobbyResult.NotHost;
            if (Phase != RoomPhase.Waiting) return LobbyResult.WrongPhase;
            if (!IsReadyToStart) return LobbyResult.NotReady;
            Phase = RoomPhase.Playing;
            return LobbyResult.Ok;
        }

        /// <summary>Ends a running match. Everyone stays and every choice stays.</summary>
        public LobbyResult TryEndMatch(ulong requester)
        {
            if (requester != HostId) return LobbyResult.NotHost;
            if (Phase != RoomPhase.Playing) return LobbyResult.WrongPhase;
            Phase = RoomPhase.Waiting;
            return LobbyResult.Ok;
        }

        /// <summary>The room ended: everyone is gone and the next room starts waiting.</summary>
        public void Clear()
        {
            slots.Clear();
            Phase = RoomPhase.Waiting;
        }
    }
}
