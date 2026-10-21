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
        /// <summary>The aim the caster had, for skills with a direction.</summary>
        public ushort Angle;
        /// <summary>The percent the server multiplied the skill's range and duration by (100 = as defined; 0 = not given).</summary>
        public byte RangeScale;
        public byte DurationScale;
        public float ServerTime;
    }

    /// <summary>
    /// The sizes a client draws for a skill effect: the defined value for the rank times the scale the server used,
    /// worked out with the same function the server judges with, so the picture matches what was hit.
    /// </summary>
    public static class SkillEffectSize
    {
        public static float Radius(SkillDef def, int rank, int rangeScale) =>
            def.Radius != null ? SkillCatalog.Scaled(def.Radius[rank - 1], rangeScale) : 0f;

        public static float Length(SkillDef def, int rank, int rangeScale) =>
            def.Length != null ? SkillCatalog.Scaled(def.Length[rank - 1], rangeScale) : 0f;

        public static float Width(SkillDef def, int rank, int rangeScale) =>
            def.HalfWidth != null ? SkillCatalog.Scaled(def.HalfWidth[rank - 1], rangeScale) * 2f : 0f;

        public static float Seconds(SkillDef def, int rank, int durationScale) =>
            def.Duration != null ? SkillCatalog.Scaled(def.Duration[rank - 1], durationScale) : 0f;
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
