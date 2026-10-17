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
    /// Server-side basic attack: remembers each survivor's latest aim and fire state and fires the survivor's
    /// character attack (one or several shots together) once per cooldown while fire is held. The cooldown keeps
    /// running after the button is released, so pressing and releasing quickly cannot fire faster than holding.
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
            public AttackDef Attack = AttackDef.Default;
        }

        readonly Dictionary<ulong, Shooter> shooters = new Dictionary<ulong, Shooter>();

        public int Count => shooters.Count;

        /// <summary>Adds a shooter with the plain single-shot attack.</summary>
        public void Register(ulong clientId) => shooters[clientId] = new Shooter();

        /// <summary>Adds a shooter that attacks the way its character does.</summary>
        public void Register(ulong clientId, AttackDef attack) => shooters[clientId] = new Shooter { Attack = attack };

        /// <summary>The latest aim received from a shooter, which skills with a direction use.</summary>
        public ushort AimOf(ulong clientId) => shooters.TryGetValue(clientId, out Shooter s) ? s.Angle : (ushort)0;

        public AttackDef AttackOf(ulong clientId) => shooters.TryGetValue(clientId, out Shooter s) ? s.Attack : AttackDef.Default;

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

                // The whole attack goes out together. If the projectile cap leaves room for only some of the
                // shots, those fire; a shot refused by the cap does not use up the cooldown.
                AttackDef attack = s.Attack;
                int damage = Math.Max(1, Mathf.RoundToInt(s.Damage * attack.DamageMultiplier));
                int fired = 0;
                for (int shot = 0; shot < attack.Shots; shot++)
                {
                    var spec = new ProjectileSpec
                    {
                        Owner = body.ClientId,
                        Origin = body.Position,
                        Angle = AimMath.ShotAngle(s.Angle, attack.Shots, attack.SpreadDegrees, shot),
                        Damage = damage,
                        Shape = attack.Shape,
                    };
                    if (!projectiles.TryFire(spec, out _)) break;
                    fired++;
                }

                if (fired > 0) s.Cooldown = attack.CooldownSeconds * s.CooldownMultiplier;
            }
        }
    }
}
