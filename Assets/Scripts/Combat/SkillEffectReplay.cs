using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>One skill use as a client sees it, stamped with the server time it happened.</summary>
    public struct SkillEffect
    {
        public byte SkillId;
        public byte Slot;
        public Vector2 Position;
        public byte Rank;
        public float ServerTime;
    }

    /// <summary>
    /// Client-side queue of skill effects. An effect is held until the display clock (the one mobs and shots
    /// use) reaches the server time it carries, so it appears together with the shots and kills around it.
    /// An effect that arrives after its time has already passed is released at once. Nothing here changes the
    /// simulation; it only decides when the view should draw.
    /// </summary>
    public sealed class SkillEffectReplay
    {
        /// <summary>A safety net: if the display clock stops, the queue cannot grow without limit.</summary>
        public const int MaxPending = 256;

        readonly List<SkillEffect> pending = new List<SkillEffect>();
        readonly List<SkillEffect> due = new List<SkillEffect>();
        readonly List<SkillEffect> keep = new List<SkillEffect>();

        public int PendingCount => pending.Count;

        public void Clear()
        {
            pending.Clear();
            due.Clear();
        }

        public void OnSkill(SkillEffect effect)
        {
            if (pending.Count >= MaxPending) pending.RemoveAt(0);
            pending.Add(effect);
        }

        /// <summary>Releases every effect whose time the display clock has reached, in the order they arrived.</summary>
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

        public void DrainDue(List<SkillEffect> into)
        {
            into.AddRange(due);
            due.Clear();
        }
    }
}
