using System;
using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>What the attack simulation needs to know about one shooter each step.</summary>
    public struct ShooterBody
    {
        public ulong ClientId;
        public Vector2 Position;
        /// <summary>False for the boss and for dead survivors.</summary>
        public bool CanFire;
    }

    /// <summary>
    /// Server-side basic attack: remembers each survivor's latest aim and fire state and fires one projectile
    /// per cooldown while fire is held. The cooldown keeps running after the button is released, so
    /// pressing and releasing quickly cannot fire faster than holding.
    /// </summary>
    public sealed class AttackSim
    {
        sealed class Shooter
        {
            public ushort Angle;
            public bool Firing;
            public float Cooldown;
            public int Damage = CombatConfig.ProjectileDamage;
            public float CooldownMultiplier = 1f;
        }

        public float CooldownSeconds = CombatConfig.FireCooldownSeconds;

        readonly Dictionary<ulong, Shooter> shooters = new Dictionary<ulong, Shooter>();

        public int Count => shooters.Count;

        public void Register(ulong clientId) => shooters[clientId] = new Shooter();

        public void Unregister(ulong clientId) => shooters.Remove(clientId);

        public void Clear() => shooters.Clear();

        /// <summary>Stores the latest aim and fire state received from a player.</summary>
        public void SetAim(ulong clientId, ushort angle, bool firing)
        {
            if (!shooters.TryGetValue(clientId, out Shooter s)) return;
            s.Angle = angle;
            s.Firing = firing;
        }

        /// <summary>
        /// Sets one shooter's projectile damage and cooldown multiplier (below 1 fires faster).
        /// A new multiplier applies from the next shot; a cooldown already running is not cut short.
        /// </summary>
        public void SetModifiers(ulong clientId, int damage, float cooldownMultiplier)
        {
            if (!shooters.TryGetValue(clientId, out Shooter s)) return;
            s.Damage = damage;
            s.CooldownMultiplier = cooldownMultiplier;
        }

        public int DamageOf(ulong clientId) => shooters.TryGetValue(clientId, out Shooter s) ? s.Damage : CombatConfig.ProjectileDamage;

        public float CooldownRemaining(ulong clientId) => shooters.TryGetValue(clientId, out Shooter s) ? s.Cooldown : 0f;

        public void Step(float dt, IReadOnlyList<ShooterBody> bodies, ProjectileSim projectiles)
        {
            foreach (Shooter s in shooters.Values)
                s.Cooldown = Math.Max(0f, s.Cooldown - dt);

            for (int i = 0; i < bodies.Count; i++)
            {
                ShooterBody body = bodies[i];
                if (!body.CanFire) continue;
                if (!shooters.TryGetValue(body.ClientId, out Shooter s)) continue;
                if (!s.Firing || s.Cooldown > 0f) continue;

                // A refused shot (projectile cap) does not use up the cooldown.
                if (projectiles.TryFire(body.ClientId, body.Position, s.Angle, s.Damage, out _))
                    s.Cooldown = CooldownSeconds * s.CooldownMultiplier;
            }
        }
    }
}
