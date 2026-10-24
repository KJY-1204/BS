using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BS
{
    /// <summary>
    /// A survivor's growth as the players see it. The level is shown to everyone; experience, mana, skill ranks,
    /// skill choices and skill results are only sent to the owner. The owner sends back only a choice number
    /// or a skill slot, and the server decides what happens.
    /// </summary>
    public class PlayerGrowth : NetworkBehaviour
    {
        /// <summary>Every spawned PlayerGrowth on this machine, for drawing levels over characters.</summary>
        public static readonly List<PlayerGrowth> All = new List<PlayerGrowth>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        readonly NetworkVariable<byte> level = new NetworkVariable<byte>(
            1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<ushort> xp = new NetworkVariable<ushort>(
            0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<ushort> mana = new NetworkVariable<ushort>(
            (ushort)GrowthConfig.BaseMaxMana, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<ushort> maxMana = new NetworkVariable<ushort>(
            (ushort)GrowthConfig.BaseMaxMana, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<ulong> ranksPacked = new NetworkVariable<ulong>(
            0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        /// <summary>The ranks of skill ids 22 to 42 (see <see cref="SkillCatalog.PackHigh"/>).</summary>
        readonly NetworkVariable<ulong> ranksPackedHigh = new NetworkVariable<ulong>(
            0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        /// <summary>The ranks of skill ids 43 and above (see <see cref="SkillCatalog.PackThird"/>).</summary>
        readonly NetworkVariable<ulong> ranksPackedThird = new NetworkVariable<ulong>(
            0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

        PlayerAvatar avatar;
        readonly int[] ranks = new int[SkillCatalog.MaxSkillId + 1];
        readonly float[] cooldownEndsAt = new float[SkillCatalog.SkillSlots];

        // Owner-side state of the open skill choice
        byte offerId;
        readonly byte[] offerChoices = new byte[GrowthConfig.OfferChoices];
        int offerCount;
        float offerEndsAt;
        bool offerOpen;

        // Owner-side feedback
        string notice = "";
        float noticeUntil;
        float autoPickAt = -1f;
        float nextAutoSkillAt;

        public int Level => level.Value;
        public bool IsMaxLevel => level.Value >= GrowthConfig.MaxLevel;
        public int Xp => xp.Value;
        public int XpNeeded => IsMaxLevel ? 0 : GrowthConfig.XpNeeded(level.Value);
        public int Mana => mana.Value;
        public int MaxMana => maxMana.Value;
        public int RankOf(byte skillId) => SkillCatalog.RankOf(ranks, skillId);

        /// <summary>The owner's skill ranks (empty of meaning for other players' avatars).</summary>
        public IReadOnlyList<int> Ranks => ranks;

        public bool OfferOpen => offerOpen;
        public int OfferCount => offerCount;
        public byte OfferChoice(int index) => index >= 0 && index < offerCount ? offerChoices[index] : (byte)0;
        public float OfferSecondsLeft => offerOpen ? Mathf.Max(0f, offerEndsAt - Time.unscaledTime) : 0f;

        public float CooldownLeft(int slot) =>
            slot >= 0 && slot < cooldownEndsAt.Length ? Mathf.Max(0f, cooldownEndsAt[slot] - Time.unscaledTime) : 0f;

        public string Notice => Time.unscaledTime < noticeUntil ? notice : "";

        void Awake()
        {
            avatar = GetComponent<PlayerAvatar>();
        }

        public override void OnNetworkSpawn()
        {
            if (!All.Contains(this)) All.Add(this);

            if (IsOwner)
            {
                ranksPacked.OnValueChanged += (_, __) => { UnpackRanks(); LogRanks(); };
                ranksPackedHigh.OnValueChanged += (_, __) => { UnpackRanks(); LogRanks(); };
                ranksPackedThird.OnValueChanged += (_, __) => { UnpackRanks(); LogRanks(); };
                UnpackRanks();
            }

            if (DevArgs.GrowthLog)
            {
                level.OnValueChanged += (previous, current) =>
                    Debug.Log($"[BS] level owner={OwnerClientId} {previous} -> {current} (server={IsServer})");
            }
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
        }

        /// <summary>Development log (-bsGrowthLog): the ranks this owner's client has received, as id:rank pairs.</summary>
        void LogRanks()
        {
            if (!DevArgs.GrowthLog || !IsOwner || IsServer) return;
            var sb = new System.Text.StringBuilder();
            for (int id = 1; id < ranks.Length; id++)
                if (ranks[id] > 0) sb.Append(id).Append(':').Append(ranks[id]).Append(' ');
            Debug.Log($"[BS] client ranks owner={OwnerClientId} {sb}");
        }

        // ---- Server side ----

        /// <summary>Server only: copies the simulation's values to the replicated variables when they change.</summary>
        void UnpackRanks() => SkillCatalog.Unpack(new[] { ranksPacked.Value, ranksPackedHigh.Value, ranksPackedThird.Value }, ranks);

        public void ServerWrite(int levelValue, int xpValue, int manaValue, int maxManaValue, ulong packedRanks, ulong packedRanksHigh, ulong packedRanksThird)
        {
            byte l = (byte)Mathf.Clamp(levelValue, 1, 255);
            ushort x = (ushort)Mathf.Clamp(xpValue, 0, ushort.MaxValue);
            ushort m = (ushort)Mathf.Clamp(manaValue, 0, ushort.MaxValue);
            ushort mm = (ushort)Mathf.Clamp(maxManaValue, 0, ushort.MaxValue);

            if (level.Value != l) level.Value = l;
            if (xp.Value != x) xp.Value = x;
            if (maxMana.Value != mm) maxMana.Value = mm;
            if (mana.Value != m) mana.Value = m;
            if (ranksPacked.Value != packedRanks) ranksPacked.Value = packedRanks;
            if (ranksPackedHigh.Value != packedRanksHigh) ranksPackedHigh.Value = packedRanksHigh;
            if (ranksPackedThird.Value != packedRanksThird) ranksPackedThird.Value = packedRanksThird;

            if (IsServer && IsOwner) SkillCatalog.Unpack(new[] { packedRanks, packedRanksHigh, packedRanksThird }, ranks);
        }

        /// <summary>Server only: tells the owner a skill choice has opened.</summary>
        public void ServerSendOffer(byte id, byte[] choices, float seconds)
        {
            byte a = choices.Length > 0 ? choices[0] : (byte)0;
            byte b = choices.Length > 1 ? choices[1] : (byte)0;
            byte c = choices.Length > 2 ? choices[2] : (byte)0;
            OfferOpenedRpc(id, a, b, c, (byte)choices.Length, seconds);
        }

        public void ServerSendOfferClosed(byte id, byte skill, bool auto) => OfferClosedRpc(id, skill, auto);

        public void ServerSendSkillResult(int slot, SkillResult result, float cooldownSeconds) =>
            SkillResultRpc((byte)slot, (byte)result, cooldownSeconds);

        [Rpc(SendTo.Server)]
        void PickRpc(byte id, byte index)
        {
            GrowthServer.Instance?.OnPick(OwnerClientId, id, index);
        }

        [Rpc(SendTo.Server)]
        void SkillRpc(byte slot)
        {
            GrowthServer.Instance?.OnSkillRequest(OwnerClientId, slot);
        }

        // ---- Owner side ----

        [Rpc(SendTo.Owner)]
        void OfferOpenedRpc(byte id, byte a, byte b, byte c, byte count, float seconds)
        {
            offerId = id;
            offerChoices[0] = a;
            offerChoices[1] = b;
            offerChoices[2] = c;
            offerCount = Mathf.Clamp(count, 0, offerChoices.Length);
            offerEndsAt = Time.unscaledTime + seconds;
            offerOpen = true;

            if (DevArgs.GrowthLog)
                Debug.Log($"[BS] offer opened id={id} choices={a},{b},{c} count={count} seconds={seconds:F1}");

            if (DevArgs.AutoPickIndex > 0) autoPickAt = Time.unscaledTime + DevArgs.AutoPickDelay;
        }

        [Rpc(SendTo.Owner)]
        void OfferClosedRpc(byte id, byte skill, bool auto)
        {
            if (offerOpen && offerId == id) offerOpen = false;
            autoPickAt = -1f;

            SkillDef def = SkillCatalog.Get(skill);
            Say($"{(auto ? "자동 선택: " : "선택: ")}{(def != null ? def.Name : "?")}", 3f);

            if (DevArgs.GrowthLog) Debug.Log($"[BS] offer closed id={id} skill={skill} auto={auto}");
        }

        [Rpc(SendTo.Owner)]
        void SkillResultRpc(byte slot, byte result, float cooldownSeconds)
        {
            var r = (SkillResult)result;
            if (r == SkillResult.Fired)
            {
                if (slot < cooldownEndsAt.Length) cooldownEndsAt[slot] = Time.unscaledTime + cooldownSeconds;
            }
            else if (r == SkillResult.NoMana) Say("마나가 부족합니다", 1.2f);
            else if (r == SkillResult.Cooldown) Say("아직 사용할 수 없습니다", 1.2f);

            if (DevArgs.GrowthLog) Debug.Log($"[BS] skill result slot={slot} result={r} cooldown={cooldownSeconds:F1}");
        }

        void Say(string text, float seconds)
        {
            notice = text;
            noticeUntil = Time.unscaledTime + seconds;
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner) return;
            if (avatar == null || avatar.Role == RoleTable.BossRole) return;
            if (!RoleTable.MatchRunning) return; // the match is over: no skills, no choices

            ReadInput();
            RunDevAutomation();
        }

        void ReadInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.qKey.wasPressedThisFrame) RequestSkill(0);
            if (kb.eKey.wasPressedThisFrame) RequestSkill(1);
            if (kb.rKey.wasPressedThisFrame) RequestSkill(SkillCatalog.UltimateSlot);

            // The number keys only mean something while a choice is open.
            if (!offerOpen) return;
            if (kb.digit1Key.wasPressedThisFrame) RequestPick(0);
            else if (kb.digit2Key.wasPressedThisFrame) RequestPick(1);
            else if (kb.digit3Key.wasPressedThisFrame) RequestPick(2);
        }

        void RequestSkill(int slot)
        {
            if (IsServer) GrowthServer.Instance?.OnSkillRequest(OwnerClientId, slot);
            else SkillRpc((byte)slot);
        }

        void RequestPick(int index)
        {
            if (!offerOpen) return;
            if (IsServer) GrowthServer.Instance?.OnPick(OwnerClientId, offerId, index);
            else PickRpc(offerId, (byte)index);
        }

        void RunDevAutomation()
        {
            if (autoPickAt >= 0f && offerOpen && Time.unscaledTime >= autoPickAt)
            {
                autoPickAt = -1f;
                RequestPick(DevArgs.AutoPickIndex - 1);
            }

            if (DevArgs.AutoSkillKeys != 0 && Time.unscaledTime >= nextAutoSkillAt)
            {
                nextAutoSkillAt = Time.unscaledTime + DevArgs.AutoSkillInterval;
                for (int slot = 0; slot < SkillCatalog.SkillSlots; slot++)
                    if ((DevArgs.AutoSkillKeys & (1 << slot)) != 0) RequestSkill(slot); // -bsAutoSkill qe presses Q and E, qer all three
            }
        }
    }
}
