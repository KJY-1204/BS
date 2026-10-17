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
        /// <summary>Hit only: a piercing projectile that keeps flying after this hit.</summary>
        public bool Continues;
    }

    /// <summary>Everything the simulation needs to start one projectile.</summary>
    public struct ProjectileSpec
    {
        public ulong Owner;
        public Vector2 Origin;
        public ushort Angle;
        public int Damage;
        public ProjectileShape Shape;
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
    /// Server-side projectiles as plain data. They fly straight at the speed of their shape and stop at the arena
    /// edge or when their lifetime ends. A projectile stops at the first mob it touches unless its shape lets it
    /// pierce, in which case it damages each of that many mobs once and stops after the last.
    /// </summary>
    public sealed class ProjectileSim : IProjectileSource
    {
        public int Cap { get; }
        public float Speed = CombatConfig.ProjectileSpeed;
        public float Lifetime = CombatConfig.ProjectileLifetimeSeconds;
        public float Radius = CombatConfig.ProjectileRadius;
        public int Damage = CombatConfig.ProjectileDamage;

        /// <summary>Most mobs one projectile can remember having hit; a shape asking for more is capped.</summary>
        public const int MaxPierce = 8;

        readonly ushort[] ids;
        readonly ulong[] owners;
        readonly int[] damages;
        readonly Vector2[] positions;
        readonly Vector2[] directions;
        readonly float[] remaining;
        readonly float[] speeds;
        readonly float[] radii;
        readonly int[] pierceLeft;
        readonly byte[] hitCounts;
        readonly ushort[] hitMobs;
        readonly Dictionary<ushort, int> indexOf = new Dictionary<ushort, int>();
        readonly List<CombatEvent> events = new List<CombatEvent>();
        readonly List<MobSim.SegmentHit> segmentHits = new List<MobSim.SegmentHit>();
        readonly List<ushort> alreadyHit = new List<ushort>();

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
            speeds = new float[cap];
            radii = new float[cap];
            pierceLeft = new int[cap];
            hitCounts = new byte[cap];
            hitMobs = new ushort[cap * MaxPierce];
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
        public bool TryFire(ulong owner, Vector2 origin, ushort angle, int damage, out ushort id) =>
            TryFire(new ProjectileSpec
            {
                Owner = owner, Origin = origin, Angle = angle, Damage = damage,
                Shape = new ProjectileShape { Speed = Speed, Lifetime = Lifetime, Radius = Radius, Pierce = 1 },
            }, out id);

        /// <summary>Starts a projectile of any shape unless the cap is reached.</summary>
        public bool TryFire(ProjectileSpec spec, out ushort id)
        {
            if (count >= Cap) { id = 0; return false; }

            id = NextFreeId();
            int i = count++;
            ids[i] = id;
            owners[i] = spec.Owner;
            damages[i] = spec.Damage;
            positions[i] = spec.Origin;
            directions[i] = AimMath.Direction(spec.Angle);
            remaining[i] = spec.Shape.Lifetime;
            speeds[i] = spec.Shape.Speed;
            radii[i] = spec.Shape.Radius;
            pierceLeft[i] = Math.Max(1, Math.Min(MaxPierce, spec.Shape.Pierce));
            hitCounts[i] = 0;
            indexOf[id] = i;

            events.Add(new CombatEvent { Kind = CombatEventKind.Fire, ProjectileId = id, Owner = spec.Owner, Position = spec.Origin, Angle = spec.Angle });
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
        public void Step(float dt, MobSim mobs) => Step(dt, mobs, null);

        /// <summary>Moves every projectile and resolves hits against the mobs and the boss.</summary>
        public void Step(float dt, MobSim mobs, IBossTarget boss)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                float travelTime = Math.Min(dt, remaining[i]);
                remaining[i] -= dt;

                Vector2 from = positions[i];
                Vector2 to = from + directions[i] * (speeds[i] * travelTime);

                bool leftArena = ClipToArena(from, ref to);

                if ((mobs != null || boss != null) && HitTargetsOnTheWay(i, from, to, mobs, boss))
                {
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

        /// <summary>
        /// Damages the mobs the projectile touched on its way, nearest first, each at most once. Returns true when the
        /// projectile used up its last allowed hit and must be removed.
        /// </summary>
        bool HitTargetsOnTheWay(int i, Vector2 from, Vector2 to, MobSim mobs, IBossTarget boss)
        {
            alreadyHit.Clear();
            int baseIndex = i * MaxPierce;
            for (int k = 0; k < hitCounts[i]; k++) alreadyHit.Add(hitMobs[baseIndex + k]);

            segmentHits.Clear();
            if (mobs != null) mobs.CollectSegmentHits(from, to, radii[i], alreadyHit, segmentHits);
            AddBossHit(from, to, radii[i], boss, segmentHits);
            if (segmentHits.Count == 0) return false;

            // The boss is just another target on the way, ordered with the mobs by how far along the sweep it is touched.
            segmentHits.Sort(HitOrder.Instance);

            for (int h = 0; h < segmentHits.Count; h++)
            {
                MobSim.SegmentHit hit = segmentHits[h];
                int percent;
                bool killed;
                if (hit.MobId == BossSim.MobId)
                {
                    if (boss == null || !boss.TakeDamage(damages[i], out percent, out killed)) continue;
                }
                else if (mobs == null || !mobs.ApplyDamage(hit.MobId, damages[i], out percent, out killed)) continue;

                hitMobs[baseIndex + hitCounts[i]] = hit.MobId;
                hitCounts[i]++;
                pierceLeft[i]--;
                bool last = pierceLeft[i] <= 0;

                events.Add(new CombatEvent
                {
                    Kind = CombatEventKind.Hit, ProjectileId = ids[i], Owner = owners[i], Position = hit.Point,
                    MobId = hit.MobId, MobHealthPercent = (byte)percent, Killed = killed, Continues = !last,
                });

                if (last) return true;
            }
            return false;
        }

        sealed class HitOrder : IComparer<MobSim.SegmentHit>
        {
            public static readonly HitOrder Instance = new HitOrder();

            public int Compare(MobSim.SegmentHit x, MobSim.SegmentHit y)
            {
                int byT = x.T.CompareTo(y.T);
                return byT != 0 ? byT : x.MobId.CompareTo(y.MobId);
            }
        }

        /// <summary>Adds the boss to the hits when the swept circle touches it and this projectile has not hit it yet.</summary>
        void AddBossHit(Vector2 a, Vector2 b, float radius, IBossTarget boss, List<MobSim.SegmentHit> into)
        {
            if (boss == null || !boss.CanBeHit) return;
            for (int k = 0; k < alreadyHit.Count; k++)
                if (alreadyHit[k] == BossSim.MobId) return;

            float reach = radius + boss.Radius;
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            float length = Mathf.Sqrt(lengthSq);
            Vector2 center = boss.Position;

            float tProj = lengthSq > 1e-8f ? Mathf.Clamp01(Vector2.Dot(center - a, ab) / lengthSq) : 0f;
            float d = Vector2.Distance(center, a + ab * tProj);
            if (d > reach) return;

            float back = length > 1e-5f ? Mathf.Sqrt(Mathf.Max(0f, reach * reach - d * d)) / length : 0f;
            float entry = Mathf.Clamp01(tProj - back);
            into.Add(new MobSim.SegmentHit { MobId = BossSim.MobId, T = entry, Point = a + ab * entry });
        }

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
                speeds[i] = speeds[last];
                radii[i] = radii[last];
                pierceLeft[i] = pierceLeft[last];
                hitCounts[i] = hitCounts[last];
                Array.Copy(hitMobs, last * MaxPierce, hitMobs, i * MaxPierce, MaxPierce);
                indexOf[ids[i]] = i;
            }
            count--;
        }
    }
}
