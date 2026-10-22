using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace BS.Tests
{
    public class HarnessReportTests
    {
        static MatchRow Row(int seed, string winner, string reason, float length, float t50 = -1f, float t25 = -1f, float t0 = -1f,
                            float bossEnd = 1f, float firstFall = -1f, int falls = 0, int revives = 0, int alive = 4, int maxLevel = 1,
                            float meanLevel = 1f, int placed = 0, int destroyed = 0, int expired = 0) =>
            new MatchRow
            {
                seed = seed, winner = winner, reason = reason, lengthSeconds = length, bossT50 = t50, bossT25 = t25, bossT0 = t0,
                bossHealthEnd = bossEnd, firstFall = firstFall, falls = falls, revives = revives, survivorsAliveEnd = alive,
                maxLevel = maxLevel, meanLevel = meanLevel, devicesPlaced = placed, devicesDestroyed = destroyed, devicesExpired = expired,
            };

        static List<MatchRow> Known() => new List<MatchRow>
        {
            Row(1, "survivors", "BossDefeated", 100f, t50: 60f, t25: 80f, t0: 100f, bossEnd: 0f, firstFall: 30f, falls: 1, revives: 1, alive: 4, maxLevel: 20, meanLevel: 18f, placed: 4, destroyed: 3, expired: 1),
            Row(2, "survivors", "BossDefeated", 200f, t50: 120f, t25: 160f, t0: 200f, bossEnd: 0f, falls: 0, alive: 4, maxLevel: 12, meanLevel: 10f, placed: 2, destroyed: 2),
            Row(3, "boss", "AllSurvivorsFallen", 300f, t50: 250f, bossEnd: 0.4f, firstFall: 150f, falls: 4, revives: 0, alive: 0, maxLevel: 10, meanLevel: 8f, placed: 6, expired: 6),
            Row(4, "boss", "TimeUp", 900f, bossEnd: 0.9f, firstFall: 600f, falls: 3, revives: 2, alive: 1, maxLevel: 16, meanLevel: 14f, placed: 0),
        };

        // ---- statistics ----

        [Test]
        public void TheStatsOfAKnownListAreRight()
        {
            var values = new List<float>();
            for (int i = 1; i <= 10; i++) values.Add(i);

            Stat s = Stat.Of(values);

            Assert.AreEqual(10, s.count);
            Assert.AreEqual(5.5f, s.mean, 1e-4f);
            Assert.AreEqual(5.5f, s.median, 1e-4f);
            Assert.AreEqual(1.9f, s.p10, 1e-4f);
            Assert.AreEqual(9.1f, s.p90, 1e-4f);
            Assert.AreEqual(1f, s.min);
            Assert.AreEqual(10f, s.max);
        }

        [Test]
        public void TheStatsOfAnUnsortedListAndOfOneOrNoValuesAreRight()
        {
            Stat unsorted = Stat.Of(new List<float> { 9f, 1f, 5f });
            Assert.AreEqual(5f, unsorted.median);
            Assert.AreEqual(1f, unsorted.min);

            Stat one = Stat.Of(new List<float> { 7f });
            Assert.AreEqual(7f, one.median);
            Assert.AreEqual(7f, one.p10);
            Assert.AreEqual(7f, one.p90);

            Stat none = Stat.Of(new List<float>());
            Assert.AreEqual(0, none.count);
            Assert.AreEqual(0f, none.mean);
        }

        [Test]
        public void TheSummaryOfKnownRowsHasTheExpectedValues()
        {
            HarnessSummary s = HarnessSummary.From(Known(), new HarnessSettings());

            Assert.AreEqual(4, s.matches);
            Assert.AreEqual(2, s.survivorWins);
            Assert.AreEqual(2, s.bossWins);
            Assert.AreEqual(0, s.undecided);
            Assert.AreEqual(0.5f, s.survivorShare, 1e-5f);
            Assert.AreEqual(2, s.reasons[0].count); // BossDefeated, listed first by count
            Assert.AreEqual("BossDefeated", s.reasons[0].reason);
            Assert.AreEqual(4, s.length.count);
            Assert.AreEqual(375f, s.length.mean, 1e-3f);
            Assert.AreEqual(250f, s.length.median, 1e-3f);
            // The boss reached 50% in three matches, 25% in two, 0% in two.
            Assert.AreEqual(0.75f, s.bossT50Share, 1e-5f);
            Assert.AreEqual(0.5f, s.bossT25Share, 1e-5f);
            Assert.AreEqual(0.5f, s.bossT0Share, 1e-5f);
            Assert.AreEqual((60f + 120f + 250f) / 3f, s.bossT50.mean, 1e-3f);
            Assert.AreEqual(150f, s.bossT0.mean, 1e-3f);
            Assert.AreEqual((0f + 0f + 0.4f + 0.9f) / 4f, s.bossHealthEndMean, 1e-5f);
            Assert.AreEqual(0.75f, s.firstFallShare, 1e-5f);
            Assert.AreEqual((30f + 150f + 600f) / 3f, s.firstFall.mean, 1e-3f);
            Assert.AreEqual(2f, s.fallsMean, 1e-5f);
            Assert.AreEqual(0.75f, s.revivesMean, 1e-5f);
            Assert.AreEqual(2.25f, s.survivorsAliveEndMean, 1e-5f);
            Assert.AreEqual(14.5f, s.maxLevelMean, 1e-5f);
            Assert.AreEqual(0.25f, s.levelCapShare, 1e-5f);
            Assert.AreEqual(3f, s.devicesPlacedMean, 1e-5f);
            Assert.AreEqual(1.25f, s.devicesDestroyedMean, 1e-5f);
            Assert.AreEqual(1.75f, s.devicesExpiredMean, 1e-5f);
        }

        [Test]
        public void ARunWithNoResultIsCountedAndACoarseStepIsNoted()
        {
            var rows = new List<MatchRow> { Row(1, "none", "none", 910f) };
            var settings = new HarnessSettings { step = 0.04f };

            HarnessSummary s = HarnessSummary.From(rows, settings);

            Assert.AreEqual(1, s.undecided);
            Assert.AreEqual(0f, s.survivorShare);
            Assert.That(s.note, Does.Contain("coarser"));
            Assert.AreEqual("", HarnessSummary.From(Known(), new HarnessSettings()).note);
        }

        // ---- files ----

        [Test]
        public void TheCsvHasAHeaderAndOneLinePerMatchWithTheRightNumberOfFields()
        {
            string csv = HarnessReport.ToCsv(Known());
            string[] lines = csv.TrimEnd().Split('\n');

            Assert.AreEqual(5, lines.Length);
            Assert.AreEqual(HarnessReport.CsvHeader, lines[0].TrimEnd('\r'));
            int fields = HarnessReport.CsvHeader.Split(',').Length;
            for (int i = 1; i < lines.Length; i++) Assert.AreEqual(fields, lines[i].TrimEnd('\r').Split(',').Length, lines[i]);
            StringAssert.StartsWith("1,survivors,BossDefeated,100,60,80,100,0,30,1,1,4,20,18,4,3,1,", lines[1]);
        }

        [Test]
        public void TheMarkdownShowsTheNumbersAndWhatWasPlayed()
        {
            var settings = new HarnessSettings { profile = "strong", bossKind = BossCatalog.Giant };
            settings.overrides.Add(new NamedValue { name = "bossHealth", value = 9000f });
            HarnessSummary s = HarnessSummary.From(Known(), settings);

            string md = HarnessReport.ToMarkdown(s);

            StringAssert.Contains("profile strong", md);
            StringAssert.Contains("거인", md);
            StringAssert.Contains("bossHealth=9000", md);
            StringAssert.Contains("| survivors win | 2 | 50% |", md);
            StringAssert.Contains("| boss wins | 2 | 50% |", md);
            StringAssert.Contains("| BossDefeated | 2 |", md);
            StringAssert.Contains("| match length | 100% | 375 | 250 |", md);
            StringAssert.DoesNotContain("Against the earlier run", md);
        }

        [Test]
        public void TheSummaryGoesThroughJsonAndComesBackTheSame()
        {
            HarnessSummary s = HarnessSummary.From(Known(), new HarnessSettings());

            HarnessSummary back = HarnessReport.FromJson(HarnessReport.ToJson(s));

            Assert.AreEqual(s.matches, back.matches);
            Assert.AreEqual(s.survivorShare, back.survivorShare, 1e-6f);
            Assert.AreEqual(s.length.median, back.length.median, 1e-4f);
            Assert.AreEqual(s.reasons.Count, back.reasons.Count);
            Assert.AreEqual(s.devicesPlacedMean, back.devicesPlacedMean, 1e-6f);
        }

        [Test]
        public void TheComparisonHasTheRightSignAndTheMarkdownShowsIt()
        {
            HarnessSummary before = HarnessSummary.From(Known(), new HarnessSettings());
            var better = Known();
            better[2] = Row(3, "survivors", "BossDefeated", 250f, t50: 100f, t25: 150f, t0: 250f, bossEnd: 0f, falls: 1, alive: 3, maxLevel: 14, meanLevel: 12f, placed: 6, destroyed: 5, expired: 1);
            HarnessSummary now = HarnessSummary.From(better, new HarnessSettings());

            List<Change> changes = HarnessReport.Compare(now, before);

            Change share = changes.Find(c => c.Name == "survivor win share");
            Assert.AreEqual(0.5f, share.Before, 1e-5f);
            Assert.AreEqual(0.75f, share.Now, 1e-5f);
            Assert.AreEqual(0.25f, share.Delta, 1e-5f);
            Assert.Less(changes.Find(c => c.Name == "falls per match").Delta, 0f);

            string md = HarnessReport.ToMarkdown(now, before);
            StringAssert.Contains("Against the earlier run", md);
            StringAssert.Contains("| survivor win share | 0.5 | 0.75 | +0.25 |", md);
        }

        // ---- failures ----

        [Test]
        public void ARunThatCannotBePlayedFailsWithAClearMessage()
        {
            Assert.That(() => HarnessRunner.Run(new HarnessSettings { matches = 0 }), Throws.ArgumentException.With.Message.Contains("matches"));

            var unknown = new HarnessSettings();
            unknown.overrides.Add(new NamedValue { name = "nope", value = 1f });
            Assert.That(() => HarnessRunner.Run(unknown), Throws.ArgumentException.With.Message.Contains("unknown override"));

            Assert.That(() => HarnessSummary.From(new List<MatchRow>(), new HarnessSettings()), Throws.ArgumentException);
            Assert.That(() => HarnessReport.FromJson("{}"), Throws.ArgumentException);
        }

        [Test]
        public void AMissingBaselineIsReportedBeforeAnyMatchIsPlayed()
        {
            var settings = new HarnessSettings { matches = 3, baselinePath = Path.Combine(Path.GetTempPath(), "bs-no-such-baseline.json") };

            Assert.That(() => HarnessRunner.Run(settings), Throws.TypeOf<FileNotFoundException>().With.Message.Contains("baseline"));
        }

        // ---- settings files and a small run ----

        static string TempFolder() => Path.Combine(Path.GetTempPath(), "bs-harness-test-" + Guid.NewGuid().ToString("N"));

        static HarnessSettings Small(int matches)
        {
            var settings = new HarnessSettings { matches = matches, firstSeed = 5 };
            settings.overrides.Add(new NamedValue { name = "matchSeconds", value = 15f });
            return settings;
        }

        [Test]
        public void ASettingsFileIsReadAndItsMistakesAreNamed()
        {
            string folder = TempFolder();
            Directory.CreateDirectory(folder);
            try
            {
                string good = Path.Combine(folder, "good.json");
                File.WriteAllText(good, JsonUtility.ToJson(Small(2)));
                HarnessSettings loaded = HarnessRunner.LoadSettings(good);
                Assert.AreEqual(2, loaded.matches);
                Assert.AreEqual(5, loaded.firstSeed);
                Assert.AreEqual("matchSeconds", loaded.overrides[0].name);

                string bad = Path.Combine(folder, "bad.json");
                File.WriteAllText(bad, "{\"matches\": 0}");
                Assert.That(() => HarnessRunner.LoadSettings(bad), Throws.ArgumentException.With.Message.Contains("matches"));

                string notJson = Path.Combine(folder, "notjson.json");
                File.WriteAllText(notJson, "this is not json");
                Assert.That(() => HarnessRunner.LoadSettings(notJson), Throws.ArgumentException.With.Message.Contains("not valid JSON"));

                Assert.That(() => HarnessRunner.LoadSettings(Path.Combine(folder, "missing.json")), Throws.TypeOf<FileNotFoundException>());
                Assert.That(() => HarnessRunner.LoadSettings(""), Throws.ArgumentException);
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void ARunPlaysTheSeedsInOrderAndWritesThreeFilesWhoseSummaryCanBeTheNextBaseline()
        {
            string folder = TempFolder();
            try
            {
                HarnessResult first = HarnessRunner.Run(Small(3));
                Assert.AreEqual(3, first.Rows.Count);
                Assert.AreEqual(5, first.Rows[0].seed);
                Assert.AreEqual(6, first.Rows[1].seed);
                Assert.AreEqual(7, first.Rows[2].seed);
                Assert.AreEqual(3, first.Summary.matches);

                HarnessRunner.Write(first, folder);
                Assert.IsTrue(File.Exists(Path.Combine(folder, HarnessRunner.RowsFile)));
                Assert.IsTrue(File.Exists(Path.Combine(folder, HarnessRunner.SummaryMarkdownFile)));
                Assert.IsTrue(File.Exists(Path.Combine(folder, HarnessRunner.SummaryJsonFile)));
                Assert.AreEqual(4, File.ReadAllLines(Path.Combine(folder, HarnessRunner.RowsFile)).Length);

                // The summary of this run is the baseline of the next.
                HarnessSettings next = Small(3);
                next.baselinePath = Path.Combine(folder, HarnessRunner.SummaryJsonFile);
                HarnessResult second = HarnessRunner.Run(next);
                Assert.IsNotNull(second.Baseline);
                Assert.AreEqual(3, second.Baseline.matches);
                StringAssert.Contains("Against the earlier run", HarnessReport.ToMarkdown(second.Summary, second.Baseline));
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }

        [Test]
        public void TheSameSettingsGiveTheSameRows()
        {
            string a = HarnessReport.ToCsv(HarnessRunner.Run(Small(3)).Rows);
            string b = HarnessReport.ToCsv(HarnessRunner.Run(Small(3)).Rows);

            Assert.AreEqual(a, b);
        }

        [Test]
        public void WritingWithoutAFolderFails()
        {
            HarnessResult r = HarnessRunner.Run(Small(1));

            Assert.That(() => HarnessRunner.Write(r, ""), Throws.ArgumentException);
        }
    }
}
