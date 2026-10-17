using UnityEngine;

namespace BS
{
    /// <summary>
    /// The boss's own ranged attack as data, and how it grows stronger as the boss loses health. All values are
    /// starting assumptions, tuned by playtest. The server and every client read the same shape, so a boss shot is
    /// drawn from its fire event alone.
    /// </summary>
    public static class BossAttack
    {
        /// <summary>Damage of a boss projectile at full boss health.</summary>
        public const int BaseDamage = 15;
        public const float CooldownSeconds = 0.8f;

        // Enrage: at zero health the cooldown is this fraction of normal and the damage this multiple of normal.
        public const float MinCooldownFraction = 0.5f;
        public const float MaxDamageMultiple = 1.5f;

        /// <summary>A boss shot flies slower and is twice as wide as a survivor's, so it can be seen and dodged.</summary>
        public static ProjectileShape Shape => new ProjectileShape { Speed = 11f, Lifetime = 1.6f, Radius = 0.3f, Pierce = 1 };

        /// <summary>The boss's attack: a single straight shot.</summary>
        public static AttackDef Def => new AttackDef
        {
            CooldownSeconds = CooldownSeconds,
            Shots = 1,
            SpreadDegrees = 0f,
            DamageMultiplier = 1f,
            Shape = Shape,
        };

        /// <summary>
        /// Multiplies the fire cooldown: 1 at full health, <see cref="MinCooldownFraction"/> at zero, and straight
        /// in between. A health fraction outside 0 to 1 is brought into it.
        /// </summary>
        public static float CooldownMultiplier(float healthFraction)
        {
            float f = Mathf.Clamp01(healthFraction);
            return Mathf.Lerp(MinCooldownFraction, 1f, f);
        }

        /// <summary>
        /// The damage of a shot fired at this health fraction: <paramref name="baseDamage"/> at full health up to
        /// <see cref="MaxDamageMultiple"/> times it at zero, rounded, never below one.
        /// </summary>
        public static int Damage(int baseDamage, float healthFraction)
        {
            float f = Mathf.Clamp01(healthFraction);
            float multiple = Mathf.Lerp(MaxDamageMultiple, 1f, f);
            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, baseDamage) * multiple));
        }
    }
}
