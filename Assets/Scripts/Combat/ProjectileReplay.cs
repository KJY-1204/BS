using System;
using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>A hit that has become due on the display clock; the view shows the effect and the mob's health bar.</summary>
    public struct HitEffect
    {
        public Vector2 Position;
        public ushort MobId;
        public byte HealthPercent;
        public bool Killed;
        /// <summary>The target was a survivor (a boss shot): <see cref="MobId"/> is then the survivor's slot, not a mob.</summary>
        public bool Survivor;
    }

    /// <summary>
    /// Client-side projectiles. A projectile is drawn from the fire event alone: it flies straight from its
    /// start at the speed of its shooter's character (looked up by the shooter's slot, so no positions are sent),
    /// and ends at the position and time the server reported. A piercing projectile shows a hit effect for each
    /// mob it passes without ending, and ends with its last hit or with an ending event. Everything is driven by
    /// the display clock the mobs use, so a shot reaches a mob on screen at the moment of the hit.
    /// There is no hit detection here; this only draws what the server decided.
    /// </summary>
    public sealed class ProjectileReplay
    {
        sealed class Track
        {
            public ushort Id;
            public byte Slot;
            public ProjectileShape Shape;
            public Vector2 Origin;
            public Vector2 Direction;
            public float Start;
            public bool Ended;
            public float End;
            public Vector2 EndPosition;
            public bool IsHit;
            public HitEffect Effect;
        }

        /// <summary>The shape used when the shooter's character is not known.</summary>
        public float Speed = CombatConfig.ProjectileSpeed;
        public float Lifetime = CombatConfig.ProjectileLifetimeSeconds;
        public float StaleMargin = CombatConfig.ProjectileStaleMarginSeconds;

        /// <summary>Finds the projectile shape of the character in a shooter slot. Null means every shooter uses the default.</summary>
        public Func<byte, ProjectileShape> ShapeOfSlot;

        /// <summary>A hit effect that is waiting for its display time while its projectile keeps flying.</summary>
        struct TimedEffect
        {
            public float Time;
            public HitEffect Effect;
        }

        public const int MaxPendingEffects = 256;
        readonly List<TimedEffect> pendingEffects = new List<TimedEffect>();

        readonly List<Track> tracks = new List<Track>();
        readonly Dictionary<ushort, int> indexOf = new Dictionary<ushort, int>();
        readonly List<Track> visible = new List<Track>();
        readonly List<Vector2> visiblePositions = new List<Vector2>();
        readonly List<HitEffect> due = new List<HitEffect>();

        /// <summary>Projectiles currently on screen.</summary>
        public int Count => visible.Count;
        public ushort IdAt(int index) => visible[index].Id;
        public byte SlotAt(int index) => visible[index].Slot;
        public ProjectileShape ShapeAt(int index) => visible[index].Shape;
        public Vector2 PositionAt(int index) => visiblePositions[index];

        /// <summary>Projectiles known to the replay, including ones that have not started yet.</summary>
        public int TrackedCount => tracks.Count;

        public void Clear()
        {
            tracks.Clear();
            indexOf.Clear();
            visible.Clear();
            visiblePositions.Clear();
            due.Clear();
            pendingEffects.Clear();
        }

        public void OnFire(ushort id, byte slot, Vector2 origin, ushort angle, float serverTime)
        {
            if (indexOf.TryGetValue(id, out int existing)) RemoveAt(existing);

            ProjectileShape shape = ShapeOfSlot != null
                ? ShapeOfSlot(slot)
                : new ProjectileShape { Speed = Speed, Lifetime = Lifetime, Radius = CombatConfig.ProjectileRadius, Pierce = 1 };
            if (shape.Speed <= 0f || shape.Lifetime <= 0f) shape = ProjectileShape.Default;

            var track = new Track { Id = id, Slot = slot, Shape = shape, Origin = origin, Direction = AimMath.Direction(angle), Start = serverTime };
            indexOf[id] = tracks.Count;
            tracks.Add(track);
        }

        public void OnHit(ushort id, ushort mobId, byte healthPercent, bool killed, Vector2 position, float serverTime, bool continues = false, bool survivor = false)
        {
            if (!indexOf.TryGetValue(id, out int i)) return;

            if (continues)
            {
                // The projectile flies on: only the effect waits for its time.
                if (pendingEffects.Count >= MaxPendingEffects) pendingEffects.RemoveAt(0);
                pendingEffects.Add(new TimedEffect
                {
                    Time = serverTime,
                    Effect = new HitEffect { Position = position, MobId = mobId, HealthPercent = healthPercent, Killed = killed, Survivor = survivor },
                });
                return;
            }

            Track t = tracks[i];
            t.Ended = true;
            t.End = serverTime;
            t.EndPosition = position;
            t.IsHit = true;
            t.Effect = new HitEffect { Position = position, MobId = mobId, HealthPercent = healthPercent, Killed = killed, Survivor = survivor };
        }

        public void OnEnd(ushort id, Vector2 position, float serverTime)
        {
            if (!indexOf.TryGetValue(id, out int i)) return;

            Track t = tracks[i];
            t.Ended = true;
            t.End = serverTime;
            t.EndPosition = position;
        }

        /// <summary>Moves the display clock forward: updates what is visible, removes finished projectiles.</summary>
        public void Advance(float displayTime)
        {
            visible.Clear();
            visiblePositions.Clear();

            ReleaseDueEffects(displayTime);

            for (int i = tracks.Count - 1; i >= 0; i--)
            {
                Track t = tracks[i];

                if (t.Ended && displayTime >= t.End)
                {
                    if (t.IsHit && displayTime >= t.Start) due.Add(t.Effect);
                    RemoveAt(i);
                    continue;
                }

                if (!t.Ended && displayTime > t.Start + t.Shape.Lifetime + StaleMargin)
                {
                    RemoveAt(i);
                    continue;
                }

                if (displayTime < t.Start) continue; // has not been fired yet on screen

                visible.Add(t);
                visiblePositions.Add(PositionOf(t, displayTime));
            }
        }

        /// <summary>Hands over the hits whose display time has come, oldest first not guaranteed.</summary>
        public void DrainDueEffects(List<HitEffect> into)
        {
            into.AddRange(due);
            due.Clear();
        }

        Vector2 PositionOf(Track t, float displayTime)
        {
            if (t.Ended)
            {
                float span = t.End - t.Start;
                float progress = span > 1e-4f ? Mathf.Clamp01((displayTime - t.Start) / span) : 1f;
                return Vector2.Lerp(t.Origin, t.EndPosition, progress);
            }

            float flown = Mathf.Clamp(displayTime - t.Start, 0f, t.Shape.Lifetime);
            return t.Origin + t.Direction * (t.Shape.Speed * flown);
        }

        void ReleaseDueEffects(float displayTime)
        {
            if (pendingEffects.Count == 0) return;

            for (int i = pendingEffects.Count - 1; i >= 0; i--)
            {
                if (pendingEffects[i].Time > displayTime) continue;
                due.Add(pendingEffects[i].Effect);
                pendingEffects.RemoveAt(i);
            }
        }

        void RemoveAt(int i)
        {
            Track removed = tracks[i];
            int last = tracks.Count - 1;
            if (i != last)
            {
                tracks[i] = tracks[last];
                indexOf[tracks[i].Id] = i;
            }
            tracks.RemoveAt(last);
            indexOf.Remove(removed.Id);
        }
    }
}
