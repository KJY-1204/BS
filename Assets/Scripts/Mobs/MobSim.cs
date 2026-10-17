using System;
using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    public struct SurvivorInfo
    {
        public ulong ClientId;
        public Vector2 Position;
        public bool Alive;
    }

    public struct MobEvent
    {
        public bool IsSpawn;
        public ushort Id;
        public byte Type;
        public Vector2 Position;
    }

    /// <summary>Read access used by the view; implemented by the server simulation and the client interpolator.</summary>
    public interface IMobSource
    {
        int Count { get; }
        ushort IdAt(int index);
        byte TypeAt(int index);
        Vector2 PositionAt(int index);
    }

    /// <summary>
    /// Server-side mob simulation as plain data: spawn, chase, separation and arena bounds.
    /// No GameObjects or physics, so it can be tested without a scene.
    /// </summary>
    public sealed class MobSim : IMobSource
    {
        const byte OverrideNone = 0;
        const byte OverridePlayer = 1;
        const byte OverridePosition = 2;

        const float CellSize = 1f;

        public int HardCap { get; }
        public int TargetCount;
        public float Speed = MobConfig.Speed;
        public float SpawnPerSecond = MobConfig.SpawnPerSecond;
        public float Radius = MobConfig.Radius;
        public float SpawnMinSurvivorDistance = MobConfig.SpawnMinSurvivorDistance;
        public int MaxHealth = MobConfig.MobMaxHealth;

        readonly int[] health;
        bool gridDirty = true;
        readonly ushort[] ids;
        readonly byte[] types;
        readonly Vector2[] positions;
        readonly Vector2[] displacement;
        readonly byte[] overrideKind;
        readonly ulong[] overrideClient;
        readonly Vector2[] overridePosition;

        readonly Dictionary<ushort, int> indexOf = new Dictionary<ushort, int>();
        readonly List<MobEvent> events = new List<MobEvent>();
        readonly System.Random rng;

        // Spatial hash (counting sort, rebuilt every step)
        readonly int gridW;
        readonly int gridH;
        readonly int[] cellStart;
        readonly int[] cellFill;
        readonly int[] cellItems;
        readonly int[] cellOfMob;

        int count;
        ushort nextId = 1;
        float spawnAccumulator;

        public MobSim(int hardCap = MobConfig.HardCap, int targetCount = MobConfig.DefaultTarget, int seed = 12345)
        {
            HardCap = hardCap;
            TargetCount = Math.Min(targetCount, hardCap);
            rng = new System.Random(seed);

            ids = new ushort[hardCap];
            health = new int[hardCap];
            types = new byte[hardCap];
            positions = new Vector2[hardCap];
            displacement = new Vector2[hardCap];
            overrideKind = new byte[hardCap];
            overrideClient = new ulong[hardCap];
            overridePosition = new Vector2[hardCap];

            gridW = Mathf.CeilToInt(ArenaConfig.Width / CellSize) + 3;
            gridH = Mathf.CeilToInt(ArenaConfig.Height / CellSize) + 3;
            cellStart = new int[gridW * gridH + 1];
            cellFill = new int[gridW * gridH];
            cellItems = new int[hardCap];
            cellOfMob = new int[hardCap];
        }

        public int Count => count;
        public ushort IdAt(int index) => ids[index];
        public byte TypeAt(int index) => types[index];
        public Vector2 PositionAt(int index) => positions[index];

        public bool Contains(ushort id) => indexOf.ContainsKey(id);

        /// <summary>Spawns a mob if the hard cap allows. Positions are clamped into the arena.</summary>
        public bool TrySpawn(Vector2 position, byte type, out ushort id)
        {
            if (count >= HardCap) { id = 0; return false; }

            id = NextFreeId();
            int i = count++;
            ids[i] = id;
            types[i] = type;
            health[i] = MaxHealth;
            positions[i] = ClampToArena(position);
            overrideKind[i] = OverrideNone;
            indexOf[id] = i;
            gridDirty = true;

            events.Add(new MobEvent { IsSpawn = true, Id = id, Type = type, Position = positions[i] });
            return true;
        }

        public bool Remove(ushort id)
        {
            if (!indexOf.TryGetValue(id, out int i)) return false;

            int last = count - 1;
            if (i != last)
            {
                ids[i] = ids[last];
                types[i] = types[last];
                health[i] = health[last];
                positions[i] = positions[last];
                overrideKind[i] = overrideKind[last];
                overrideClient[i] = overrideClient[last];
                overridePosition[i] = overridePosition[last];
                indexOf[ids[i]] = i;
            }

            count--;
            indexOf.Remove(id);
            gridDirty = true;
            events.Add(new MobEvent { IsSpawn = false, Id = id });
            return true;
        }

        public int HealthAt(int index) => health[index];

        public bool TryGetHealth(ushort id, out int value)
        {
            if (indexOf.TryGetValue(id, out int i)) { value = health[i]; return true; }
            value = 0;
            return false;
        }

        /// <summary>
        /// Reduces a mob's health. At zero the mob is removed (with the usual removal event).
        /// Returns false when the mob does not exist.
        /// </summary>
        public bool ApplyDamage(ushort id, int damage, out int healthPercent, out bool killed)
        {
            healthPercent = 0;
            killed = false;
            if (!indexOf.TryGetValue(id, out int i)) return false;

            health[i] = Math.Max(0, health[i] - Math.Max(0, damage));
            healthPercent = MaxHealth > 0 ? health[i] * 100 / MaxHealth : 0;

            if (health[i] <= 0)
            {
                killed = true;
                Remove(id);
            }
            return true;
        }

        /// <summary>
        /// Finds the first mob a swept circle touches while moving from a to b. The circle has the given
        /// radius; mobs use their own radius. Returns the point where the circle first touches the mob.
        /// </summary>
        public bool TryHitSegment(Vector2 a, Vector2 b, float radius, out ushort mobId, out Vector2 hitPoint)
        {
            mobId = 0;
            hitPoint = default;
            if (count == 0) return false;

            EnsureGrid();

            float reach = radius + Radius;
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            float length = Mathf.Sqrt(lengthSq);

            GetCellRange(Mathf.Min(a.x, b.x) - reach, Mathf.Min(a.y, b.y) - reach,
                         Mathf.Max(a.x, b.x) + reach, Mathf.Max(a.y, b.y) + reach,
                         out int x0, out int y0, out int x1, out int y1);

            float bestEntry = float.MaxValue;
            int best = -1;

            for (int cy = y0; cy <= y1; cy++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    int c = cy * gridW + cx;
                    for (int k = cellStart[c]; k < cellStart[c + 1]; k++)
                    {
                        int m = cellItems[k];
                        Vector2 center = positions[m];

                        float tProj = lengthSq > 1e-8f ? Mathf.Clamp01(Vector2.Dot(center - a, ab) / lengthSq) : 0f;
                        Vector2 closest = a + ab * tProj;
                        float d = Vector2.Distance(center, closest);
                        if (d > reach) continue;

                        float back = length > 1e-5f ? Mathf.Sqrt(Mathf.Max(0f, reach * reach - d * d)) / length : 0f;
                        float entry = Mathf.Clamp01(tProj - back);
                        if (entry < bestEntry || (Mathf.Approximately(entry, bestEntry) && ids[m] < ids[best]))
                        {
                            bestEntry = entry;
                            best = m;
                        }
                    }
                }
            }

            if (best < 0) return false;

            mobId = ids[best];
            hitPoint = a + ab * bestEntry;
            return true;
        }

        /// <summary>True when any mob's centre is within reach of the point (a survivor touching a mob).</summary>
        public bool AnyWithin(Vector2 center, float reach)
        {
            if (count == 0) return false;

            EnsureGrid();
            GetCellRange(center.x - reach, center.y - reach, center.x + reach, center.y + reach,
                         out int x0, out int y0, out int x1, out int y1);

            float reachSq = reach * reach;
            for (int cy = y0; cy <= y1; cy++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    int c = cy * gridW + cx;
                    for (int k = cellStart[c]; k < cellStart[c + 1]; k++)
                        if ((positions[cellItems[k]] - center).sqrMagnitude <= reachSq) return true;
                }
            }
            return false;
        }

        /// <summary>Drops every mob without emitting events (the room ended).</summary>
        public void Clear()
        {
            count = 0;
            indexOf.Clear();
            events.Clear();
            spawnAccumulator = 0f;
        }

        /// <summary>Moves queued spawn/remove events into the list and empties the queue.</summary>
        public void DrainEvents(List<MobEvent> into)
        {
            into.AddRange(events);
            events.Clear();
        }

        public void Step(float dt, IReadOnlyList<SurvivorInfo> survivors)
        {
            AutoSpawn(dt, survivors);
            Chase(dt, survivors);
            Separate();
            for (int i = 0; i < count; i++) positions[i] = ClampToArena(positions[i]);
            gridDirty = true; // queries rebuild the grid on demand
        }

        // ---- Target override (used later by boss commands) ----

        public bool SetTarget(ushort id, ulong clientId)
        {
            if (!indexOf.TryGetValue(id, out int i)) return false;
            overrideKind[i] = OverridePlayer;
            overrideClient[i] = clientId;
            return true;
        }

        public bool SetTargetPosition(ushort id, Vector2 position)
        {
            if (!indexOf.TryGetValue(id, out int i)) return false;
            overrideKind[i] = OverridePosition;
            overridePosition[i] = position;
            return true;
        }

        public bool ClearTarget(ushort id)
        {
            if (!indexOf.TryGetValue(id, out int i)) return false;
            overrideKind[i] = OverrideNone;
            return true;
        }

        public void OnPlayerLeft(ulong clientId)
        {
            for (int i = 0; i < count; i++)
                if (overrideKind[i] == OverridePlayer && overrideClient[i] == clientId)
                    overrideKind[i] = OverrideNone;
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

        void AutoSpawn(float dt, IReadOnlyList<SurvivorInfo> survivors)
        {
            if (!AnyAlive(survivors)) { spawnAccumulator = 0f; return; }

            if (count >= TargetCount) { spawnAccumulator = 0f; return; }

            spawnAccumulator += SpawnPerSecond * dt;
            while (spawnAccumulator >= 1f && count < TargetCount)
            {
                spawnAccumulator -= 1f;
                if (TryPickSpawnPosition(survivors, out Vector2 position))
                    TrySpawn(position, 0, out _);
            }
        }

        bool TryPickSpawnPosition(IReadOnlyList<SurvivorInfo> survivors, out Vector2 position)
        {
            position = default;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 p = RandomEdgePoint();
                if (FarFromSurvivors(p, survivors))
                {
                    position = p;
                    return true;
                }
            }
            return false;
        }

        Vector2 RandomEdgePoint()
        {
            float hx = ArenaConfig.Width * 0.5f - Radius;
            float hy = ArenaConfig.Height * 0.5f - Radius;
            float perimeter = 2f * (2f * hx + 2f * hy);
            float t = (float)rng.NextDouble() * perimeter;

            float top = 2f * hx;
            if (t < top) return new Vector2(-hx + t, hy);
            t -= top;
            if (t < top) return new Vector2(-hx + t, -hy);
            t -= top;
            float side = 2f * hy;
            if (t < side) return new Vector2(-hx, -hy + t);
            t -= side;
            return new Vector2(hx, -hy + t);
        }

        bool FarFromSurvivors(Vector2 p, IReadOnlyList<SurvivorInfo> survivors)
        {
            float min = SpawnMinSurvivorDistance * SpawnMinSurvivorDistance;
            for (int s = 0; s < survivors.Count; s++)
                if (survivors[s].Alive && (survivors[s].Position - p).sqrMagnitude < min) return false;
            return true;
        }

        static bool AnyAlive(IReadOnlyList<SurvivorInfo> survivors)
        {
            for (int s = 0; s < survivors.Count; s++)
                if (survivors[s].Alive) return true;
            return false;
        }

        void Chase(float dt, IReadOnlyList<SurvivorInfo> survivors)
        {
            float step = Speed * dt;
            for (int i = 0; i < count; i++)
            {
                if (!TryGetTargetPosition(i, survivors, out Vector2 target)) continue;

                Vector2 toTarget = target - positions[i];
                float distance = toTarget.magnitude;
                if (distance < 1e-4f) continue;

                positions[i] += toTarget * (Mathf.Min(step, distance) / distance);
            }
        }

        bool TryGetTargetPosition(int i, IReadOnlyList<SurvivorInfo> survivors, out Vector2 target)
        {
            if (overrideKind[i] == OverridePosition)
            {
                target = overridePosition[i];
                return true;
            }

            if (overrideKind[i] == OverridePlayer)
            {
                for (int s = 0; s < survivors.Count; s++)
                {
                    if (survivors[s].ClientId == overrideClient[i] && survivors[s].Alive)
                    {
                        target = survivors[s].Position;
                        return true;
                    }
                }
            }

            return TryFindNearestSurvivor(positions[i], survivors, out target);
        }

        static bool TryFindNearestSurvivor(Vector2 from, IReadOnlyList<SurvivorInfo> survivors, out Vector2 nearest)
        {
            nearest = default;
            float best = float.MaxValue;
            bool found = false;
            for (int s = 0; s < survivors.Count; s++)
            {
                if (!survivors[s].Alive) continue;
                float d = (survivors[s].Position - from).sqrMagnitude;
                if (d < best) { best = d; nearest = survivors[s].Position; found = true; }
            }
            return found;
        }

        // ---- Separation via spatial hash ----

        void Separate()
        {
            if (count < 2) return;

            BuildGrid();
            for (int i = 0; i < count; i++) displacement[i] = Vector2.zero;

            float minDistance = Radius * 2f;
            for (int i = 0; i < count; i++)
            {
                int cell = cellOfMob[i];
                int cx = cell % gridW;
                int cy = cell / gridW;

                for (int ny = Math.Max(cy - 1, 0); ny <= Math.Min(cy + 1, gridH - 1); ny++)
                {
                    for (int nx = Math.Max(cx - 1, 0); nx <= Math.Min(cx + 1, gridW - 1); nx++)
                    {
                        int c = ny * gridW + nx;
                        for (int k = cellStart[c]; k < cellStart[c + 1]; k++)
                        {
                            int j = cellItems[k];
                            if (j <= i) continue;
                            PushApart(i, j, minDistance);
                        }
                    }
                }
            }

            for (int i = 0; i < count; i++) positions[i] += displacement[i];
        }

        void PushApart(int i, int j, float minDistance)
        {
            Vector2 delta = positions[j] - positions[i];
            float distance = delta.magnitude;
            if (distance >= minDistance) return;

            Vector2 direction;
            if (distance < 1e-4f)
            {
                // Identical positions: break the tie deterministically from the ids.
                float angle = ids[i] * 12.9898f + ids[j] * 78.233f;
                direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                distance = 0f;
            }
            else
            {
                direction = delta / distance;
            }

            Vector2 push = direction * ((minDistance - distance) * 0.25f);
            displacement[i] -= push;
            displacement[j] += push;
        }

        void EnsureGrid()
        {
            if (gridDirty) BuildGrid();
        }

        void GetCellRange(float minX, float minY, float maxX, float maxY, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = CellX(minX);
            x1 = CellX(maxX);
            y0 = CellY(minY);
            y1 = CellY(maxY);
        }

        int CellX(float x) => Mathf.Clamp(Mathf.FloorToInt((x + ArenaConfig.Width * 0.5f) / CellSize) + 1, 0, gridW - 1);
        int CellY(float y) => Mathf.Clamp(Mathf.FloorToInt((y + ArenaConfig.Height * 0.5f) / CellSize) + 1, 0, gridH - 1);

        void BuildGrid()
        {
            gridDirty = false;
            Array.Clear(cellFill, 0, cellFill.Length);
            for (int i = 0; i < count; i++)
            {
                int cell = CellIndex(positions[i]);
                cellOfMob[i] = cell;
                cellFill[cell]++;
            }

            int running = 0;
            for (int c = 0; c < cellFill.Length; c++)
            {
                cellStart[c] = running;
                running += cellFill[c];
                cellFill[c] = 0;
            }
            cellStart[cellFill.Length] = running;

            for (int i = 0; i < count; i++)
            {
                int c = cellOfMob[i];
                cellItems[cellStart[c] + cellFill[c]++] = i;
            }
        }

        int CellIndex(Vector2 p) => CellY(p.y) * gridW + CellX(p.x);

        Vector2 ClampToArena(Vector2 p)
        {
            float hx = ArenaConfig.Width * 0.5f - Radius;
            float hy = ArenaConfig.Height * 0.5f - Radius;
            return new Vector2(Mathf.Clamp(p.x, -hx, hx), Mathf.Clamp(p.y, -hy, hy));
        }
    }
}
