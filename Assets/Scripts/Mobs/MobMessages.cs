using UnityEngine;

namespace BS
{
    /// <summary>Names of the custom network messages used for mobs, and the server time they are stamped with.</summary>
    public static class MobMessages
    {
        /// <summary>Mob positions, unreliable and sequenced, split into chunks.</summary>
        public const string Snapshot = "bs.mob.snap";
        /// <summary>Spawn and removal events, reliable.</summary>
        public const string Events = "bs.mob.evt";
        /// <summary>Every current mob, sent once to a player who joins a room in progress.</summary>
        public const string Full = "bs.mob.full";

        /// <summary>Server time in seconds of a snapshot tick.</summary>
        public static float ServerTime(uint tick) => tick / MobConfig.SnapshotHz;

        /// <summary>Server time in seconds of an event stamped in milliseconds.</summary>
        public static float ServerTimeFromMs(uint milliseconds) => milliseconds / 1000f;

        /// <summary>
        /// Server time in milliseconds between snapshots: the last snapshot tick plus the time since it was sent.
        /// At a tick boundary it equals <see cref="ServerTime"/>, so events and snapshots share one timeline.
        /// </summary>
        public static uint ServerTimeMs(uint lastTick, float secondsSinceTick)
            => (uint)Mathf.Max(0f, Mathf.Round((lastTick / MobConfig.SnapshotHz + secondsSinceTick) * 1000f));
    }
}
