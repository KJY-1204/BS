using UnityEngine;

namespace BS
{
    /// <summary>
    /// The boss kinds' traits as pure functions of the kind's data: the sniper's Steady (shots hit harder after it stood still) and the
    /// giant's Momentum (contact hits harder while it moves, softer while it stands). A kind with no trait scales nothing.
    /// </summary>
    public static class BossTraits
    {
        /// <summary>The multiple of a basic shot's damage after the boss stood still this long; 1 for a kind with no such trait.</summary>
        public static float ShotMultiple(BossDef def, float stillSeconds)
        {
            if (def == null || def.SteadySeconds <= 0f || def.SteadyShotMultiple <= 1f) return 1f;
            return stillSeconds >= def.SteadySeconds ? def.SteadyShotMultiple : 1f;
        }

        /// <summary>The contact damage while the boss moves or stands still: the base damage scaled by the kind's multiple, rounded, never below one.</summary>
        public static int ContactDamage(BossDef def, int baseDamage, bool moving)
        {
            if (def == null) return baseDamage;
            float multiple = moving ? def.MovingContactMultiple : def.StillContactMultiple;
            if (Mathf.Approximately(multiple, 1f)) return baseDamage;
            return Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiple));
        }
    }
}
