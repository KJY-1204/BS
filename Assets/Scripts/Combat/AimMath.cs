using UnityEngine;

namespace BS
{
    /// <summary>Aim directions travel as 16-bit angles; the server and every client use the same decoded direction.</summary>
    public static class AimMath
    {
        const float Tau = Mathf.PI * 2f;

        public static ushort QuantizeAngle(float radians)
        {
            float turns = radians / Tau;
            turns -= Mathf.Floor(turns);
            int q = Mathf.RoundToInt(turns * 65536f) & 0xFFFF;
            return (ushort)q;
        }

        public static float DequantizeAngle(ushort angle) => angle / 65536f * Tau;

        public static Vector2 Direction(ushort angle)
        {
            float a = DequantizeAngle(angle);
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        public static ushort FromDirection(Vector2 direction) =>
            direction.sqrMagnitude < 1e-8f ? (ushort)0 : QuantizeAngle(Mathf.Atan2(direction.y, direction.x));
    }
}
