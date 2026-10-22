using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class MatchHarnessCoreTests
    {
        const float Dt = 0.02f;

        static HarnessSettings Short(float seconds = 20f, params (string name, float value)[] more)
        {
            var s = new HarnessSettings();
            s.overrides.Add(new NamedValue { name = "matchSeconds", value = seconds });
            foreach ((string name, float value) o in more) s.overrides.Add(new NamedValue { name = o.name, value = o.value });
            return s;
        }

        /// <summary>A controller that makes things happen: every survivor shoots toward the boss and walks a little, the boss chases and summons.</summary>
        sealed class BusyController : IMatchController
        {
            readonly System.Random rng;
            public BusyController(int seed) { rng = new System.Random(seed); }

            public void Decide(MatchSession s, float dt)
            {
                Vector2 boss = s.PositionOf(MatchSession.BossId);
                foreach (ulong id in s.SurvivorIds)
                {
                    Vector2 toBoss = boss - s.PositionOf(id);
                    s.Aim(id, AimMath.FromDirection(toBoss), true);
                    s.Move(id, new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f));
                }
                s.Aim(MatchSession.BossId, AimMath.FromDirection(Vector2.down), true);
                s.Summon(MobKinds.Basic);
            }
        }

        static Func<MatchSession, IMatchController> Busy => s => new BusyController(s.Seed);

        // ---- 2.1 one match from a seed ----

        [Test]
        public void TheSameSeedPlaysTheSameMatchTwice()
        {
            HarnessSettings settings = Short(30f);

            MatchRow a = MatchRunner.RunOne(settings, 5, Busy);
            MatchRow b = MatchRunner.RunOne(settings, 5, Busy);

            Assert.AreEqual(JsonUtility.ToJson(a), JsonUtility.ToJson(b));
        }

        [Test]
        public void ADifferentSeedPlaysADifferentMatch()
        {
            HarnessSettings settings = Short(30f);

            MatchRow a = MatchRunner.RunOne(settings, 5, Busy);
            MatchRow b = MatchRunner.RunOne(settings, 6, Busy);

            Assert.AreNotEqual(a.stateHash, b.stateHash);
        }

        [Test]
        public void AMatchEndsWithAResultAtTheClockWhenNobodyWinsSooner()
        {
            MatchRow row = MatchRunner.RunOne(Short(10f, ("mobAutoTarget", 0)), 1, s => new IdleController());

            Assert.AreEqual("boss", row.winner);
            Assert.AreEqual("TimeUp", row.reason);
            Assert.AreEqual(10f, row.lengthSeconds, 0.1f);
        }

        [Test]
        public void TheBossKindIsRestoredAfterAMatch()
        {
            byte before = BossCatalog.CurrentKind;
            BossCatalog.SetCurrentKind(BossCatalog.Sniper);

            var settings = Short(5f);
            settings.bossKind = BossCatalog.Giant;
            MatchRunner.RunOne(settings, 1, s => new IdleController());

            Assert.AreEqual(BossCatalog.Sniper, BossCatalog.CurrentKind);
            BossCatalog.SetCurrentKind(before);
        }

        [Test]
        public void TheKindIsRestoredEvenWhenTheMatchFails()
        {
            byte before = BossCatalog.CurrentKind;
            BossCatalog.SetCurrentKind(BossCatalog.Giant);

            var settings = Short(5f);
            settings.characters = new[] { 1, 1 };
            Assert.Throws<ArgumentException>(() => MatchRunner.RunOne(settings, 1));

            Assert.AreEqual(BossCatalog.Giant, BossCatalog.CurrentKind);
            BossCatalog.SetCurrentKind(before);
        }

        [Test]
        public void AnOverrideChangesTheBossOfThatMatchOnlyAndNeverTheCatalog()
        {
            int catalogHealth = BossCatalog.Get(BossCatalog.Standard).MaxHealth;
            int catalogShot = BossCatalog.Get(BossCatalog.Standard).Attack.BaseDamage;
            var settings = Short(5f, ("bossHealth", 12345f), ("bossShotDamage", 99f));

            MatchSession session = MatchSession.Start(settings, 1);
            BossCatalog.SetCurrentKind(BossCatalog.Standard);

            Assert.AreEqual(12345, session.World.Boss.MaxHealth);
            Assert.AreEqual(99, session.World.BossShotDamage);
            Assert.AreEqual(catalogHealth, BossCatalog.Get(BossCatalog.Standard).MaxHealth);
            Assert.AreEqual(catalogShot, BossCatalog.Get(BossCatalog.Standard).Attack.BaseDamage);

            MatchSession plain = MatchSession.Start(Short(5f), 1);
            Assert.AreEqual(catalogHealth, plain.World.Boss.MaxHealth);
        }

        [Test]
        public void WorldOverridesReachTheSimulations()
        {
            var settings = Short(5f, ("mobHealth", 77f), ("summonRegen", 9f), ("deviceMax", 5f), ("survivorHealthBonus", 200f), ("mobAutoTarget", 3f));

            MatchSession session = MatchSession.Start(settings, 1);

            Assert.AreEqual(77, session.World.Mobs.MaxHealth);
            Assert.AreEqual(9f, session.World.Summon.Config.RegenPerSecond);
            Assert.AreEqual(5, session.World.Mobs.DeviceSettings.MaxDevices);
            Assert.AreEqual(3, session.World.Mobs.TargetCount);
            Assert.AreEqual(CharacterCatalog.Get(CharacterIds.Reimu).MaxHealth + 200, session.World.Survivors.MaxHealthOf(1));
        }

        [Test]
        public void ASessionIsSetUpLikeTheServerSetsUpAMatch()
        {
            MatchSession session = MatchSession.Start(Short(5f), 1);
            MatchWorld w = session.World;

            Assert.IsTrue(w.MatchRunning);
            Assert.AreEqual(4, session.SurvivorIds.Count);
            Assert.AreEqual(BossCatalog.Get(BossCatalog.Standard).MaxHealth, w.Boss.MaxHealth);
            Assert.AreEqual(new Vector2(0f, 6f), session.PositionOf(MatchSession.BossId));
            Assert.AreEqual(ArenaConfig.SpawnPoint(2), session.PositionOf(2));
            Assert.IsTrue(w.Clock.Enabled);
            Assert.AreEqual(5f, w.Clock.TimeLimit);
            Assert.AreEqual(MobConfig.BossMatchAutoTarget, w.Mobs.TargetCount);
            Assert.AreEqual(MobConfig.BossMatchSpawnPerSecond, w.Mobs.SpawnPerSecond);
            Assert.AreEqual(w.Summon.Config.StartEnergy, w.Summon.Energy);
            for (int i = 0; i < 4; i++) Assert.IsTrue(w.HasGrowth(session.SurvivorIds[i]));
            Assert.AreEqual(RoleTable.BossRole, w.SlotOf(MatchSession.BossId));
            Assert.AreEqual(3, w.SlotOf(3));
        }

        [Test]
        public void FewerSurvivorsAreSetUpFromTheCharacterList()
        {
            var settings = Short(5f);
            settings.characters = new[] { 4, 2 };

            MatchSession session = MatchSession.Start(settings, 1);

            Assert.AreEqual(2, session.SurvivorIds.Count);
            Assert.AreEqual(CharacterIds.Sanae, session.CharacterOf(1));
            Assert.AreEqual(CharacterIds.Marisa, session.CharacterOf(2));
        }

        [Test]
        public void Movement_UsesTheCharacterSpeedAndStaysInTheArena()
        {
            MatchSession session = MatchSession.Start(Short(30f, ("mobAutoTarget", 0)), 1);
            Vector2 start = session.PositionOf(1);
            float speed = CharacterCatalog.Get(CharacterIds.Reimu).MoveSpeed;

            session.Move(1, Vector2.right);
            for (int i = 0; i < 50; i++) { session.Move(1, Vector2.right); session.Advance(); }

            Assert.AreEqual(start.x + speed * 1f, session.PositionOf(1).x, 0.05f);

            for (int i = 0; i < 1500; i++) { session.Move(1, new Vector2(10f, 10f)); if (!session.Advance()) break; }
            Vector2 p = session.PositionOf(1);
            Assert.LessOrEqual(p.x, ArenaConfig.Width * 0.5f - ArenaConfig.CharacterMargin + 1e-3f);
            Assert.LessOrEqual(p.y, ArenaConfig.Height * 0.5f - ArenaConfig.CharacterMargin + 1e-3f);
        }

        [Test]
        public void ASettingsFileThatCannotBePlayedIsRefusedWithAReason()
        {
            Assert.IsNull(new HarnessSettings().Validate());

            Assert.That(new HarnessSettings { matches = 0 }.Validate(), Does.Contain("matches"));
            Assert.That(new HarnessSettings { bossKind = 9 }.Validate(), Does.Contain("bossKind"));
            Assert.That(new HarnessSettings { characters = new[] { 1, 1 } }.Validate(), Does.Contain("twice"));
            Assert.That(new HarnessSettings { characters = new[] { 7 } }.Validate(), Does.Contain("does not exist"));
            Assert.That(new HarnessSettings { characters = new int[0] }.Validate(), Does.Contain("one to four"));
            Assert.That(new HarnessSettings { step = 1f }.Validate(), Does.Contain("step"));

            var unknown = new HarnessSettings();
            unknown.overrides.Add(new NamedValue { name = "bossHelth", value = 1f });
            Assert.That(unknown.Validate(), Does.Contain("unknown override"));
        }

        [Test]
        public void AFullMatchOfTheServerLengthIsPlayedInReasonableTime()
        {
            // A boss that cannot be beaten and survivors that cannot fall, so the match runs the whole clock.
            var settings = new HarnessSettings();
            settings.overrides.Add(new NamedValue { name = "matchSeconds", value = 900f });
            settings.overrides.Add(new NamedValue { name = "bossHealth", value = 50000000f });
            settings.overrides.Add(new NamedValue { name = "survivorHealthBonus", value = 1000000f });
            var watch = Stopwatch.StartNew();

            MatchRow row = MatchRunner.RunOne(settings, 3, Busy);

            watch.Stop();
            UnityEngine.Debug.Log($"[harness] a {row.lengthSeconds:F0} s match ({row.winner}/{row.reason}) took {watch.Elapsed.TotalSeconds:F2} s of real time");
            Assert.AreEqual("TimeUp", row.reason);
            Assert.AreEqual(900f, row.lengthSeconds, 0.1f);
            Assert.Less(watch.Elapsed.TotalSeconds, 60.0);
        }

        // ---- 2.2 what the recorder notes ----

        static MatchSession Quiet(params (string name, float value)[] more)
        {
            var list = new List<(string, float)> { ("mobAutoTarget", 0f), ("matchSeconds", 60f) };
            list.AddRange(more);
            var s = new HarnessSettings();
            foreach ((string name, float value) o in list) s.overrides.Add(new NamedValue { name = o.name, value = o.value });
            BossCatalog.SetCurrentKind(BossCatalog.Standard);
            return MatchSession.Start(s, 1);
        }

        static void Advance(MatchSession session, MatchRecorder recorder, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / session.Step);
            for (int i = 0; i < steps; i++)
            {
                session.Advance();
                recorder.Observe(session);
            }
        }

        [Test]
        public void ABossThatNothingHappensToLeavesTheNotedValuesEmpty()
        {
            MatchSession session = Quiet();
            var recorder = new MatchRecorder(1);

            Advance(session, recorder, 2f);
            MatchRow row = recorder.Finish(session);

            Assert.AreEqual(-1f, row.bossT50);
            Assert.AreEqual(-1f, row.bossT25);
            Assert.AreEqual(-1f, row.bossT0);
            Assert.AreEqual(-1f, row.firstFall);
            Assert.AreEqual(0, row.falls);
            Assert.AreEqual(0, row.revives);
            Assert.AreEqual(1f, row.bossHealthEnd, 1e-4f);
            Assert.AreEqual(4, row.survivorsAliveEnd);
            Assert.AreEqual(0, row.devicesPlaced);
            Assert.AreEqual(1, row.maxLevel);
        }

        [Test]
        public void TheBossHealthThresholdsAreNotedAtTheStepTheyAreCrossed()
        {
            MatchSession session = Quiet();
            var recorder = new MatchRecorder(1);
            int max = session.World.Boss.MaxHealth;

            Advance(session, recorder, 1f);
            session.World.Boss.TakeDamage(max * 4 / 10, out _, out _); // 60% left
            Advance(session, recorder, 1f);
            session.World.Boss.TakeDamage(max / 10, out _, out _);     // 50% left
            Advance(session, recorder, 1f);
            session.World.Boss.TakeDamage(max / 4, out _, out _);      // 25% left
            Advance(session, recorder, 1f);
            session.World.Boss.TakeDamage(max, out _, out _);          // gone
            Advance(session, recorder, 0.1f);
            MatchRow row = recorder.Finish(session);

            Assert.AreEqual(2f, row.bossT50, 0.05f);
            Assert.AreEqual(3f, row.bossT25, 0.05f);
            Assert.AreEqual(4f, row.bossT0, 0.05f);
            Assert.AreEqual(0f, row.bossHealthEnd, 1e-4f);
            Assert.AreEqual("survivors", row.winner);
            Assert.AreEqual("BossDefeated", row.reason);
        }

        [Test]
        public void AFallAndARevivalAreCounted()
        {
            MatchSession session = Quiet();
            var recorder = new MatchRecorder(1);
            Advance(session, recorder, 1f);

            session.World.Survivors.Kill(1);
            Advance(session, recorder, 0.1f);
            Assert.AreEqual(1.02f, recorder.Finish(session).firstFall, 0.03f);

            // Survivor 2 stands next to the fallen one for the revival time.
            for (int i = 0; i < session.World.Bodies.Count; i++)
            {
                MatchBody b = session.World.Bodies[i];
                if (b.ClientId == 2) { b.Position = session.PositionOf(1) + new Vector2(0.5f, 0f); session.World.Bodies[i] = b; }
            }
            Advance(session, recorder, CombatConfig.ReviveSeconds + 1f);
            MatchRow row = recorder.Finish(session);

            Assert.AreEqual(1, row.falls);
            Assert.AreEqual(1, row.revives);
        }

        [Test]
        public void DevicesPlacedDestroyedAndExpiredAreCounted()
        {
            MatchSession session = Quiet(("deviceLifetime", 2f), ("deviceEnergy", 10f), ("summonMaxEnergy", 100f));
            var recorder = new MatchRecorder(1);

            Assert.AreEqual(SummonResult.Created, session.PlaceDevice());
            Advance(session, recorder, 2.2f); // it expires
            Assert.AreEqual(0, session.World.Mobs.DeviceCount);

            Advance(session, recorder, 3.1f); // the placing cooldown has passed
            Assert.AreEqual(SummonResult.Created, session.PlaceDevice());
            ushort id = 0;
            for (int i = 0; i < session.World.Mobs.Count; i++) if (session.World.Mobs.IsDevice(i)) id = session.World.Mobs.IdAt(i);
            session.World.Mobs.ApplyDamage(id, 1000000, out _, out _);
            Advance(session, recorder, 0.1f);
            MatchRow row = recorder.Finish(session);

            Assert.AreEqual(2, row.devicesPlaced);
            Assert.AreEqual(1, row.devicesExpired);
            Assert.AreEqual(1, row.devicesDestroyed);
        }

        [Test]
        public void TheFinalLevelsAreNoted()
        {
            MatchSession session = Quiet();
            var recorder = new MatchRecorder(1);
            session.World.Leveling.AddXp(1, GrowthConfig.XpNeeded(1) + GrowthConfig.XpNeeded(2));
            Advance(session, recorder, 0.5f);

            MatchRow row = recorder.Finish(session);

            Assert.AreEqual(3, row.maxLevel);
            Assert.AreEqual((3 + 1 + 1 + 1) / 4f, row.meanLevel, 1e-4f);
        }
    }
}
