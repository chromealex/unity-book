using System;
using BookBench.Rts;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BookBench.Editor {

    /// <summary>
    /// Статистика отрисовки «до/после» в редакторе: кадр симуляции, затем Camera.Render() в RenderTexture 1920x1080
    /// и чтение UnityEditor.UnityStats. Всё синхронно, внутри одного кадра редактора, поэтому:
    /// - значения UnityStats пишутся вместе с первым и последним сэмплом: если они растут от рендера к рендеру,
    ///   счетчик копится за кадр редактора и цифрам верить нельзя;
    /// - UnityStats.batches в Unity 6 не публичный, вместо него — SKIPPED-строка и компоненты dynamic/static/instancedBatches.
    /// Честные цифры кадра — в плеере (Book/Build and run Player (IL2CPP, render stats)).
    /// </summary>
    public static class EditorRenderStats {

        private const string Id = "rts.render.n10000.editor";
        private const string Chapter = "rts";
        private const int UnitCount = 10_000;
        private const int Warmup = 10;
        private const int Samples = 10;

        private static readonly RtsRenderKind[] Kinds = {
            RtsRenderKind.GameObjectsNoInstancing,
            RtsRenderKind.GameObjectsInstancing,
            RtsRenderKind.Step4Instanced,
        };

        public static void Run() {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) {
                Bench.Info(Id, Chapter, "render stats", -1, "SKIPPED: no graphics device");
                return;
            }
            Bench.Log("rts render stats in Editor (Camera.Render + UnityStats), " + UnitCount + " units ...");
            var target = new RenderTexture(1920, 1080, 24);
            Camera camera = null;
            Light light = null;
            try {
                target.Create();
                camera = RtsRenderScenario.CreateCamera(target);
                light = RtsRenderScenario.CreateLight();
                for (int k = 0; k < Kinds.Length; ++k) {
                    RunKind(Kinds[k], camera);
                }
            } catch (Exception e) {
                Bench.Error(Id, Chapter, "render stats", e);
            } finally {
                if (camera != null) UnityEngine.Object.DestroyImmediate(camera.gameObject);
                if (light != null) UnityEngine.Object.DestroyImmediate(light.gameObject);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void RunKind(RtsRenderKind kind, Camera camera) {
            var label = RtsRenderScenario.Describe(kind);
            RtsRenderScenario scenario = null;
            var renderNs = new double[Samples];
            var drawCalls = new double[Samples];
            var setPassCalls = new double[Samples];
            var dynamicBatches = new double[Samples];
            var staticBatches = new double[Samples];
            var instancedBatches = new double[Samples];
            var instancedBatchedDrawCalls = new double[Samples];
            var triangles = new double[Samples];
            try {
                scenario = new RtsRenderScenario(kind, UnitCount, camera);
                for (int f = 0; f < Warmup; ++f) {
                    scenario.Update();
                    scenario.LateUpdate();
                    camera.Render();
                }
                for (int s = 0; s < Samples; ++s) {
                    scenario.Update();
                    scenario.LateUpdate();
                    var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    camera.Render();
                    renderNs[s] = Bench.TicksToNs(System.Diagnostics.Stopwatch.GetTimestamp() - t0);
                    drawCalls[s] = UnityStats.drawCalls;
                    setPassCalls[s] = UnityStats.setPassCalls;
                    dynamicBatches[s] = UnityStats.dynamicBatches;
                    staticBatches[s] = UnityStats.staticBatches;
                    instancedBatches[s] = UnityStats.instancedBatches;
                    instancedBatchedDrawCalls[s] = UnityStats.instancedBatchedDrawCalls;
                    triangles[s] = UnityStats.triangles;
                }
                var notes = scenario.Info() + "; Editor, synchronous Camera.Render() into 1920x1080 RenderTexture, " + Warmup + " warmup + " + Samples + " samples";
                Bench.FromSamples(Id, Chapter, label + " Camera.Render() CPU (main thread)", renderNs, -1, notes + "; ns per Camera.Render() call (culling + submission; GPU not awaited)");
                Count(label, "drawCalls", drawCalls, notes);
                Count(label, "setPassCalls", setPassCalls, notes);
                Count(label, "dynamicBatches", dynamicBatches, notes);
                Count(label, "staticBatches", staticBatches, notes);
                Count(label, "instancedBatches", instancedBatches, notes);
                Count(label, "instancedBatchedDrawCalls", instancedBatchedDrawCalls, notes);
                Count(label, "triangles", triangles, notes);
                Bench.Info(Id, Chapter, label + " UnityStats.batches", -1, "SKIPPED: UnityStats.batches is not public in current Unity 6 reference source (internal batchStats.batches), not used so the Editor assembly compiles; see dynamic/static/instancedBatches rows here and 'Batches Count' in player_il2cpp_render (ProfilerRecorder)");
            } catch (Exception e) {
                Bench.Error(Id, Chapter, label, e);
            } finally {
                if (scenario != null) {
                    try {
                        scenario.Dispose();
                    } catch (Exception e) {
                        Bench.Log("render stats: dispose failed: " + e);
                    }
                }
            }
        }

        private static void Count(string label, string name, double[] values, string notes) {
            var variant = label + " UnityStats." + name;
            var sorted = (double[])values.Clone();
            Array.Sort(sorted);
            var n = sorted.Length;
            if (n == 0 || sorted[n - 1] <= 0) {
                if (name == "staticBatches" || name == "dynamicBatches" || name == "instancedBatches" || name == "instancedBatchedDrawCalls") {
                    // ноль здесь — нормальный результат (батчинг этого вида не сработал)
                    Bench.Info(Id, Chapter, variant, 0, "bytesPerOp column = count (not bytes): 0 in all " + n + " samples; " + notes);
                } else {
                    Bench.Info(Id, Chapter, variant, -1, "SKIPPED: stayed 0 after Camera.Render() in edit mode (UnityStats may be filled only for Game view rendering); " + notes);
                }
                return;
            }
            var median = (n & 1) == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) * 0.5;
            Bench.Info(Id, Chapter, variant, median, "bytesPerOp column = median count (not bytes) after each Camera.Render(); first sample " + values[0] + ", last " + values[n - 1] + " (if it grows from render to render, the counter accumulates over the Editor frame and is not per render); " + notes);
        }
    }
}
