namespace BS
{
    /// <summary>Tuning constants for growth. Adjusted by playtest; the values are starting assumptions.</summary>
    public static class GrowthConfig
    {
        public const int MaxLevel = 10;
        public const int XpPerKill = 10;
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

        /// <summary>Experience needed to go from <paramref name="level"/> to the next one.</summary>
        public static int XpNeeded(int level) => 30 + 20 * (level - 1);
    }
}
