using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace BookBench {

    /// <summary>Глава 5: Jobs и Burst. Все джобы, кроме тех, что про планирование, вызываются через Run().</summary>
    public static class Ch05Jobs {

        private const string Chapter = "5";
        private const int Million = 1 << 20;

        public static void Run() {
            RunV3V4();
            RunLoopOrder();
            RunNoAlias();
            RunMad();
            RunClamp();
            RunBitScan();
            RunNativeListVsUnsafeList();
            RunScheduleBatched();
        }

        /// <summary>Отдельно: создает GameObject'ы, поэтому только там, где есть сцена.</summary>
        public static void RunTransforms() {
            RunTransformRoots();
        }

        // ================= float3 vs float4 =================

        public struct V3 {
            public float3 value;
        }

        public struct V4 {
            public float4 value;
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct CopyV3Job : IJob {
            [ReadOnly] public NativeArray<V3> src;
            [WriteOnly] public NativeArray<V3> dst;

            public void Execute() {
                for (int i = 0; i < this.src.Length; ++i) {
                    this.dst[i] = this.src[i];
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct CopyV4Job : IJob {
            [ReadOnly] public NativeArray<V4> src;
            [WriteOnly] public NativeArray<V4> dst;

            public void Execute() {
                for (int i = 0; i < this.src.Length; ++i) {
                    this.dst[i] = this.src[i];
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct ScaleV3Job : IJob {
            [ReadOnly] public NativeArray<V3> src;
            [WriteOnly] public NativeArray<V3> dst;
            public float k;

            public void Execute() {
                for (int i = 0; i < this.src.Length; ++i) {
                    this.dst[i] = new V3 { value = this.src[i].value * this.k + 1f };
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct ScaleV4Job : IJob {
            [ReadOnly] public NativeArray<V4> src;
            [WriteOnly] public NativeArray<V4> dst;
            public float k;

            public void Execute() {
                for (int i = 0; i < this.src.Length; ++i) {
                    this.dst[i] = new V4 { value = this.src[i].value * this.k + 1f };
                }
            }
        }

        private static void RunV3V4() {
            var src3 = new NativeArray<V3>(Million, Allocator.Persistent);
            var dst3 = new NativeArray<V3>(Million, Allocator.Persistent);
            var src4 = new NativeArray<V4>(Million, Allocator.Persistent);
            var dst4 = new NativeArray<V4>(Million, Allocator.Persistent);
            try {
                var rnd = new Unity.Mathematics.Random(10);
                for (int i = 0; i < Million; ++i) {
                    var v = rnd.NextFloat3();
                    src3[i] = new V3 { value = v };
                    src4[i] = new V4 { value = new float4(v, 0f) };
                }
                Bench.Measure("ch5.v3v4.copy", Chapter, "copy NativeArray<V3{float3}> 1M", () => new CopyV3Job { src = src3, dst = dst3 }.Run(), 3, 21, Million);
                Bench.Measure("ch5.v3v4.copy", Chapter, "copy NativeArray<V4{float4}> 1M", () => new CopyV4Job { src = src4, dst = dst4 }.Run(), 3, 21, Million);
                Bench.Measure("ch5.v3v4.scale", Chapter, "v * k + 1 over V3{float3} 1M", () => new ScaleV3Job { src = src3, dst = dst3, k = 2f }.Run(), 3, 21, Million);
                Bench.Measure("ch5.v3v4.scale", Chapter, "v * k + 1 over V4{float4} 1M", () => new ScaleV4Job { src = src4, dst = dst4, k = 2f }.Run(), 3, 21, Million);
                Bench.SinkFloat = dst3[100].value.x + dst4[100].value.x;
            } finally {
                src3.Dispose();
                dst3.Dispose();
                src4.Dispose();
                dst4.Dispose();
            }
        }

        // ================= loop order =================

        private const int JobCount = 4096;
        private const int Inner = 128;

        /// <summary>Внешний цикл по задачам, внутренний по 128 элементам задачи: чтение подряд.</summary>
        [BurstCompile(CompileSynchronously = true)]
        public struct LoopJobOuterJob : IJob {
            [ReadOnly] public NativeArray<float> data;
            public NativeArray<float> arrSum;

            public void Execute() {
                for (int job = 0; job < JobCount; ++job) {
                    for (int k = 0; k < Inner; ++k) {
                        this.arrSum[job] += this.data[job * Inner + k];
                    }
                }
            }
        }

        /// <summary>Внешний цикл по 128, внутренний по 4096 задачам: шаг Inner * 4 байт, каждый доступ в новую кэш-линию.</summary>
        [BurstCompile(CompileSynchronously = true)]
        public struct LoopInnerOuterJob : IJob {
            [ReadOnly] public NativeArray<float> data;
            public NativeArray<float> arrSum;

            public void Execute() {
                for (int k = 0; k < Inner; ++k) {
                    for (int job = 0; job < JobCount; ++job) {
                        this.arrSum[job] += this.data[job * Inner + k];
                    }
                }
            }
        }

        private static void RunLoopOrder() {
            var data = new NativeArray<float>(JobCount * Inner, Allocator.Persistent);
            var arrSum = new NativeArray<float>(JobCount, Allocator.Persistent);
            try {
                var rnd = new Unity.Mathematics.Random(11);
                for (int i = 0; i < data.Length; ++i) {
                    data[i] = rnd.NextFloat();
                }
                Bench.Measure("ch5.looporder", Chapter, "for job(4096) { for k(128) } sequential", () => {
                    new LoopJobOuterJob { data = data, arrSum = arrSum }.Run();
                }, 3, 21, JobCount * Inner);
                Bench.Measure("ch5.looporder", Chapter, "for k(128) { for job(4096) } strided", () => {
                    new LoopInnerOuterJob { data = data, arrSum = arrSum }.Run();
                }, 3, 21, JobCount * Inner);
                Bench.SinkFloat = arrSum[7];
            } finally {
                data.Dispose();
                arrSum.Dispose();
            }
        }

        // ================= NoAlias =================

        // Вызываются из Burst-джоба AliasJob и компилируются вместе с ним.
        public static unsafe class AliasKernels {

            // Без NoAlias: компилятор обязан считать, что запись в dst может изменить *add и *mul,
            // и перечитывать их на каждой итерации (или вставлять проверки пересечения).
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void Process(int* dst, int* src, int* add, int* mul, int count) {
                for (int i = 0; i < count; ++i) {
                    dst[i] = src[i] * *mul + *add;
                }
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void ProcessNoAlias([NoAlias] int* dst, [NoAlias] int* src, [NoAlias] int* add, [NoAlias] int* mul, int count) {
                for (int i = 0; i < count; ++i) {
                    dst[i] = src[i] * *mul + *add;
                }
            }
        }

        private const int AliasChunk = 1024;

        [BurstCompile(CompileSynchronously = true)]
        public unsafe struct AliasJob : IJob {
            [ReadOnly] public NativeArray<int> src;
            public NativeArray<int> dst;
            public NativeArray<int> parameters;
            public bool noAlias;

            public void Execute() {
                var s = (int*)this.src.GetUnsafeReadOnlyPtr();
                var d = (int*)this.dst.GetUnsafePtr();
                var p = (int*)this.parameters.GetUnsafePtr();
                var chunks = this.src.Length / AliasChunk;
                if (this.noAlias) {
                    for (int c = 0; c < chunks; ++c) {
                        AliasKernels.ProcessNoAlias(d + c * AliasChunk, s + c * AliasChunk, p, p + 1, AliasChunk);
                    }
                } else {
                    for (int c = 0; c < chunks; ++c) {
                        AliasKernels.Process(d + c * AliasChunk, s + c * AliasChunk, p, p + 1, AliasChunk);
                    }
                }
            }
        }

        private static void RunNoAlias() {
            var src = new NativeArray<int>(Million, Allocator.Persistent);
            var dst = new NativeArray<int>(Million, Allocator.Persistent);
            var parameters = new NativeArray<int>(2, Allocator.Persistent);
            try {
                for (int i = 0; i < Million; ++i) {
                    src[i] = i * 7;
                }
                parameters[0] = 3;
                parameters[1] = 5;
                Bench.Measure("ch5.noalias", Chapter, "int* params without [NoAlias] (1024-chunk calls)", () => {
                    new AliasJob { src = src, dst = dst, parameters = parameters, noAlias = false }.Run();
                }, 3, 21, Million);
                Bench.Measure("ch5.noalias", Chapter, "int* params with [NoAlias] (1024-chunk calls)", () => {
                    new AliasJob { src = src, dst = dst, parameters = parameters, noAlias = true }.Run();
                }, 3, 21, Million);
                Bench.SinkInt = dst[1234];
            } finally {
                src.Dispose();
                dst.Dispose();
                parameters.Dispose();
            }
        }

        // ================= math.mad vs a * b + c =================

        [BurstCompile(CompileSynchronously = true)]
        public struct MadJob : IJob {
            [ReadOnly] public NativeArray<float4> a;
            [ReadOnly] public NativeArray<float4> b;
            [ReadOnly] public NativeArray<float4> c;
            [WriteOnly] public NativeArray<float4> result;

            public void Execute() {
                for (int i = 0; i < this.a.Length; ++i) {
                    this.result[i] = math.mad(this.a[i], this.b[i], this.c[i]);
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct MulAddJob : IJob {
            [ReadOnly] public NativeArray<float4> a;
            [ReadOnly] public NativeArray<float4> b;
            [ReadOnly] public NativeArray<float4> c;
            [WriteOnly] public NativeArray<float4> result;

            public void Execute() {
                for (int i = 0; i < this.a.Length; ++i) {
                    this.result[i] = this.a[i] * this.b[i] + this.c[i];
                }
            }
        }

        /// <summary>Зависимая цепочка: здесь видна латентность одной FMA против mul + add.</summary>
        [BurstCompile(CompileSynchronously = true)]
        public struct MadChainJob : IJob {
            [ReadOnly] public NativeArray<float4> a;
            [WriteOnly] public NativeArray<float4> result;
            public bool useMad;

            public void Execute() {
                var acc = new float4(1f);
                var k = new float4(0.999f);
                if (this.useMad) {
                    for (int i = 0; i < this.a.Length; ++i) {
                        acc = math.mad(acc, k, this.a[i]);
                    }
                } else {
                    for (int i = 0; i < this.a.Length; ++i) {
                        acc = acc * k + this.a[i];
                    }
                }
                this.result[0] = acc;
            }
        }

        private static void RunMad() {
            const int count = Million / 4;
            var a = new NativeArray<float4>(count, Allocator.Persistent);
            var b = new NativeArray<float4>(count, Allocator.Persistent);
            var c = new NativeArray<float4>(count, Allocator.Persistent);
            var result = new NativeArray<float4>(count, Allocator.Persistent);
            try {
                var rnd = new Unity.Mathematics.Random(12);
                for (int i = 0; i < count; ++i) {
                    a[i] = rnd.NextFloat4();
                    b[i] = rnd.NextFloat4();
                    c[i] = rnd.NextFloat4();
                }
                Bench.Measure("ch5.mad", Chapter, "math.mad(a, b, c) float4 x 256K (throughput)", () => new MadJob { a = a, b = b, c = c, result = result }.Run(), 3, 21, count);
                Bench.Measure("ch5.mad", Chapter, "a * b + c float4 x 256K (throughput)", () => new MulAddJob { a = a, b = b, c = c, result = result }.Run(), 3, 21, count);
                Bench.Measure("ch5.mad.chain", Chapter, "acc = math.mad(acc, k, a[i]) (latency chain)", () => new MadChainJob { a = a, result = result, useMad = true }.Run(), 3, 21, count);
                Bench.Measure("ch5.mad.chain", Chapter, "acc = acc * k + a[i] (latency chain)", () => new MadChainJob { a = a, result = result, useMad = false }.Run(), 3, 21, count);
                Bench.SinkFloat = result[5].x;
            } finally {
                a.Dispose();
                b.Dispose();
                c.Dispose();
                result.Dispose();
            }
        }

        // ================= clamp =================

        public enum ClampMode {
            Branch,
            MinMax,
            Select,
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct ClampJob : IJob {
            [ReadOnly] public NativeArray<float> src;
            [WriteOnly] public NativeArray<float> dst;
            public ClampMode mode;

            public void Execute() {
                const float lo = -1f;
                const float hi = 1f;
                if (this.mode == ClampMode.Branch) {
                    for (int i = 0; i < this.src.Length; ++i) {
                        var v = this.src[i];
                        if (v < lo) {
                            v = lo;
                        } else if (v > hi) {
                            v = hi;
                        }
                        this.dst[i] = v;
                    }
                } else if (this.mode == ClampMode.MinMax) {
                    for (int i = 0; i < this.src.Length; ++i) {
                        this.dst[i] = math.min(math.max(this.src[i], lo), hi);
                    }
                } else {
                    for (int i = 0; i < this.src.Length; ++i) {
                        var v = this.src[i];
                        v = math.select(v, lo, v < lo);
                        v = math.select(v, hi, v > hi);
                        this.dst[i] = v;
                    }
                }
            }
        }

        /// <summary>Та же ветка, но со счетчиком: компилятору труднее превратить if в select.</summary>
        [BurstCompile(CompileSynchronously = true)]
        public struct ClampCountJob : IJob {
            [ReadOnly] public NativeArray<float> src;
            [WriteOnly] public NativeArray<float> dst;
            public NativeArray<int> clipped;
            public bool branchless;

            public void Execute() {
                const float lo = -1f;
                const float hi = 1f;
                var count = 0;
                if (this.branchless) {
                    for (int i = 0; i < this.src.Length; ++i) {
                        var v = this.src[i];
                        var outside = (v < lo) | (v > hi);
                        count += math.select(0, 1, outside);
                        this.dst[i] = math.clamp(v, lo, hi);
                    }
                } else {
                    for (int i = 0; i < this.src.Length; ++i) {
                        var v = this.src[i];
                        if (v < lo) {
                            v = lo;
                            ++count;
                        } else if (v > hi) {
                            v = hi;
                            ++count;
                        }
                        this.dst[i] = v;
                    }
                }
                this.clipped[0] = count;
            }
        }

        private static void RunClamp() {
            var src = new NativeArray<float>(Million, Allocator.Persistent);
            var dst = new NativeArray<float>(Million, Allocator.Persistent);
            var clipped = new NativeArray<int>(1, Allocator.Persistent);
            try {
                var rnd = new Unity.Mathematics.Random(13);
                for (int i = 0; i < Million; ++i) {
                    src[i] = rnd.NextFloat(-2f, 2f); // ~50% вне диапазона, случайно: предсказатель не угадает
                }
                Bench.Measure("ch5.clamp", Chapter, "if/else clamp 1M (random, 50% clipped)", () => new ClampJob { src = src, dst = dst, mode = ClampMode.Branch }.Run(), 3, 21, Million);
                Bench.Measure("ch5.clamp", Chapter, "math.min(math.max()) 1M", () => new ClampJob { src = src, dst = dst, mode = ClampMode.MinMax }.Run(), 3, 21, Million);
                Bench.Measure("ch5.clamp", Chapter, "math.select 1M", () => new ClampJob { src = src, dst = dst, mode = ClampMode.Select }.Run(), 3, 21, Million);
                Bench.Measure("ch5.clamp.count", Chapter, "if/else clamp + count clipped", () => new ClampCountJob { src = src, dst = dst, clipped = clipped, branchless = false }.Run(), 3, 21, Million);
                Bench.Measure("ch5.clamp.count", Chapter, "math.clamp + math.select count", () => new ClampCountJob { src = src, dst = dst, clipped = clipped, branchless = true }.Run(), 3, 21, Million);
                Bench.SinkFloat = dst[17] + clipped[0];
            } finally {
                src.Dispose();
                dst.Dispose();
                clipped.Dispose();
            }
        }

        // ================= bit scanning =================

        private const int Words = 1000;
        private const int ScanRepeats = 200;

        [BurstCompile(CompileSynchronously = true)]
        public struct BitScanJob : IJob {
            [ReadOnly] public NativeArray<ulong> words;
            public NativeArray<long> result;
            public bool useTzcnt;
            public int repeats;

            public void Execute() {
                long sum = 0;
                for (int r = 0; r < this.repeats; ++r) {
                    if (this.useTzcnt) {
                        for (int w = 0; w < this.words.Length; ++w) {
                            var bits = this.words[w];
                            if (bits == 0) continue;
                            var baseIndex = w << 6;
                            if (bits == ulong.MaxValue) {
                                // все 64 бита: сумма индексов baseIndex..baseIndex+63
                                sum += 64L * baseIndex + 2016;
                                continue;
                            }
                            while (bits != 0) {
                                sum += baseIndex + math.tzcnt(bits);
                                bits &= bits - 1;
                            }
                        }
                    } else {
                        for (int w = 0; w < this.words.Length; ++w) {
                            var bits = this.words[w];
                            var baseIndex = w << 6;
                            for (int b = 0; b < 64; ++b) {
                                if ((bits & (1UL << b)) != 0) sum += baseIndex + b;
                            }
                        }
                    }
                }
                this.result[0] = sum;
            }
        }

        private static void RunBitScan() {
            var mixed = new NativeArray<ulong>(Words, Allocator.Persistent);
            var dense = new NativeArray<ulong>(Words, Allocator.Persistent);
            var sparse = new NativeArray<ulong>(Words, Allocator.Persistent);
            var result = new NativeArray<long>(1, Allocator.Persistent);
            try {
                var rnd = new Unity.Mathematics.Random(14);
                for (int w = 0; w < Words; ++w) {
                    var random = ((ulong)rnd.NextUInt() << 32) | rnd.NextUInt();
                    dense[w] = random; // ~50% бит
                    var kind = rnd.NextInt(3);
                    mixed[w] = kind == 0 ? 0UL : (kind == 1 ? ulong.MaxValue : random);
                    // ~3% бит: AND пяти случайных слов
                    var s = random;
                    for (int k = 0; k < 4; ++k) {
                        s &= ((ulong)rnd.NextUInt() << 32) | rnd.NextUInt();
                    }
                    sparse[w] = s;
                }
                var total = Words * ScanRepeats;
                Bench.Measure("ch5.bitscan.mixed", Chapter, "naive per-bit loop (1/3 empty, 1/3 full, 1/3 random words)", () => new BitScanJob { words = mixed, result = result, useTzcnt = false, repeats = ScanRepeats }.Run(), 3, 21, total);
                Bench.Measure("ch5.bitscan.mixed", Chapter, "tzcnt loop + 0/full shortcuts (1/3 empty, 1/3 full, 1/3 random words)", () => new BitScanJob { words = mixed, result = result, useTzcnt = true, repeats = ScanRepeats }.Run(), 3, 21, total);
                Bench.Measure("ch5.bitscan.dense50", Chapter, "naive per-bit loop (50% density)", () => new BitScanJob { words = dense, result = result, useTzcnt = false, repeats = ScanRepeats }.Run(), 3, 21, total);
                Bench.Measure("ch5.bitscan.dense50", Chapter, "tzcnt loop (50% density)", () => new BitScanJob { words = dense, result = result, useTzcnt = true, repeats = ScanRepeats }.Run(), 3, 21, total);
                Bench.Measure("ch5.bitscan.sparse3", Chapter, "naive per-bit loop (~3% density)", () => new BitScanJob { words = sparse, result = result, useTzcnt = false, repeats = ScanRepeats }.Run(), 3, 21, total);
                Bench.Measure("ch5.bitscan.sparse3", Chapter, "tzcnt loop (~3% density)", () => new BitScanJob { words = sparse, result = result, useTzcnt = true, repeats = ScanRepeats }.Run(), 3, 21, total);
                Bench.SinkLong = result[0];
            } finally {
                mixed.Dispose();
                dense.Dispose();
                sparse.Dispose();
                result.Dispose();
            }
        }

        // ================= NativeList vs UnsafeList =================

        [BurstCompile(CompileSynchronously = true)]
        public struct NativeListSumJob : IJob {
            [ReadOnly] public NativeList<int> list;
            public NativeArray<long> result;

            public void Execute() {
                long sum = 0;
                for (int i = 0; i < this.list.Length; ++i) {
                    sum += this.list[i];
                }
                this.result[0] = sum;
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct UnsafeListSumJob : IJob {
            public UnsafeList<int> list;
            public NativeArray<long> result;

            public void Execute() {
                long sum = 0;
                for (int i = 0; i < this.list.Length; ++i) {
                    sum += this.list[i];
                }
                this.result[0] = sum;
            }
        }

        private static void RunNativeListVsUnsafeList() {
            var native = new NativeList<int>(Million, Allocator.Persistent);
            var unsafeList = new UnsafeList<int>(Million, Allocator.Persistent);
            var result = new NativeArray<long>(1, Allocator.Persistent);
            try {
                var rnd = new Unity.Mathematics.Random(15);
                for (int i = 0; i < Million; ++i) {
                    var v = rnd.NextInt(1000);
                    native.Add(v);
                    unsafeList.Add(v);
                }
                Bench.Measure("ch5.list", Chapter, "NativeList<int> indexer sum 1M", () => new NativeListSumJob { list = native, result = result }.Run(), 3, 21, Million);
                Bench.Measure("ch5.list", Chapter, "UnsafeList<int> indexer sum 1M", () => new UnsafeListSumJob { list = unsafeList, result = result }.Run(), 3, 21, Million);
                Bench.SinkLong = result[0];
            } finally {
                native.Dispose();
                unsafeList.Dispose();
                result.Dispose();
            }
        }

        // ================= ScheduleBatchedJobs =================

        private const int SmallJobs = 64;
        private const int SmallJobSize = 4096;

        [BurstCompile(CompileSynchronously = true)]
        public struct SmallSumJob : IJob {
            [ReadOnly] public NativeArray<float> input;
            [WriteOnly] public NativeArray<float> output;

            public void Execute() {
                var sum = 0f;
                for (int i = 0; i < this.input.Length; ++i) {
                    sum += this.input[i];
                }
                this.output[0] = sum;
            }
        }

        private static void RunScheduleBatched() {
            var input = new NativeArray<float>(SmallJobSize, Allocator.Persistent);
            var outputs = new NativeArray<float>[SmallJobs];
            var handles = new NativeArray<JobHandle>(SmallJobs, Allocator.Persistent);
            try {
                for (int i = 0; i < SmallJobSize; ++i) {
                    input[i] = i * 0.001f;
                }
                for (int j = 0; j < SmallJobs; ++j) {
                    outputs[j] = new NativeArray<float>(1, Allocator.Persistent);
                }
                Bench.Measure("ch5.schedulebatched", Chapter, "64 small jobs: ScheduleBatchedJobs after each Schedule", () => {
                    for (int j = 0; j < SmallJobs; ++j) {
                        handles[j] = new SmallSumJob { input = input, output = outputs[j] }.Schedule();
                        JobHandle.ScheduleBatchedJobs();
                    }
                    JobHandle.CompleteAll(handles);
                }, 5, 41, 1);
                Bench.Measure("ch5.schedulebatched", Chapter, "64 small jobs: one ScheduleBatchedJobs after all", () => {
                    for (int j = 0; j < SmallJobs; ++j) {
                        handles[j] = new SmallSumJob { input = input, output = outputs[j] }.Schedule();
                    }
                    JobHandle.ScheduleBatchedJobs();
                    JobHandle.CompleteAll(handles);
                }, 5, 41, 1);
                Bench.Measure("ch5.schedulebatched", Chapter, "64 small jobs: no ScheduleBatchedJobs, CompleteAll only", () => {
                    for (int j = 0; j < SmallJobs; ++j) {
                        handles[j] = new SmallSumJob { input = input, output = outputs[j] }.Schedule();
                    }
                    JobHandle.CompleteAll(handles);
                }, 5, 41, 1);
                Bench.SinkFloat = outputs[3][0];
            } finally {
                input.Dispose();
                handles.Dispose();
                for (int j = 0; j < SmallJobs; ++j) {
                    if (outputs[j].IsCreated) outputs[j].Dispose();
                }
            }
        }

        // ================= IJobParallelForTransform: roots =================

        private const int TransformCount = 10_000;
        private const int PerRoot = 256;

        [BurstCompile(CompileSynchronously = true)]
        public struct MoveTransformsJob : IJobParallelForTransform {
            [ReadOnly] public NativeArray<float3> positions;
            public float time;

            public void Execute(int index, TransformAccess transform) {
                var p = this.positions[index];
                transform.position = new float3(p.x + math.sin(this.time + index), p.y, p.z);
                transform.rotation = quaternion.RotateY(this.time + index * 0.01f);
            }
        }

        private enum RootLayout {
            SceneRoot,
            OneParent,
            Roots256,
        }

        private static void RunTransformRoots() {
            MeasureTransformLayout(RootLayout.SceneRoot, "10 000 transforms at scene root");
            MeasureTransformLayout(RootLayout.Roots256, "10 000 transforms under 40 roots x 256");
            MeasureTransformLayout(RootLayout.OneParent, "10 000 transforms under 1 parent");
        }

        private static void MeasureTransformLayout(RootLayout layout, string variant) {
            var created = new System.Collections.Generic.List<GameObject>(TransformCount + 64);
            var positions = new NativeArray<float3>(TransformCount, Allocator.Persistent);
            var access = new TransformAccessArray(TransformCount);
            try {
                Transform parent = null;
                for (int i = 0; i < TransformCount; ++i) {
                    if (layout == RootLayout.Roots256 && i % PerRoot == 0) {
                        var root = new GameObject("BenchRoot " + (i / PerRoot));
                        root.hideFlags = HideFlags.DontSave;
                        created.Add(root);
                        parent = root.transform;
                    } else if (layout == RootLayout.OneParent && i == 0) {
                        var root = new GameObject("BenchRoot");
                        root.hideFlags = HideFlags.DontSave;
                        created.Add(root);
                        parent = root.transform;
                    }
                    var go = new GameObject("t");
                    go.hideFlags = HideFlags.DontSave;
                    if (parent != null) go.transform.SetParent(parent, false);
                    created.Add(go);
                    access.Add(go.transform);
                    positions[i] = new float3(i % 100, 0f, i / 100);
                }
                var time = 0f;
                var r = Bench.Measure("ch5.transformroots", Chapter, variant, () => {
                    time += 0.016f;
                    new MoveTransformsJob { positions = positions, time = time }.Schedule(access).Complete();
                }, 5, 31, TransformCount);
                r.notes = "Schedule(TransformAccessArray).Complete(), ns per transform";
            } finally {
                access.Dispose();
                positions.Dispose();
                for (int i = created.Count - 1; i >= 0; --i) {
                    if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
                }
            }
        }
    }
}
