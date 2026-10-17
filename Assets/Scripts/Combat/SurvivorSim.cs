using System;
using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>What the survivor simulation needs to know about one survivor each step.</summary>
    public struct SurvivorBody
    {
        public ulong ClientId;
        public Vector2 Position;
        /// <summary>True while the survivor's player is giving move input.</summary>
        public bool Moving;
    }

    /// <summary>
    /// Server-side survivor rules as plain data: health, damage, the short protection after a hit, death,
    /// contact damage from mobs and revival by a teammate. The boss is never part of this simulation.
    /// </summary>
    public sealed class SurvivorSim
    {
        sealed class State
        {
            public int Health;
            public int MaxHealth;
            public float Invulnerable;
            public float ReviveGauge;
        }

        /// <summary>Maximum health a survivor starts with; each survivor's own maximum can then change.</summary>
        public int MaxHealth = CombatConfig.SurvivorMaxHealth;
        public int ContactDamage = CombatConfig.ContactDamage;
        public float InvulnerabilitySeconds = CombatConfig.InvulnerabilitySeconds;
        public float Radius = CombatConfig.SurvivorRadius;
        public float ReviveRange = CombatConfig.ReviveRange;
        public float ReviveSeconds = CombatConfig.ReviveSeconds;
        public float ReviveHealthFraction = CombatConfig.ReviveHealthFraction;
        public float ReviveInvulnerabilitySeconds = CombatConfig.ReviveInvulnerabilitySeconds;

        readonly Dictionary<ulong, State> states = new Dictionary<ulong, State>();
        readonly List<ulong> scratch = new List<ulong>();

        public int Count => states.Count;

        public void Register(ulong clientId)
        {
            states[clientId] = new State { Health = MaxHealth, MaxHealth = MaxHealth };
        }

        /// <summary>Registers a survivor that starts with full health of its own maximum.</summary>
        public void Register(ulong clientId, int maxHealth)
        {
            int max = Math.Max(1, maxHealth);
            states[clientId] = new State { Health = max, MaxHealth = max };
        }

        /// <summary>This survivor's own maximum health (the default for unknown survivors).</summary>
        public int MaxHealthOf(ulong clientId) => states.TryGetValue(clientId, out State s) ? s.MaxHealth : MaxHealth;

        /// <summary>
        /// Changes one survivor's maximum health. Current health is not healed when the maximum rises,
        /// and is only cut down if the maximum falls below it.
        /// </summary>
        public void SetMaxHealth(ulong clientId, int maxHealth)
        {
            if (!states.TryGetValue(clientId, out State s)) return;
            s.MaxHealth = Math.Max(1, maxHealth);
            if (s.Health > s.MaxHealth) s.Health = s.MaxHealth;
        }

        public void Unregister(ulong clientId) => states.Remove(clientId);

        public void Clear() => states.Clear();

        public bool Contains(ulong clientId) => states.ContainsKey(clientId);

        public int Health(ulong clientId) => states.TryGetValue(clientId, out State s) ? s.Health : 0;

        public bool IsAlive(ulong clientId) => states.TryGetValue(clientId, out State s) && s.Health > 0;

        public bool IsInvulnerable(ulong clientId) => states.TryGetValue(clientId, out State s) && s.Invulnerable > 0f;

        /// <summary>Revive progress from 0 to 1; only meaningful for a dead survivor.</summary>
        public float ReviveGauge(ulong clientId) => states.TryGetValue(clientId, out State s) ? s.ReviveGauge : 0f;

        /// <summary>
        /// Deals damage unless the survivor is dead, protected, or unknown. A hit starts the protection time.
        /// Returns true when damage was dealt.
        /// </summary>
        public bool ApplyDamage(ulong clientId, int damage)
        {
            if (damage <= 0) return false;
            if (!states.TryGetValue(clientId, out State s)) return false;
            if (s.Health <= 0 || s.Invulnerable > 0f) return false;

            s.Health = Math.Max(0, s.Health - damage);
            s.Invulnerable = InvulnerabilitySeconds;
            if (s.Health == 0) s.ReviveGauge = 0f;
            return true;
        }

        /// <summary>Sets health to zero at once (development scenarios and tests).</summary>
        public void Kill(ulong clientId)
        {
            if (!states.TryGetValue(clientId, out State s)) return;
            s.Health = 0;
            s.Invulnerable = 0f;
            s.ReviveGauge = 0f;
        }

        /// <summary>
        /// Advances protection timers, applies mob contact damage and runs revival.
        /// <paramref name="bodies"/> lists every survivor (dead ones included) and never the boss.
        /// </summary>
        public void Step(float dt, IReadOnlyList<SurvivorBody> bodies, MobSim mobs)
        {
            scratch.Clear();
            foreach (KeyValuePair<ulong, State> pair in states)
                if (pair.Value.Invulnerable > 0f) scratch.Add(pair.Key);
            for (int i = 0; i < scratch.Count; i++)
            {
                State s = states[scratch[i]];
                s.Invulnerable = Math.Max(0f, s.Invulnerable - dt);
            }

            ApplyContactDamage(bodies, mobs);
            StepRevival(dt, bodies);
        }

        void ApplyContactDamage(IReadOnlyList<SurvivorBody> bodies, MobSim mobs)
        {
            if (mobs == null) return;

            float reach = Radius + mobs.Radius;
            for (int i = 0; i < bodies.Count; i++)
            {
                SurvivorBody body = bodies[i];
                if (!IsAlive(body.ClientId)) continue;
                if (mobs.AnyWithin(body.Position, reach)) ApplyDamage(body.ClientId, ContactDamage);
            }
        }

        void StepRevival(float dt, IReadOnlyList<SurvivorBody> bodies)
        {
            for (int i = 0; i < bodies.Count; i++)
            {
                SurvivorBody dead = bodies[i];
                if (!states.TryGetValue(dead.ClientId, out State s) || s.Health > 0) continue;

                if (!HasStationaryRescuerInRange(dead, bodies))
                {
                    s.ReviveGauge = 0f;
                    continue;
                }

                s.ReviveGauge += ReviveSeconds > 0f ? dt / ReviveSeconds : 1f;
                if (s.ReviveGauge >= 1f)
                {
                    s.Health = Math.Max(1, Mathf.CeilToInt(s.MaxHealth * ReviveHealthFraction));
                    s.Invulnerable = ReviveInvulnerabilitySeconds;
                    s.ReviveGauge = 0f;
                }
            }
        }

        bool HasStationaryRescuerInRange(SurvivorBody dead, IReadOnlyList<SurvivorBody> bodies)
        {
            float rangeSq = ReviveRange * ReviveRange;
            for (int j = 0; j < bodies.Count; j++)
            {
                SurvivorBody other = bodies[j];
                if (other.ClientId == dead.ClientId) continue;
                if (other.Moving) continue;
                if (!IsAlive(other.ClientId)) continue;
                if ((other.Position - dead.Position).sqrMagnitude <= rangeSq) return true;
            }
            return false;
        }
    }
}
