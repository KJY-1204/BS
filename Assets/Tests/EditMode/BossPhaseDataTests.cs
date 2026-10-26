using System.Linq;
using NUnit.Framework;

namespace BS.Tests
{
    public class BossPhaseDataTests
    {
        static readonly BossSkillEffect[] Effects = { BossSkillEffect.ShotRing, BossSkillEffect.Shockwave, BossSkillEffect.Beam, BossSkillEffect.Roar };

        // ---- the rule ----

        [Test]
        public void PhaseOfFollowsTheThresholds()
        {
            Assert.AreEqual(1, BossPhases.PhaseOf(1f));
            Assert.AreEqual(1, BossPhases.PhaseOf(0.67f));
            Assert.AreEqual(2, BossPhases.PhaseOf(0.66f), "the threshold itself belongs to the new phase");
            Assert.AreEqual(2, BossPhases.PhaseOf(0.34f));
            Assert.AreEqual(3, BossPhases.PhaseOf(0.33f));
            Assert.AreEqual(3, BossPhases.PhaseOf(0f));
        }

        [Test]
        public void PhaseOfIsSafeOutsideZeroToOne()
        {
            Assert.AreEqual(3, BossPhases.PhaseOf(-5f));
            Assert.AreEqual(1, BossPhases.PhaseOf(7f));
        }

        [Test]
        public void TheThresholdsAreInOrder()
        {
            Assert.Less(BossPhases.ThirdPhaseHealth, BossPhases.SecondPhaseHealth);
            Assert.Greater(BossPhases.ThirdPhaseHealth, 0f);
            Assert.Less(BossPhases.SecondPhaseHealth, 1f);
            Assert.Less(BossPhases.ThirdPhaseCooldownFactor, 1f);
            Assert.Greater(BossPhases.ThirdPhaseCooldownFactor, 0f);
        }

        [Test]
        public void TheBurstGrowsInPhaseThreeAndDoesNotExistInPhaseOne()
        {
            Assert.AreEqual(0, BossPhases.BurstMobsOf(8, 1));
            Assert.AreEqual(8, BossPhases.BurstMobsOf(8, 2));
            Assert.AreEqual(12, BossPhases.BurstMobsOf(8, 3));
            Assert.AreEqual(0, BossPhases.BurstMobsOf(8, 0));
        }

        // ---- the data of the kinds ----

        [Test]
        public void EveryKindHasAPhaseSkillOnFWhoseEffectIsNotOneOfItsQAndE()
        {
            foreach (BossDef kind in BossCatalog.All)
            {
                Assert.IsNotNull(kind.PhaseSkill, kind.Name);
                Assert.AreEqual("F", kind.PhaseSkill.Key, kind.Name);
                CollectionAssert.DoesNotContain(kind.Skills.Select(s => s.Effect).ToArray(), kind.PhaseSkill.Effect, kind.Name);
            }
        }

        [Test]
        public void ThePhaseSkillsAreTheRoarTheShotRingAndTheShockwave()
        {
            Assert.AreEqual(BossSkillEffect.Roar, BossCatalog.Get(BossCatalog.Standard).PhaseSkill.Effect);
            Assert.AreEqual(BossSkillEffect.ShotRing, BossCatalog.Get(BossCatalog.Giant).PhaseSkill.Effect);
            Assert.AreEqual(BossSkillEffect.Shockwave, BossCatalog.Get(BossCatalog.Sniper).PhaseSkill.Effect);
        }

        [Test]
        public void ThePhaseSkillsHaveTheValuesTheirEffectsNeed()
        {
            foreach (BossDef kind in BossCatalog.All)
            {
                BossSkillDef s = kind.PhaseSkill;
                Assert.Greater(s.CooldownSeconds, 0f, kind.Name);
                if (s.Effect != BossSkillEffect.Roar) Assert.Greater(s.Damage, 0, kind.Name);
                Assert.Greater(s.WarningSeconds, 0f, kind.Name);
                switch (s.Effect)
                {
                    case BossSkillEffect.Beam: Assert.Greater(s.Length, 0f); Assert.Greater(s.HalfWidth, 0f); break;
                    case BossSkillEffect.ShotRing: Assert.Greater(s.Count, 0); break;
                    case BossSkillEffect.Shockwave: Assert.Greater(s.Radius, 0f); break;
                    case BossSkillEffect.Roar: Assert.Greater(s.Count, 0); break;
                }
            }
        }

