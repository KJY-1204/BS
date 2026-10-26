using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class LevelUpHealTests
    {
        static HarnessSettings Settings(params (string name, float value)[] overrides)
        {
            var s = new HarnessSettings();
            s.overrides.Add(new NamedValue { name = "mobAutoTarget", value = 0f });
            s.overrides.Add(new NamedValue { name = "matchSeconds", value = 120f });
            foreach ((string name, float value) o in overrides) s.overrides.Add(new NamedValue { name = o.name, value = o.value });
            return s;
        }

        static MatchSession Start(params (string name, float value)[] overrides)
        {
            BossCatalog.SetCurrentKind(BossCatalog.Standard);
            return MatchSession.Start(Settings(overrides), 1);
        }

        static void LevelUp(MatchSession s, ulong id, int levels = 1)
        {
            for (int i = 0; i < levels; i++) s.World.Leveling.AddXp(id, s.World.Leveling.XpNeeded(id));
            s.Advance();
        }

        static ulong First(MatchSession s) => s.SurvivorIds[0];

        [Test]
        public void ALevelUpHealsTenPercentOfTheNewMaximumOfAHurtSurvivor()
        {
            MatchSession s = Start();
            ulong id = First(s);
            s.World.Survivors.ApplyDamage(id, 70);
            int before = s.World.Survivors.Health(id);

            LevelUp(s, id);

            int max = s.World.Survivors.MaxHealthOf(id);
            Assert.AreEqual(before + Mathf.RoundToInt(max * GrowthConfig.LevelUpHealFraction), s.World.Survivors.Health(id));
            Assert.Greater(s.World.Survivors.Health(id), before);
        }

        [Test]
        public void AtFullHealthALevelUpAddsTheHealToTheOldHealthAndNeverPassesTheNewMaximum()
        {
            MatchSession s = Start();
            ulong id = First(s);
            int before = s.World.Survivors.Health(id);
            Assert.AreEqual(s.World.Survivors.MaxHealthOf(id), before);

            LevelUp(s, id);

            int max = s.World.Survivors.MaxHealthOf(id);
            Assert.AreEqual(Mathf.Min(max, before + Mathf.RoundToInt(max * GrowthConfig.LevelUpHealFraction)), s.World.Survivors.Health(id));
            Assert.LessOrEqual(s.World.Survivors.Health(id), max);
        }

        [Test]
        public void AFallenSurvivorIsNotHealedOrRevivedByALevelUp()
        {
            MatchSession s = Start();
            ulong id = First(s);
            s.World.Survivors.Kill(id);

            LevelUp(s, id);

            Assert.IsFalse(s.World.Survivors.IsAlive(id));
            Assert.AreEqual(0, s.World.Survivors.Health(id));
        }

        [Test]
        public void TwoLevelsHealTwice()
        {
            MatchSession one = Start();
            ulong a = First(one);
            one.World.Survivors.ApplyDamage(a, 90);
            LevelUp(one, a, 1);
            int gainedOne = one.World.Survivors.Health(a) - 10;

            MatchSession two = Start();
            ulong b = First(two);
            two.World.Survivors.ApplyDamage(b, 90);
            // Two levels from one share of experience.
            two.World.Leveling.AddXp(b, two.World.Leveling.XpNeeded(b) + GrowthConfig.XpNeeded(2));
            two.Advance();
            int gainedTwo = two.World.Survivors.Health(b) - 10;

            Assert.AreEqual(2, two.World.Leveling.Level(b) - 1);
            Assert.Greater(gainedTwo, gainedOne);
        }

        [Test]
        public void TheOverrideTurnsTheHealOffOrChangesTheFractionAndRefusesBadValues()
        {
            Assert.IsTrue(HarnessOverrides.IsKnown("levelUpHeal"));
            Assert.IsNull(Settings(("levelUpHeal", 0f)).Validate());
            Assert.IsNull(Settings(("levelUpHeal", 1f)).Validate());
            StringAssert.Contains("levelUpHeal", Settings(("levelUpHeal", -0.1f)).Validate());
            StringAssert.Contains("levelUpHeal", Settings(("levelUpHeal", 1.5f)).Validate());

            MatchSession off = Start(("levelUpHeal", 0f));
            ulong id = First(off);
            off.World.Survivors.ApplyDamage(id, 70);
            int before = off.World.Survivors.Health(id);
            LevelUp(off, id);
            Assert.AreEqual(before, off.World.Survivors.Health(id), "no heal");

            MatchSession quarter = Start(("levelUpHeal", 0.25f));
            ulong q = First(quarter);
            quarter.World.Survivors.ApplyDamage(q, 70);
            int qBefore = quarter.World.Survivors.Health(q);
            LevelUp(quarter, q);
            Assert.AreEqual(qBefore + Mathf.RoundToInt(quarter.World.Survivors.MaxHealthOf(q) * 0.25f), quarter.World.Survivors.Health(q));
        }
    }
}
