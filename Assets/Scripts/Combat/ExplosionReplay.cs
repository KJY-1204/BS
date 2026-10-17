using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>One blast of an exploding mob as a client sees it, stamped with the server time it happened.</summary>
    public struct ExplosionEffect
    {
        public Vector2 Position;
        public float Radius;
        public float ServerTime;
    }

    /// <summary>
    /// Client-side queue of blasts. A blast is held until the display clock (the one mobs and shots use) reaches the server time it
    /// carries, so the effect appears at the moment the exploding mob disappears from the screen. A blast that arrives after its
    /// time has passed is released at once. Nothing here changes the simulation.
    /// </summary>
    public sealed class ExplosionReplay
    {
        /// <summary>A safety net: if the display clock stops, the queue cannot grow without limit.</summary>
        public const int MaxPending = 128;

        readonly List<ExplosionEffect> pending = new List<ExplosionEffect>();
        readonly List<ExplosionEffect> due = new List<ExplosionEffect>();
        readonly List<ExplosionEffect> keep = new List<ExplosionEffect>();

        public int PendingCount => pending.Count;

        public void Clear()
        {
            pending.Clear();
            due.Clear();
        }

        public void OnExplosion(ExplosionEffect effect)
        {
            if (pending.Count >= MaxPending) pending.RemoveAt(0);
            pending.Add(effect);
        }

        /// <summary>Releases every blast whose time the display clock has reached, in the order they arrived.</summary>
        public void Advance(float displayTime)
        {
            if (pending.Count == 0) return;

            keep.Clear();
            for (int i = 0; i < pending.Count; i++)
            {
                if (pending[i].ServerTime <= displayTime) due.Add(pending[i]);
                else keep.Add(pending[i]);
            }

            pending.Clear();
            pending.AddRange(keep);
        }

        public void DrainDue(List<ExplosionEffect> into)
        {
            into.AddRange(due);
            due.Clear();
        }
    }
}
