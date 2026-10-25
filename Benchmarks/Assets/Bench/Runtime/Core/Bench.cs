using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine.Profiling;
using UnityEngine.Scripting;

namespace BookBench {

    /// <summary>
    /// Минимальный харнесс. Все результаты копятся в Bench.Results, BenchSuite забирает их в отчет.
    /// Соглашение для Measure: action вызывается один раз на сэмпл и сам выполняет innerIterations операций;
    /// время сэмпла делится на innerIterations, так что в отчете — нс на одну операцию.
    /// </summary>
    public static class Bench {

        public static readonly List<BenchResult> Results = new List<BenchResult>();
        public static Action<string> Log = s => { };

        // Синки: сюда пишутся результаты, чтобы JIT/IL2CPP/Burst не выкинули вычисления
        public static long SinkLong;
        public static int SinkInt;
        public static float SinkFloat;
        public static double SinkDouble;
        public static object SinkObject;

        private static readonly double NsPerTick = 1_000_000_000.0 / Stopwatch.Frequency;

        public static double TicksToNs(long ticks) {
            return ticks * NsPerTick;
        }

        public static BenchResult Measure(string id, string chapter, string variant, Action action, int warmup = 5, int samples = 25, int innerIterations = 1) {
            var result = new BenchResult {
                id = id,
                chapter = chapter,
                variant = variant,
            };
            try {
                for (int i = 0; i < warmup; ++i) {
                    action();
                }
                var values = new double[samples];
                var sw = new Stopwatch();
                for (int s = 0; s < samples; ++s) {
                    sw.Restart();
                    action();
                    sw.Stop();
                    values[s] = TicksToNs(sw.ElapsedTicks) / innerIterations;
                }
                FillStats(result, values);
            } catch (Exception e) {
                result.error = e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace;
            }
            Add(result);
            return result;
        }

        /// <summary>Записывает готовую выборку (нс на операцию), например время кадра RTS.</summary>
        public static BenchResult FromSamples(string id, string chapter, string variant, double[] valuesNs, double bytesPerOp = -1, string notes = "") {
            var result = new BenchResult {
                id = id,
                chapter = chapter,
                variant = variant,
                bytesPerOp = bytesPerOp,
                notes = notes ?? "",
            };
            FillStats(result, valuesNs);
            Add(result);
            return result;
        }

        public static BenchResult Info(string id, string chapter, string variant, double bytesPerOp, string notes = "") {
            var result = new BenchResult {
                id = id,
                chapter = chapter,
                variant = variant,
                bytesPerOp = bytesPerOp,
                notes = notes ?? "",
            };
            Add(result);
            return result;
        }

        public static BenchResult Error(string id, string chapter, string variant, Exception e) {
            var result = new BenchResult {
                id = id,
                chapter = chapter,
                variant = variant,
                error = e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace,
            };
            Add(result);
            return result;
        }

        public static BenchResult MeasureAlloc(string id, string chapter, string variant, Action action, int iterations) {
            var result = new BenchResult {
                id = id,
                chapter = chapter,
                variant = variant,
                n = iterations,
            };
            try {
                // прогрев: статические конструкторы, JIT, кэши делегатов
                action();
                action();
                result.bytesPerOp = MeasureBytes(action, iterations, out var method);
                result.notes = "alloc via " + method;
            } catch (Exception e) {
                result.error = e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace;
            }
            Add(result);
            return result;
        }

        /// <summary>
        /// Байт на вызов. Порядок источников:
        /// 1) GC.GetAllocatedBytesForCurrentThread, если рантайм его реально поддерживает (точный счетчик);
        /// 2) Profiler.GetMonoUsedSizeLong при выключенном GC (GarbageCollector.GCMode = Disabled);
        /// 3) GC.GetTotalMemory(false) при выключенном GC.
        /// </summary>
        public static double MeasureBytes(Action action, int iterations, out string method, bool allThreads = false) {
            if (allThreads == false && AllocatedBytesCounterWorks()) {
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < iterations; ++i) {
                    action();
                }
                var after = GC.GetAllocatedBytesForCurrentThread();
                method = "GC.GetAllocatedBytesForCurrentThread";
                return Math.Max(0, after - before) / (double)iterations;
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var gcDisabled = false;
            GarbageCollector.Mode previous = GarbageCollector.Mode.Enabled;
            try {
                previous = GarbageCollector.GCMode;
                GarbageCollector.GCMode = GarbageCollector.Mode.Disabled;
                gcDisabled = GarbageCollector.GCMode == GarbageCollector.Mode.Disabled;
            } catch (Exception) {
                gcDisabled = false;
            }

            try {
                var profBefore = Profiler.GetMonoUsedSizeLong();
                var memBefore = GC.GetTotalMemory(false);
                for (int i = 0; i < iterations; ++i) {
                    action();
                }
                var profAfter = Profiler.GetMonoUsedSizeLong();
                var memAfter = GC.GetTotalMemory(false);

                var suffix = gcDisabled ? " (GC disabled)" : " (GC enabled, may undercount)";
                if (profBefore > 0 || profAfter > 0) {
                    method = "Profiler.GetMonoUsedSizeLong" + suffix;
                    return Math.Max(0, profAfter - profBefore) / (double)iterations;
                }
                method = "GC.GetTotalMemory" + suffix;
                return Math.Max(0, memAfter - memBefore) / (double)iterations;
            } finally {
                if (gcDisabled) {
                    try {
                        GarbageCollector.GCMode = previous;
                    } catch (Exception) {
                        // ничего не делаем: режим не менялся
                    }
                }
                GC.Collect();
            }
        }

        private static int allocCounterState; // 0 = не проверяли, 1 = работает, -1 = нет

        public static bool AllocatedBytesCounterWorks() {
            if (allocCounterState != 0) return allocCounterState > 0;
            try {
                var before = GC.GetAllocatedBytesForCurrentThread();
                SinkObject = new byte[4000];
                var after = GC.GetAllocatedBytesForCurrentThread();
                allocCounterState = after - before >= 4000 ? 1 : -1;
            } catch (Exception) {
                allocCounterState = -1;
            }
            SinkObject = null;
            return allocCounterState > 0;
        }

        public static void FillStats(BenchResult result, double[] values) {
            var n = values.Length;
            result.n = n;
            if (n == 0) return;
            var sorted = (double[])values.Clone();
            Array.Sort(sorted);
            result.minNs = sorted[0];
            result.medianNs = (n & 1) == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) * 0.5;
            var p90Index = (int)Math.Ceiling(0.9 * n) - 1;
            if (p90Index < 0) p90Index = 0;
            if (p90Index >= n) p90Index = n - 1;
            result.p90Ns = sorted[p90Index];
            var mean = 0.0;
            for (int i = 0; i < n; ++i) {
                mean += values[i];
            }
            mean /= n;
            var variance = 0.0;
            for (int i = 0; i < n; ++i) {
                var d = values[i] - mean;
                variance += d * d;
            }
            result.stddevNs = Math.Sqrt(variance / n);
        }

        private static void Add(BenchResult result) {
            Results.Add(result);
            if (string.IsNullOrEmpty(result.error) == false) {
                Log("[FAIL] " + result.id + " / " + result.variant + ": " + result.error);
                return;
            }
            var bytes = result.bytesPerOp >= 0 ? ", " + result.bytesPerOp.ToString("0.##") + " B/op" : "";
            Log("[ok] " + result.id + " / " + result.variant + ": median " + BenchReportWriter.FormatNs(result.medianNs) + bytes + (string.IsNullOrEmpty(result.notes) ? "" : " (" + result.notes + ")"));
        }
    }
}
