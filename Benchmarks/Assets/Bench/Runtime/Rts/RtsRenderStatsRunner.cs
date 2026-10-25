using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace BookBench.Rts {

    /// <summary>
    /// [bench] Фаза рендеринга в плеере (аргумент -renderStats, без -batchmode). Крутит настоящие кадры через PlayerLoop:
    /// в Update — кадр симуляции шага 3/4, в LateUpdate — Complete (+ Draw для шага 4), дальше Unity рендерит и показывает кадр.
    /// Каждый кадр читает FrameTimingManager (cpuMainThreadFrameTime, cpuRenderThreadFrameTime, gpuFrameTime)
    /// и счетчики ProfilerRecorder (Batches / SetPass Calls / Draw Calls). Пишет отчет и закрывает плеер.
    /// </summary>
    public sealed class RtsRenderStatsRunner : MonoBehaviour {

        private const string Chapter = "rts";
        private const int UnitCount = 10_000;
        private const int WarmupFrames = 30;
        private const int SampleFrames = 120;
        private const int MaxFramesPerPhase = 600;

        private static readonly RtsRenderKind[] Phases = {
            RtsRenderKind.GameObjectsNoInstancing,
            RtsRenderKind.GameObjectsInstancing,
            RtsRenderKind.Step4Instanced,
        };

        private string resultsPath;
        private BenchReport report;
        private Camera renderCamera;
        private Light sun;
        private RtsRenderScenario scenario;
        private bool scenarioDone;
        private int phase = -1;
        private int phaseFrame;
        private long simTicks;
        private ulong lastFrameStart;
        private bool finished;
        private int exitCode;

        private readonly FrameTiming[] timings = new FrameTiming[1];
        private readonly List<double> mainThreadNs = new List<double>(SampleFrames);
        private readonly List<double> renderThreadNs = new List<double>(SampleFrames);
        private readonly List<double> gpuNs = new List<double>(SampleFrames);
        private readonly List<double> cpuFrameNs = new List<double>(SampleFrames);
        private readonly List<double> deltaNs = new List<double>(SampleFrames);
        private readonly List<double> simNs = new List<double>(SampleFrames);
        private readonly List<double> batches = new List<double>(SampleFrames);
        private readonly List<double> setPassCalls = new List<double>(SampleFrames);
        private readonly List<double> drawCalls = new List<double>(SampleFrames);

        private ProfilerRecorder batchesRecorder;
        private ProfilerRecorder setPassRecorder;
        private ProfilerRecorder drawCallsRecorder;

        private string Id => "rts.render.n" + UnitCount;

        public static void Launch(string resultsPath) {
            Bench.Log = s => Debug.Log("[Book] " + s);
            var go = new GameObject("BookRenderStats");
            DontDestroyOnLoad(go);
            var runner = go.AddComponent<RtsRenderStatsRunner>();
            runner.resultsPath = resultsPath;
        }

        private void Start() {
            Bench.Results.Clear();
            this.report = new BenchReport {
                environment = BenchEnvironment.Capture(),
            };
            Bench.Log("render stats: start, screen " + Screen.width + "x" + Screen.height + ", graphics " + SystemInfo.graphicsDeviceType + ", batchmode=" + Application.isBatchMode + ", frame timing enabled=" + FrameTimingManager.IsFeatureEnabled());
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) {
                Bench.Info(this.Id, Chapter, "render stats", -1, "SKIPPED: no graphics device (-nographics)");
                this.Finish();
                return;
            }
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            this.renderCamera = RtsRenderScenario.CreateCamera(null);
            this.sun = RtsRenderScenario.CreateLight();
            this.batchesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            this.setPassRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            this.drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        }

        private void Update() {
            if (this.finished) return;
            try {
                if (this.scenario == null || this.scenarioDone) {
                    // уборка прошлой фазы здесь, а не в LateUpdate: ее Draw-вызовы того кадра уже отрисованы
                    this.DisposeScenario();
                    this.NextPhase();
                    if (this.finished) return;
                }

                FrameTimingManager.CaptureFrameTimings();
                if (this.phaseFrame >= WarmupFrames) {
                    var n = FrameTimingManager.GetLatestTimings(1, this.timings);
                    if (n > 0 && this.timings[0].frameStartTimestamp != this.lastFrameStart) {
                        var t = this.timings[0];
                        this.lastFrameStart = t.frameStartTimestamp;
                        this.mainThreadNs.Add(t.cpuMainThreadFrameTime * 1e6);
                        this.renderThreadNs.Add(t.cpuRenderThreadFrameTime * 1e6);
                        this.gpuNs.Add(t.gpuFrameTime * 1e6);
                        this.cpuFrameNs.Add(t.cpuFrameTime * 1e6);
                    }
                    this.deltaNs.Add(UnityEngine.Time.unscaledDeltaTime * 1e9);
                    AddCounter(this.batches, this.batchesRecorder);
                    AddCounter(this.setPassCalls, this.setPassRecorder);
                    AddCounter(this.drawCalls, this.drawCallsRecorder);
                }

                var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                this.scenario.Update();
                this.simTicks = System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            } catch (Exception e) {
                this.Fail(e);
            }
        }

        private void LateUpdate() {
            if (this.finished || this.scenario == null || this.scenarioDone) return;
            try {
                var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                this.scenario.LateUpdate();
                this.simTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
                if (this.phaseFrame >= WarmupFrames) this.simNs.Add(Bench.TicksToNs(this.simTicks));
                ++this.phaseFrame;
                if (this.mainThreadNs.Count >= SampleFrames || this.phaseFrame >= MaxFramesPerPhase) {
                    this.RecordPhase();
                    this.scenarioDone = true;
                }
            } catch (Exception e) {
                this.Fail(e);
            }
        }

        private static void AddCounter(List<double> list, ProfilerRecorder recorder) {
            if (recorder.Valid) list.Add(recorder.LastValue);
        }

        private void NextPhase() {
            ++this.phase;
            if (this.phase >= Phases.Length) {
                this.Finish();
                return;
            }
            this.phaseFrame = 0;
            this.scenarioDone = false;
            this.mainThreadNs.Clear();
            this.renderThreadNs.Clear();
            this.gpuNs.Clear();
            this.cpuFrameNs.Clear();
            this.deltaNs.Clear();
            this.simNs.Clear();
            this.batches.Clear();
            this.setPassCalls.Clear();
            this.drawCalls.Clear();
            var kind = Phases[this.phase];
            Bench.Log("render stats: " + RtsRenderScenario.Describe(kind) + ", " + UnitCount + " units ...");
            this.scenario = new RtsRenderScenario(kind, UnitCount, this.renderCamera);
        }

        private void RecordPhase() {
            var label = RtsRenderScenario.Describe(this.scenario.kind);
            var notes = this.scenario.Info() + "; " + this.phaseFrame + " real frames (" + WarmupFrames + " warmup), screen " + Screen.width + "x" + Screen.height + ", vSyncCount=" + QualitySettings.vSyncCount + ", FrameTimingManager enabled=" + FrameTimingManager.IsFeatureEnabled();
            var id = this.Id;
            this.AddTiming(id, label + " cpuMainThreadFrameTime", this.mainThreadNs, notes + "; FrameTimingManager, ms -> ns");
            this.AddTiming(id, label + " cpuRenderThreadFrameTime", this.renderThreadNs, notes + "; FrameTimingManager, ms -> ns");
            this.AddTiming(id, label + " gpuFrameTime", this.gpuNs, notes + "; FrameTimingManager, ms -> ns; on Metal GPU time may exceed frame time (TBDR)");
            this.AddTiming(id, label + " cpuFrameTime", this.cpuFrameNs, notes + "; FrameTimingManager, ms -> ns");
            this.AddTiming(id, label + " frame dt (Time.unscaledDeltaTime)", this.deltaNs, notes);
            this.AddTiming(id, label + " simulation Update+LateUpdate (Stopwatch)", this.simNs, notes + "; main-thread time of the step code incl. Complete and Draw()");
            AddCount(id + ".counters", label + " Batches Count", this.batches, this.batchesRecorder.Valid, notes);
            AddCount(id + ".counters", label + " SetPass Calls Count", this.setPassCalls, this.setPassRecorder.Valid, notes);
            AddCount(id + ".counters", label + " Draw Calls Count", this.drawCalls, this.drawCallsRecorder.Valid, notes);
        }

        private void AddTiming(string id, string variant, List<double> values, string notes) {
            var nonZero = false;
            for (int i = 0; i < values.Count; ++i) {
                if (values[i] > 0) {
                    nonZero = true;
                    break;
                }
            }
            if (values.Count == 0 || nonZero == false) {
                Bench.Info(id, Chapter, variant, -1, "SKIPPED: no non-zero samples (" + values.Count + " samples; platform/driver did not report this timing); " + notes);
                return;
            }
            Bench.FromSamples(id, Chapter, variant, values.ToArray(), -1, notes);
        }

        private static void AddCount(string id, string variant, List<double> values, bool valid, string notes) {
            if (valid == false || values.Count == 0) {
                Bench.Info(id, Chapter, variant, -1, "SKIPPED: ProfilerRecorder counter not available in this player (" + values.Count + " samples); " + notes);
                return;
            }
            var sorted = values.ToArray();
            Array.Sort(sorted);
            var n = sorted.Length;
            var median = (n & 1) == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) * 0.5;
            Bench.Info(id, Chapter, variant, median, "bytesPerOp column = median count per frame (not bytes), ProfilerRecorder(Render) LastValue, min " + sorted[0] + ", max " + sorted[n - 1] + ", " + n + " frames; " + notes);
        }

        private void DisposeScenario() {
            if (this.scenario == null) return;
            this.scenario.Dispose();
            this.scenario = null;
        }

        private void Fail(Exception e) {
            Bench.Error(this.Id, Chapter, "render stats" + (this.phase >= 0 && this.phase < Phases.Length ? " (" + RtsRenderScenario.Describe(Phases[this.phase]) + ")" : ""), e);
            this.exitCode = 1;
            this.Finish();
        }

        private void Finish() {
            if (this.finished) return;
            this.finished = true;
            try {
                this.DisposeScenario();
            } catch (Exception e) {
                Bench.Log("render stats: dispose failed: " + e);
            }
            this.batchesRecorder.Dispose();
            this.setPassRecorder.Dispose();
            this.drawCallsRecorder.Dispose();
            try {
                if (this.report == null) this.report = new BenchReport { environment = BenchEnvironment.Capture() };
                this.report.results.AddRange(Bench.Results);
                var md = BenchReportWriter.Write(this.report, this.resultsPath);
                Debug.Log("[Book] render stats written: " + this.resultsPath + " and " + md);
            } catch (Exception e) {
                this.exitCode = 1;
                Debug.LogError("[Book] render stats: writing results failed: " + e);
            }
            Application.Quit(this.exitCode);
        }

        private void OnDestroy() {
            this.batchesRecorder.Dispose();
            this.setPassRecorder.Dispose();
            this.drawCallsRecorder.Dispose();
        }
    }
}
