using System;
using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>Tuning of the boss's summoning. Values are starting assumptions, adjusted by playtest.</summary>
    public sealed class SummonConfig
    {
        public float MaxEnergy = 100f;
        public float StartEnergy = 50f;
        public float RegenPerSecond = 5f;
        public float CostPerMob = 5f;
        public int GroupSize = 4;
        public float CooldownSeconds = 0.6f;
        /// <summary>Summoned mobs appear on a ring around the boss between these two distances.</summary>
        public float RingMin = 1.5f;
        public float RingMax = 3f;
        /// <summary>No summoned mob appears closer than this to a living survivor.</summary>
        public float SafeDistance = 2.5f;
        /// <summary>A target command names the nearest living survivor within this distance of the pointer.</summary>
        public float PickDistance = 2f;

        public static SummonConfig Default => new SummonConfig();
    }

    public enum SummonResult
    {
        /// <summary>A whole group was created.</summary>
        Created = 0,
        /// <summary>Fewer mobs than a group were created (energy, room on the field or free positions ran short).</summary>
        Partial = 1,
        NoEnergy = 2,
        Cooldown = 3,
        FieldFull = 4,
        /// <summary>No allowed position was found, so nothing was created.</summary>
        NoRoom = 5,
        /// <summary>The match is not running or the boss has fallen.</summary>
        Unavailable = 6,
        /// <summary>The request did not come from the boss.</summary>
        Ignored = 7,
        /// <summary>The request named a mob kind that does not exist.</summary>
        UnknownKind = 8,
    }

    public enum OrderKind : byte
    {
        None = 0,
        /// <summary>Summoned mobs chase one survivor.</summary>
        Player = 1,
        /// <summary>Summoned mobs go to one point and then chase the nearest survivor.</summary>
        Point = 2,
    }

    /// <summary>What the summoning needs to know about the world when a command arrives.</summary>
    public struct SummonContext
    {
        public MobSim Mobs;
        public Vector2 BossPosition;
        /// <summary>The boss exists and has not fallen.</summary>
        public bool BossStanding;
        public bool MatchRunning;
        public IReadOnlyList<SurvivorInfo> Survivors;
    }

    /// <summary>
    /// The boss's summoning as plain data: the energy that refills with time, summons that create a group of mobs on a ring
    /// around the boss, and the standing order that tells summoned mobs where to go. No GameObjects, so it can be tested without a scene.
    /// </summary>
    public sealed class SummonSim
    {
        const int PlacementAttempts = 8;

        public SummonConfig Config { get; }

        public float Energy { get; private set; }
        public float CooldownLeft { get; private set; }

        public OrderKind Order { get; private set; }
        /// <summary>The ordered survivor when <see cref="Order"/> is <see cref="OrderKind.Player"/>.</summary>
        public ulong OrderClient { get; private set; }
        /// <summary>The ordered point when <see cref="Order"/> is <see cref="OrderKind.Point"/> (already inside the arena).</summary>
        public Vector2 OrderPoint { get; private set; }

        readonly System.Random rng;

        public SummonSim(SummonConfig config = null, int seed = 4242)
        {
            Config = config ?? SummonConfig.Default;
            rng = new System.Random(seed);
            Reset();
        }

        /// <summary>A new match: starting energy, no cooldown, no order.</summary>
        public void Reset()
        {
            Energy = Mathf.Min(Config.StartEnergy, Config.MaxEnergy);
            CooldownLeft = 0f;
            Order = OrderKind.None;
            OrderClient = 0;
            OrderPoint = default;
        }

        /// <summary>Advances time. Energy refills and the cooldown runs down only while the match is running.</summary>
        public void Step(float dt, bool matchRunning)
        {
            if (!matchRunning) return;

            Energy = Mathf.Min(Config.MaxEnergy, Energy + Config.RegenPerSecond * dt);
            CooldownLeft = Mathf.Max(0f, CooldownLeft - dt);
        }

        // ---- Summoning ----

        /// <summary>
        /// Summons a group around the boss. Creates as many mobs as the energy pays for, the field has room for and positions
        /// allow, up to the group size, and takes energy only for the mobs it created.
        /// </summary>
        public SummonResult Summon(in SummonContext ctx, bool fromBoss, out int created) =>
            Summon(ctx, fromBoss, MobKinds.Basic, out created);

        /// <summary>The energy one mob of a kind costs.</summary>
        public float CostOf(byte kind) => Config.CostPerMob * MobKinds.Get(kind).CostMultiple;

        /// <summary>How many mobs one summon of a kind tries to create (at least one).</summary>
        public int GroupSizeOf(byte kind) => Math.Max(1, Mathf.RoundToInt(Config.GroupSize * MobKinds.Get(kind).GroupMultiple));

        /// <summary>
        /// Summons a group of the given kind. A kind that does not exist is refused without taking energy or starting the
        /// cooldown, the same as every other refusal.
        /// </summary>
        public SummonResult Summon(in SummonContext ctx, bool fromBoss, byte kind, out int created)
        {
            created = 0;
            if (!fromBoss) return SummonResult.Ignored;
            if (!ctx.MatchRunning || !ctx.BossStanding) return SummonResult.Unavailable;
            if (!MobKinds.IsValid(kind)) return SummonResult.UnknownKind;
            if (CooldownLeft > 0f) return SummonResult.Cooldown;

            MobSim mobs = ctx.Mobs;
            int room = mobs.HardCap - mobs.Count;
            if (room <= 0) return SummonResult.FieldFull;

            float cost = CostOf(kind);
            int group = GroupSizeOf(kind);
            int affordable = cost > 0f ? Mathf.FloorToInt(Energy / cost + 1e-4f) : group;
            if (affordable <= 0) return SummonResult.NoEnergy;

            float radius = mobs.RadiusOf(kind);
            byte type = MobKinds.Encode(MobKinds.OriginSummoned, kind);
            int wanted = Math.Min(group, Math.Min(affordable, room));
            for (int i = 0; i < wanted; i++)
            {
                if (!TryPickPosition(ctx, radius, out Vector2 position)) continue;
                if (!mobs.TrySpawn(position, type, out ushort id)) break;

                ApplyOrderTo(mobs, id);
                created++;
            }

            if (created == 0) return SummonResult.NoRoom;

            Energy = Mathf.Max(0f, Energy - created * cost);
            CooldownLeft = Config.CooldownSeconds;
            return created == group ? SummonResult.Created : SummonResult.Partial;
        }

        bool TryPickPosition(in SummonContext ctx, float radius, out Vector2 position)
        {
            position = default;
            float safeSq = Config.SafeDistance * Config.SafeDistance;

            for (int attempt = 0; attempt < PlacementAttempts; attempt++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float distance = Config.RingMin + (float)rng.NextDouble() * (Config.RingMax - Config.RingMin);
                Vector2 p = ClampToArena(ctx.BossPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance, radius);

                bool safe = true;
                for (int s = 0; s < ctx.Survivors.Count && safe; s++)
                    if (ctx.Survivors[s].Alive && (ctx.Survivors[s].Position - p).sqrMagnitude < safeSq) safe = false;

                if (!safe) continue;
                position = p;
                return true;
            }
            return false;
        }

        // ---- Orders ----

        /// <summary>
        /// A target command at a pointer position: names the nearest living survivor within the pick distance, otherwise orders the
        /// point (pulled inside the arena). Applies to every summoned mob and to mobs summoned later.
        /// </summary>
        public bool GiveOrder(in SummonContext ctx, bool fromBoss, Vector2 pointer)
        {
            if (!fromBoss || !ctx.MatchRunning || !ctx.BossStanding) return false;

            float best = Config.PickDistance * Config.PickDistance;
            bool found = false;
            ulong client = 0;
            for (int s = 0; s < ctx.Survivors.Count; s++)
            {
                if (!ctx.Survivors[s].Alive) continue;
                float d = (ctx.Survivors[s].Position - pointer).sqrMagnitude;
                if (d <= best) { best = d; client = ctx.Survivors[s].ClientId; found = true; }
            }

            if (found)
            {
                Order = OrderKind.Player;
                OrderClient = client;
                ctx.Mobs.ApplyOrderToSummoned(true, client, false, default);
            }
            else
            {
                Order = OrderKind.Point;
                OrderPoint = ClampToArena(pointer, ctx.Mobs.Radius);
                ctx.Mobs.ApplyOrderToSummoned(false, 0, true, OrderPoint);
            }
            return true;
        }

        /// <summary>The clear command: summoned mobs chase the nearest survivor again.</summary>
        public bool ClearOrder(in SummonContext ctx, bool fromBoss)
        {
            if (!fromBoss || !ctx.MatchRunning || !ctx.BossStanding) return false;

            Order = OrderKind.None;
            ctx.Mobs.ApplyOrderToSummoned(false, 0, false, default);
            return true;
        }

        /// <summary>The ordered survivor left the room: the order ends. (The mob simulation drops its own copies of the order.)</summary>
        public void OnPlayerLeft(ulong clientId)
        {
            if (Order == OrderKind.Player && OrderClient == clientId) Order = OrderKind.None;
        }

        void ApplyOrderTo(MobSim mobs, ushort id)
        {
            if (Order == OrderKind.Player) mobs.SetTarget(id, OrderClient);
            else if (Order == OrderKind.Point) mobs.SetTargetPosition(id, OrderPoint);
        }

        static Vector2 ClampToArena(Vector2 p, float margin)
        {
            float hx = ArenaConfig.Width * 0.5f - margin;
            float hy = ArenaConfig.Height * 0.5f - margin;
            return new Vector2(Mathf.Clamp(p.x, -hx, hx), Mathf.Clamp(p.y, -hy, hy));
        }
    }
}
