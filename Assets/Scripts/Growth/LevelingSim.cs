using System;
using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>What the leveling simulation needs to know about one survivor when experience is handed out.</summary>
    public struct GrowthBody
    {
        public ulong ClientId;
        public Vector2 Position;
        public bool Alive;
    }

    public enum GrowthEventKind : byte
    {
        LevelUp = 1,
        OfferOpened = 2,
        OfferClosed = 3,
    }

    /// <summary>Something the server must tell a player about: a level up or a skill choice opening or closing.</summary>
    public struct GrowthEvent
    {
        public GrowthEventKind Kind;
        public ulong ClientId;
        /// <summary>LevelUp: the level reached.</summary>
        public int Level;
        public byte OfferId;
        /// <summary>OfferOpened: the offered skill ids, in order.</summary>
        public byte[] Choices;
        /// <summary>OfferClosed: the skill that gained a rank.</summary>
        public byte Skill;
        /// <summary>OfferClosed: true when the time ran out and the server picked.</summary>
        public bool Auto;
        /// <summary>OfferOpened: time allowed to choose.</summary>
        public float Seconds;
    }

    /// <summary>
    /// Server-side growth rules as plain data: experience shared by nearby survivors, levels and their cap,
    /// learned skill ranks, and the queue of skill choices each level up opens. A choice that is not made
    /// in time is made at random.
    /// </summary>
    public sealed class LevelingSim
    {
        sealed class State
        {
            public int Level = 1;
            public int Xp;
            public readonly int[] Ranks = new int[SkillCatalog.MaxSkillId + 1];
            public int Pending;
            public bool HasOffer;
            public byte OfferId;
            public readonly byte[] Choices = new byte[GrowthConfig.OfferChoices];
            public int ChoiceCount;
            public float TimeLeft;
            public byte LastOfferId;
            /// <summary>The character this survivor plays; decides which skills can be offered.</summary>
            public byte Character;
        }

        public int MaxLevel = GrowthConfig.MaxLevel;
        public int XpPerKill = GrowthConfig.XpPerKill;
        public int XpPerDevice = GrowthConfig.XpPerDevice;
        public float ShareDistance = GrowthConfig.XpShareDistance;
        public float OfferSeconds = GrowthConfig.OfferSeconds;

        readonly Dictionary<ulong, State> states = new Dictionary<ulong, State>();
        readonly List<GrowthEvent> events = new List<GrowthEvent>();
        readonly List<SkillDef> available = new List<SkillDef>();
        readonly List<ulong> order = new List<ulong>();
        readonly System.Random rng;

        public LevelingSim(System.Random random = null)
        {
            rng = random ?? new System.Random();
        }

        public int Count => states.Count;

        /// <summary>Adds a survivor of a character. Only common skills and that character's own can be offered.</summary>
        public void Register(ulong clientId, byte character) => states[clientId] = new State { Character = character };

        public byte CharacterOf(ulong clientId) => states.TryGetValue(clientId, out State s) ? s.Character : CharacterIds.None;

        public void Unregister(ulong clientId) => states.Remove(clientId);

        public void Clear()
        {
            states.Clear();
            events.Clear();
        }

        public bool Contains(ulong clientId) => states.ContainsKey(clientId);

        public int Level(ulong clientId) => states.TryGetValue(clientId, out State s) ? s.Level : 1;

        public int Xp(ulong clientId) => states.TryGetValue(clientId, out State s) ? s.Xp : 0;

        public int XpNeeded(ulong clientId) => Level(clientId) >= MaxLevel ? 0 : GrowthConfig.XpNeeded(Level(clientId));

        /// <summary>Learned ranks indexed by skill id. Unknown survivors have none.</summary>
        public IReadOnlyList<int> Ranks(ulong clientId) =>
            states.TryGetValue(clientId, out State s) ? s.Ranks : EmptyRanks;

        static readonly int[] EmptyRanks = new int[SkillCatalog.MaxSkillId + 1];

        /// <summary>Development only: sets a skill's rank directly, without a choice.</summary>
        public void GrantRank(ulong clientId, byte skillId, int rank)
        {
            if (!states.TryGetValue(clientId, out State s) || skillId == 0 || skillId > SkillCatalog.MaxSkillId) return;
            s.Ranks[skillId] = Math.Max(0, Math.Min(SkillCatalog.MaxPackedRank, rank));
        }

        public int RankOf(ulong clientId, byte skillId) => SkillCatalog.RankOf(Ranks(clientId), skillId);

        /// <summary>Choices still waiting behind the one that is open.</summary>
        public int PendingOffers(ulong clientId) => states.TryGetValue(clientId, out State s) ? s.Pending : 0;

        public bool HasOffer(ulong clientId) => states.TryGetValue(clientId, out State s) && s.HasOffer;

        public byte CurrentOfferId(ulong clientId) => states.TryGetValue(clientId, out State s) && s.HasOffer ? s.OfferId : (byte)0;

        public float OfferTimeLeft(ulong clientId) => states.TryGetValue(clientId, out State s) && s.HasOffer ? s.TimeLeft : 0f;

        public byte[] CurrentChoices(ulong clientId)
        {
            if (!states.TryGetValue(clientId, out State s) || !s.HasOffer) return new byte[0];
            var copy = new byte[s.ChoiceCount];
            Array.Copy(s.Choices, copy, s.ChoiceCount);
            return copy;
        }

        public void DrainEvents(List<GrowthEvent> into)
        {
            into.AddRange(events);
            events.Clear();
        }

        // ---- Experience ----

        /// <summary>
        /// A mob died at <paramref name="deathPosition"/>: every living survivor within the share distance
        /// gets the full experience, whoever dealt the damage. Returns how many received it.
        /// </summary>
        public int AwardKill(Vector2 deathPosition, IReadOnlyList<GrowthBody> bodies) => AwardShared(deathPosition, bodies, XpPerKill);

        /// <summary>A boss device was destroyed at <paramref name="deathPosition"/>: shared like a kill, with the device's own base amount.</summary>
        public int AwardDeviceKill(Vector2 deathPosition, IReadOnlyList<GrowthBody> bodies) => AwardShared(deathPosition, bodies, XpPerDevice);

        int AwardShared(Vector2 deathPosition, IReadOnlyList<GrowthBody> bodies, int baseAmount)
        {
            float limit = ShareDistance * ShareDistance;
            int recipients = 0;
            for (int i = 0; i < bodies.Count; i++)
            {
                GrowthBody body = bodies[i];
                if (!body.Alive || !states.ContainsKey(body.ClientId)) continue;
                if ((body.Position - deathPosition).sqrMagnitude > limit) continue;

                State recipient = states[body.ClientId];
                AddXp(body.ClientId, SkillCatalog.EffectiveExperience(baseAmount, recipient.Ranks, recipient.Character));
                recipients++;
            }
            return recipients;
        }

        /// <summary>Gives experience directly. Levels are gained while the threshold is reached; the surplus carries over.</summary>
        public void AddXp(ulong clientId, int amount)
        {
            if (amount <= 0 || !states.TryGetValue(clientId, out State s)) return;
            if (s.Level >= MaxLevel) return;

            s.Xp += amount;
            while (s.Level < MaxLevel && s.Xp >= GrowthConfig.XpNeeded(s.Level))
            {
                s.Xp -= GrowthConfig.XpNeeded(s.Level);
                s.Level++;
                s.Pending++;
                events.Add(new GrowthEvent { Kind = GrowthEventKind.LevelUp, ClientId = clientId, Level = s.Level });
            }
            if (s.Level >= MaxLevel) s.Xp = 0;

            if (!s.HasOffer) OpenNextOffer(clientId, s);
        }

        // ---- Skill choices ----

        /// <summary>Advances every open choice; one that runs out of time is decided at random.</summary>
        public void Step(float dt)
        {
            order.Clear();
            foreach (KeyValuePair<ulong, State> pair in states)
                if (pair.Value.HasOffer) order.Add(pair.Key);

            for (int i = 0; i < order.Count; i++)
            {
                ulong id = order[i];
                if (!states.TryGetValue(id, out State s) || !s.HasOffer) continue;

                s.TimeLeft -= dt;
                if (s.TimeLeft > 0f) continue;

                int index = rng.Next(s.ChoiceCount);
                Resolve(id, s, index, auto: true);
            }
        }

        /// <summary>
        /// The player's pick. Ignored unless a choice is open, it is the one being answered, and the number is
        /// one of the offered ones. Returns true when it was accepted.
        /// </summary>
        public bool Pick(ulong clientId, byte offerId, int index)
        {
            if (!states.TryGetValue(clientId, out State s)) return false;
            if (!s.HasOffer || offerId == 0 || offerId != s.OfferId) return false;
            if (index < 0 || index >= s.ChoiceCount) return false;

            Resolve(clientId, s, index, auto: false);
            return true;
        }

        void Resolve(ulong clientId, State s, int index, bool auto)
        {
            byte skill = s.Choices[index];
            if (s.Ranks[skill] < SkillCatalog.MaxPackedRank) s.Ranks[skill]++;

            events.Add(new GrowthEvent
            {
                Kind = GrowthEventKind.OfferClosed, ClientId = clientId, OfferId = s.OfferId, Skill = skill, Auto = auto,
            });
            s.HasOffer = false;
            OpenNextOffer(clientId, s);
        }

        void OpenNextOffer(ulong clientId, State s)
        {
            while (s.Pending > 0 && !s.HasOffer)
            {
                s.Pending--;

                available.Clear();
                IReadOnlyList<SkillDef> all = SkillCatalog.All;
                for (int i = 0; i < all.Count; i++)
                    if (SkillCatalog.IsAvailable(all[i], s.Ranks, s.Character)) available.Add(all[i]);

                if (available.Count == 0) continue; // nothing left to learn: the level up is not held up

                int count = Math.Min(GrowthConfig.OfferChoices, available.Count);
                for (int k = 0; k < count; k++)
                {
                    int pick = k + rng.Next(available.Count - k);
                    SkillDef chosen = available[pick];
                    available[pick] = available[k];
                    available[k] = chosen;
                    s.Choices[k] = chosen.Id;
                }

                s.ChoiceCount = count;
                s.LastOfferId = s.LastOfferId == byte.MaxValue ? (byte)1 : (byte)(s.LastOfferId + 1);
                s.OfferId = s.LastOfferId;
                s.TimeLeft = OfferSeconds;
                s.HasOffer = true;

                var choices = new byte[count];
                Array.Copy(s.Choices, choices, count);
                events.Add(new GrowthEvent
                {
                    Kind = GrowthEventKind.OfferOpened, ClientId = clientId, OfferId = s.OfferId, Choices = choices, Seconds = OfferSeconds,
                });
            }
        }
    }
}
