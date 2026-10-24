using UnityEngine;

namespace BS
{
    /// <summary>The plain rules behind the boss player's controls and what the server tells them: the development key list and how a cooldown is replicated.</summary>
    public static class BossControls
    {
        /// <summary>The boss skill slots named by the letters q (Q, slot 0), e (E, slot 1) and f (F, slot 2) in the text, as bits; other letters mean nothing.</summary>
        public static int SlotMaskOfKeys(string keys)
        {
            int mask = 0;
            foreach (char c in (keys ?? "").ToLowerInvariant())
            {
                if (c == 'q') mask |= 1;
                else if (c == 'e') mask |= 2;
                else if (c == 'f') mask |= 4;
            }
            return mask;
        }

        /// <summary>A cooldown in seconds as the replicated byte: tenths of a second, rounded up, at most 255.</summary>
        public static byte CooldownByte(float seconds) => (byte)Mathf.Clamp(Mathf.CeilToInt(seconds * 10f), 0, 255);
    }
}
