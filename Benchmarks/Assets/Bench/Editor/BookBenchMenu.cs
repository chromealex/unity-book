using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BookBench.Editor {

    public static class BookBenchMenu {

        private const string ScenePath = "Assets/Bench/Bench.unity";
        private const string ResourcesDir = "Assets/Bench/Resources";
        private const int PlayerTimeoutMs = 30 * 60 * 1000;
        private const int RenderTimeoutMs = 10 * 60 * 1000;

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        private static string ResultsDir => Path.Combine(ProjectRoot, "Results");

        private static void Log(string message) {
            Debug.Log("[Book] " + message);
        }

        // ================= Editor run =================

        [MenuItem("Book/Run benchmarks in Editor")]
        public static void RunInEditor() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                Debug.LogError("[Book] exit Play mode first");
                return;
            }
            EnsureRtsMaterials();

            var burstOptions = BurstCompiler.Options;
            var prevBurstEnabled = burstOptions.EnableBurstCompilation;
            var prevSafety = burstOptions.EnableBurstSafetyChecks;
            var prevSync = burstOptions.EnableBurstCompileSynchronously;
            var prevLeak = NativeLeakDetection.Mode;
            var prevJobDebugger = JobsUtility.JobDebuggerEnabled;
            try {
                burstOptions.EnableBurstCompilation = true;
                burstOptions.EnableBurstSafetyChecks = false;
                burstOptions.EnableBurstCompileSynchronously = true;
                NativeLeakDetection.Mode = NativeLeakDetectionMode.Disabled;
                JobsUtility.JobDebuggerEnabled = false;
                Log("Editor run: Burst on, Burst safety checks off, synchronous compilation, leak detection off, job debugger off");

                var report = BenchSuite.RunAll(Debug.Log);
                // Статистика отрисовки до/после (UnityStats есть только в редакторе): строки дописываются в тот же отчет
                var firstNew = Bench.Results.Count;
                try {
                    EditorRenderStats.Run();
                } catch (Exception e) {
                    Bench.Error("rts.render.n10000.editor", "rts", "render stats", e);
                }
                for (int i = firstNew; i < Bench.Results.Count; ++i) {
                    report.results.Add(Bench.Results[i]);
                }
                var backend = report.environment.scriptingBackend.ToLowerInvariant();
                var jsonPath = Path.Combine(ResultsDir, "editor_" + backend + ".json");
                var mdPath = BenchReportWriter.Write(report, jsonPath);
                Log("Editor results written: " + jsonPath + " and " + mdPath);
            } catch (Exception e) {
                Debug.LogError("[Book] Editor run failed: " + e);
            } finally {
                burstOptions.EnableBurstCompilation = prevBurstEnabled;
                burstOptions.EnableBurstSafetyChecks = prevSafety;
                burstOptions.EnableBurstCompileSynchronously = prevSync;
                NativeLeakDetection.Mode = prevLeak;
                JobsUtility.JobDebuggerEnabled = prevJobDebugger;
                Log("Editor settings restored");
            }
        }

        // ================= Player build & run =================

        [MenuItem("Book/Build and run Player (IL2CPP)")]
        public static void BuildAndRunIl2Cpp() {
            BuildAndRun(ScriptingImplementation.IL2CPP);
        }

        [MenuItem("Book/Build and run Player (Mono)")]
        public static void BuildAndRunMono() {
            BuildAndRun(ScriptingImplementation.Mono2x);
        }

        [MenuItem("Book/Build and run Player (IL2CPP, render stats)")]
        public static void BuildAndRunIl2CppRenderStats() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                Debug.LogError("[Book] exit Play mode first");
                return;
            }
            var exe = BuildPlayer(ScriptingImplementation.IL2CPP, "il2cpp");
            if (exe == null) return;
            var resultsPath = Path.Combine(ResultsDir, "player_il2cpp_render.json");
            var logPath = Path.Combine(ResultsDir, "player_il2cpp_render.log");
            // Без -batchmode: нужны настоящие кадры с Present, иначе FrameTimingManager нечего мерить
            var args = "-renderStats -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -resultsPath \"" + resultsPath + "\" -logFile \"" + logPath + "\"";
            StartPlayer(exe, args, resultsPath, logPath, "il2cpp render stats", RenderTimeoutMs);
        }

        private static void BuildAndRun(ScriptingImplementation backend) {
            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                Debug.LogError("[Book] exit Play mode first");
                return;
            }
            var backendName = backend == ScriptingImplementation.IL2CPP ? "il2cpp" : "mono";
            var exe = BuildPlayer(backend, backendName);
            if (exe == null) return;
            var resultsPath = Path.Combine(ResultsDir, "player_" + backendName + ".json");
            var logPath = Path.Combine(ResultsDir, "player_" + backendName + ".log");
            var args = "-batchmode -resultsPath \"" + resultsPath + "\" -logFile \"" + logPath + "\"";
            StartPlayer(exe, args, resultsPath, logPath, backendName, PlayerTimeoutMs);
        }

        /// <summary>Собирает release-плеер. Возвращает путь к исполняемому файлу или null (ошибки уже в Console).</summary>
        private static string BuildPlayer(ScriptingImplementation backend, string backendName) {
            var appPath = Path.Combine(ProjectRoot, "Builds", backendName, "BookBenchmarks.app");

            EnsureRtsMaterials();
            if (EnsureScene() == false) return null;

            var target = NamedBuildTarget.Standalone;
            var prevBackend = PlayerSettings.GetScriptingBackend(target);
            var prevDevelopment = EditorUserBuildSettings.development;
            var prevFrameTiming = PlayerSettings.enableFrameTimingStats;
            var prevRunInBackground = PlayerSettings.runInBackground;
            var prevArch = TrySetMacArchitectureArm64();
            BuildReport report;
            try {
                PlayerSettings.SetScriptingBackend(target, backend);
                EditorUserBuildSettings.development = false;
                // FrameTimingManager в release-плеере работает только с этим флагом (render stats)
                PlayerSettings.enableFrameTimingStats = true;
                // окно render stats может оказаться не в фокусе: плеер не должен вставать на паузу
                PlayerSettings.runInBackground = true;
                Log("Building " + backendName + " player (release, StandaloneOSX, frame timing stats on) to " + appPath);
                var options = new BuildPlayerOptions {
                    scenes = new[] { ScenePath },
                    locationPathName = appPath,
                    target = BuildTarget.StandaloneOSX,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None,
                };
                report = BuildPipeline.BuildPlayer(options);
            } catch (Exception e) {
                Debug.LogError("[Book] Build threw: " + e);
                return null;
            } finally {
                PlayerSettings.SetScriptingBackend(target, prevBackend);
                EditorUserBuildSettings.development = prevDevelopment;
                PlayerSettings.enableFrameTimingStats = prevFrameTiming;
                PlayerSettings.runInBackground = prevRunInBackground;
                RestoreMacArchitecture(prevArch);
            }

            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded) {
                Debug.LogError("[Book] Build " + backendName + " failed: result=" + summary.result + ", errors=" + summary.totalErrors + ". If IL2CPP: check that 'Mac Build Support (IL2CPP)' module and Xcode command line tools are installed.");
                foreach (var step in report.steps) {
                    foreach (var message in step.messages) {
                        if (message.type == LogType.Error || message.type == LogType.Exception) {
                            Debug.LogError("[Book] build error (" + step.name + "): " + message.content);
                        }
                    }
                }
                return null;
            }
            Log("Build " + backendName + " succeeded in " + summary.totalTime.TotalSeconds.ToString("0") + " s, size " + (summary.totalSize / (1024 * 1024)) + " MB");

            var exe = FindExecutable(appPath);
            if (exe == null) {
                Debug.LogError("[Book] executable not found in " + appPath + "/Contents/MacOS");
                return null;
            }
            return exe;
        }

        private static void StartPlayer(string exe, string args, string resultsPath, string logPath, string runName, int timeoutMs) {
            Directory.CreateDirectory(ResultsDir);
            if (File.Exists(resultsPath)) File.Delete(resultsPath);
            Log("Starting player: " + exe + " " + args + " (results appear when it exits; Editor stays responsive)");
            var thread = new Thread(() => RunPlayerProcess(exe, args, resultsPath, logPath, runName, timeoutMs)) {
                IsBackground = true,
                Name = "Book player runner",
            };
            thread.Start();
        }

        private static void RunPlayerProcess(string exe, string args, string resultsPath, string logPath, string backendName, int timeoutMs) {
            try {
                var info = new System.Diagnostics.ProcessStartInfo {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using (var process = System.Diagnostics.Process.Start(info)) {
                    if (process == null) {
                        Debug.LogError("[Book] failed to start player " + exe);
                        return;
                    }
                    var started = DateTime.Now;
                    if (process.WaitForExit(timeoutMs) == false) {
                        Debug.LogError("[Book] player " + backendName + " timed out after " + (timeoutMs / 60000) + " min, killing");
                        try {
                            process.Kill();
                        } catch (Exception) {
                            // уже завершился
                        }
                        return;
                    }
                    var seconds = (DateTime.Now - started).TotalSeconds;
                    if (File.Exists(resultsPath)) {
                        Debug.Log("[Book] player " + backendName + " finished in " + seconds.ToString("0") + " s (exit code " + process.ExitCode + "). Results: " + resultsPath + " and " + Path.ChangeExtension(resultsPath, ".md") + ", log: " + logPath);
                    } else {
                        Debug.LogError("[Book] player " + backendName + " exited (code " + process.ExitCode + ") without results. See log: " + logPath);
                    }
                }
            } catch (Exception e) {
                Debug.LogError("[Book] player run failed: " + e);
            }
        }

        private static string FindExecutable(string appPath) {
            var macOsDir = Path.Combine(appPath, "Contents", "MacOS");
            var expected = Path.Combine(macOsDir, "BookBenchmarks");
            if (File.Exists(expected)) return expected;
            if (Directory.Exists(macOsDir) == false) return null;
            var files = Directory.GetFiles(macOsDir);
            return files.Length > 0 ? files[0] : null;
        }

        // ================= assets =================

        private static bool EnsureScene() {
            if (File.Exists(Path.Combine(ProjectRoot, ScenePath))) return true;
            try {
                Scene scene;
                var additive = true;
                try {
                    scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                } catch (Exception) {
                    additive = false;
                    scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                if (EditorSceneManager.SaveScene(scene, ScenePath) == false) {
                    Debug.LogError("[Book] failed to save " + ScenePath);
                    return false;
                }
                if (additive) EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.Refresh();
                Log("Created empty scene " + ScenePath);
                return true;
            } catch (Exception e) {
                Debug.LogError("[Book] failed to create scene: " + e);
                return false;
            }
        }

        /// <summary>Материалы для шага 4 лежат в Resources, чтобы шейдеры попали в билд.</summary>
        private static void EnsureRtsMaterials() {
            try {
                if (AssetDatabase.IsValidFolder(ResourcesDir) == false) AssetDatabase.CreateFolder("Assets/Bench", "Resources");
                CreateMaterial(ResourcesDir + "/RtsUnitMaterial.mat", "Standard", true);
                CreateMaterial(ResourcesDir + "/RtsBarMaterial.mat", "RTS/HealthBar", false);
                AssetDatabase.SaveAssets();
            } catch (Exception e) {
                Debug.LogWarning("[Book] could not create RTS materials (step 4 will be skipped): " + e.Message);
            }
        }

        private static void CreateMaterial(string path, string shaderName, bool instancing) {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var shader = Shader.Find(shaderName);
            if (shader == null) {
                Debug.LogWarning("[Book] shader not found: " + shaderName);
                return;
            }
            var material = new Material(shader);
            material.enableInstancing = instancing;
            AssetDatabase.CreateAsset(material, path);
            Log("Created " + path);
        }

        // ================= macOS architecture (best effort, via reflection) =================

        private static object TrySetMacArchitectureArm64() {
            try {
                var property = FindMacArchitectureProperty();
                if (property == null) {
                    Log("macOS architecture setting not found; check Build Profiles > macOS > Architecture = Apple silicon");
                    return null;
                }
                var previous = property.GetValue(null);
                var arm = Enum.Parse(property.PropertyType, "ARM64", true);
                property.SetValue(null, arm);
                Log("macOS architecture: " + previous + " -> " + arm);
                return previous;
            } catch (Exception e) {
                Log("could not set macOS architecture to ARM64 (" + e.Message + "); check Build Profiles > macOS > Architecture");
                return null;
            }
        }

        private static void RestoreMacArchitecture(object previous) {
            if (previous == null) return;
            try {
                FindMacArchitectureProperty()?.SetValue(null, previous);
            } catch (Exception) {
                // не критично
            }
        }

        private static PropertyInfo FindMacArchitectureProperty() {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                var type = assembly.GetType("UnityEditor.OSXStandalone.UserBuildSettings", false);
                if (type == null) continue;
                var property = type.GetProperty("architecture", BindingFlags.Public | BindingFlags.Static);
                if (property != null && property.PropertyType.IsEnum && property.CanWrite) return property;
            }
            return null;
        }
    }
}
