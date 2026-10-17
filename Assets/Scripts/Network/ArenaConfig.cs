using UnityEngine;

namespace BS
{
    /// <summary>Arena size in world units (PPU 32 => 1 unit = one character). Tuned by playtest.</summary>
    public static class ArenaConfig
    {
        public const float Width = 32f;
        public const float Height = 20f;
        public const float CharacterMargin = 0.5f;

        public static Vector2 ClampCharacter(Vector2 p)
        {
            float hx = Width * 0.5f - CharacterMargin;
            float hy = Height * 0.5f - CharacterMargin;
            return new Vector2(Mathf.Clamp(p.x, -hx, hx), Mathf.Clamp(p.y, -hy, hy));
        }

        /// <summary>Role 0 is the boss, roles 1..4 are survivor slots.</summary>
        public static Vector2 SpawnPoint(byte role)
        {
            switch (role)
            {
                case 0: return new Vector2(0f, 6f);
                case 1: return new Vector2(-3f, -2f);
                case 2: return new Vector2(3f, -2f);
                case 3: return new Vector2(-3f, -5f);
                default: return new Vector2(3f, -5f);
            }
        }
    }
}
