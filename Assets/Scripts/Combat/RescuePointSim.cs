using System;
using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>The rescue point's numbers. All are starting assumptions, tuned with the match harness.</summary>
    public static class RescueConfig
    {
        /// <summary>Match seconds until the first point opens.</summary>
        public const float FirstOpenSeconds = 90f;
        /// <summary>Seconds from the end of a point (captured or expired) until the next opens.</summary>
        public const float GapSeconds = 120f;
        /// <summary>How long a point stays open without being captured.</summary>
        public const float LifeSeconds = 45f;
        public const float Radius = 2.5f;
        /// <summary>Seconds one survivor needs to fill the progress; more survivors inside fill it proportionally faster.</summary>
        public const float CaptureSeconds = 12f;
        /// <summary>The share of their maximum health the survivors inside get back at the capture.</summary>
        public const float HealFraction = 0.5f;
        /// <summary>The speed factor of a mob inside an open point.</summary>
        public const float SlowFactor = 0.6f;
        /// <summary>A point's centre is at least this far from where the boss starts.</summary>
        public const float MinBossDistance = 12f;
    }

    /// <summary>
    /// The rescue point as plain data: it opens on a schedule, survivors standing in it fill its progress, a capture heals them, and an
    /// open point lets the world slow the mobs inside. The server steps it; clients only draw what is replicated.
    /// </summary>
    public sealed class RescuePointSim
    {
        const int PlaceTries = 60;

        /// <summary>False opens no point (the harness override rescuePoints 0).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>False lets the boss stand in the point without stopping a capture (the harness override rescueContest 0).</summary>
        public bool ContestEnabled { get; set; } = true;

        /// <summary>True when the last step found the boss's body in the open point, which stops the progress.</summary>
        public bool Contested { get; private set; }

        public bool IsOpen { get; private set; }
        public Vector2 Center { get; private set; }
        /// <summary>0 to 1.</summary>
        public float Progress { get; private set; }
        public float OpenSeconds { get; private set; }
        public int Captured { get; private set; }
        /// <summary>How many points have opened in this match.</summary>
        public int Opened { get; private set; }

        public float SecondsLeft => IsOpen ? Mathf.Max(0f, RescueConfig.LifeSeconds - OpenSeconds) : 0f;

        int seed;
        Vector2 bossStart;
        float elapsed;
        float nextOpenAt = RescueConfig.FirstOpenSeconds;

        /// <summary>Sets up a new match: nothing open, the first point due at <see cref="RescueConfig.FirstOpenSeconds"/>.</summary>
        public void Reset(int matchSeed, Vector2 bossStartPosition)
        {
            seed = matchSeed;
            bossStart = bossStartPosition;
            elapsed = 0f;
            nextOpenAt = RescueConfig.FirstOpenSeconds;
            Captured = 0;
            Opened = 0;
            Close();
        }

        /// <summary>No match.</summary>
        public void Clear()
        {
            elapsed = 0f;
            nextOpenAt = RescueConfig.FirstOpenSeconds;
            Captured = 0;
            Opened = 0;
            Close();
        }

        void Close()
        {
            IsOpen = false;
            Contested = false;
            Progress = 0f;
            OpenSeconds = 0f;
        }

        /// <summary>The centre of the point with this index: inside the arena with its circle, far from the boss's start. The same inputs give the same place.</summary>
        public static Vector2 PickCenter(int matchSeed, int index, Vector2 bossStartPosition)
        {
            var rng = new System.Random(unchecked(matchSeed * 31 + index * 7919 + 1));
            float hx = ArenaConfig.Width * 0.5f - RescueConfig.Radius;
            float hy = ArenaConfig.Height * 0.5f - RescueConfig.Radius;
            for (int i = 0; i < PlaceTries; i++)
            {
                var p = new Vector2(Mathf.Lerp(-hx, hx, (float)rng.NextDouble()), Mathf.Lerp(-hy, hy, (float)rng.NextDouble()));
                if (Vector2.Distance(p, bossStartPosition) >= RescueConfig.MinBossDistance) return p;
            }

            Vector2 best = new Vector2(hx, hy);
            float bestDistance = -1f;
            foreach (Vector2 corner in new[] { new Vector2(hx, hy), new Vector2(-hx, hy), new Vector2(hx, -hy), new Vector2(-hx, -hy) })
            {
                float d = Vector2.Distance(corner, bossStartPosition);
                if (d > bestDistance) { bestDistance = d; best = corner; }
            }
            return best;
        }

        /// <summary>Whether a position is inside the open point's circle.</summary>
        public bool Contains(Vector2 position) => IsOpen && (position - Center).sqrMagnitude <= RescueConfig.Radius * RescueConfig.Radius;

        /// <summary>
        /// Advances the schedule and the progress. <paramref name="active"/> is false when the match is not running or the boss does not
        /// stand: nothing opens then and an open point closes. Returns true on the step that captures the point; the living survivors inside
        /// have been healed by then. <paramref name="bossInside"/> says the boss's body overlaps the circle: the point is then contested and
        /// its progress does not rise (its life keeps running).
        /// </summary>
        public bool Step(float dt, bool active, IReadOnlyList<SurvivorBody> bodies, SurvivorSim survivors, bool bossInside = false)
        {
            if (!Enabled || !active)
            {
                if (IsOpen) Close();
                return false;
            }

            elapsed += dt;
            if (!IsOpen)
            {
                if (elapsed < nextOpenAt) return false;
                IsOpen = true;
                Center = PickCenter(seed, Opened, bossStart);
                Opened++;
                Progress = 0f;
                OpenSeconds = 0f;
            }

            OpenSeconds += dt;
            int inside = 0;
            for (int i = 0; i < bodies.Count; i++)
                if (survivors.IsAlive(bodies[i].ClientId) && Contains(bodies[i].Position)) inside++;
            Contested = ContestEnabled && bossInside;
            if (!Contested) Progress = Mathf.Min(1f, Progress + inside * dt / RescueConfig.CaptureSeconds);

            if (Progress >= 1f)
            {
                for (int i = 0; i < bodies.Count; i++)
                {
                    ulong id = bodies[i].ClientId;
                    if (survivors.IsAlive(id) && Contains(bodies[i].Position))
                        survivors.Heal(id, Mathf.Max(1, Mathf.RoundToInt(survivors.MaxHealthOf(id) * RescueConfig.HealFraction)));
                }
                Captured++;
                Close();
                nextOpenAt = elapsed + RescueConfig.GapSeconds;
                return true;
            }

            if (OpenSeconds >= RescueConfig.LifeSeconds)
            {
                Close();
                nextOpenAt = elapsed + RescueConfig.GapSeconds;
            }
            return false;
        }
    }
}
