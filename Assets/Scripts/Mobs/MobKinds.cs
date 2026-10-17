using UnityEngine;

namespace BS
{
    /// <summary>
    /// One kind of mob as data. Every stat is a multiple of the basic mob's value, so changing the basic value changes every
    /// kind with it. Rules read these numbers and never a kind's identity.
    /// </summary>
    public sealed class MobKindDef
    {
        public string Name;
        public float SpeedMultiple = 1f;
        public float HealthMultiple = 1f;
        public float RadiusMultiple = 1f;
        public float ContactDamageMultiple = 1f;
        /// <summary>Multiplies the boss's summon energy cost of one mob.</summary>
        public float CostMultiple = 1f;
        /// <summary>Multiplies the number of mobs one summon tries to create.</summary>
        public float GroupMultiple = 1f;
        public Color Color;
    }

    /// <summary>
    /// The mob kinds and the one byte that tells a mob's origin and kind to every client: bit 0 is the origin (0 automatic,
    /// 1 summoned) and the bits above are the kind. A basic automatic mob is 0 and a basic summoned mob is 1, as before kinds existed.
    /// Values are starting assumptions, tuned by playtest.
    /// </summary>
    public static class MobKinds
    {
        public const byte Basic = 0;
        public const byte Fast = 1;
        public const byte Tough = 2;
        public const int Count = 3;

        public const byte OriginAuto = 0;
        public const byte OriginSummoned = 1;

        /// <summary>
        /// No mob body is bigger than this. Mobs are kept apart using a grid whose cells are one unit wide and only the
        /// neighbouring cells are looked at, which finds every overlapping pair while two radii add up to at most one unit.
        /// </summary>
        public const float MaxRadius = 0.5f;

        static readonly MobKindDef[] kinds =
        {
            new MobKindDef { Name = "기본", Color = new Color(0.95f, 0.55f, 0.15f) },
            new MobKindDef { Name = "빠른 몹", SpeedMultiple = 1.6f, HealthMultiple = 0.5f, RadiusMultiple = 0.8f, ContactDamageMultiple = 0.8f,
                             CostMultiple = 0.8f, GroupMultiple = 1.25f, Color = new Color(0.2f, 0.75f, 0.8f) },
            new MobKindDef { Name = "단단한 몹", SpeedMultiple = 0.6f, HealthMultiple = 4f, RadiusMultiple = 1.25f, ContactDamageMultiple = 1.5f,
                             CostMultiple = 3f, GroupMultiple = 0.5f, Color = new Color(0.5f, 0.28f, 0.14f) },
        };

        /// <summary>The color of a basic mob that a boss summoned; the other kinds keep their own color whatever the origin.</summary>
        public static readonly Color SummonedBasicColor = new Color(0.8f, 0.2f, 0.6f);

        /// <summary>The biggest radius multiple of any kind, used to widen grid queries so no mob is missed.</summary>
        public static float LargestRadiusMultiple
        {
            get
            {
                float largest = 1f;
                for (int i = 0; i < kinds.Length; i++) largest = Mathf.Max(largest, kinds[i].RadiusMultiple);
                return largest;
            }
        }

        public static bool IsValid(byte kind) => kind < kinds.Length;

        /// <summary>The kind's data. A kind that does not exist is treated as the basic kind.</summary>
        public static MobKindDef Get(byte kind) => kinds[IsValid(kind) ? kind : Basic];

        public static byte Encode(byte origin, byte kind) => (byte)((kind << 1) | (origin & 1));

        public static byte OriginOf(byte type) => (byte)(type & 1);

        public static byte KindOf(byte type) => (byte)(type >> 1);

        public static bool IsSummoned(byte type) => (type & 1) != 0;

        /// <summary>The color a mob of this encoded type is drawn in.</summary>
        public static Color ColorOf(byte type)
        {
            byte kind = KindOf(type);
            if (kind == Basic) return IsSummoned(type) ? SummonedBasicColor : Get(Basic).Color;
            return Get(kind).Color;
        }
    }
}
