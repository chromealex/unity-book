using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace BookBench {

    /// <summary>Глава 4: потоки, false sharing, блокировки.</summary>
    public static class Ch04Threads {

        private const string Chapter = "4";
        private const int TotalIncrements = 10_000_000;

        public static void Run() {
            RunCounters();
            RunLocks();
        }

        // ---------- helpers ----------

        /// <summary>Запускает body(threadIndex) на threadCount потоках одновременно и возвращает время в тиках Stopwatch.</summary>
        private static long RunThreads(int threadCount, Action<int> body) {
            var threads = new Thread[threadCount];
            using (var ready = new CountdownEvent(threadCount)) {
                using (var go = new ManualResetEventSlim(false)) {
                    for (int t = 0; t < threadCount; ++t) {
                        var index = t;
                        threads[t] = new Thread(() => {
                            ready.Signal();
                            go.Wait();
                            body(index);
                        }) {
                            IsBackground = true,
                        };
                        threads[t].Start();
                    }
                    ready.Wait();
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    go.Set();
                    for (int t = 0; t < threadCount; ++t) {
                        threads[t].Join();
                    }
                    sw.Stop();
                    return sw.ElapsedTicks;
                }
            }
        }

        private static void MeasureThreaded(string id, string variant, int threadCount, int opsPerSample, Action<int> body, Action reset, int samples = 5) {
            var values = new double[samples];
            reset();
            RunThreads(threadCount, body); // прогрев
            for (int s = 0; s < samples; ++s) {
                reset();
                var ticks = RunThreads(threadCount, body);
                values[s] = Bench.TicksToNs(ticks) / opsPerSample;
            }
            Bench.FromSamples(id, Chapter, variant, values, -1, "threads=" + threadCount + ", ns per increment/operation (total wall time / total ops)");
        }

        // ---------- counters ----------

        private static int sharedCounter;
        private static int[] adjacent;
        private static PaddedCounter[] padded;

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct PaddedCounter {
            [FieldOffset(64)] public int value;
        }

        private static void RunCounters() {
            var threadCount = Math.Max(1, Environment.ProcessorCount);
            var perThread = TotalIncrements / threadCount;
            var total = perThread * threadCount;

            MeasureThreaded("ch4.counter", "Interlocked.Increment on shared int", threadCount, total, t => {
                for (int i = 0; i < perThread; ++i) {
                    Interlocked.Increment(ref sharedCounter);
                }
            }, () => sharedCounter = 0);
            Bench.SinkInt = sharedCounter;

            adjacent = new int[threadCount];
            MeasureThreaded("ch4.counter", "per-thread int[] slots, adjacent (false sharing)", threadCount, total, t => {
                var arr = adjacent;
                for (int i = 0; i < perThread; ++i) {
                    ++arr[t];
                }
            }, () => Array.Clear(adjacent, 0, adjacent.Length));
            Bench.SinkInt = adjacent[0];

            padded = new PaddedCounter[threadCount];
            MeasureThreaded("ch4.counter", "per-thread slots padded to 128 bytes", threadCount, total, t => {
                var arr = padded;
                for (int i = 0; i < perThread; ++i) {
                    ++arr[t].value;
                }
            }, () => Array.Clear(padded, 0, padded.Length));
            Bench.SinkInt = padded[0].value;

            MeasureThreaded("ch4.counter", "per-thread local variable, write once (reference)", threadCount, total, t => {
                var local = 0;
                for (int i = 0; i < perThread; ++i) {
                    ++local;
                }
                padded[t].value = local;
            }, () => Array.Clear(padded, 0, padded.Length));
        }

        // ---------- locks ----------

        private static readonly object LockObject = new object();
        private static int spinFlag;
        private static long protectedValue;
        private const int LockOpsPerThread = 50_000;

        private static void ShortCriticalSection() {
            // очень короткая критическая секция: несколько операций
            protectedValue = protectedValue * 31 + 7;
        }

        private static void RunLocks() {
            var threadCount = Math.Max(1, Environment.ProcessorCount);
            var total = threadCount * LockOpsPerThread;

            MeasureThreaded("ch4.lock", "lock (Monitor)", threadCount, total, t => {
                for (int i = 0; i < LockOpsPerThread; ++i) {
                    lock (LockObject) {
                        ShortCriticalSection();
                    }
                }
            }, () => protectedValue = 0, 7);

            MeasureThreaded("ch4.lock", "spin lock: busy Interlocked.Exchange", threadCount, total, t => {
                for (int i = 0; i < LockOpsPerThread; ++i) {
                    while (Interlocked.Exchange(ref spinFlag, 1) != 0) {
                    }
                    ShortCriticalSection();
                    Volatile.Write(ref spinFlag, 0);
                }
            }, () => protectedValue = 0, 7);

            MeasureThreaded("ch4.lock", "spin lock: Interlocked.Exchange + SpinWait", threadCount, total, t => {
                for (int i = 0; i < LockOpsPerThread; ++i) {
                    if (Interlocked.Exchange(ref spinFlag, 1) != 0) {
                        var spin = new SpinWait();
                        do {
                            spin.SpinOnce();
                        } while (Volatile.Read(ref spinFlag) != 0 || Interlocked.Exchange(ref spinFlag, 1) != 0);
                    }
                    ShortCriticalSection();
                    Volatile.Write(ref spinFlag, 0);
                }
            }, () => protectedValue = 0, 7);

            var systemSpin = new SpinLock(false);
            MeasureThreaded("ch4.lock", "System.Threading.SpinLock (reference)", threadCount, total, t => {
                for (int i = 0; i < LockOpsPerThread; ++i) {
                    var taken = false;
                    try {
                        systemSpin.Enter(ref taken);
                        ShortCriticalSection();
                    } finally {
                        if (taken) systemSpin.Exit(false);
                    }
                }
            }, () => protectedValue = 0, 7);
            Bench.SinkLong = protectedValue;
        }
    }
}
