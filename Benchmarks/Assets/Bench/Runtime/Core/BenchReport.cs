using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Burst;
using UnityEngine;

namespace BookBench {

    [Serializable]
    public class BenchEnvironment {
        public string unityVersion;
        public string scriptingBackend;
        public bool isEditor;
        public bool burstEnabled;
        public bool burstSafetyChecks;
        public bool developmentBuild;
        public bool safetyChecks;
        public string processorType;
        public int processorCount;
        public int systemMemorySize;
        public string operatingSystem;
        public string graphicsDeviceType;
        public string date;

        public static BenchEnvironment Capture() {
            var env = new BenchEnvironment();
            env.unityVersion = Application.unityVersion;
#if ENABLE_IL2CPP
            env.scriptingBackend = "IL2CPP";
#else
            env.scriptingBackend = "Mono";
#endif
            env.isEditor = Application.isEditor;
            env.burstEnabled = BurstCompiler.IsEnabled;
            try {
                env.burstSafetyChecks = BurstCompiler.Options.EnableBurstSafetyChecks;
            } catch (Exception) {
                env.burstSafetyChecks = false;
            }
            env.developmentBuild = Debug.isDebugBuild;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            env.safetyChecks = true;
#else
            env.safetyChecks = false;
#endif
            env.processorType = SystemInfo.processorType;
            env.processorCount = SystemInfo.processorCount;
            env.systemMemorySize = SystemInfo.systemMemorySize;
            env.operatingSystem = SystemInfo.operatingSystem;
            env.graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString();
            env.date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            return env;
        }
    }

    [Serializable]
    public class BenchResult {
        public string id;
        public string chapter;
        public string variant;
        public double medianNs;
        public double minNs;
        public double p90Ns;
        public double stddevNs;
        // -1 = не измерялось
        public double bytesPerOp = -1;
        public int n;
        public string notes = "";
        public string error = "";
    }

    [Serializable]
    public class BenchReport {
        public BenchEnvironment environment;
        public List<BenchResult> results = new List<BenchResult>();
    }

    public static class BenchReportWriter {

        public static string Write(BenchReport report, string jsonPath) {
            var dir = Path.GetDirectoryName(jsonPath);
            if (string.IsNullOrEmpty(dir) == false) Directory.CreateDirectory(dir);
            File.WriteAllText(jsonPath, JsonUtility.ToJson(report, true));
            var mdPath = Path.ChangeExtension(jsonPath, ".md");
            File.WriteAllText(mdPath, ToMarkdown(report));
            return mdPath;
        }

        public static string ToMarkdown(BenchReport report) {
            var sb = new StringBuilder();
            var env = report.environment;
            sb.Append("# BookBenchmarks results\n\n");
            if (env != null) {
                sb.Append("| Key | Value |\n|---|---|\n");
                sb.Append("| Unity | ").Append(env.unityVersion).Append(" |\n");
                sb.Append("| Backend | ").Append(env.scriptingBackend).Append(" |\n");
                sb.Append("| Editor | ").Append(env.isEditor).Append(" |\n");
                sb.Append("| Burst enabled | ").Append(env.burstEnabled).Append(" |\n");
                sb.Append("| Burst safety checks | ").Append(env.burstSafetyChecks).Append(" |\n");
                sb.Append("| Collections checks (managed) | ").Append(env.safetyChecks).Append(" |\n");
                sb.Append("| Development build | ").Append(env.developmentBuild).Append(" |\n");
                sb.Append("| CPU | ").Append(env.processorType).Append(" x").Append(env.processorCount).Append(" |\n");
                sb.Append("| RAM (MB) | ").Append(env.systemMemorySize).Append(" |\n");
                sb.Append("| OS | ").Append(env.operatingSystem).Append(" |\n");
                sb.Append("| Graphics | ").Append(env.graphicsDeviceType).Append(" |\n");
                sb.Append("| Date | ").Append(env.date).Append(" |\n\n");
            }

            sb.Append("| id | variant | median | min | p90 | bytes/op | n | notes |\n");
            sb.Append("|---|---|---:|---:|---:|---:|---:|---|\n");
            for (int i = 0; i < report.results.Count; ++i) {
                var r = report.results[i];
                sb.Append("| ").Append(r.id);
                sb.Append(" | ").Append(Escape(r.variant));
                sb.Append(" | ").Append(FormatNs(r.medianNs));
                sb.Append(" | ").Append(FormatNs(r.minNs));
                sb.Append(" | ").Append(FormatNs(r.p90Ns));
                sb.Append(" | ").Append(r.bytesPerOp < 0 ? "-" : r.bytesPerOp.ToString("0.##", CultureInfo.InvariantCulture));
                sb.Append(" | ").Append(r.n);
                var notes = r.notes;
                if (string.IsNullOrEmpty(r.error) == false) {
                    var firstLine = r.error.Split('\n')[0];
                    notes = "ERROR: " + firstLine + (string.IsNullOrEmpty(notes) ? "" : "; " + notes);
                }
                sb.Append(" | ").Append(Escape(notes)).Append(" |\n");
            }
            return sb.ToString();
        }

        public static string FormatNs(double ns) {
            if (ns <= 0) return "-";
            if (ns < 1_000) return ns.ToString("0.###", CultureInfo.InvariantCulture) + " ns";
            if (ns < 1_000_000) return (ns / 1_000).ToString("0.###", CultureInfo.InvariantCulture) + " µs";
            return (ns / 1_000_000).ToString("0.###", CultureInfo.InvariantCulture) + " ms";
        }

        private static string Escape(string s) {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
