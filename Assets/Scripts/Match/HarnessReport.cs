using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BS
{
    /// <summary>The spread of one measured value over the matches that have it: how many, the mean, the median and the 10th and 90th percentile.</summary>
    [Serializable]
    public class Stat
    {
        public int count;
        public float mean;
        public float median;
        public float p10;
        public float p90;
        public float min;
        public float max;

        public static Stat Of(List<float> values)
        {
            var s = new Stat { count = values.Count };
            if (values.Count == 0) return s;

            var sorted = new List<float>(values);
            sorted.Sort();
            float sum = 0f;
            foreach (float v in sorted) sum += v;
            s.mean = sum / sorted.Count;
            s.median = Percentile(sorted, 0.5f);
            s.p10 = Percentile(sorted, 0.1f);
            s.p90 = Percentile(sorted, 0.9f);
            s.min = sorted[0];
            s.max = sorted[sorted.Count - 1];
            return s;
        }

        /// <summary>The value at a fraction of a sorted list, between the two nearest ranks.</summary>
        public static float Percentile(List<float> sorted, float fraction)
        {
            if (sorted.Count == 0) return 0f;
            if (sorted.Count == 1) return sorted[0];
            float position = fraction * (sorted.Count - 1);
            int lower = (int)Math.Floor(position);
            int upper = Math.Min(lower + 1, sorted.Count - 1);
            return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
        }
    }

    [Serializable]
    public class ReasonCount
    {
        public string reason;
        public int count;
    }

    /// <summary>What a run of matches came to, in numbers. Written as summary.json, which a later run can compare itself with.</summary>
    [Serializable]
    public class HarnessSummary
    {
        // What was played
        public int bossKind;
        public string profile;
        public int[] characters;
        public int firstSeed;
        public float step;
        public List<NamedValue> overrides = new List<NamedValue>();
        /// <summary>Empty, or a warning such as a coarse step.</summary>
        public string note = "";

        // How it went
        public int matches;
        public int survivorWins;
        public int bossWins;
        public int undecided;
        public float survivorShare;
        public List<ReasonCount> reasons = new List<ReasonCount>();
        public Stat length = new Stat();

        /// <summary>When the boss's health first fell to 50%, 25% and 0%, among the matches where it did; the share is of all matches.</summary>
        public Stat bossT50 = new Stat();
        public Stat bossT25 = new Stat();
        public Stat bossT0 = new Stat();
        public float bossT50Share;
        public float bossT25Share;
        public float bossT0Share;
        public float bossHealthEndMean;

        /// <summary>When the match entered phase 2 and phase 3, among the matches that did; the share is of all matches.</summary>
        public Stat phaseT2 = new Stat();
        public Stat phaseT3 = new Stat();
        public float phaseT2Share;
        public float phaseT3Share;

        public Stat firstFall = new Stat();
        public float firstFallShare;
        public float fallsMean;
        public float revivesMean;
        public float survivorsAliveEndMean;

        public float maxLevelMean;
        public float levelCapShare;
        public float meanLevelMean;

        public float devicesPlacedMean;
        public float devicesDestroyedMean;
        public float devicesExpiredMean;

        /// <summary>The level at which survivors stop growing (a match "reaches the cap" when someone gets there).</summary>
        const int LevelCap = GrowthConfig.MaxLevel;

        public static HarnessSummary From(IReadOnlyList<MatchRow> rows, HarnessSettings settings)
        {
            if (rows == null || rows.Count == 0) throw new ArgumentException("no matches to summarize");

            var s = new HarnessSummary
            {
                bossKind = settings.bossKind,
                profile = settings.profile,
                characters = (int[])settings.characters.Clone(),
                firstSeed = settings.firstSeed,
                step = settings.step,
                matches = rows.Count,
            };
            foreach (NamedValue o in settings.overrides) s.overrides.Add(new NamedValue { name = o.name, value = o.value });
            if (Math.Abs(settings.step - HarnessSettings.ServerStep) > 1e-6f)
                s.note = $"The step is {settings.step:0.###} s, not the server's {HarnessSettings.ServerStep:0.###} s: this run is coarser than the game.";

            var reasons = new Dictionary<string, int>();
            var length = new List<float>();
            var t50 = new List<float>(); var t25 = new List<float>(); var t0 = new List<float>(); var firstFall = new List<float>();
            var p2 = new List<float>(); var p3 = new List<float>();
            float bossEnd = 0f, falls = 0f, revives = 0f, alive = 0f, maxLevel = 0f, meanLevel = 0f, placed = 0f, destroyed = 0f, expired = 0f;
            int capped = 0;

            foreach (MatchRow r in rows)
            {
                if (r.winner == "survivors") s.survivorWins++;
                else if (r.winner == "boss") s.bossWins++;
                else s.undecided++;

                reasons.TryGetValue(r.reason, out int n);
                reasons[r.reason] = n + 1;

                length.Add(r.lengthSeconds);
                if (r.bossT50 >= 0f) t50.Add(r.bossT50);
                if (r.bossT25 >= 0f) t25.Add(r.bossT25);
                if (r.bossT0 >= 0f) t0.Add(r.bossT0);
                if (r.phaseT2 >= 0f) p2.Add(r.phaseT2);
                if (r.phaseT3 >= 0f) p3.Add(r.phaseT3);
                if (r.firstFall >= 0f) firstFall.Add(r.firstFall);

                bossEnd += r.bossHealthEnd; falls += r.falls; revives += r.revives; alive += r.survivorsAliveEnd;
                maxLevel += r.maxLevel; meanLevel += r.meanLevel;
                if (r.maxLevel >= LevelCap) capped++;
                placed += r.devicesPlaced; destroyed += r.devicesDestroyed; expired += r.devicesExpired;
            }

            float total = rows.Count;
            s.survivorShare = s.survivorWins / total;
            foreach (KeyValuePair<string, int> pair in reasons) s.reasons.Add(new ReasonCount { reason = pair.Key, count = pair.Value });
            s.reasons.Sort((a, b) => b.count != a.count ? b.count.CompareTo(a.count) : string.CompareOrdinal(a.reason, b.reason));

            s.length = Stat.Of(length);
            s.bossT50 = Stat.Of(t50); s.bossT25 = Stat.Of(t25); s.bossT0 = Stat.Of(t0);
            s.bossT50Share = t50.Count / total; s.bossT25Share = t25.Count / total; s.bossT0Share = t0.Count / total;
            s.bossHealthEndMean = bossEnd / total;
            s.phaseT2 = Stat.Of(p2); s.phaseT3 = Stat.Of(p3);
            s.phaseT2Share = p2.Count / total; s.phaseT3Share = p3.Count / total;
            s.firstFall = Stat.Of(firstFall);
            s.firstFallShare = firstFall.Count / total;
            s.fallsMean = falls / total; s.revivesMean = revives / total; s.survivorsAliveEndMean = alive / total;
            s.maxLevelMean = maxLevel / total; s.meanLevelMean = meanLevel / total; s.levelCapShare = capped / total;
            s.devicesPlacedMean = placed / total; s.devicesDestroyedMean = destroyed / total; s.devicesExpiredMean = expired / total;
            return s;
        }
    }

    /// <summary>One line of the comparison with an earlier run.</summary>
    public readonly struct Change
    {
        public readonly string Name;
        public readonly float Before;
        public readonly float Now;

        public Change(string name, float before, float now)
        {
            Name = name;
            Before = before;
            Now = now;
        }

        public float Delta => Now - Before;
    }

    /// <summary>Writes a run as CSV, markdown and JSON, and compares two runs.</summary>
    public static class HarnessReport
    {
        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        // ---- CSV ----

        public const string CsvHeader = "seed,winner,reason,lengthSeconds,bossT50,bossT25,bossT0,bossHealthEnd,firstFall,falls,revives,survivorsAliveEnd,maxLevel,meanLevel,devicesPlaced,devicesDestroyed,devicesExpired,stateHash,phaseT2,phaseT3";

        public static string ToCsv(IReadOnlyList<MatchRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(CsvHeader);
            foreach (MatchRow r in rows)
            {
                sb.Append(r.seed).Append(',').Append(r.winner).Append(',').Append(r.reason).Append(',').Append(F(r.lengthSeconds)).Append(',')
                  .Append(F(r.bossT50)).Append(',').Append(F(r.bossT25)).Append(',').Append(F(r.bossT0)).Append(',').Append(F(r.bossHealthEnd)).Append(',')
                  .Append(F(r.firstFall)).Append(',').Append(r.falls).Append(',').Append(r.revives).Append(',').Append(r.survivorsAliveEnd).Append(',')
                  .Append(r.maxLevel).Append(',').Append(F(r.meanLevel)).Append(',').Append(r.devicesPlaced).Append(',').Append(r.devicesDestroyed).Append(',')
                  .Append(r.devicesExpired).Append(',').Append(r.stateHash).Append(',').Append(F(r.phaseT2)).Append(',').Append(F(r.phaseT3)).AppendLine();
            }
            return sb.ToString();
        }

        // ---- JSON ----

        public static string ToJson(HarnessSummary summary) => UnityEngine.JsonUtility.ToJson(summary, true);

        public static HarnessSummary FromJson(string json)
        {
            HarnessSummary s = UnityEngine.JsonUtility.FromJson<HarnessSummary>(json);
            if (s == null || s.matches <= 0) throw new ArgumentException("not a harness summary");
            return s;
        }

        // ---- Comparison ----

        /// <summary>The values that matter for the target, now against an earlier run.</summary>
        public static List<Change> Compare(HarnessSummary now, HarnessSummary before)
        {
            return new List<Change>
            {
                new Change("survivor win share", before.survivorShare, now.survivorShare),
                new Change("length mean (s)", before.length.mean, now.length.mean),
                new Change("length median (s)", before.length.median, now.length.median),
                new Change("length p10 (s)", before.length.p10, now.length.p10),
                new Change("length p90 (s)", before.length.p90, now.length.p90),
                new Change("boss reaches 50% (share)", before.bossT50Share, now.bossT50Share),
                new Change("boss 50% at (s, mean)", before.bossT50.mean, now.bossT50.mean),
                new Change("boss 25% at (s, mean)", before.bossT25.mean, now.bossT25.mean),
                new Change("phase 2 at (s, mean)", before.phaseT2.mean, now.phaseT2.mean),
                new Change("phase 3 at (s, mean)", before.phaseT3.mean, now.phaseT3.mean),
                new Change("boss health at the end (mean)", before.bossHealthEndMean, now.bossHealthEndMean),
                new Change("first fall at (s, mean)", before.firstFall.mean, now.firstFall.mean),
                new Change("falls per match", before.fallsMean, now.fallsMean),
                new Change("revives per match", before.revivesMean, now.revivesMean),
                new Change("highest level (mean)", before.maxLevelMean, now.maxLevelMean),
                new Change("level cap reached (share)", before.levelCapShare, now.levelCapShare),
                new Change("devices placed per match", before.devicesPlacedMean, now.devicesPlacedMean),
                new Change("devices destroyed per match", before.devicesDestroyedMean, now.devicesDestroyedMean),
            };
        }

        // ---- Markdown ----

        static string Pct(float share) => (share * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%";

        static string Signed(float v) => (v >= 0f ? "+" : "") + F(v);

        public static string ToMarkdown(HarnessSummary s, HarnessSummary baseline = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Match harness summary");
            sb.AppendLine();
            sb.AppendLine($"- Boss kind {s.bossKind} ({BossCatalog.Get((byte)s.bossKind).Name}), profile {s.profile}, characters {string.Join(",", s.characters)}");
            sb.AppendLine($"- {s.matches} matches from seed {s.firstSeed}, step {F(s.step)} s");
            if (s.overrides.Count > 0)
            {
                var parts = new List<string>();
                foreach (NamedValue o in s.overrides) parts.Add($"{o.name}={F(o.value)}");
                sb.AppendLine($"- Overrides: {string.Join(", ", parts)}");
            }
            if (!string.IsNullOrEmpty(s.note)) sb.AppendLine($"- **Note:** {s.note}");
            sb.AppendLine();

            sb.AppendLine("## Result");
            sb.AppendLine();
            sb.AppendLine("| | matches | share |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine($"| survivors win | {s.survivorWins} | {Pct(s.survivorShare)} |");
            sb.AppendLine($"| boss wins | {s.bossWins} | {Pct(s.bossWins / (float)s.matches)} |");
            if (s.undecided > 0) sb.AppendLine($"| no result | {s.undecided} | {Pct(s.undecided / (float)s.matches)} |");
            sb.AppendLine();
            sb.AppendLine("| reason | matches |");
            sb.AppendLine("|---|---|");
            foreach (ReasonCount r in s.reasons) sb.AppendLine($"| {r.reason} | {r.count} |");
            sb.AppendLine();

            sb.AppendLine("## Time (seconds)");
            sb.AppendLine();
            sb.AppendLine("| | reached | mean | median | p10 | p90 |");
            sb.AppendLine("|---|---|---|---|---|---|");
            sb.AppendLine($"| match length | {Pct(1f)} | {F(s.length.mean)} | {F(s.length.median)} | {F(s.length.p10)} | {F(s.length.p90)} |");
            sb.AppendLine($"| boss at 50% | {Pct(s.bossT50Share)} | {F(s.bossT50.mean)} | {F(s.bossT50.median)} | {F(s.bossT50.p10)} | {F(s.bossT50.p90)} |");
            sb.AppendLine($"| boss at 25% | {Pct(s.bossT25Share)} | {F(s.bossT25.mean)} | {F(s.bossT25.median)} | {F(s.bossT25.p10)} | {F(s.bossT25.p90)} |");
            sb.AppendLine($"| phase 2 starts | {Pct(s.phaseT2Share)} | {F(s.phaseT2.mean)} | {F(s.phaseT2.median)} | {F(s.phaseT2.p10)} | {F(s.phaseT2.p90)} |");
            sb.AppendLine($"| phase 3 starts | {Pct(s.phaseT3Share)} | {F(s.phaseT3.mean)} | {F(s.phaseT3.median)} | {F(s.phaseT3.p10)} | {F(s.phaseT3.p90)} |");
            sb.AppendLine($"| boss at 0% | {Pct(s.bossT0Share)} | {F(s.bossT0.mean)} | {F(s.bossT0.median)} | {F(s.bossT0.p10)} | {F(s.bossT0.p90)} |");
            sb.AppendLine($"| first survivor falls | {Pct(s.firstFallShare)} | {F(s.firstFall.mean)} | {F(s.firstFall.median)} | {F(s.firstFall.p10)} | {F(s.firstFall.p90)} |");
            sb.AppendLine();

            sb.AppendLine("## Per match (mean)");
            sb.AppendLine();
            sb.AppendLine("| value | mean |");
            sb.AppendLine("|---|---|");
            sb.AppendLine($"| boss health at the end | {Pct(s.bossHealthEndMean)} |");
            sb.AppendLine($"| survivor falls | {F(s.fallsMean)} |");
            sb.AppendLine($"| revivals | {F(s.revivesMean)} |");
            sb.AppendLine($"| survivors alive at the end | {F(s.survivorsAliveEndMean)} |");
            sb.AppendLine($"| highest level | {F(s.maxLevelMean)} |");
            sb.AppendLine($"| mean level | {F(s.meanLevelMean)} |");
            sb.AppendLine($"| matches where someone reached level {GrowthConfig.MaxLevel} | {Pct(s.levelCapShare)} |");
            sb.AppendLine($"| devices placed / destroyed / expired | {F(s.devicesPlacedMean)} / {F(s.devicesDestroyedMean)} / {F(s.devicesExpiredMean)} |");

            if (baseline != null)
            {
                sb.AppendLine();
                sb.AppendLine($"## Against the earlier run ({baseline.matches} matches, profile {baseline.profile}, boss kind {baseline.bossKind})");
                sb.AppendLine();
                sb.AppendLine("| value | before | now | change |");
                sb.AppendLine("|---|---|---|---|");
                foreach (Change c in Compare(s, baseline)) sb.AppendLine($"| {c.Name} | {F(c.Before)} | {F(c.Now)} | {Signed(c.Delta)} |");
            }
            return sb.ToString();
        }
    }
}
