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
        /// <summary>0 for a spawn or removal; <see cref="DeviceReady"/> or <see cref="DeviceProduce"/> for a beat of a standing device.</summary>
        public byte Device;
        public ushort Id;
        public byte Type;
        public Vector2 Position;

        /// <summary>A device is about to make mobs (it is getting ready).</summary>
        public const byte DeviceReady = 1;
        /// <summary>A device just made mobs.</summary>
        public const byte DeviceProduce = 2;
    }

    /// <summary>What shows the state of the devices: the beats they have had since the last time they were asked for.</summary>
    public interface IDeviceBeatSource
    {
        void DrainDeviceBeats(List<MobEvent> into);
    }

    /// <summary>A shot a ranged mob wants to fire this step; the server turns it into a projectile.</summary>
    public struct MobShotRequest
    {
        public ushort MobId;
        public Vector2 Origin;
        /// <summary>Aim as a 16-bit angle, toward the nearest living survivor at the moment of the shot.</summary>
        public ushort Angle;
        public byte Kind;
    }

    /// <summary>An exploding mob that blasted this step (it has already been removed); the server hurts the survivors around it.</summary>
    public struct MobBlast
    {
        public ushort MobId;
        public Vector2 Position;
        public float Radius;
        public int Damage;
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
    public sealed class MobSim : IMobSource, IDeviceBeatSource
    {
        /// <summary>
        /// Type of an automatically spawned basic mob. A mob's type is one byte that holds its origin (bit 0) and its kind (the
        /// bits above), see <see cref="MobKinds"/>; it is what clients use to draw the mob.
        /// </summary>
        public const byte TypeAuto = 0;
        /// <summary>Type of a basic mob the boss summoned.</summary>
        public const byte TypeSummoned = 1;

        /// <summary>A mob under a position order that gets this close has arrived and goes back to chasing survivors.</summary>
        public const float ArrivalRadius = 0.4f;

        const byte OverrideNone = 0;
        const byte OverridePlayer = 1;
        const byte OverridePosition = 2;

        const float CellSize = 1f;

        public int HardCap { get; }
        /// <summary>How many automatically spawned mobs there may be at once (summoned mobs are not counted).</summary>
        public int TargetCount;
        public float Speed = MobConfig.Speed;
        public float SpawnPerSecond = MobConfig.SpawnPerSecond;
        public float Radius = MobConfig.Radius;
        public float SpawnMinSurvivorDistance = MobConfig.SpawnMinSurvivorDistance;
        public int MaxHealth = MobConfig.MobMaxHealth;

        /// <summary>How much longer every mob stays stopped (see <see cref="Freeze"/>).</summary>
        public float FrozenSeconds { get; private set; }

        /// <summary>While true mobs do not move, do not spawn and do not hurt survivors.</summary>
        public bool IsFrozen => FrozenSeconds > 0f;

        readonly int[] health;
        bool gridDirty = true;
        readonly ushort[] ids;
        readonly byte[] types;
        readonly Vector2[] positions;
        readonly Vector2[] displacement;
        readonly byte[] overrideKind;
        readonly ulong[] overrideClient;
        readonly Vector2[] overridePosition;
        // Behavior state: the time until a ranged mob's next shot, or until a lit exploder blasts.
        readonly float[] timer;
        readonly bool[] fuseLit;
        // A device's remaining lifetime (the production countdown is its timer).
        readonly float[] lifetime;
        readonly System.Random behaviorRng;
        readonly List<MobShotRequest> shotRequests = new List<MobShotRequest>();
        readonly List<MobBlast> blasts = new List<MobBlast>();

        readonly Dictionary<ushort, int> indexOf = new Dictionary<ushort, int>();
        readonly List<MobEvent> events = new List<MobEvent>();
        /// <summary>Device beats waiting for the network, and a copy for the view of this machine to show.</summary>
        readonly List<MobEvent> networkBeats = new List<MobEvent>();
        readonly List<MobEvent> viewBeats = new List<MobEvent>();
        const int MaxViewBeats = 64;
        readonly System.Random rng;

        // Spatial hash (counting sort, rebuilt every step)
        readonly int gridW;
        readonly int gridH;
        readonly int[] cellStart;
        readonly int[] cellFill;
        readonly int[] cellItems;
        readonly int[] cellOfMob;

        int count;
        int summonedCount;
        int deviceCount;

        /// <summary>How many devices were placed, destroyed by damage and expired since the last <see cref="Clear"/> (for the harness's report).</summary>
        public int DevicesPlaced { get; private set; }
        public int DevicesDestroyed { get; private set; }
        public int DevicesExpired { get; private set; }

        /// <summary>What the devices do: how long they stand, how often and how much they make. See <see cref="DeviceConfig"/>.</summary>
        public DeviceConfig DeviceSettings = DeviceConfig.Default;

        // The standing order for summoned mobs, kept so the mobs a device makes can obey it too.
        byte standingOrder;
        ulong standingClient;
        Vector2 standingPoint;
        readonly List<ushort> deviceIds = new List<ushort>();

        // Devices destroyed by damage (not by expiry) since the growth server last took them: they give experience of their own.
        readonly List<ushort> deviceKillIds = new List<ushort>();
        readonly List<Vector2> deviceKillPositions = new List<Vector2>();
        ushort nextId = 1;
        float spawnAccumulator;

        public MobSim(int hardCap = MobConfig.HardCap, int targetCount = MobConfig.DefaultTarget, int seed = 12345)
        {
            HardCap = hardCap;
            TargetCount = Math.Min(targetCount, hardCap);
            rng = new System.Random(seed);
            behaviorRng = new System.Random(seed + 7919); // apart from rng, so behaviors never change where automatic mobs appear

            ids = new ushort[hardCap];
            timer = new float[hardCap];
            fuseLit = new bool[hardCap];
            lifetime = new float[hardCap];
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
        public int SummonedCount => summonedCount;
        /// <summary>How many devices stand. A device is no mob kind: it is neither an automatic nor a summoned mob in these counts.</summary>
        public int DeviceCount => deviceCount;
        public int AutoCount => count - summonedCount - deviceCount;
        public bool IsSummoned(int index) => MobKinds.IsSummoned(types[index]) && !MobKinds.IsDeviceType(types[index]);
        public bool IsDevice(int index) => MobKinds.IsDeviceType(types[index]);
        public byte KindAt(int index) => MobKinds.KindOf(types[index]);

        // The basic values are the fields above; a kind's value is the field times the kind's multiple.
        public float SpeedOf(byte kind) => Speed * MobKinds.Get(kind).SpeedMultiple;

        /// <summary>Multiplies the speed of summoned mobs (the standard boss's trait); devices stand still and automatic mobs are unaffected.</summary>
        public float SummonedSpeedFactor { get; set; } = 1f;

        /// <summary>A circle in which mobs move slower (the open rescue point); inactive by default.</summary>
        public bool SlowZoneActive { get; private set; }
        Vector2 slowZoneCenter;
        float slowZoneRadius;
        float slowZoneFactor = 1f;

        /// <summary>Sets or clears the slow zone: a mob whose position is inside moves at this factor of its speed.</summary>
        public void SetSlowZone(bool active, Vector2 center, float radius, float factor)
        {
            SlowZoneActive = active;
            slowZoneCenter = center;
            slowZoneRadius = radius;
            slowZoneFactor = factor;
        }
        public float RadiusOf(byte kind) => Radius * MobKinds.Get(kind).RadiusMultiple;
        public int MaxHealthOf(byte kind) => Mathf.Max(1, Mathf.RoundToInt(MaxHealth * MobKinds.Get(kind).HealthMultiple));
        /// <summary>How many times the basic contact damage this mob's touch deals.</summary>
        public float ContactMultipleOf(byte kind) => MobKinds.Get(kind).ContactDamageMultiple;

        /// <summary>The largest body radius any mob can have, which widens grid queries so no mob is missed.</summary>
        float LargestRadius => Radius * MobKinds.LargestRadiusMultiple;
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
            health[i] = MaxHealthOf(MobKinds.KindOf(type));
            positions[i] = ClampToArena(position, RadiusOf(MobKinds.KindOf(type)));
            overrideKind[i] = OverrideNone;
            fuseLit[i] = false;
            MobKindDef def = MobKinds.Get(MobKinds.KindOf(type));
            // A ranged mob waits a varying part of its cooldown before the first shot, so mobs made together do not fire together.
            timer[i] = def.Behavior == MobBehavior.Ranged ? def.ShotCooldownSeconds * (0.4f + 0.6f * (float)behaviorRng.NextDouble()) : 0f;
            lifetime[i] = 0f;
            if (def.Behavior == MobBehavior.Device)
            {
                timer[i] = DeviceSettings.ProductionIntervalSeconds;
                lifetime[i] = DeviceSettings.LifetimeSeconds;
                deviceCount++;
                DevicesPlaced++;
            }
            indexOf[id] = i;
            if (MobKinds.IsSummoned(type) && !MobKinds.IsDeviceType(type)) summonedCount++;
            gridDirty = true;

            events.Add(new MobEvent { IsSpawn = true, Id = id, Type = type, Position = positions[i] });
            return true;
        }

        public bool Remove(ushort id)
        {
            if (!indexOf.TryGetValue(id, out int i)) return false;

            if (MobKinds.IsDeviceType(types[i])) deviceCount--;
            else if (MobKinds.IsSummoned(types[i])) summonedCount--;

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
                timer[i] = timer[last];
                lifetime[i] = lifetime[last];
                fuseLit[i] = fuseLit[last];
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
            int max = MaxHealthOf(MobKinds.KindOf(types[i]));
            healthPercent = max > 0 ? health[i] * 100 / max : 0;

            if (health[i] <= 0)
            {
                killed = true;
                if (MobKinds.IsDeviceType(types[i]))
                {
                    DevicesDestroyed++;
                    deviceKillIds.Add(id);
                    deviceKillPositions.Add(positions[i]);
                }
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

            float widest = radius + LargestRadius;
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            float length = Mathf.Sqrt(lengthSq);

            GetCellRange(Mathf.Min(a.x, b.x) - widest, Mathf.Min(a.y, b.y) - widest,
                         Mathf.Max(a.x, b.x) + widest, Mathf.Max(a.y, b.y) + widest,
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
                        float reach = radius + RadiusOf(MobKinds.KindOf(types[m]));

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

        /// <summary>One mob a swept circle touched, with where along the sweep (0 to 1) and the point it first touched.</summary>
        public struct SegmentHit
        {
            public ushort MobId;
            public Vector2 Point;
            public float T;
        }

        /// <summary>
        /// Finds every mob a swept circle touches while moving from a to b, nearest first, leaving out the mobs in
        /// <paramref name="exclude"/> (the ones a piercing projectile already hit). The hits are appended to
        /// <paramref name="into"/>; nothing is damaged here. Returns how many were found.
        /// </summary>
        public int CollectSegmentHits(Vector2 a, Vector2 b, float radius, IReadOnlyList<ushort> exclude, List<SegmentHit> into)
        {
            int before = into.Count;
            if (count == 0) return 0;

            EnsureGrid();

            float widest = radius + LargestRadius;
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            float length = Mathf.Sqrt(lengthSq);

            GetCellRange(Mathf.Min(a.x, b.x) - widest, Mathf.Min(a.y, b.y) - widest,
                         Mathf.Max(a.x, b.x) + widest, Mathf.Max(a.y, b.y) + widest,
                         out int x0, out int y0, out int x1, out int y1);

            for (int cy = y0; cy <= y1; cy++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    int c = cy * gridW + cx;
                    for (int k = cellStart[c]; k < cellStart[c + 1]; k++)
                    {
                        int m = cellItems[k];
                        if (IsExcluded(ids[m], exclude)) continue;

                        Vector2 center = positions[m];
                        float reach = radius + RadiusOf(MobKinds.KindOf(types[m]));
                        float tProj = lengthSq > 1e-8f ? Mathf.Clamp01(Vector2.Dot(center - a, ab) / lengthSq) : 0f;
                        Vector2 closest = a + ab * tProj;
                        float d = Vector2.Distance(center, closest);
                        if (d > reach) continue;

                        float back = length > 1e-5f ? Mathf.Sqrt(Mathf.Max(0f, reach * reach - d * d)) / length : 0f;
                        float entry = Mathf.Clamp01(tProj - back);
                        into.Add(new SegmentHit { MobId = ids[m], T = entry, Point = a + ab * entry });
                    }
                }
            }

            // Nearest first; the id breaks ties so the order never depends on the grid layout.
            if (into.Count - before > 1)
            {
                into.Sort(before, into.Count - before, SegmentHitOrder.Instance);
            }
            return into.Count - before;
        }

        sealed class SegmentHitOrder : IComparer<SegmentHit>
        {
            public static readonly SegmentHitOrder Instance = new SegmentHitOrder();

            public int Compare(SegmentHit x, SegmentHit y)
            {
                int byT = x.T.CompareTo(y.T);
                return byT != 0 ? byT : x.MobId.CompareTo(y.MobId);
            }
        }

        static bool IsExcluded(ushort id, IReadOnlyList<ushort> exclude)
        {
            if (exclude == null) return false;
            for (int i = 0; i < exclude.Count; i++)
                if (exclude[i] == id) return true;
            return false;
        }

        /// <summary>Stops every mob for a time. A stop already running is never shortened by a shorter one.</summary>
        public void Freeze(float seconds)
        {
            if (seconds > FrozenSeconds) FrozenSeconds = seconds;
        }

        /// <summary>
        /// Damages every mob whose centre is within <paramref name="halfWidth"/> of the line that starts at
        /// <paramref name="origin"/> and runs <paramref name="length"/> along <paramref name="direction"/>. Where each
        /// killed mob died is appended to <paramref name="deaths"/>. Returns how many mobs were hit.
        /// </summary>
        public int ApplyLineDamage(Vector2 origin, Vector2 direction, float length, float halfWidth, int damage, List<Vector2> deaths)
        {
            if (count == 0 || length <= 0f || halfWidth <= 0f || direction.sqrMagnitude < 1e-8f) return 0;

            EnsureGrid();
            Vector2 dir = direction.normalized;
            Vector2 end = origin + dir * length;
            GetCellRange(Mathf.Min(origin.x, end.x) - halfWidth, Mathf.Min(origin.y, end.y) - halfWidth,
                         Mathf.Max(origin.x, end.x) + halfWidth, Mathf.Max(origin.y, end.y) + halfWidth,
                         out int x0, out int y0, out int x1, out int y1);

            areaTargets.Clear();
            for (int cy = y0; cy <= y1; cy++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    int c = cy * gridW + cx;
                    for (int k = cellStart[c]; k < cellStart[c + 1]; k++)
                    {
                        int m = cellItems[k];
                        Vector2 along = positions[m] - origin;
                        float t = Vector2.Dot(along, dir);
                        if (t < 0f || t > length) continue;
                        float side = Mathf.Abs(along.x * dir.y - along.y * dir.x);
                        if (side <= halfWidth) areaTargets.Add(ids[m]);
                    }
                }
            }

            for (int i = 0; i < areaTargets.Count; i++)
            {
                ushort id = areaTargets[i];
                Vector2 where = default;
                if (indexOf.TryGetValue(id, out int index)) where = positions[index];

                int deviceKillsBefore = deviceKillIds.Count;
                if (ApplyDamage(id, damage, out _, out bool killed) && killed && deaths != null && deviceKillIds.Count == deviceKillsBefore)
                    deaths.Add(where); // a destroyed device is reported by DrainDeviceKills, not here
            }
            return areaTargets.Count;
        }

        /// <summary>
        /// Pushes every mob whose centre is within <paramref name="radius"/> of a point straight away from it by
        /// <paramref name="distance"/>, keeping it inside the arena. A mob exactly on the point is pushed up.
        /// Health is not touched. Returns how many mobs were pushed.
        /// </summary>
        public int Push(Vector2 center, float radius, float distance)
        {
            if (count == 0 || radius <= 0f || distance <= 0f) return 0;

            EnsureGrid();
            GetCellRange(center.x - radius, center.y - radius, center.x + radius, center.y + radius,
                         out int x0, out int y0, out int x1, out int y1);

            // Collect first: pushing changes positions, which the grid was built from.
            areaTargets.Clear();
            float radiusSq = radius * radius;
            for (int cy = y0; cy <= y1; cy++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    int c = cy * gridW + cx;
                    for (int k = cellStart[c]; k < cellStart[c + 1]; k++)
                    {
                        int m = cellItems[k];
                        if (MobKinds.IsDeviceType(types[m])) continue; // a device is not moved
                        if ((positions[m] - center).sqrMagnitude <= radiusSq) areaTargets.Add(ids[m]);
                    }
                }
            }

            for (int i = 0; i < areaTargets.Count; i++)
            {
                if (!indexOf.TryGetValue(areaTargets[i], out int m)) continue;
                Vector2 away = positions[m] - center;
                Vector2 dir = away.sqrMagnitude < 1e-8f ? Vector2.up : away.normalized;
                positions[m] = ClampToArena(positions[m] + dir * distance, RadiusOf(MobKinds.KindOf(types[m])));
            }

            gridDirty = true;
            return areaTargets.Count;
        }

        readonly List<ushort> areaTargets = new List<ushort>();

        /// <summary>
        /// Damages every mob whose centre is within <paramref name="radius"/> of a point. Mobs that reach zero
        /// health are removed as usual, and where each one died is appended to <paramref name="deaths"/>.
        /// Returns how many mobs were hit.
        /// </summary>
        public int ApplyAreaDamage(Vector2 center, float radius, int damage, List<Vector2> deaths)
        {
            if (count == 0 || radius <= 0f) return 0;

            EnsureGrid();
            GetCellRange(center.x - radius, center.y - radius, center.x + radius, center.y + radius,
                         out int x0, out int y0, out int x1, out int y1);

            // Collect first: removing a mob while walking the grid would move others around.
            areaTargets.Clear();
            float radiusSq = radius * radius;
            for (int cy = y0; cy <= y1; cy++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    int c = cy * gridW + cx;
                    for (int k = cellStart[c]; k < cellStart[c + 1]; k++)
                    {
                        int m = cellItems[k];
                        if ((positions[m] - center).sqrMagnitude <= radiusSq) areaTargets.Add(ids[m]);
                    }
                }
            }

            for (int i = 0; i < areaTargets.Count; i++)
            {
                ushort id = areaTargets[i];
                Vector2 where = default;
                if (indexOf.TryGetValue(id, out int index)) where = positions[index];

                int deviceKillsBefore = deviceKillIds.Count;
                if (ApplyDamage(id, damage, out _, out bool killed) && killed && deaths != null && deviceKillIds.Count == deviceKillsBefore)
                    deaths.Add(where); // a destroyed device is reported by DrainDeviceKills, not here
            }
            return areaTargets.Count;
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

        /// <summary>
        /// The highest contact-damage multiple among the mobs touching a survivor whose body has the given radius (a mob touches when
        /// its center is within its own radius plus the survivor's). Returns false when none touches.
        /// </summary>
        public bool MaxContactMultipleWithin(Vector2 center, float survivorRadius, out float multiple)
        {
            multiple = 0f;
            if (count == 0) return false;

            EnsureGrid();
            float widest = survivorRadius + LargestRadius;
            GetCellRange(center.x - widest, center.y - widest, center.x + widest, center.y + widest,
                         out int x0, out int y0, out int x1, out int y1);

            bool touched = false;
            for (int cy = y0; cy <= y1; cy++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    int c = cy * gridW + cx;
                    for (int k = cellStart[c]; k < cellStart[c + 1]; k++)
                    {
                        int m = cellItems[k];
                        byte kind = MobKinds.KindOf(types[m]);
                        if (MobKinds.IsDevice(kind)) continue; // a device hurts nobody by touch
                        float reach = survivorRadius + RadiusOf(kind);
                        if ((positions[m] - center).sqrMagnitude > reach * reach) continue;

                        float own = ContactMultipleOf(kind);
                        if (!touched || own > multiple) multiple = own;
                        touched = true;
                    }
                }
            }
            return touched;
        }

        /// <summary>The device with this identifier was destroyed by damage since the last <see cref="DrainDeviceKills"/>.</summary>
        public bool WasDeviceKill(ushort id) => deviceKillIds.Contains(id);

        /// <summary>
        /// Hands over where the devices destroyed by damage stood and forgets them. A device's destruction is reported here and not in
        /// the death lists of the area and line damage, because it gives a different experience from a killed mob.
        /// </summary>
        public void DrainDeviceKills(List<Vector2> into)
        {
            into.AddRange(deviceKillPositions);
            deviceKillPositions.Clear();
            deviceKillIds.Clear();
        }

        /// <summary>Drops every mob without emitting events (the room ended).</summary>
        public void Clear()
        {
            deviceKillIds.Clear();
            deviceKillPositions.Clear();
            count = 0;
            summonedCount = 0;
            deviceCount = 0;
            DevicesPlaced = DevicesDestroyed = DevicesExpired = 0;
            standingOrder = OverrideNone;
            indexOf.Clear();
            events.Clear();
            networkBeats.Clear();
            viewBeats.Clear();
            shotRequests.Clear();
            blasts.Clear();
            spawnAccumulator = 0f;
            FrozenSeconds = 0f;
        }

        /// <summary>
        /// Hands over what mobs did this step on their own: shots to fire and blasts that happened. The lists belong to the latest
        /// step and are emptied when the next step starts, so the server takes them once per tick.
        /// </summary>
        public void DrainActions(List<MobShotRequest> shots, List<MobBlast> blastsOut)
        {
            shots.AddRange(shotRequests);
            blastsOut.AddRange(blasts);
            shotRequests.Clear();
            blasts.Clear();
        }

        /// <summary>A shot a mob asked for could not be fired (the projectile cap): the mob may ask again right away.</summary>
        public void RefundShot(ushort mobId)
        {
            if (indexOf.TryGetValue(mobId, out int i)) timer[i] = 0f;
        }

        /// <summary>True while the exploding mob's fuse is running (for tests and logs).</summary>
        public bool IsFuseLit(ushort mobId) => indexOf.TryGetValue(mobId, out int i) && fuseLit[i];

        /// <summary>Moves queued spawn/remove events into the list and empties the queue.</summary>
        public void DrainEvents(List<MobEvent> into)
        {
            into.AddRange(events);
            events.Clear();
        }

        /// <summary>Moves the device beats queued for the network into the list and empties the queue.</summary>
        public void DrainNetworkBeats(List<MobEvent> into)
        {
            into.AddRange(networkBeats);
            networkBeats.Clear();
        }

        /// <summary>The beats this machine's view has not shown yet (the host draws from the simulation itself).</summary>
        public void DrainDeviceBeats(List<MobEvent> into)
        {
            into.AddRange(viewBeats);
            viewBeats.Clear();
        }

        void AddBeat(ushort id, byte kind, Vector2 position)
        {
            var beat = new MobEvent { Device = kind, Id = id, Type = types[indexOf[id]], Position = position };
            networkBeats.Add(beat);
            if (viewBeats.Count >= MaxViewBeats) viewBeats.RemoveAt(0); // nobody is drawing (a headless run): keep the newest only
            viewBeats.Add(beat);
        }

        public void Step(float dt, IReadOnlyList<SurvivorInfo> survivors)
        {
            shotRequests.Clear();
            blasts.Clear();

            // The devices keep working while time is stopped: they only count down and make mobs.
            UpdateDevices(dt, survivors);

            if (FrozenSeconds > 0f)
            {
                // Time is stopped: nothing moves and nothing new appears. Mobs can still be damaged and killed.
                FrozenSeconds = Math.Max(0f, FrozenSeconds - dt);
                return;
            }

            AutoSpawn(dt, survivors);
            Chase(dt, survivors);
            UpdateBehaviors(dt, survivors);
            Separate();
            for (int i = 0; i < count; i++) positions[i] = ClampToArena(positions[i], RadiusOf(MobKinds.KindOf(types[i])));
            gridDirty = true; // queries rebuild the grid on demand
        }

        // ---- Target override (used later by boss commands) ----

        public bool SetTarget(ushort id, ulong clientId)
        {
            if (!indexOf.TryGetValue(id, out int i) || MobKinds.IsDeviceType(types[i])) return false;
            overrideKind[i] = OverridePlayer;
            overrideClient[i] = clientId;
            return true;
        }

        public bool SetTargetPosition(ushort id, Vector2 position)
        {
            if (!indexOf.TryGetValue(id, out int i) || MobKinds.IsDeviceType(types[i])) return false;
            overrideKind[i] = OverridePosition;
            overridePosition[i] = ClampToArena(position);
            return true;
        }

        public bool ClearTarget(ushort id)
        {
            if (!indexOf.TryGetValue(id, out int i)) return false;
            overrideKind[i] = OverrideNone;
            return true;
        }

        /// <summary>Gives every summoned mob the same target (automatic mobs are left alone). Returns how many mobs it touched.</summary>
        public int ApplyOrderToSummoned(bool hasPlayer, ulong clientId, bool hasPosition, Vector2 position)
        {
            standingOrder = hasPlayer ? OverridePlayer : hasPosition ? OverridePosition : OverrideNone;
            standingClient = clientId;
            standingPoint = hasPosition ? ClampToArena(position) : default;

            int touched = 0;
            for (int i = 0; i < count; i++)
            {
                if (!MobKinds.IsSummoned(types[i]) || MobKinds.IsDeviceType(types[i])) continue;
                touched++;
                if (hasPlayer) { overrideKind[i] = OverridePlayer; overrideClient[i] = clientId; }
                else if (hasPosition) { overrideKind[i] = OverridePosition; overridePosition[i] = ClampToArena(position); }
                else overrideKind[i] = OverrideNone;
            }
            return touched;
        }

        /// <summary>The target a mob is currently under an order to, for tests and logs.</summary>
        public bool TryGetOverride(ushort id, out bool isPlayer, out ulong clientId, out Vector2 position)
        {
            isPlayer = false; clientId = 0; position = default;
            if (!indexOf.TryGetValue(id, out int i) || overrideKind[i] == OverrideNone) return false;
            isPlayer = overrideKind[i] == OverridePlayer;
            clientId = overrideClient[i];
            position = overridePosition[i];
            return true;
        }

        public void OnPlayerLeft(ulong clientId)
        {
            if (standingOrder == OverridePlayer && standingClient == clientId) standingOrder = OverrideNone;
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
                if (candidate == BossSim.MobId) continue; // reserved: a hit with this identifier is a hit on the boss
                if (!indexOf.ContainsKey(candidate)) return candidate;
            }
        }

        void AutoSpawn(float dt, IReadOnlyList<SurvivorInfo> survivors)
        {
            if (!AnyAlive(survivors)) { spawnAccumulator = 0f; return; }

            if (AutoCount >= TargetCount) { spawnAccumulator = 0f; return; }

            spawnAccumulator += SpawnPerSecond * dt;
            while (spawnAccumulator >= 1f && AutoCount < TargetCount)
            {
                spawnAccumulator -= 1f;
                if (TryPickSpawnPosition(survivors, out Vector2 position))
                    TrySpawn(position, TypeAuto, out _);
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
            for (int i = 0; i < count; i++)
            {
                if (MobKinds.IsDeviceType(types[i])) continue; // a device stands still
                float step = SpeedOf(MobKinds.KindOf(types[i])) * dt;
                if (MobKinds.IsSummoned(types[i])) step *= SummonedSpeedFactor;
                if (SlowZoneActive && (positions[i] - slowZoneCenter).sqrMagnitude <= slowZoneRadius * slowZoneRadius) step *= slowZoneFactor;
                if (!TryGetTargetPosition(i, survivors, out Vector2 target)) continue;

                Vector2 toTarget = target - positions[i];
                float distance = toTarget.magnitude;

                // A position order is a destination: on arrival the mob goes back to chasing the nearest survivor.
                if (overrideKind[i] == OverridePosition && distance <= ArrivalRadius)
                {
                    overrideKind[i] = OverrideNone;
                    if (!TryFindNearestSurvivor(positions[i], survivors, out target)) continue;
                    toTarget = target - positions[i];
                    distance = toTarget.magnitude;
                }

                // Ranged and exploding mobs stop: a ranged mob that has come within its hold distance of a survivor it chases
                // (a point it is ordered to is a destination, not something to keep a distance from) and an exploder whose fuse is lit.
                MobKindDef def = MobKinds.Get(MobKinds.KindOf(types[i]));
                if (def.Behavior == MobBehavior.Ranged && overrideKind[i] != OverridePosition && distance <= def.HoldDistance) continue;
                if (def.Behavior == MobBehavior.Exploder && fuseLit[i]) continue;

                if (distance < 1e-4f) continue;

                positions[i] += toTarget * (Mathf.Min(step, distance) / distance);
            }
        }

        /// <summary>
        /// Runs what the kinds do besides moving: a ranged mob asks to shoot the nearest living survivor within its range when its
        /// cooldown has passed, and an exploder lights its fuse when a living survivor is close and blasts when the fuse ends.
        /// </summary>
        void UpdateBehaviors(float dt, IReadOnlyList<SurvivorInfo> survivors)
        {
            // From the end, so removing a blasted mob (the last mob takes its place) never skips one.
            for (int i = count - 1; i >= 0; i--)
            {
                MobKindDef def = MobKinds.Get(MobKinds.KindOf(types[i]));
                if (def.Behavior == MobBehavior.Chase || def.Behavior == MobBehavior.Device) continue;

                if (def.Behavior == MobBehavior.Ranged)
                {
                    timer[i] = Math.Max(0f, timer[i] - dt);
                    if (timer[i] > 0f) continue;
                    if (!TryFindNearestSurvivor(positions[i], survivors, out Vector2 target)) continue;
                    Vector2 toTarget = target - positions[i];
                    if (toTarget.magnitude > def.ShootRange) continue;

                    shotRequests.Add(new MobShotRequest
                    {
                        MobId = ids[i], Origin = positions[i], Angle = AimMath.FromDirection(toTarget), Kind = MobKinds.KindOf(types[i]),
                    });
                    timer[i] = def.ShotCooldownSeconds;
                    continue;
                }

                // Exploder
                if (!fuseLit[i])
                {
                    if (!AnySurvivorWithin(positions[i], def.TriggerDistance, survivors)) continue;
                    fuseLit[i] = true;
                    timer[i] = def.FuseSeconds;
                    continue;
                }

                timer[i] -= dt;
                if (timer[i] > 0f) continue;

                blasts.Add(new MobBlast { MobId = ids[i], Position = positions[i], Radius = def.BlastRadius, Damage = def.BlastDamage });
                Remove(ids[i]);
            }
        }

        /// <summary>
        /// Runs the devices: each counts down its lifetime and disappears when it has passed (no death record, so no experience), and
        /// at every production interval makes its mobs on a ring around itself under the hard cap and the safe distance. The mobs are
        /// basic summoned mobs that take the standing order, and they cost no energy.
        /// </summary>
        void UpdateDevices(float dt, IReadOnlyList<SurvivorInfo> survivors)
        {
            if (deviceCount == 0) return;

            // Ids first: making mobs and removing a device change the arrays under the loop.
            deviceIds.Clear();
            for (int i = 0; i < count; i++)
                if (MobKinds.IsDeviceType(types[i])) deviceIds.Add(ids[i]);

            DeviceConfig cfg = DeviceSettings;
            for (int d = 0; d < deviceIds.Count; d++)
            {
                if (!indexOf.TryGetValue(deviceIds[d], out int i)) continue;

                lifetime[i] -= dt;
                if (lifetime[i] <= 0f) { DevicesExpired++; Remove(deviceIds[d]); continue; }

                float before = timer[i];
                timer[i] -= dt;
                if (before > cfg.ReadySeconds && timer[i] <= cfg.ReadySeconds && timer[i] > 0f) AddBeat(deviceIds[d], MobEvent.DeviceReady, positions[i]);
                if (timer[i] > 0f) continue;
                timer[i] += cfg.ProductionIntervalSeconds;
                if (timer[i] <= 0f) timer[i] = cfg.ProductionIntervalSeconds;

                AddBeat(deviceIds[d], MobEvent.DeviceProduce, positions[i]);
                MakeMobs(positions[i], survivors, cfg);
            }
        }

        void MakeMobs(Vector2 center, IReadOnlyList<SurvivorInfo> survivors, DeviceConfig cfg)
        {
            for (int n = 0; n < cfg.MobsPerBatch; n++)
            {
                if (count >= HardCap) return;
                if (!SpawnRing.TryPick(center, cfg.RingMin, cfg.RingMax, cfg.MobSafeDistance, Radius, survivors, behaviorRng, out Vector2 position)) continue;
                if (!TrySpawn(position, TypeSummoned, out ushort id)) return;

                if (standingOrder == OverridePlayer) SetTarget(id, standingClient);
                else if (standingOrder == OverridePosition) SetTargetPosition(id, standingPoint);
            }
        }

        static bool AnySurvivorWithin(Vector2 from, float distance, IReadOnlyList<SurvivorInfo> survivors)
        {
            float limit = distance * distance;
            for (int s = 0; s < survivors.Count; s++)
                if (survivors[s].Alive && (survivors[s].Position - from).sqrMagnitude <= limit) return true;
            return false;
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

            for (int i = 0; i < count; i++)
            {
                if (MobKinds.IsDeviceType(types[i])) continue; // a device is neither pushed nor pushes
                float radiusI = RadiusOf(MobKinds.KindOf(types[i]));
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
                            if (j <= i || MobKinds.IsDeviceType(types[j])) continue;
                            PushApart(i, j, radiusI + RadiusOf(MobKinds.KindOf(types[j])));
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

        Vector2 ClampToArena(Vector2 p) => ClampToArena(p, Radius);

        Vector2 ClampToArena(Vector2 p, float radius)
        {
            float hx = ArenaConfig.Width * 0.5f - radius;
            float hy = ArenaConfig.Height * 0.5f - radius;
            return new Vector2(Mathf.Clamp(p.x, -hx, hx), Mathf.Clamp(p.y, -hy, hy));
        }
    }
}
