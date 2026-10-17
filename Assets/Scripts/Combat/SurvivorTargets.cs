using System;
using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>
    /// Lets a boss projectile hurt survivors through the same damage rule as every other source: it reads this step's
    /// survivor positions and applies damage with <see cref="SurvivorSim.ApplyDamage"/>, so health, the protection
    /// time and falling follow the survivor-health rules.
    /// </summary>
    public sealed class SurvivorTargets : ISurvivorTargets
    {
        readonly SurvivorSim survivors;
        readonly Func<ulong, byte> slotOf;
        IReadOnlyList<SurvivorBody> bodies = new List<SurvivorBody>();

        public SurvivorTargets(SurvivorSim survivors, Func<ulong, byte> slotOf)
        {
            this.survivors = survivors;
            this.slotOf = slotOf;
        }

        /// <summary>The survivors as they stand this step (fallen ones included).</summary>
        public void SetBodies(IReadOnlyList<SurvivorBody> value) => bodies = value;

        public int Count => bodies.Count;
        public float Radius => survivors.Radius;
        public Vector2 PositionAt(int index) => bodies[index].Position;
        public byte SlotAt(int index) => slotOf != null ? slotOf(bodies[index].ClientId) : (byte)0;

        public bool CanBeHit(int index)
        {
            ulong id = bodies[index].ClientId;
            return survivors.IsAlive(id) && !survivors.IsInvulnerable(id);
        }

        public bool TakeDamage(int index, int damage, out int healthPercent, out bool fell)
        {
            ulong id = bodies[index].ClientId;
            healthPercent = 0;
            fell = false;
            if (!survivors.ApplyDamage(id, damage)) return false;

            int max = Math.Max(1, survivors.MaxHealthOf(id));
            int health = survivors.Health(id);
            healthPercent = (int)((long)health * 100 / max);
            fell = health <= 0;
            return true;
        }
    }
}
