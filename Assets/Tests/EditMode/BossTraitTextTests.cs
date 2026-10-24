using NUnit.Framework;

namespace BS.Tests
{
    public class BossTraitTextTests
    {
        [Test]
        public void TheGiantAndTheSniperNameTheirTraitAndTheStandardKindHasNone()
        {
            BossDef giant = BossCatalog.Get(BossCatalog.Giant);
            BossDef sniper = BossCatalog.Get(BossCatalog.Sniper);
            BossDef standard = BossCatalog.Get(BossCatalog.Standard);

            Assert.IsNotEmpty(giant.TraitName);
            Assert.IsNotEmpty(giant.TraitText);
            Assert.IsNotEmpty(sniper.TraitName);
            Assert.IsNotEmpty(sniper.TraitText);
            Assert.IsEmpty(standard.TraitName);
            Assert.IsEmpty(standard.TraitText);
        }

        [Test]
        public void TheTextsStateTheNumbersTheRulesUse()
        {
            BossDef giant = BossCatalog.Get(BossCatalog.Giant);
            BossDef sniper = BossCatalog.Get(BossCatalog.Sniper);

            StringAssert.Contains(giant.MovingContactMultiple.ToString("0.0"), giant.TraitText);
            StringAssert.Contains(giant.StillContactMultiple.ToString("0.0"), giant.TraitText);
            StringAssert.Contains(sniper.SteadySeconds.ToString("0"), sniper.TraitText);
            StringAssert.Contains(sniper.SteadyShotMultiple.ToString("0.0"), sniper.TraitText);
        }

        [Test]
        public void TheTraitLineIsEmptyWithoutATraitAndNamesItOtherwise()
        {
            Assert.AreEqual("", SummonHud.TraitLineText(BossCatalog.Get(BossCatalog.Standard)));
            Assert.AreEqual("", SummonHud.TraitLineText(null));
            StringAssert.Contains("정조준", SummonHud.TraitLineText(BossCatalog.Get(BossCatalog.Sniper)));
            StringAssert.Contains("기세", SummonHud.TraitLineText(BossCatalog.Get(BossCatalog.Giant)));
        }

        [Test]
        public void TheKindButtonShowsTheTraitNameOnlyForKindsWithATrait()
        {
            StringAssert.Contains("특성: 기세", NetworkMenu.KindLabel(BossCatalog.Get(BossCatalog.Giant), false));
            StringAssert.DoesNotContain("특성", NetworkMenu.KindLabel(BossCatalog.Get(BossCatalog.Standard), false));
            StringAssert.Contains("(선택함)", NetworkMenu.KindLabel(BossCatalog.Get(BossCatalog.Sniper), true));
            StringAssert.DoesNotContain("(선택함)", NetworkMenu.KindLabel(BossCatalog.Get(BossCatalog.Sniper), false));
        }

        [Test]
        public void TheMatchCopiesCarryTheTexts()
        {
            foreach (BossDef def in BossCatalog.All)
            {
                BossDef copy = BossCatalog.ForMatch(def.Id);
                Assert.AreEqual(def.TraitName, copy.TraitName, def.Name);
                Assert.AreEqual(def.TraitText, copy.TraitText, def.Name);
            }
        }
    }
}
