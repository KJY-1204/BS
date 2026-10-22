using System;
using System.Collections.Generic;
using System.IO;

namespace BS
{
    /// <summary>The matches of a run, their summary and, when the settings name one, the earlier summary it is compared with.</summary>
    public sealed class HarnessResult
    {
        public List<MatchRow> Rows = new List<MatchRow>();
        public HarnessSummary Summary;
        public HarnessSummary Baseline;
    }

    /// <summary>Plays many matches one after another from a settings file and writes what came of them.</summary>
    public static class HarnessRunner
    {
        public const string RowsFile = "rows.csv";
        public const string SummaryMarkdownFile = "summary.md";
        public const string SummaryJsonFile = "summary.json";

        /// <summary>Reads a settings file. Fails with a message that says what is wrong with the file or the values.</summary>
        public static HarnessSettings LoadSettings(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("no settings file given");
            if (!File.Exists(path)) throw new FileNotFoundException($"settings file not found: {path}");

            HarnessSettings settings;
            try
            {
                settings = UnityEngine.JsonUtility.FromJson<HarnessSettings>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                throw new ArgumentException($"settings file {path} is not valid JSON: {e.Message}");
            }
            if (settings == null) throw new ArgumentException($"settings file {path} is empty");

            string problem = settings.Validate();
            if (problem != null) throw new ArgumentException($"settings file {path}: {problem}");
            return settings;
        }

        /// <summary>Plays <c>settings.matches</c> matches with the seeds <c>firstSeed</c>, <c>firstSeed + 1</c>, and so on.</summary>
        public static HarnessResult Run(HarnessSettings settings, Action<int, int> progress = null)
        {
            string problem = settings.Validate();
            if (problem != null) throw new ArgumentException(problem);

            var result = new HarnessResult();
            if (!string.IsNullOrEmpty(settings.baselinePath))
            {
                if (!File.Exists(settings.baselinePath)) throw new FileNotFoundException($"baseline summary not found: {settings.baselinePath}");
                result.Baseline = HarnessReport.FromJson(File.ReadAllText(settings.baselinePath));
            }

            for (int i = 0; i < settings.matches; i++)
            {
                result.Rows.Add(MatchRunner.RunOne(settings, settings.firstSeed + i));
                progress?.Invoke(i + 1, settings.matches);
            }

            result.Summary = HarnessSummary.From(result.Rows, settings);
            return result;
        }

        /// <summary>Writes rows.csv, summary.md and summary.json into the folder (made when missing).</summary>
        public static void Write(HarnessResult result, string folder)
        {
            if (string.IsNullOrEmpty(folder)) throw new ArgumentException("no output folder given");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, RowsFile), HarnessReport.ToCsv(result.Rows));
            File.WriteAllText(Path.Combine(folder, SummaryMarkdownFile), HarnessReport.ToMarkdown(result.Summary, result.Baseline));
            File.WriteAllText(Path.Combine(folder, SummaryJsonFile), HarnessReport.ToJson(result.Summary));
        }
    }
}
