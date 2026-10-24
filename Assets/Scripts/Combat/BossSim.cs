using System;
using UnityEngine;

namespace BS
{
    /// <summary>What a projectile or a skill needs to know about the boss to hit it.</summary>
    public interface IBossTarget
    {
        /// <summary>True while the boss can still be hit (it exists and has not fallen).</summary>
        bool CanBeHit { get; }
        Vector2 Position { get; }
        float Radius { get; }
        /// <summary>
        /// Damages the boss. Returns false when it cannot be hit; otherwise reports the health that is left as a
        /// percentage and whether this hit made it fall.
        /// </summary>
        bool TakeDamage(int damage, out int healthPercent, out bool fell);
    }

    /// <summary>
    /// Server-side boss health as plain data. The boss is moved by its player, so the simulation only keeps the
    /// position it is told, takes damage and notices when the boss falls.
    /// </summary>
    public sealed class BossSim : IBossTarget
    {
        /// <summary>
        /// The mob identifier that stands for the boss in hit events. Mob identifiers never use it
        /// (<see cref="MobSim"/> skips it), so a hit on the boss can be told from a hit on any mob.
        /// </summary>
        public const ushort MobId = ushort.MaxValue;

        public int MaxHealth { get; private set; }
        public int Health { get; private set; }
        public Vector2 Position { get; private set; }
        public float Radius { get; private set; }
        public int ContactDamage { get; private set; }

        /// <summary>A step that moves the boss less than this counts as standing still.</summary>
        public const float StillDistance = 0.02f;

        /// <summary>False turns the kind's trait off (the harness override bossTraits).</summary>
        public bool TraitsEnabled { get; set; } = true;

        /// <summary>True when the boss moved during the last <see cref="Advance"/>.</summary>
        public bool Moving { get; private set; }

        /// <summary>Seconds the boss has stood still since it last moved.</summary>
        public float StillSeconds { get; private set; }

        BossDef traits;
        Vector2 lastPosition;

        /// <summary>The contact damage now: the kind's trait scales it by whether the boss is moving.</summary>
        public int CurrentContactDamage => TraitsEnabled ? BossTraits.ContactDamage(traits, ContactDamage, Moving) : ContactDamage;

        /// <summary>The multiple the boss's basic shot deals now: the sniper's Steady after it has stood still long enough.</summary>
        public float ShotMultiple => TraitsEnabled ? BossTraits.ShotMultiple(traits, StillSeconds) : 1f;

        /// <summary>True from the moment the boss falls until the simulation is reset.</summary>
        public bool HasFallen => Health <= 0;

        /// <summary>True for the single step after the boss fell, so the outcome can react to it once.</summary>
        public bool JustFell { get; private set; }

        public bool CanBeHit => MaxHealth > 0 && Health > 0;

        /// <summary>True once a boss has been set up for this match.</summary>
        public bool Exists => MaxHealth > 0;

        public BossSim() { }

        public BossSim(BossDef def, Vector2 position)
        {
            Reset(def, position);
        }

        public void Reset(BossDef def, Vector2 position)
        {
            MaxHealth = Math.Max(1, def.MaxHealth);
            Health = MaxHealth;
            Radius = def.Radius;
            ContactDamage = def.ContactDamage;
            Position = position;
            JustFell = false;
            traits = def;
            lastPosition = position;
            Moving = false;
            StillSeconds = 0f;
        }

        /// <summary>No boss in this match.</summary>
        public void Clear()
        {
            MaxHealth = 0;
            Health = 0;
            JustFell = false;
            traits = null;
            Moving = false;
            StillSeconds = 0f;
        }

        /// <summary>Call once per step after the boss's position was set: tells whether the boss moved and how long it has stood still.</summary>
        public void Advance(float dt)
        {
            if (!CanBeHit)
            {
                Moving = false;
                StillSeconds = 0f;
                lastPosition = Position;
                return;
            }

            Moving = (Position - lastPosition).sqrMagnitude > StillDistance * StillDistance;
            StillSeconds = Moving ? 0f : StillSeconds + dt;
            lastPosition = Position;
        }

        /// <summary>Where the boss's player has moved it (the boss is not moved by the simulation).</summary>
        public void SetPosition(Vector2 position) => Position = position;

        /// <summary>Call once per step after everything that can hurt the boss has run.</summary>
        public void EndStep() => JustFell = false;

        public int HealthPercent => MaxHealth > 0 ? (int)((long)Health * 100 / MaxHealth) : 0;

        public bool TakeDamage(int damage, out int healthPercent, out bool fell)
        {
            healthPercent = HealthPercent;
            fell = false;
            if (!CanBeHit || damage <= 0) return false;

            Health = Math.Max(0, Health - damage);
            healthPercent = HealthPercent;
            if (Health == 0)
            {
                fell = true;
                JustFell = true;
            }
            return true;
        }
    }
}
