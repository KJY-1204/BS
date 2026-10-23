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
            /// <summary>The share of each hit this survivor takes (a damage-reduction passive lowers it).</summary>
            public float DamageTaken = 1f;
            /// <summary>How many times this survivor has fallen and been revived since it was registered (for the match summary).</summary>
            public int Falls;
            public int Revives;
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

        /// <summary>How many times the survivor has fallen (health taken to zero) since it was registered; zero for an unknown survivor.</summary>
        public int FallsOf(ulong clientId) => states.TryGetValue(clientId, out State s) ? s.Falls : 0;

        /// <summary>How many times the survivor has been revived since it was registered; zero for an unknown survivor.</summary>
        public int RevivesOf(ulong clientId) => states.TryGetValue(clientId, out State s) ? s.Revives : 0;

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

        /// <summary>How many registered survivors are alive (a survivor who left is no longer registered).</summary>
        public int AliveCount
        {
            get
            {
                int n = 0;
                foreach (State s in states.Values) if (s.Health > 0) n++;
                return n;
            }
        }

        public int Health(ulong clientId) => states.TryGetValue(clientId, out State s) ? s.Health : 0;

        public bool IsAlive(ulong clientId) => states.TryGetValue(clientId, out State s) && s.Health > 0;

        public bool IsInvulnerable(ulong clientId) => states.TryGetValue(clientId, out State s) && s.Invulnerable > 0f;

        /// <summary>Revive progress from 0 to 1; only meaningful for a dead survivor.</summary>
        public float ReviveGauge(ulong clientId) => states.TryGetValue(clientId, out State s) ? s.ReviveGauge : 0f;

        /// <summary>Sets the share of each hit this survivor takes: 1 is all of it, less is a damage-reduction passive. Unknown survivors are ignored.</summary>
        public void SetDamageTakenMultiplier(ulong clientId, float multiplier)
        {
            if (states.TryGetValue(clientId, out State s)) s.DamageTaken = Mathf.Clamp(multiplier, 0f, 1f);
        }

        /// <summary>
        /// Deals damage unless the survivor is dead, protected, or unknown. A hit starts the protection time.
        /// A survivor with a damage-reduction passive takes less, but never less than one. Returns true when damage was dealt.
        /// </summary>
        public bool ApplyDamage(ulong clientId, int damage)
        {
            if (damage <= 0) return false;
            if (!states.TryGetValue(clientId, out State s)) return false;
            if (s.Health <= 0 || s.Invulnerable > 0f) return false;

            if (s.DamageTaken < 1f) damage = Math.Max(1, (int)Math.Round(damage * s.DamageTaken, MidpointRounding.AwayFromZero));
            s.Health = Math.Max(0, s.Health - damage);
            s.Invulnerable = InvulnerabilitySeconds;
            if (s.Health == 0)
            {
                s.ReviveGauge = 0f;
                s.Falls++;
            }
            return true;
        }

        /// <summary>
        /// A blast: every living survivor whose center is within the radius of the point takes the damage through the usual rule, so a
        /// protected or fallen survivor takes none. Returns how many survivors were hurt.
        /// </summary>
        public int ApplyBlast(Vector2 center, float radius, int damage, IReadOnlyList<SurvivorBody> bodies)
        {
            int hurt = 0;
            float limit = radius * radius;
            for (int i = 0; i < bodies.Count; i++)
            {
                SurvivorBody body = bodies[i];
                if ((body.Position - center).sqrMagnitude > limit) continue;
                if (ApplyDamage(body.ClientId, damage)) hurt++;
            }
            return hurt;
        }

        /// <summary>
        /// Restores health up to this survivor's maximum. A dead or unknown survivor is not healed.
        /// Returns how much was actually restored.
        /// </summary>
        public int Heal(ulong clientId, int amount)
        {
            if (amount <= 0) return 0;
            if (!states.TryGetValue(clientId, out State s) || s.Health <= 0) return 0;

            int healed = Math.Min(amount, s.MaxHealth - s.Health);
            if (healed <= 0) return 0;
            s.Health += healed;
            return healed;
        }

        /// <summary>
        /// Makes a living survivor unhurtable for a time. A longer protection already running is not shortened.
        /// </summary>
        public bool GrantInvulnerability(ulong clientId, float seconds)
        {
            if (seconds <= 0f) return false;
            if (!states.TryGetValue(clientId, out State s) || s.Health <= 0) return false;
            if (seconds > s.Invulnerable) s.Invulnerable = seconds;
            return true;
        }

        /// <summary>Sets health to zero at once (development scenarios and tests).</summary>
        public void Kill(ulong clientId)
        {
            if (!states.TryGetValue(clientId, out State s)) return;
            if (s.Health > 0) s.Falls++;
            s.Health = 0;
            s.Invulnerable = 0f;
            s.ReviveGauge = 0f;
        }

        /// <summary>
        /// Advances protection timers, applies mob contact damage and runs revival.
        /// <paramref name="bodies"/> lists every survivor (dead ones included) and never the boss.
        /// </summary>
        public void Step(float dt, IReadOnlyList<SurvivorBody> bodies, MobSim mobs) => Step(dt, bodies, mobs, null);

        /// <summary>The same step with a boss that hurts survivors it touches (contact damage of its own).</summary>
        public void Step(float dt, IReadOnlyList<SurvivorBody> bodies, MobSim mobs, BossSim boss)
        {
            scratch.Clear();
            foreach (KeyValuePair<ulong, State> pair in states)
                if (pair.Value.Invulnerable > 0f) scratch.Add(pair.Key);
            for (int i = 0; i < scratch.Count; i++)
            {
                State s = states[scratch[i]];
                s.Invulnerable = Math.Max(0f, s.Invulnerable - dt);
            }

            // The boss first: it hits harder, and a survivor takes only one hit until the protection ends.
            ApplyBossContactDamage(bodies, boss);
            ApplyContactDamage(bodies, mobs);
            StepRevival(dt, bodies);
        }

        /// <summary>A living boss deals its own contact damage to a living survivor it overlaps. It is not stopped by time stop.</summary>
        void ApplyBossContactDamage(IReadOnlyList<SurvivorBody> bodies, BossSim boss)
        {
            if (boss == null || !boss.CanBeHit) return;

            float reach = Radius + boss.Radius;
            float reachSq = reach * reach;
            for (int i = 0; i < bodies.Count; i++)
            {
                SurvivorBody body = bodies[i];
                if (!IsAlive(body.ClientId)) continue;
                if ((body.Position - boss.Position).sqrMagnitude <= reachSq) ApplyDamage(body.ClientId, boss.ContactDamage);
            }
        }

        void ApplyContactDamage(IReadOnlyList<SurvivorBody> bodies, MobSim mobs)
        {
            if (mobs == null || mobs.IsFrozen) return; // stopped mobs do not hurt

            for (int i = 0; i < bodies.Count; i++)
            {
                SurvivorBody body = bodies[i];
                if (!IsAlive(body.ClientId)) continue;

                // One hit per protection time, as hard as the hardest mob touching.
                if (mobs.MaxContactMultipleWithin(body.Position, Radius, out float multiple))
                    ApplyDamage(body.ClientId, Mathf.Max(1, Mathf.RoundToInt(ContactDamage * multiple)));
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
                    s.Revives++;
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
