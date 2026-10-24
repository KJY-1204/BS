using UnityEngine;

namespace BS
{
    /// <summary>
    /// The phases of a match with a boss as data and rules. The boss's health moves the match from phase 1 to 2 to 3 and never back; a change is announced
    /// and brings a burst of mobs, phase 2 unlocks a third boss skill (the F key) and phase 3 shortens the cooldowns. All values are starting assumptions,
    /// tuned with the match harness.
    /// </summary>
    public static class BossPhases
    {
        public const int First = 1;
        public const int Second = 2;
        public const int Third = 3;

        /// <summary>The boss's health, as a share of its maximum, at or below which phase 2 and phase 3 begin.</summary>
        public const float SecondPhaseHealth = 0.66f;
        public const float ThirdPhaseHealth = 0.33f;

        /// <summary>In phase 3 the cooldown of each boss skill is this share of what it would be.</summary>
        public const float ThirdPhaseCooldownFactor = 0.85f;

        /// <summary>How long a phase change is announced before the mobs appear; the same for every kind (the roar of the giant has it too).</summary>
        public const float BurstWarningSeconds = 1.0f;

        /// <summary>The mobs of the burst of phase 2, and of phase 3 as a multiple of it.</summary>
        public const int BurstMobs = 8;
        public const float ThirdPhaseBurstFactor = 1.5f;

        /// <summary>The phase of a boss at this health share (outside 0 to 1 is brought into it).</summary>
        public static int PhaseOf(float healthFraction)
        {
            float f = Mathf.Clamp01(healthFraction);
            if (f <= ThirdPhaseHealth) return Third;
            if (f <= SecondPhaseHealth) return Second;
            return First;
        }

        /// <summary>The mobs of the burst that announces this phase: none for phase 1, <paramref name="baseCount"/> for phase 2 and more for phase 3.</summary>
        public static int BurstMobsOf(int baseCount, int phase)
        {
            if (phase < Second) return 0;
            return phase >= Third ? Mathf.RoundToInt(baseCount * ThirdPhaseBurstFactor) : baseCount;
        }
    }
}
