using System;
using System.IO;
using UnityEngine;

namespace BookBench {

    public static class BenchPlayerEntry {

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnLoad() {
            if (Application.isEditor) return;
            string customPath = null;
            var renderStats = false;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; ++i) {
                if (args[i] == "-resultsPath") customPath = args[i + 1];
            }
            for (int i = 0; i < args.Length; ++i) {
                if (args[i] == "-rtsNoNaive10k") Rts.RtsBenchmarks.IncludeNaive10k = false;
                if (args[i] == "-rtsNoDetail") Rts.RtsBenchmarks.IncludeDetail = false;
                if (args[i] == "-renderStats") renderStats = true;
            }

            if (renderStats) {
                // Только фаза рендеринга: настоящие кадры через PlayerLoop, отчет и выход делает RtsRenderStatsRunner
                var renderPath = customPath ?? Path.Combine(Application.persistentDataPath, "results_player_render.json");
                try {
                    Debug.Log("[Book] player: render stats mode, results -> " + renderPath);
                    Rts.RtsRenderStatsRunner.Launch(renderPath);
                } catch (Exception e) {
                    Debug.LogError("[Book] player render stats failed to start: " + e);
                    Application.Quit(1);
                }
                return;
            }

            var path = customPath ?? Path.Combine(Application.persistentDataPath, "results_player.json");
            var exitCode = 0;
            try {
                var report = BenchSuite.RunAll(Debug.Log);
                var md = BenchReportWriter.Write(report, path);
                Debug.Log("[Book] player results written: " + path + " and " + md);
            } catch (Exception e) {
                exitCode = 1;
                Debug.LogError("[Book] player run failed: " + e);
            } finally {
                Application.Quit(exitCode);
            }
        }
    }
}
