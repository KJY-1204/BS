using NUnit.Framework;

namespace BS.Tests
{
    public class HarnessSkillOverrideTests
    {
        static MatchSession Start(byte kind, params (string name, float value)[] overrides)
        {
            var settings = new HarnessSettings { bossKind = kind };
            settings.overrides.Add(new NamedValue { name = "matchSeconds", value = 30f });
            foreach ((string name, float value) o in overrides) settings.overrides.Add(new NamedValue { name = o.name, value = o.value });
            MatchSession session = MatchSession.Start(settings, 1);
            return session;
        }

        [Test]
        public void TheNewNamesAreKnown()
        {
            Assert.IsTrue(HarnessOverrides.IsKnown("bossSkillQDamage"));
            Assert.IsTrue(HarnessOverrides.IsKnown("bossSkillEDamage"));
        }

        [Test]
        public void OneSkillCanBeChangedWithoutTheOtherAndWithoutTheCatalog()
        {
            byte before = BossCatalog.CurrentKind;
            try
            {
                int catalogQ = BossCatalog.Get(BossCatalog.Sniper).Skills[0].Damage;
                int catalogE = BossCatalog.Get(BossCatalog.Sniper).Skills[1].Damage;

                MatchSession e = Start(BossCatalog.Sniper, ("bossSkillEDamage", 7f));
                Assert.AreEqual(catalogQ, e.BossData.Skills[0].Damage);
                Assert.AreEqual(7, e.BossData.Skills[1].Damage);

                MatchSession q = Start(BossCatalog.Sniper, ("bossSkillQDamage", 3f));
                Assert.AreEqual(3, q.BossData.Skills[0].Damage);
                Assert.AreEqual(catalogE, q.BossData.Skills[1].Damage);

                Assert.AreEqual(catalogQ, BossCatalog.Get(BossCatalog.Sniper).Skills[0].Damage);
                Assert.AreEqual(catalogE, BossCatalog.Get(BossCatalog.Sniper).Skills[1].Damage);
            }
            finally { BossCatalog.SetCurrentKind(before); }
        }

        [Test]
        public void ARefusedNameListsTheNewNames()
        {
            var settings = new HarnessSettings();
            settings.overrides.Add(new NamedValue { name = "bossSkillDamag", value = 1f });

            string problem = settings.Validate();

            StringAssert.Contains("bossSkillQDamage", problem);
            StringAssert.Contains("bossSkillEDamage", problem);
        }
    }
}
