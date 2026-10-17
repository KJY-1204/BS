namespace BS
{
    /// <summary>Names of the custom network messages used for mobs.</summary>
    public static class MobMessages
    {
        /// <summary>Mob positions, unreliable and sequenced, split into chunks.</summary>
        public const string Snapshot = "bs.mob.snap";
        /// <summary>Spawn and removal events, reliable.</summary>
        public const string Events = "bs.mob.evt";
        /// <summary>Every current mob, sent once to a player who joins a room in progress.</summary>
        public const string Full = "bs.mob.full";

        public static float ServerTime(uint tick) => tick / MobConfig.SnapshotHz;
    }
}
