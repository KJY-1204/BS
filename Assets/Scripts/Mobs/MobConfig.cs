namespace BS
{
    /// <summary>Tuning constants for mobs. Adjusted by playtest; the values are starting assumptions.</summary>
    public static class MobConfig
    {
        /// <summary>Most mobs that may exist at once.</summary>
        public const int HardCap = 100;
        /// <summary>Automatic spawning stops at this many mobs.</summary>
        public const int DefaultTarget = 80;

        public const int MobMaxHealth = 30;

        public const float Speed = 2.5f;
        public const float SpawnPerSecond = 2f;
        /// <summary>In a match with a boss the automatic spawning is only a floor; the boss's summons are the main source.</summary>
        public const float BossMatchSpawnPerSecond = 0.5f;
        public const int BossMatchAutoTarget = 20;
        public const float Radius = 0.4f;
        /// <summary>Automatic spawns avoid positions closer than this to any survivor.</summary>
        public const float SpawnMinSurvivorDistance = 5f;

        // Replication
        public const float SnapshotHz = 15f;
        public const int SnapshotChunkSize = 120;
        public const float InterpolationDelay = 0.13f;
        public const float StaleSeconds = 2f;
    }
}
