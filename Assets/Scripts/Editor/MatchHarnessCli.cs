using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BS.Editor
{
    /// <summary>
    /// Plays harness matches from the command line or from the Unity MCP and writes the report.
    /// <code>Unity -batchmode -nographics -projectPath . -executeMethod BS.Editor.MatchHarnessCli.Run -bsHarness settings.json -bsHarnessOut out -quit</code>
    /// </summary>
    public static class MatchHarnessCli
    {
        /// <summary>The command line entry point: reads <c>-bsHarness</c> and <c>-bsHarnessOut</c> and exits with 0 when the report was written and 1 when not.</summary>
        public static void Run()
        {
            string settings = ArgumentOf("-bsHarness");
            string output = ArgumentOf("-bsHarnessOut");
            try
            {
                string message = RunWith(settings, output);
                Debug.Log($"[BS] harness: {message}");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[BS] harness failed: {e.Message}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        /// <summary>Plays the matches of a settings file and writes the report into the folder. Returns a one-line summary; throws with the reason when it cannot.</summary>
        public static string RunWith(string settingsPath, string outputFolder)
        {
            if (string.IsNullOrEmpty(outputFolder)) throw new ArgumentException("no output folder given (-bsHarnessOut <folder>)");

            HarnessSettings settings = HarnessRunner.LoadSettings(settingsPath);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            HarnessResult result = HarnessRunner.Run(settings);
            watch.Stop();
            HarnessRunner.Write(result, outputFolder);

            HarnessSummary s = result.Summary;
            return $"{s.matches} matches in {watch.Elapsed.TotalSeconds:F1} s: survivors {s.survivorWins}, boss {s.bossWins}, length median {s.length.median:F0} s. " +
                   $"Written to {Path.GetFullPath(outputFolder)}";
        }

        static string ArgumentOf(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
