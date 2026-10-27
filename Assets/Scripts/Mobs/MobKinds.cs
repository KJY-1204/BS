using UnityEngine;

namespace BS
{
    /// <summary>What a kind of mob does besides moving toward its target.</summary>
    public enum MobBehavior : byte
    {
        /// <summary>Moves toward the target and hurts a survivor by touching it, as every mob did before behaviors existed.</summary>
        Chase = 0,
        /// <summary>Stops at a distance from its target and shoots the nearest survivor.</summary>
        Ranged = 1,
        /// <summary>Runs in, stops for a short fuse when a survivor is close and blasts the survivors around it.</summary>
        Exploder = 2,
        /// <summary>Stands still, makes mobs at an interval and disappears after a lifetime. Only the boss's device has it.</summary>
        Device = 3,
    }

    /// <summary>
    /// One kind of mob as data. Every stat is a multiple of the basic mob's value, so changing the basic value changes every
    /// kind with it. Rules read these numbers and never a kind's identity.
    /// </summary>
    public sealed class MobKindDef
    {
        public string Name;
        /// <summary>The key of the kind's sprite set in the sprite library.</summary>
        public string SpriteId = "";
        public float SpeedMultiple = 1f;
        public float HealthMultiple = 1f;
        public float RadiusMultiple = 1f;
        public float ContactDamageMultiple = 1f;
        /// <summary>Multiplies the boss's summon energy cost of one mob.</summary>
        public float CostMultiple = 1f;
        /// <summary>Multiplies the number of mobs one summon tries to create.</summary>
        public float GroupMultiple = 1f;
        public Color Color;

        public MobBehavior Behavior = MobBehavior.Chase;

        // Ranged: stops this close to its target, shoots the nearest survivor within the range, once per cooldown.
        public float HoldDistance;
        public float ShootRange;
        public float ShotCooldownSeconds;
        public int ShotDamage;

        // Exploder: a survivor this close starts the fuse; when it ends survivors within the blast radius take the damage.
        public float TriggerDistance;
        public float FuseSeconds;
        public float BlastRadius;
        public int BlastDamage;
    }

    /// <summary>The projectile a ranged mob fires, read by the server and by every client so a shot is drawn from its fire event alone.</summary>
    public static class MobShot
    {
        public static ProjectileShape Shape => new ProjectileShape { Speed = 8f, Lifetime = 1.6f, Radius = 0.2f, Pierce = 1 };
    }

    /// <summary>
    /// One kind of mob as data.

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
        public const byte Ranged = 3;
        public const byte Exploder = 4;
        /// <summary>The five kinds of mob, which the boss can pick from. The device is not one of them (see <see cref="Device"/>).</summary>
        public const int Count = 5;
        /// <summary>
        /// The boss's device: a still body that makes mobs. It uses the mob entry, the type byte and the mob messages but is no mob
        /// kind in the rules (the boss cannot summon it by hand and it is no kind that orders, rewards or the number keys know).
        /// </summary>
        public const byte Device = 5;

        public const byte OriginAuto = 0;
        public const byte OriginSummoned = 1;

        /// <summary>
        /// No mob body is bigger than this. Mobs are kept apart using a grid whose cells are one unit wide and only the
        /// neighbouring cells are looked at, which finds every overlapping pair while two radii add up to at most one unit.
        /// </summary>
        public const float MaxRadius = 0.5f;

        static readonly MobKindDef[] kinds =
        {
            new MobKindDef { Name = "기본", SpriteId = "mob_fairy_red", Color = new Color(0.95f, 0.55f, 0.15f) },
            new MobKindDef { Name = "빠른 몹", SpriteId = "mob_fairy_blue", SpeedMultiple = 1.6f, HealthMultiple = 0.5f, RadiusMultiple = 0.8f, ContactDamageMultiple = 0.8f,
                             CostMultiple = 0.8f, GroupMultiple = 1.25f, Color = new Color(0.2f, 0.75f, 0.8f) },
            new MobKindDef { Name = "단단한 몹", SpriteId = "mob_umbrella", SpeedMultiple = 0.6f, HealthMultiple = 4f, RadiusMultiple = 1.25f, ContactDamageMultiple = 1.5f,
                             CostMultiple = 3f, GroupMultiple = 0.5f, Color = new Color(0.5f, 0.28f, 0.14f) },
            new MobKindDef { Name = "원거리 몹", SpriteId = "mob_ghost", SpeedMultiple = 1f, HealthMultiple = 0.8f, RadiusMultiple = 1f, ContactDamageMultiple = 0.6f,
                             CostMultiple = 2f, GroupMultiple = 0.5f, Color = new Color(0.6f, 0.4f, 0.9f),
                             Behavior = MobBehavior.Ranged, HoldDistance = 6f, ShootRange = 9f, ShotCooldownSeconds = 2.5f, ShotDamage = 8 },
            new MobKindDef { Name = "폭발 몹", SpriteId = "mob_ufo", SpeedMultiple = 1.4f, HealthMultiple = 0.6f, RadiusMultiple = 1f, ContactDamageMultiple = 0.5f,
                             CostMultiple = 1.6f, GroupMultiple = 0.75f, Color = new Color(0.95f, 0.85f, 0.2f),
                             Behavior = MobBehavior.Exploder, TriggerDistance = 1.6f, FuseSeconds = 0.7f, BlastRadius = 2.2f, BlastDamage = 25 },
            // The boss's device: a still, sturdy body that hurts nobody by touch. Speed and contact damage are zero.
            new MobKindDef { Name = "장치", SpriteId = "device", SpeedMultiple = 0f, HealthMultiple = 4f, RadiusMultiple = 1.15f, ContactDamageMultiple = 0f,
                             CostMultiple = 0f, GroupMultiple = 0f, Color = new Color(0.25f, 0.9f, 0.45f), Behavior = MobBehavior.Device },
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

        /// <summary>The kind exists, the device included.</summary>
        public static bool IsValid(byte kind) => kind < kinds.Length;

        /// <summary>The boss can summon this kind by hand (every kind but the device).</summary>
        public static bool IsSummonable(byte kind) => kind < Count;

        public static bool IsDevice(byte kind) => kind == Device;

        /// <summary>The encoded type byte is a device's.</summary>
        public static bool IsDeviceType(byte type) => KindOf(type) == Device;

        /// <summary>The kind's data. A kind that does not exist is treated as the basic kind.</summary>
        public static MobKindDef Get(byte kind) => kinds[IsValid(kind) ? kind : Basic];

        public static byte Encode(byte origin, byte kind) => (byte)((kind << 1) | (origin & 1));

        public static byte OriginOf(byte type) => (byte)(type & 1);

        public static byte KindOf(byte type) => (byte)(type >> 1);

        public static bool IsSummoned(byte type) => (type & 1) != 0;

        /// <summary>The key of the sprite set a mob of this encoded type is drawn with: a basic mob a boss summoned is a green fairy, not a red one.</summary>
        public static string SpriteIdOf(byte type)
        {
            byte kind = KindOf(type);
            if (kind == Basic && IsSummoned(type)) return "mob_fairy_green";
            return Get(kind).SpriteId;
        }

        /// <summary>The color a mob of this encoded type is drawn in.</summary>
        public static Color ColorOf(byte type)
        {
            byte kind = KindOf(type);
            if (kind == Basic) return IsSummoned(type) ? SummonedBasicColor : Get(Basic).Color;
            return Get(kind).Color;
        }
    }
}
