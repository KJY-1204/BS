using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    /// <summary>
    /// The rules of boss-kinds that keep matches long: every boss is slower than every survivor and has far more health than they can
    /// deal in a moment. The tuned numbers themselves come from harness runs (tools/harness/pacing/results.md).
    /// </summary>
    public class BossPacingTests
    {
        static float SlowestSurvivor()
        {
            float slowest = float.MaxValue;
            foreach (CharacterDef c in CharacterCatalog.All) slowest = Mathf.Min(slowest, c.MoveSpeed);
            return slowest;
        }

        /// <summary>The damage a survivor's basic attack deals in one second at the first level, the highest of the characters.</summary>
        static float BestBasicAttackPerSecond()
        {
            float best = 0f;
            foreach (CharacterDef c in CharacterCatalog.All)
            {
                float perSecond = CombatConfig.ProjectileDamage * c.Attack.DamageMultiplier * c.Attack.Shots / c.Attack.CooldownSeconds;
                best = Mathf.Max(best, perSecond);
            }
            return best;
        }

        [Test]
        public void TheKindsHaveTheTunedHealthAndSpeed()
        {
            // Set with the match harness; change these together with a new run (tools/harness/pacing/results.md).
            BossDef standard = BossCatalog.Get(BossCatalog.Standard);
            Assert.AreEqual(190000, standard.MaxHealth);
            Assert.AreEqual(4.5f, standard.MoveSpeed);

            BossDef giant = BossCatalog.Get(BossCatalog.Giant);
            Assert.AreEqual(230000, giant.MaxHealth);
            Assert.AreEqual(4.0f, giant.MoveSpeed);

            BossDef sniper = BossCatalog.Get(BossCatalog.Sniper);
            Assert.AreEqual(170000, sniper.MaxHealth);
            Assert.AreEqual(5.0f, sniper.MoveSpeed);
            Assert.AreEqual(5, sniper.Attack.BaseDamage);
            Assert.AreEqual(30, sniper.SkillWithEffect(BossSkillEffect.Beam).Damage);
        }

        [Test]
        public void EveryBossIsSlowerThanEverySurvivor()
        {
            float slowest = SlowestSurvivor();
            foreach (BossDef kind in BossCatalog.All)
            {
                foreach (CharacterDef c in CharacterCatalog.All)
                    Assert.Less(kind.MoveSpeed, c.MoveSpeed, $"{kind.Name} against {c.Name}");
                Assert.Less(kind.MoveSpeed, slowest, kind.Name);
            }
        }

        [Test]
        public void TheKindsAreOrderedBySpeedGiantStandardSniper()
        {
            Assert.Less(BossCatalog.Get(BossCatalog.Giant).MoveSpeed, BossCatalog.Get(BossCatalog.Standard).MoveSpeed);
            Assert.Less(BossCatalog.Get(BossCatalog.Standard).MoveSpeed, BossCatalog.Get(BossCatalog.Sniper).MoveSpeed);
        }

        [Test]
        public void EveryBossHasAtLeastAThousandTimesWhatASurvivorDealsInASecond()
        {
            float perSecond = BestBasicAttackPerSecond();
            Assert.Greater(perSecond, 0f);

            foreach (BossDef kind in BossCatalog.All)
                Assert.GreaterOrEqual(kind.MaxHealth, 1000f * perSecond, $"{kind.Name} ({kind.MaxHealth} against {perSecond}/s)");
        }

        [Test]
        public void TheSniperIsStillTheFastestAndTheFastShooter()
        {
            BossDef standard = BossCatalog.Get(BossCatalog.Standard);
            BossDef sniper = BossCatalog.Get(BossCatalog.Sniper);

            Assert.Greater(sniper.MoveSpeed, standard.MoveSpeed);
            Assert.Less(sniper.Attack.CooldownSeconds, standard.Attack.CooldownSeconds);
            Assert.Less(sniper.MaxHealth, standard.MaxHealth);
            Assert.Less(sniper.Radius, standard.Radius);
        }

        // ---- a survivor outruns the boss in the simulation ----

        static float GapAfterRunning(byte kind, byte character, float seconds)
        {
            var settings = new HarnessSettings { bossKind = kind, characters = new int[] { character } };
            settings.overrides.Add(new NamedValue { name = "mobAutoTarget", value = 0f });
            settings.overrides.Add(new NamedValue { name = "summonMaxEnergy", value = 0f });
            settings.overrides.Add(new NamedValue { name = "matchSeconds", value = 60f });
            BossCatalog.SetCurrentKind(kind);
            MatchSession s = MatchSession.Start(settings, 1);

            for (int i = 0; i < s.World.Bodies.Count; i++)
            {
                MatchBody b = s.World.Bodies[i];
                b.Position = b.ClientId == MatchSession.BossId ? new Vector2(-14f, 0f) : new Vector2(-5f, 0f);
                s.World.Bodies[i] = b;
            }

            float before = s.PositionOf(1).x - s.PositionOf(MatchSession.BossId).x;
            int steps = Mathf.RoundToInt(seconds / s.Step);
            for (int i = 0; i < steps; i++)
            {
                s.Move(1, Vector2.right);                    // the survivor walks straight away
                s.Move(MatchSession.BossId, Vector2.right);  // the boss walks straight after it
                s.Advance();
            }
            return (s.PositionOf(1).x - s.PositionOf(MatchSession.BossId).x) - before;
        }

        [Test]
        public void ASurvivorWalkingAwayFromABossWalkingAfterItGainsDistanceForEveryKindAndCharacter()
        {
            byte before = BossCatalog.CurrentKind;
            try
            {
                foreach (BossDef kind in BossCatalog.All)
                {
                    foreach (CharacterDef c in CharacterCatalog.All)
                    {
                        float gained = GapAfterRunning(kind.Id, c.Id, 2f);
                        float expected = (c.MoveSpeed - kind.MoveSpeed) * 2f;
                        Assert.Greater(gained, 0.5f * expected, $"{kind.Name} against {c.Name}");
                        Assert.AreEqual(expected, gained, 0.2f, $"{kind.Name} against {c.Name}");
                    }
                }
            }
            finally { BossCatalog.SetCurrentKind(before); }
        }
    }
}
