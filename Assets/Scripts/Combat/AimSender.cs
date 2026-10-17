using UnityEngine;

namespace BS
{
    /// <summary>
    /// Decides when the owner's aim and fire state is worth sending to the server: only when it changed
    /// (fire pressed or released, or the aim moved by about a degree while firing) and never more often than
    /// 20 times per second.
    /// </summary>
    public sealed class AimSender
    {
        public const float MinIntervalSeconds = 0.05f;
        /// <summary>About one degree in the 16-bit angle.</summary>
        public const int AngleStep = 182;

        ushort lastAngle;
        bool lastFiring;
        float lastSendTime = float.NegativeInfinity;

        public bool ShouldSend(float now, ushort angle, bool firing)
        {
            if (now - lastSendTime < MinIntervalSeconds) return false;

            bool changed = firing != lastFiring || (firing && AngleDistance(angle, lastAngle) >= AngleStep);
            if (!changed) return false;

            lastAngle = angle;
            lastFiring = firing;
            lastSendTime = now;
            return true;
        }

        /// <summary>Forgets what was sent, so the next fire state is sent again (after a respawn, for instance).</summary>
        public void Reset()
        {
            lastFiring = false;
            lastSendTime = float.NegativeInfinity;
        }

        static int AngleDistance(ushort a, ushort b)
        {
            int d = Mathf.Abs(a - b);
            return Mathf.Min(d, 65536 - d);
        }
    }
}