        [Test]
        public void TheBurstIsARoarWithTheSharedWarningAndTheMobsOfPhaseTwo()
        {
            foreach (BossDef kind in BossCatalog.All)
            {
                Assert.IsNotNull(kind.PhaseBurst, kind.Name);
                Assert.AreEqual(BossSkillEffect.Roar, kind.PhaseBurst.Effect, kind.Name);
                Assert.AreEqual(BossPhases.BurstMobs, kind.PhaseBurst.Count, kind.Name);
                Assert.AreEqual(BossPhases.BurstWarningSeconds, kind.PhaseBurst.WarningSeconds, kind.Name);
            }
        }

        [Test]
        public void TheGiantsRoarHasTheSameWarningAsTheBurstSoAClientDrawsTheRightRing()
        {
            BossSkillDef roar = BossCatalog.Get(BossCatalog.Giant).Skills.Single(s => s.Effect == BossSkillEffect.Roar);
            Assert.AreEqual(BossPhases.BurstWarningSeconds, roar.WarningSeconds);
        }

        [Test]
        public void EveryKindFindsADefinitionForEveryEffectItCanAnnounce()
        {
            foreach (BossDef kind in BossCatalog.All)
            {
                var announced = kind.Skills.Select(s => s.Effect).Concat(new[] { kind.PhaseSkill.Effect, kind.PhaseBurst.Effect }).Distinct().ToArray();
                foreach (BossSkillEffect effect in announced)
                    Assert.IsNotNull(kind.SkillWithEffect(effect), $"{kind.Name} {effect}");
            }
        }

        [Test]
        public void TheLookupPrefersTheKindsOwnSkillsOverThePhaseData()
        {
            BossDef giant = BossCatalog.Get(BossCatalog.Giant);
            Assert.AreSame(giant.Skills.Single(s => s.Effect == BossSkillEffect.Roar), giant.SkillWithEffect(BossSkillEffect.Roar));
            BossDef standard = BossCatalog.Get(BossCatalog.Standard);
            Assert.AreSame(standard.PhaseSkill, standard.SkillWithEffect(BossSkillEffect.Roar), "the rally comes before the burst");
            BossDef sniper = BossCatalog.Get(BossCatalog.Sniper);
            Assert.AreSame(sniper.Skills.Single(s => s.Effect == BossSkillEffect.Beam), sniper.SkillWithEffect(BossSkillEffect.Beam));
            Assert.AreSame(sniper.PhaseBurst, sniper.SkillWithEffect(BossSkillEffect.Roar));
        }

        [Test]
        public void AHarnessMatchPlaysOnACopyOfThePhaseData()
        {
            BossCatalog.SetCurrentKind(BossCatalog.Sniper);
            var settings = new HarnessSettings { bossKind = BossCatalog.Sniper };
            settings.overrides.Add(new NamedValue { name = "mobAutoTarget", value = 0f });
            MatchSession s = MatchSession.Start(settings, 1);

            BossDef used = s.World.BossSkills.Kind;
            BossDef catalog = BossCatalog.Get(BossCatalog.Sniper);
            Assert.IsNotNull(used.PhaseSkill);
            Assert.IsNotNull(used.PhaseBurst);
            Assert.AreNotSame(catalog.PhaseSkill, used.PhaseSkill);
            Assert.AreNotSame(catalog.PhaseBurst, used.PhaseBurst);
            Assert.AreEqual(catalog.PhaseSkill.Damage, used.PhaseSkill.Damage);
        }

        [Test]
        public void TheDevelopmentCopyOfAKeepsThePhaseData()
        {
            BossDef copy = BossCatalog.ForMatch(BossCatalog.Giant);
            Assert.IsNotNull(copy.PhaseSkill);
            Assert.IsNotNull(copy.PhaseBurst);
        }
    }
}
