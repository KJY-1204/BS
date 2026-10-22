namespace BS
{
    /// <summary>
    /// Tuning of the boss's devices: what placing one costs and allows, and how a standing one behaves. The device's health and size
    /// are the device kind's multiples in <see cref="MobKinds"/>. Values are starting assumptions, adjusted by the balance pass.
    /// </summary>
    public sealed class DeviceConfig
    {
        // Placing
        public float EnergyCost = 30f;
        public float PlaceCooldownSeconds = 3f;
        public int MaxDevices = 3;
        /// <summary>A device is not placed when a living survivor is closer than this to the boss.</summary>
        public float PlaceSafeDistance = 3f;

        // A standing device
        public float LifetimeSeconds = 25f;
        public float ProductionIntervalSeconds = 4f;
        public int MobsPerBatch = 3;
        /// <summary>The mobs a device makes appear on a ring around it between these two distances.</summary>
        public float RingMin = 1.2f;
        public float RingMax = 2.4f;
        /// <summary>No mob of a device appears closer than this to a living survivor.</summary>
        public float MobSafeDistance = 2.5f;

        public static DeviceConfig Default => new DeviceConfig();
    }
}
