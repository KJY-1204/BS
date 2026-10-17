namespace BS
{
    /// <summary>Tuning constants for survivor combat. Adjusted by playtest; the values are starting assumptions.</summary>
    public static class CombatConfig
    {
        // Survivors
        public const int SurvivorMaxHealth = 100;
        public const float SurvivorRadius = 0.5f;
        public const int ContactDamage = 10;
        /// <summary>After taking damage a survivor cannot be hurt for this long.</summary>
        public const float InvulnerabilitySeconds = 0.6f;

        // Basic attack and projectile
        public const float FireCooldownSeconds = 0.25f;
        public const int ProjectileDamage = 10;
        public const float ProjectileSpeed = 14f;
        public const float ProjectileLifetimeSeconds = 0.9f;
        public const float ProjectileRadius = 0.15f;
        public const int ProjectileCap = 256;

        // Revival
        public const float ReviveRange = 1.5f;
        public const float ReviveSeconds = 4f;
        public const float ReviveHealthFraction = 0.5f;
        public const float ReviveInvulnerabilitySeconds = 2f;

        // Display
        public const float MobHealthBarSeconds = 1.5f;
        /// <summary>A client drops a projectile that outlived its lifetime by this much without an end event.</summary>
        public const float ProjectileStaleMarginSeconds = 0.5f;
    }
}
