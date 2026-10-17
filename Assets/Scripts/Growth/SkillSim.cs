using System;
using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    public enum SkillResult : byte
    {
        Fired = 0,
        NotLearned = 1,
        Dead = 2,
        Cooldown = 3,
        NoMana = 4,
        UnknownSlot = 5,
    }

    /// <summary>The numbers of one skill activation at the learned rank.</summary>
    public struct SkillActivation
    {
        public byte SkillId;
        public int Slot;
        public int Rank;
        public float Radius;
        public int Damage;
        public float BuffSeconds;
        public float CooldownMultiplier;
        public float Cooldown;
    }

    /// <summary>
    /// Server-side mana and key skills as plain data: mana refills with time, a skill fires only for a living
    /// survivor who has learned it, is off cooldown and has the mana. Applying a skill's effect on mobs is
    /// left to the caller; the temporary basic attack speedup lives here as a timed multiplier.
    /// </summary>
    public sealed class SkillSim
    {
        sealed class State
        {
            public float Mana;
            public float MaxMana;
            public float Regen;
            public readonly float[] Cooldown = new float[SkillCatalog.SkillSlots];
            public float BuffLeft;
            public float BuffMultiplier = 1f;
        }

        readonly Dictionary<ulong, State> states = new Dictionary<ulong, State>();

        public int Count => states.Count;

        /// <summary>Adds a survivor with full mana.</summary>
        public void Register(ulong clientId, int maxMana, float regenPerSecond)
        {
            states[clientId] = new State { Mana = maxMana, MaxMana = maxMana, Regen = regenPerSecond };
        }

        public void Unregister(ulong clientId) => states.Remove(clientId);

        public void Clear() => states.Clear();

        public bool Contains(ulong clientId) => states.ContainsKey(clientId);

        /// <summary>Changes the maximum and refill rate. Current mana is not refilled, only cut down if it is above a lower maximum.</summary>
        public void SetStats(ulong clientId, int maxMana, float regenPerSecond)
        {
            if (!states.TryGetValue(clientId, out State s)) return;
            s.MaxMana = maxMana;
            s.Regen = regenPerSecond;
            if (s.Mana > s.MaxMana) s.Mana = s.MaxMana;
        }

        public float Mana(ulong clientId) => states.TryGetValue(clientId, out State s) ? s.Mana : 0f;

        public float MaxMana(ulong clientId) => states.TryGetValue(clientId, out State s) ? s.MaxMana : 0f;

        public float CooldownLeft(ulong clientId, int slot) =>
            states.TryGetValue(clientId, out State s) && slot >= 0 && slot < s.Cooldown.Length ? s.Cooldown[slot] : 0f;

        public float BuffLeft(ulong clientId) => states.TryGetValue(clientId, out State s) ? s.BuffLeft : 0f;

        /// <summary>Multiplier on the basic attack cooldown from an active Rapid Fire (1 when none).</summary>
        public float AttackMultiplier(ulong clientId) =>
            states.TryGetValue(clientId, out State s) && s.BuffLeft > 0f ? s.BuffMultiplier : 1f;

        public void Step(float dt)
        {
            foreach (State s in states.Values)
            {
                s.Mana = Math.Min(s.MaxMana, s.Mana + s.Regen * dt);
                for (int i = 0; i < s.Cooldown.Length; i++)
                    s.Cooldown[i] = Math.Max(0f, s.Cooldown[i] - dt);
                if (s.BuffLeft > 0f) s.BuffLeft = Math.Max(0f, s.BuffLeft - dt);
            }
        }

        /// <summary>A dead survivor loses any running Rapid Fire.</summary>
        public void EndBuff(ulong clientId)
        {
            if (states.TryGetValue(clientId, out State s)) s.BuffLeft = 0f;
        }

        /// <summary>
        /// The player asked for a skill slot. Fires it, spending mana and starting the cooldown, only when
        /// every condition holds; the result says why not otherwise.
        /// </summary>
        public SkillResult TryActivate(ulong clientId, int slot, bool alive, IReadOnlyList<int> ranks, out SkillActivation activation)
        {
            activation = default;
            if (!states.TryGetValue(clientId, out State s)) return SkillResult.UnknownSlot;

            SkillDef def = SkillCatalog.ForSlot(slot);
            if (def == null) return SkillResult.UnknownSlot;
            if (!alive) return SkillResult.Dead;

            int rank = SkillCatalog.RankOf(ranks, def.Id);
            if (rank <= 0) return SkillResult.NotLearned;
            if (s.Cooldown[slot] > 0f) return SkillResult.Cooldown;
            if (s.Mana < def.ManaCost) return SkillResult.NoMana;

            s.Mana -= def.ManaCost;
            s.Cooldown[slot] = def.Cooldown;

            activation = new SkillActivation
            {
                SkillId = def.Id,
                Slot = slot,
                Rank = rank,
                Radius = def.Radius != null ? def.Radius[rank - 1] : 0f,
                Damage = def.Damage != null ? def.Damage[rank - 1] : 0,
                BuffSeconds = def.Duration != null ? def.Duration[rank - 1] : 0f,
                CooldownMultiplier = def.CooldownMultiplier != null ? def.CooldownMultiplier[rank - 1] : 1f,
                Cooldown = def.Cooldown,
            };

            if (activation.BuffSeconds > 0f)
            {
                s.BuffLeft = activation.BuffSeconds;
                s.BuffMultiplier = activation.CooldownMultiplier;
            }
            return SkillResult.Fired;
        }
    }

    /// <summary>Applies the effect of a fired skill to the mobs. Pure logic so it can be tested without a scene.</summary>
    public static class SkillExecutor
    {
        /// <summary>
        /// Burst damages every mob in its radius around <paramref name="origin"/>; other skills have no area
        /// effect. Returns how many mobs were hit and appends where killed mobs died to <paramref name="deaths"/>.
        /// </summary>
        public static int Apply(SkillActivation activation, Vector2 origin, MobSim mobs, List<Vector2> deaths)
        {
            if (activation.SkillId != SkillIds.Burst || mobs == null) return 0;
            return mobs.ApplyAreaDamage(origin, activation.Radius, activation.Damage, deaths);
        }
    }
}
