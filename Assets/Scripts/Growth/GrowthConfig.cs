namespace BS
{
    /// <summary>Tuning constants for growth. Adjusted by playtest; the values are starting assumptions.</summary>
    public static class GrowthConfig
    {
        public const int MaxLevel = 20;
        public const int XpPerKill = 10;
        /// <summary>The base experience for destroying a boss device, more than a mob gives.</summary>
        public const int XpPerDevice = 30;
        /// <summary>Living survivors within this distance of where a mob died share its experience.</summary>
        public const float XpShareDistance = 10f;

        public const int OfferChoices = 3;
        public const float OfferSeconds = 10f;

        public const int BaseMaxMana = 100;
        public const float BaseManaRegenPerSecond = 5f;

        // Automatic growth for every level gained after the first.
        public const int HealthPerLevel = 10;
        public const int ManaPerLevel = 5;
        public const int DamagePerLevel = 1;

        /// <summary>The last level whose step grows by <see cref="XpStepEarly"/>; later steps grow more slowly.</summary>
        const int XpEarlyLevels = 9;
        const int XpBase = 30;
        const int XpStepEarly = 20;
        const int XpStepLate = 10;

        /// <summary>
        /// Experience needed to go from <paramref name="level"/> to the next one: 30 at the first level, 20 more per level up to
        /// the ninth (190), then 10 more per level. It never gets smaller as the level rises.
        /// </summary>
        public static int XpNeeded(int level) => level <= XpEarlyLevels
            ? XpBase + XpStepEarly * (level - 1)
            : XpBase + XpStepEarly * (XpEarlyLevels - 1) + XpStepLate * (level - XpEarlyLevels);
    }
}
