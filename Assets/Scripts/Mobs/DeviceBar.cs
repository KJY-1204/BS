using System.Collections.Generic;

namespace BS
{
    /// <summary>
    /// What the screens draw of a boss device that can be worked out from plain values: the health a standing device's bar shows and the
    /// line the boss sees about placing one. Kept apart from the views so it can be tested without a scene.
    /// </summary>
    public static class DeviceBar
    {
        /// <summary>How far above the device's centre its bar is drawn.</summary>
        public const float Offset = 0.72f;

        /// <summary>
        /// The health percentage a device's bar shows: the latest one a hit on it told the client, or full when no hit has told it
        /// anything yet (a device that was just placed, or one a late joiner finds already standing).
        /// </summary>
        public static int Percent(IReadOnlyDictionary<ushort, byte> known, ushort id) =>
            known != null && known.TryGetValue(id, out byte percent) ? percent : 100;

        /// <summary>The boss's line about devices: the key, how many stand out of the limit, the cost and either "ready" or the cooldown.</summary>
        public static string HudText(int standing, int limit, float cooldownSeconds, float energyCost)
        {
            string state = standing >= limit && limit > 0 ? "가득 참" : cooldownSeconds > 0f ? $"{cooldownSeconds:F1}초" : "준비";
            return $"R 장치 {standing}/{limit} ({UnityEngine.Mathf.RoundToInt(energyCost)}) {state}";
        }
    }
}
