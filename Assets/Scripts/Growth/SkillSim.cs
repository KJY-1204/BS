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
        public SkillEffectKind Effect;
        public int Slot;
        public int Rank;
        public float Radius;
        public int Damage;
        /// <summary>How long a timed effect lasts (attack speed, barrier, time stop).</summary>
        public float BuffSeconds;
        public float CooldownMultiplier;
        public float Cooldown;
        /// <summary>Beam length and half width.</summary>
        public float Length;
        public float HalfWidth;
        /// <summary>Projectiles in a ring.</summary>
        public int Count;
        /// <summary>How far mobs are pushed.</summary>
        public float Distance;
        /// <summary>Health restored.</summary>
        public int Amount;
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
        public SkillResult TryActivate(ulong clientId, byte character, int slot, bool alive, IReadOnlyList<int> ranks, out SkillActivation activation)
        {
            activation = default;
            if (!states.TryGetValue(clientId, out State s)) return SkillResult.UnknownSlot;

            SkillDef def = SkillCatalog.ForSlot(slot, character);
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
                Effect = def.Effect,
                Slot = slot,
                Rank = rank,
                Radius = def.Radius != null ? def.Radius[rank - 1] : 0f,
                Damage = def.Damage != null ? def.Damage[rank - 1] : 0,
                BuffSeconds = def.Duration != null ? def.Duration[rank - 1] : 0f,
                CooldownMultiplier = def.CooldownMultiplier != null ? def.CooldownMultiplier[rank - 1] : 1f,
                Cooldown = def.Cooldown,
                Length = def.Length != null ? def.Length[rank - 1] : 0f,
                HalfWidth = def.HalfWidth != null ? def.HalfWidth[rank - 1] : 0f,
                Count = def.Count != null ? def.Count[rank - 1] : 0,
                Distance = def.Distance != null ? def.Distance[rank - 1] : 0f,
                Amount = def.Amount != null ? def.Amount[rank - 1] : 0,
            };

            if (def.Effect == SkillEffectKind.AttackSpeed && activation.BuffSeconds > 0f)
            {
                s.BuffLeft = activation.BuffSeconds;
                s.BuffMultiplier = activation.CooldownMultiplier;
            }
            return SkillResult.Fired;
        }
    }

    /// <summary>Everything a fired skill's effect may need to touch. Unused parts can stay null.</summary>
    public struct SkillContext
    {
        public ulong CasterId;
        public Vector2 Origin;
        /// <summary>The caster's aim when the skill fired (beam direction, where a ring of projectiles starts).</summary>
        public ushort AimAngle;
        public MobSim Mobs;
        public ProjectileSim Projectiles;
        public SurvivorSim Survivors;
        /// <summary>Every survivor in the room with position and whether alive (the caster included), for healing.</summary>
        public IReadOnlyList<GrowthBody> Bodies;
        /// <summary>The shape of the caster's basic shots, which a ring of projectiles uses.</summary>
        public ProjectileShape Shape;
        /// <summary>Where killed mobs died, so they can give experience.</summary>
        public List<Vector2> Deaths;
    }

    /// <summary>Applies the effect of a fired skill. Pure logic so it can be tested without a scene.</summary>
    public static class SkillExecutor
    {
        /// <summary>
        /// Applies the skill and returns how many things it touched: mobs damaged or pushed, survivors healed or
        /// protected, projectiles fired, or 1 for stopping time. Skills whose effect lives elsewhere (the attack
        /// speed boost belongs to <see cref="SkillSim"/>) touch nothing here and return 0.
        /// </summary>
        public static int Apply(SkillActivation activation, in SkillContext context)
        {
            switch (activation.Effect)
            {
                case SkillEffectKind.AreaDamage:
                    return context.Mobs == null ? 0 : context.Mobs.ApplyAreaDamage(context.Origin, activation.Radius, activation.Damage, context.Deaths);

                case SkillEffectKind.Beam:
                    return context.Mobs == null
                        ? 0
                        : context.Mobs.ApplyLineDamage(context.Origin, AimMath.Direction(context.AimAngle), activation.Length, activation.HalfWidth, activation.Damage, context.Deaths);

                case SkillEffectKind.Barrier:
                    return context.Survivors != null && context.Survivors.GrantInvulnerability(context.CasterId, activation.BuffSeconds) ? 1 : 0;

                case SkillEffectKind.TimeStop:
                    if (context.Mobs == null || activation.BuffSeconds <= 0f) return 0;
                    context.Mobs.Freeze(activation.BuffSeconds);
                    return 1;

                case SkillEffectKind.Volley:
                    return FireRing(activation, context);

                case SkillEffectKind.Heal:
                    return Heal(activation, context);

                case SkillEffectKind.Push:
                    return context.Mobs == null ? 0 : context.Mobs.Push(context.Origin, activation.Radius, activation.Distance);

                default:
                    return 0;
            }
        }

        /// <summary>
        /// The area burst only, kept for callers that have nothing but mobs: damages every mob in the radius around
        /// <paramref name="origin"/> and appends where killed mobs died to <paramref name="deaths"/>.
        /// </summary>
        public static int Apply(SkillActivation activation, Vector2 origin, MobSim mobs, List<Vector2> deaths)
        {
            if (activation.Effect != SkillEffectKind.AreaDamage || mobs == null) return 0;
            return mobs.ApplyAreaDamage(origin, activation.Radius, activation.Damage, deaths);
        }

        static int FireRing(SkillActivation activation, in SkillContext context)
        {
            if (context.Projectiles == null || activation.Count <= 0) return 0;

            int fired = 0;
            for (int i = 0; i < activation.Count; i++)
            {
                var spec = new ProjectileSpec
                {
                    Owner = context.CasterId,
                    Origin = context.Origin,
                    Angle = AimMath.Offset(context.AimAngle, 360f * i / activation.Count),
                    Damage = activation.Damage,
                    Shape = context.Shape,
                };
                if (!context.Projectiles.TryFire(spec, out _)) break; // the cap: the ones that fit fire
                fired++;
            }
            return fired;
        }

        static int Heal(SkillActivation activation, in SkillContext context)
        {
            if (context.Survivors == null || context.Bodies == null) return 0;

            float radiusSq = activation.Radius * activation.Radius;
            int healed = 0;
            for (int i = 0; i < context.Bodies.Count; i++)
            {
                GrowthBody body = context.Bodies[i];
                if (!body.Alive) continue;
                bool isCaster = body.ClientId == context.CasterId;
                if (!isCaster && (body.Position - context.Origin).sqrMagnitude > radiusSq) continue;
                if (context.Survivors.Heal(body.ClientId, activation.Amount) > 0) healed++;
            }
            return healed;
        }
    }
}
