using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>
    /// Picks where a mob made around a point (the boss or a device) appears: on a ring around the point, inside the arena and
    /// never closer than the safe distance to a living survivor. Shared by the boss's summon and by the devices.
    /// </summary>
    public static class SpawnRing
    {
        public const int Attempts = 8;

        public static bool TryPick(Vector2 center, float ringMin, float ringMax, float safeDistance, float bodyRadius,
                                   IReadOnlyList<SurvivorInfo> survivors, System.Random rng, out Vector2 position)
        {
            position = default;
            float safeSq = safeDistance * safeDistance;

            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float distance = ringMin + (float)rng.NextDouble() * (ringMax - ringMin);
                Vector2 p = ClampToArena(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance, bodyRadius);

                bool safe = true;
                for (int s = 0; s < survivors.Count && safe; s++)
                    if (survivors[s].Alive && (survivors[s].Position - p).sqrMagnitude < safeSq) safe = false;

                if (!safe) continue;
                position = p;
                return true;
            }
            return false;
        }

        public static Vector2 ClampToArena(Vector2 p, float margin)
        {
            float hx = ArenaConfig.Width * 0.5f - margin;
            float hy = ArenaConfig.Height * 0.5f - margin;
            return new Vector2(Mathf.Clamp(p.x, -hx, hx), Mathf.Clamp(p.y, -hy, hy));
        }
    }
}
