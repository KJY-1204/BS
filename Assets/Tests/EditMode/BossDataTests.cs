using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class BossDataTests
    {
        // ---- 1.1 the definition ----

        [Test]
        public void EveryBossValueIsPositive()
        {
            BossDef boss = BossCatalog.Default;
            Assert.IsFalse(string.IsNullOrEmpty(boss.Name));
            Assert.Greater(boss.MaxHealth, 0);
            Assert.Greater(boss.MoveSpeed, 0f);
            Assert.Greater(boss.Radius, 0f);
            Assert.Greater(boss.ContactDamage, 0);
            Assert.Greater(boss.MatchSeconds, 0f);
        }

        [Test]
        public void TheBossIsBiggerAndHitsHarderThanAMob()
        {
            BossDef boss = BossCatalog.Default;
            Assert.Greater(boss.ContactDamage, CombatConfig.ContactDamage, "a different, larger contact damage than a mob's");
            Assert.Greater(boss.Radius, MobConfig.Radius);
        }

        [Test]
        public void TheBossSpeedIsWhatItWasBeforeTheBossHadData()
        {
            Assert.AreEqual(6f, BossCatalog.Default.MoveSpeed);
            Assert.AreEqual(BossCatalog.Default.MoveSpeed, CharacterCatalog.BossMoveSpeed);
        }

        [Test]
        public void TheBossColorStaysDistinctFromEveryCharacter()
        {
            foreach (CharacterDef c in CharacterCatalog.All)
                Assert.Greater(Mathf.Abs(c.Color.g - BossCatalog.Default.Color.g) + Mathf.Abs(c.Color.b - BossCatalog.Default.Color.b), 0.3f, c.Name);
        }

        [Test]
        public void ForMatchGivesACopy_SoChangingItNeverChangesTheSharedData()
        {
            BossDef a = BossCatalog.ForMatch();
            a.MaxHealth = 1;
            a.MatchSeconds = 1f;

            Assert.AreNotEqual(1, BossCatalog.Default.MaxHealth);
            Assert.AreNotEqual(1f, BossCatalog.Default.MatchSeconds);
            Assert.AreEqual(BossCatalog.Default.MaxHealth, BossCatalog.ForMatch().MaxHealth, "without development overrides it is the shared data");
        }

        // ---- 1.2 the boss simulation ----

        static BossDef Def(int health = 100, float radius = 0.9f, int contact = 25) =>
            new BossDef { Name = "t", MaxHealth = health, MoveSpeed = 6f, Radius = radius, ContactDamage = contact, MatchSeconds = 60f };

        [Test]
        public void AnewBossStartsAtFullHealthAtItsPosition()
        {
            var boss = new BossSim(Def(500), new Vector2(1, 2));

            Assert.AreEqual(500, boss.Health);
            Assert.AreEqual(500, boss.MaxHealth);
            Assert.AreEqual(new Vector2(1, 2), boss.Position);
            Assert.IsTrue(boss.CanBeHit);
            Assert.IsFalse(boss.HasFallen);
            Assert.AreEqual(100, boss.HealthPercent);
        }

        [Test]
        public void DamageLowersHealthAndReportsThePercentLeft()
        {
            var boss = new BossSim(Def(200), Vector2.zero);

            Assert.IsTrue(boss.TakeDamage(50, out int percent, out bool fell));

            Assert.AreEqual(150, boss.Health);
            Assert.AreEqual(75, percent);
            Assert.IsFalse(fell);
        }

        [Test]
        public void HealthNeverGoesBelowZero_AndTheHitThatKillsSaysSo()
        {
            var boss = new BossSim(Def(30), Vector2.zero);

            Assert.IsTrue(boss.TakeDamage(1000, out int percent, out bool fell));

            Assert.AreEqual(0, boss.Health);
            Assert.AreEqual(0, percent);
            Assert.IsTrue(fell);
            Assert.IsTrue(boss.HasFallen);
            Assert.IsFalse(boss.CanBeHit);
        }

        [Test]
        public void AFallenBossTakesNoMoreDamage_AndFallsOnlyOnce()
        {
            var boss = new BossSim(Def(30), Vector2.zero);
            boss.TakeDamage(30, out _, out _);

            Assert.IsFalse(boss.TakeDamage(10, out _, out bool fellAgain));

            Assert.IsFalse(fellAgain);
            Assert.AreEqual(0, boss.Health);
        }

        [Test]
        public void ZeroAndNegativeDamageDoNothing()
        {
            var boss = new BossSim(Def(30), Vector2.zero);
            Assert.IsFalse(boss.TakeDamage(0, out _, out _));
            Assert.IsFalse(boss.TakeDamage(-5, out _, out _));
            Assert.AreEqual(30, boss.Health);
        }

        [Test]
        public void JustFellIsRaisedForOneStepOnly()
        {
            var boss = new BossSim(Def(30), Vector2.zero);
            Assert.IsFalse(boss.JustFell);

            boss.TakeDamage(30, out _, out _);
            Assert.IsTrue(boss.JustFell);

            boss.EndStep();
            Assert.IsFalse(boss.JustFell);
            Assert.IsTrue(boss.HasFallen, "still fallen after the step");
        }

        [Test]
        public void ThePositionFollowsThePlayer()
        {
            var boss = new BossSim(Def(), Vector2.zero);
            boss.SetPosition(new Vector2(4, -3));
            Assert.AreEqual(new Vector2(4, -3), boss.Position);
        }

        [Test]
        public void ClearRemovesTheBoss_AndResetBringsABossBack()
        {
            var boss = new BossSim(Def(), Vector2.zero);

            boss.Clear();
            Assert.IsFalse(boss.Exists);
            Assert.IsFalse(boss.CanBeHit);
            Assert.IsFalse(boss.TakeDamage(10, out _, out _));

            boss.Reset(Def(77), Vector2.one);
            Assert.IsTrue(boss.Exists);
            Assert.AreEqual(77, boss.Health);
            Assert.IsFalse(boss.JustFell);
        }

        [Test]
        public void AResetAfterAFallBringsTheBossBackAtFullHealth()
        {
            var boss = new BossSim(Def(50), Vector2.zero);
            boss.TakeDamage(50, out _, out _);

            boss.Reset(Def(50), Vector2.zero);

            Assert.IsTrue(boss.CanBeHit);
            Assert.AreEqual(50, boss.Health);
            Assert.IsFalse(boss.JustFell);
        }

        [Test]
        public void TheBossIdIsTheReservedValue()
        {
            Assert.AreEqual(ushort.MaxValue, BossSim.MobId);
        }

        // ---- 1.3 mob identifiers never use the reserved value ----

        [Test]
        public void MobIdentifiersNeverTakeTheReservedBossValue_EvenWhenTheCounterWrapsAround()
        {
            var sim = new MobSim(hardCap: 100, targetCount: 0);
            var seen = new HashSet<ushort>();
            int spawned = 0;

            // Enough spawns and removals for the 16-bit counter to go round more than once.
            for (int round = 0; round < 70000; round++)
            {
                Assert.IsTrue(sim.TrySpawn(new Vector2(0, 0), 0, out ushort id));
                spawned++;
                Assert.AreNotEqual(BossSim.MobId, id, $"spawn {spawned}");
                Assert.AreNotEqual((ushort)0, id);
                seen.Add(id);
                sim.Remove(id);
            }

            Assert.IsFalse(seen.Contains(BossSim.MobId));
            Assert.Greater(seen.Count, 60000, "the counter really did go most of the way round");
        }

        [Test]
        public void TheReservedValueIsSkippedAndTheNextFreeOneIsUsed()
        {
            var sim = new MobSim(hardCap: 100, targetCount: 0);
            ushort before = 0, after = 0;
            bool crossed = false;

            for (int i = 0; i < 70000 && !crossed; i++)
            {
                sim.TrySpawn(Vector2.zero, 0, out ushort id);
                if (id == ushort.MaxValue - 1) before = id;
                if (before != 0 && id != before) { after = id; crossed = true; }
                sim.Remove(id);
            }

            Assert.IsTrue(crossed);
            Assert.AreEqual(1, after, "after 65534 the counter skips 65535 and wraps to 1");
        }

        [Test]
        public void StillManyMobsGetDifferentIdentifiers()
        {
            var sim = new MobSim(hardCap: 100, targetCount: 0);
            var ids = new HashSet<ushort>();
            for (int i = 0; i < 100; i++)
            {
                sim.TrySpawn(new Vector2(i * 0.01f, 0), 0, out ushort id);
                Assert.IsTrue(ids.Add(id));
            }
        }
    }
}
