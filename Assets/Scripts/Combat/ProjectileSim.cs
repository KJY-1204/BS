using System;
using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    public enum CombatEventKind : byte
    {
        Fire = 1,
        Hit = 2,
        End = 3,
        /// <summary>A skill was used (not tied to a projectile).</summary>
        Skill = 4,
    }

    /// <summary>One thing that happened to a projectile, handed to the network layer.</summary>
    public struct CombatEvent
    {
        public CombatEventKind Kind;
        public ushort ProjectileId;
        public ulong Owner;
        /// <summary>The shooter's survivor slot (1 to 4). Filled in when the event is encoded or decoded.</summary>
        public byte Slot;
        /// <summary>Fire: start position. Hit and End: where the projectile ended.</summary>
        public Vector2 Position;
        /// <summary>Fire only: aim as a 16-bit angle.</summary>
        public ushort Angle;
        /// <summary>Skill only: which skill and at what rank. The place is <see cref="Position"/>, the user is <see cref="Slot"/>.</summary>
        public byte SkillId;
        public byte Rank;
        /// <summary>Hit only.</summary>
        public ushort MobId;
        public byte MobHealthPercent;
        public bool Killed;
    }

    /// <summary>Read access to live projectiles, used by the view on the host.</summary>
    public interface IProjectileSource
    {
        int Count { get; }
        ushort IdAt(int index);
        ulong OwnerAt(int index);
        Vector2 PositionAt(int index);
    }

    /// <summary>
    /// Server-side projectiles as plain data. They fly straight at a fixed speed, stop at the first mob
    /// they touch (damaging it), at the arena edge, or when their lifetime ends.
    /// </summary>
    public sealed class ProjectileSim : IProjectileSource
    {
        public int Cap { get; }
        public float Speed = CombatConfig.ProjectileSpeed;
        public float Lifetime = CombatConfig.ProjectileLifetimeSeconds;
        public float Radius = CombatConfig.ProjectileRadius;
        public int Damage = CombatConfig.ProjectileDamage;

        readonly ushort[] ids;
        readonly ulong[] owners;
        readonly int[] damages;
        readonly Vector2[] positions;
        readonly Vector2[] directions;
        readonly float[] remaining;
        readonly Dictionary<ushort, int> indexOf = new Dictionary<ushort, int>();
        readonly List<CombatEvent> events = new List<CombatEvent>();

        int count;
        ushort nextId = 1;

        public ProjectileSim(int cap = CombatConfig.ProjectileCap)
        {
            Cap = cap;
            ids = new ushort[cap];
            owners = new ulong[cap];
            damages = new int[cap];
            positions = new Vector2[cap];
            directions = new Vector2[cap];
            remaining = new float[cap];
        }

        public int Count => count;
        public ushort IdAt(int index) => ids[index];
        public ulong OwnerAt(int index) => owners[index];
        public Vector2 PositionAt(int index) => positions[index];

        /// <summary>Starts a projectile with the default damage unless the cap is reached.</summary>
        public bool TryFire(ulong owner, Vector2 origin, ushort angle, out ushort id) =>
            TryFire(owner, origin, angle, Damage, out id);

        /// <summary>
        /// Starts a projectile unless the cap is reached. The projectile keeps the damage it was fired with,
        /// so a later change to its shooter's damage does not change shots already in flight.
        /// </summary>
        public bool TryFire(ulong owner, Vector2 origin, ushort angle, int damage, out ushort id)
        {
            if (count >= Cap) { id = 0; return false; }

            id = NextFreeId();
            int i = count++;
            ids[i] = id;
            owners[i] = owner;
            damages[i] = damage;
            positions[i] = origin;
            directions[i] = AimMath.Direction(angle);
            remaining[i] = Lifetime;
            indexOf[id] = i;

            events.Add(new CombatEvent { Kind = CombatEventKind.Fire, ProjectileId = id, Owner = owner, Position = origin, Angle = angle });
            return true;
        }

        public void Clear()
        {
            count = 0;
            indexOf.Clear();
            events.Clear();
        }

        public void DrainEvents(List<CombatEvent> into)
        {
            into.AddRange(events);
            events.Clear();
        }

        /// <summary>Moves every projectile and resolves hits against the mobs.</summary>
        public void Step(float dt, MobSim mobs)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                float travelTime = Math.Min(dt, remaining[i]);
                remaining[i] -= dt;

                Vector2 from = positions[i];
                Vector2 to = from + directions[i] * (Speed * travelTime);

                bool leftArena = ClipToArena(from, ref to);

                if (mobs != null && mobs.TryHitSegment(from, to, Radius, out ushort mobId, out Vector2 point))
                {
                    mobs.ApplyDamage(mobId, damages[i], out int percent, out bool killed);
                    events.Add(new CombatEvent
                    {
                        Kind = CombatEventKind.Hit, ProjectileId = ids[i], Owner = owners[i], Position = point,
                        MobId = mobId, MobHealthPercent = (byte)percent, Killed = killed,
                    });
                    RemoveAt(i);
                    continue;
                }

                if (leftArena || remaining[i] <= 0f)
                {
                    events.Add(new CombatEvent { Kind = CombatEventKind.End, ProjectileId = ids[i], Owner = owners[i], Position = to });
                    RemoveAt(i);
                    continue;
                }

                positions[i] = to;
            }
        }

        // ---- Internals ----

        ushort NextFreeId()
        {
            while (true)
            {
                ushort candidate = nextId;
                nextId = nextId == ushort.MaxValue ? (ushort)1 : (ushort)(nextId + 1);
                if (!indexOf.ContainsKey(candidate)) return candidate;
            }
        }

        /// <summary>Shortens the segment to the arena rectangle. Returns true when it was cut.</summary>
        static bool ClipToArena(Vector2 from, ref Vector2 to)
        {
            float hx = ArenaConfig.Width * 0.5f;
            float hy = ArenaConfig.Height * 0.5f;
            if (Mathf.Abs(to.x) <= hx && Mathf.Abs(to.y) <= hy) return false;

            Vector2 delta = to - from;
            float t = 1f;
            if (delta.x > 1e-6f) t = Mathf.Min(t, (hx - from.x) / delta.x);
            else if (delta.x < -1e-6f) t = Mathf.Min(t, (-hx - from.x) / delta.x);
            if (delta.y > 1e-6f) t = Mathf.Min(t, (hy - from.y) / delta.y);
            else if (delta.y < -1e-6f) t = Mathf.Min(t, (-hy - from.y) / delta.y);

            to = from + delta * Mathf.Clamp01(t);
            return true;
        }

        void RemoveAt(int i)
        {
            int last = count - 1;
            indexOf.Remove(ids[i]);
            if (i != last)
            {
                ids[i] = ids[last];
                owners[i] = owners[last];
                damages[i] = damages[last];
                positions[i] = positions[last];
                directions[i] = directions[last];
                remaining[i] = remaining[last];
                indexOf[ids[i]] = i;
            }
            count--;
        }
    }
}
