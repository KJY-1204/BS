using UnityEngine;

namespace BS
{
    /// <summary>The boss as data. Values are starting assumptions, tuned by playtest.</summary>
    public sealed class BossDef
    {
        public string Name;
        public int MaxHealth;
        public float MoveSpeed;
        public float Radius;
        public int ContactDamage;
        public Color Color;
        /// <summary>How long a match with this boss lasts before the boss wins on time.</summary>
        public float MatchSeconds;
    }

    /// <summary>The boss definitions. Until boss kinds exist there is exactly one.</summary>
    public static class BossCatalog
    {
        static readonly BossDef boss = new BossDef
        {
            Name = "보스",
            MaxHealth = 6000,
            MoveSpeed = 6f,
            Radius = 0.9f,
            ContactDamage = 25,
            Color = new Color(0.85f, 0.15f, 0.15f),
            MatchSeconds = 900f,
        };

        /// <summary>The boss of a match. Boss kinds come later; for now every match has this one.</summary>
        public static BossDef Default => boss;

        /// <summary>
        /// The boss definition with the development overrides applied (-bsBossHealth, -bsMatchSeconds). A copy, so
        /// changing it never changes the shared data.
        /// </summary>
        public static BossDef ForMatch()
        {
            return new BossDef
            {
                Name = boss.Name,
                MaxHealth = DevArgs.BossHealth > 0 ? DevArgs.BossHealth : boss.MaxHealth,
                MoveSpeed = boss.MoveSpeed,
                Radius = boss.Radius,
                ContactDamage = boss.ContactDamage,
                Color = boss.Color,
                MatchSeconds = DevArgs.MatchSeconds > 0f ? DevArgs.MatchSeconds : boss.MatchSeconds,
            };
        }
    }
}
