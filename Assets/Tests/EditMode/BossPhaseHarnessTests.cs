using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class BossPhaseHarnessTests
    {
        static readonly SkillProfile Exact = new SkillProfile { Name = "exact", AimErrorDegrees = 0f, ReactionSeconds = 0.3f, BossPreference = 1f };

        static MatchSession Start(params (string name, float value)[] overrides)
        {
            var settings = new HarnessSettings();
            settings.overrides.Add(new NamedValue { name = "mobAutoTarget", value = 0f });
            settings.overrides.Add(new NamedValue { name = "matchSeconds", value = 120f });
            foreach ((string name, float value) o in overrides) settings.overrides.Add(new NamedValue { name = o.name, value = o.value });
            BossCatalog.SetCurrentKind(BossCatalog.Standard);
            return MatchSession.Start(settings, 1);
        }

        static void HurtTo(MatchSession s, float fraction)
        {
            BossSim boss = s.World.Boss;
            boss.TakeDamage(boss.Health - Mathf.RoundToInt(boss.MaxHealth * fraction), out _, out _);
        }

        // ---- the recorder ----

        [Test]
        public void TheRecorderNotesWhenEachPhaseStarted()
        {
            MatchSession s = Start();
            var recorder = new MatchRecorder(1);
            float second = -1f, third = -1f;

            for (int i = 0; i < 240; i++)
            {
                if (i == 60) HurtTo(s, 0.6f);
                if (i == 140) HurtTo(s, 0.3f);
                s.Advance();
                recorder.Observe(s);
                if (i == 60) second = s.Time;
                if (i == 140) third = s.Time;
            }
            MatchRow row = recorder.Finish(s);

            Assert.AreEqual(second, row.phaseT2, 1e-4f);
            Assert.AreEqual(third, row.phaseT3, 1e-4f);
            Assert.Less(row.phaseT2, row.phaseT3);
        }

        [Test]
        public void AMatchThatNeverLeavesPhaseOneRecordsMinusOne()
        {
            MatchSession s = Start();
            var recorder = new MatchRecorder(1);
            for (int i = 0; i < 50; i++) { s.Advance(); recorder.Observe(s); }

            MatchRow row = recorder.Finish(s);

            Assert.AreEqual(-1f, row.phaseT2);
            Assert.AreEqual(-1f, row.phaseT3);
        }

        [Test]
        public void AMatchWithThePhasesOffRecordsNoPhaseEvenWhenTheBossIsBeaten()
        {
            MatchSession s = Start(("bossPhases", 0f));
            var recorder = new MatchRecorder(1);
            HurtTo(s, 0.1f);
            for (int i = 0; i < 50; i++) { s.Advance(); recorder.Observe(s); }

            MatchRow row = recorder.Finish(s);

            Assert.AreEqual(-1f, row.phaseT2);
            Assert.AreEqual(-1f, row.phaseT3);
        }

        // ---- the report ----

        static MatchRow Row(int seed, float p2, float p3) => new MatchRow
        {
            seed = seed, winner = "survivors", reason = "BossDefeated", lengthSeconds = 800f, phaseT2 = p2, phaseT3 = p3, bossT50 = -1f, bossT25 = -1f, bossT0 = -1f,
            firstFall = -1f, survivorsAliveEnd = 4, maxLevel = 20, meanLevel = 20f,
        };

        [Test]
        public void TheSummaryHasTheStatisticsOfThePhaseStarts()
        {
            var rows = new List<MatchRow> { Row(1, 300f, 600f), Row(2, 340f, 700f), Row(3, 320f, -1f), Row(4, -1f, -1f) };

            HarnessSummary s = HarnessSummary.From(rows, new HarnessSettings());

            Assert.AreEqual(320f, s.phaseT2.mean, 1e-3f);
            Assert.AreEqual(3, s.phaseT2.count);
            Assert.AreEqual(0.75f, s.phaseT2Share, 1e-4f);
            Assert.AreEqual(650f, s.phaseT3.mean, 1e-3f);
            Assert.AreEqual(0.5f, s.phaseT3Share, 1e-4f);
        }

        [Test]
        public void TheCsvAndTheMarkdownCarryThePhaseStarts()
        {
            var rows = new List<MatchRow> { Row(1, 300.5f, 600f), Row(2, -1f, -1f) };
            string csv = HarnessReport.ToCsv(rows);
            string[] lines = csv.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();

            StringAssert.EndsWith("phaseT2,phaseT3", lines[0]);
            Assert.AreEqual(lines[0].Split(',').Length, lines[1].Split(',').Length);
            StringAssert.EndsWith("300.5,600", lines[1]);

            string md = HarnessReport.ToMarkdown(HarnessSummary.From(rows, new HarnessSettings()));
            StringAssert.Contains("| phase 2 starts |", md);
            StringAssert.Contains("| phase 3 starts |", md);
        }

        [Test]
        public void TheComparisonWithAnEarlierRunListsThePhases()
        {
            HarnessSummary now = HarnessSummary.From(new List<MatchRow> { Row(1, 300f, 600f) }, new HarnessSettings());
            HarnessSummary before = HarnessSummary.From(new List<MatchRow> { Row(1, 320f, 640f) }, new HarnessSettings());

            List<Change> changes = HarnessReport.Compare(now, before);

            Assert.IsTrue(changes.Any(c => c.Name.StartsWith("phase 2 at")));
            Assert.IsTrue(changes.Any(c => c.Name.StartsWith("phase 3 at")));
        }

        [Test]
        public void AnEarlierSummaryWithoutPhaseFieldsStillReads()
        {
            HarnessSummary old = HarnessReport.FromJson("{\"matches\":3,\"survivorWins\":2}");

            Assert.IsNotNull(old.phaseT2);
            Assert.AreEqual(0, old.phaseT2.count);
            Assert.AreEqual(0f, old.phaseT3Share);
            Assert.AreEqual(3, old.matches);
        }

        // ---- the bot ----

        [Test]
        public void TheBossBotUsesTheThirdSkillOnlyAfterPhaseTwo()
        {
            MatchSession s = Start();
            var bot = new BossBot(s.BossData, Exact, 1);

            bot.Decide(s, s.Step);
            Assert.AreEqual(0f, s.World.BossSkills.CooldownLeft(BossSkills.PhaseSlot), "locked in phase 1: nothing started");

            HurtTo(s, 0.6f);
            s.Advance();
            for (int i = 0; i < 40; i++) { bot.Decide(s, s.Step); s.Advance(); }

            Assert.Greater(s.World.BossSkills.CooldownLeft(BossSkills.PhaseSlot), 0f, "used once it opened");
        }
    }
}
