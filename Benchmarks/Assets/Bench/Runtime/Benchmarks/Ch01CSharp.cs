using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace BookBench {

    /// <summary>Глава 1: C#.</summary>
    public static class Ch01CSharp {

        private const string Chapter = "1";
        private const int Calls = 1_000_000;

        public static void Run() {
            RunParams();
            RunNewVsDefault();
            RunClosures();
            RunInterfaceVsGeneric();
            RunForeach();
            RunShortCircuit();
        }

        // ---------- params vs overload ----------

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int SumParams(params int[] values) {
            var result = 0;
            for (int i = 0; i < values.Length; ++i) {
                result += values[i];
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int SumThree(int a, int b, int c) {
            return a + b + c;
        }

        private static void ParamsLoop() {
            var acc = 0;
            for (int i = 0; i < Calls; ++i) {
                acc += SumParams(i, 2, 3);
            }
            Bench.SinkInt = acc;
        }

        private static void OverloadLoop() {
            var acc = 0;
            for (int i = 0; i < Calls; ++i) {
                acc += SumThree(i, 2, 3);
            }
            Bench.SinkInt = acc;
        }

        private static void RunParams() {
            Bench.Measure("ch1.params", Chapter, "params int[] (3 args)", ParamsLoop, 3, 15, Calls);
            Bench.Measure("ch1.params", Chapter, "overload (int,int,int)", OverloadLoop, 3, 15, Calls);
            Bench.MeasureAlloc("ch1.params.alloc", Chapter, "params int[] (3 args)", () => Bench.SinkInt = SumParams(1, 2, 3), 10_000);
            Bench.MeasureAlloc("ch1.params.alloc", Chapter, "overload (int,int,int)", () => Bench.SinkInt = SumThree(1, 2, 3), 10_000);
        }

        // ---------- new T() vs default(T) ----------

        public struct Payload {
            public int a;
            public int b;
            public float c;
            public long d;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static T MakeNew<T>() where T : struct {
            return new T();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static T MakeDefault<T>() where T : struct {
            return default(T);
        }

        private static void NewLoop() {
            long acc = 0;
            for (int i = 0; i < Calls; ++i) {
                acc += MakeNew<Payload>().d + i;
            }
            Bench.SinkLong = acc;
        }

        private static void DefaultLoop() {
            long acc = 0;
            for (int i = 0; i < Calls; ++i) {
                acc += MakeDefault<Payload>().d + i;
            }
            Bench.SinkLong = acc;
        }

        private static void RunNewVsDefault() {
            Bench.Measure("ch1.newT", Chapter, "new T() (generic struct)", NewLoop, 3, 15, Calls);
            Bench.Measure("ch1.newT", Chapter, "default(T) (generic struct)", DefaultLoop, 3, 15, Calls);
            Bench.MeasureAlloc("ch1.newT.alloc", Chapter, "new T() (generic struct)", () => Bench.SinkLong = MakeNew<Payload>().d, 10_000);
            Bench.MeasureAlloc("ch1.newT.alloc", Chapter, "default(T) (generic struct)", () => Bench.SinkLong = MakeDefault<Payload>().d, 10_000);
        }

        // ---------- closure vs static lambda with context ----------

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Invoke(Func<int, int> f, int x) {
            return f(x);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int InvokeWithContext(Func<int, int, int> f, int context, int x) {
            return f(context, x);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ClosureCall(int offset, int x) {
            // замыкание: новый объект-дисплейкласс и новый делегат на каждый вызов метода
            return Invoke(v => v + offset, x);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int StaticLambdaCall(int offset, int x) {
            // без захвата: делегат кэшируется компилятором
            return InvokeWithContext(static (ctx, v) => v + ctx, offset, x);
        }

        private static void ClosureLoop() {
            var acc = 0;
            for (int i = 0; i < Calls; ++i) {
                acc += ClosureCall(i, 1);
            }
            Bench.SinkInt = acc;
        }

        private static void StaticLambdaLoop() {
            var acc = 0;
            for (int i = 0; i < Calls; ++i) {
                acc += StaticLambdaCall(i, 1);
            }
            Bench.SinkInt = acc;
        }

        private static void RunClosures() {
            Bench.Measure("ch1.closure", Chapter, "closure (captures local)", ClosureLoop, 3, 15, Calls);
            Bench.Measure("ch1.closure", Chapter, "static lambda + context arg", StaticLambdaLoop, 3, 15, Calls);
            Bench.MeasureAlloc("ch1.closure.alloc", Chapter, "closure (captures local)", () => Bench.SinkInt = ClosureCall(5, 1), 10_000);
            Bench.MeasureAlloc("ch1.closure.alloc", Chapter, "static lambda + context arg", () => Bench.SinkInt = StaticLambdaCall(5, 1), 10_000);
        }

        // ---------- struct as interface vs generic constraint ----------

        public interface IValue {
            int GetValue();
        }

        public struct ValueStruct : IValue {
            public int value;

            public int GetValue() {
                return this.value;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ReadInterface(IValue v) {
            return v.GetValue();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ReadGeneric<T>(T v) where T : struct, IValue {
            return v.GetValue();
        }

        private static void InterfaceLoop() {
            var acc = 0;
            var s = new ValueStruct { value = 3 };
            for (int i = 0; i < Calls; ++i) {
                s.value = i;
                acc += ReadInterface(s); // боксинг
            }
            Bench.SinkInt = acc;
        }

        private static void GenericLoop() {
            var acc = 0;
            var s = new ValueStruct { value = 3 };
            for (int i = 0; i < Calls; ++i) {
                s.value = i;
                acc += ReadGeneric(s);
            }
            Bench.SinkInt = acc;
        }

        private static void RunInterfaceVsGeneric() {
            Bench.Measure("ch1.boxing", Chapter, "struct passed as interface", InterfaceLoop, 3, 15, Calls);
            Bench.Measure("ch1.boxing", Chapter, "generic where T : struct, IValue", GenericLoop, 3, 15, Calls);
            var s = new ValueStruct { value = 3 };
            Bench.MeasureAlloc("ch1.boxing.alloc", Chapter, "struct passed as interface", () => Bench.SinkInt = ReadInterface(s), 10_000);
            Bench.MeasureAlloc("ch1.boxing.alloc", Chapter, "generic where T : struct, IValue", () => Bench.SinkInt = ReadGeneric(s), 10_000);
        }

        // ---------- foreach List<int> vs IEnumerable<int> ----------

        private static readonly List<int> ForeachList = CreateList(1000);

        private static List<int> CreateList(int count) {
            var list = new List<int>(count);
            var rnd = new Random(1);
            for (int i = 0; i < count; ++i) {
                list.Add(rnd.Next(1000));
            }
            return list;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int SumList(List<int> list) {
            var result = 0;
            foreach (var v in list) {
                result += v;
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int SumEnumerable(IEnumerable<int> list) {
            var result = 0;
            foreach (var v in list) {
                result += v;
            }
            return result;
        }

        private const int ForeachRepeats = 2_000;

        private static void ForeachListLoop() {
            var acc = 0;
            for (int i = 0; i < ForeachRepeats; ++i) {
                acc += SumList(ForeachList);
            }
            Bench.SinkInt = acc;
        }

        private static void ForeachEnumerableLoop() {
            var acc = 0;
            for (int i = 0; i < ForeachRepeats; ++i) {
                acc += SumEnumerable(ForeachList);
            }
            Bench.SinkInt = acc;
        }

        private static void RunForeach() {
            Bench.Measure("ch1.foreach", Chapter, "foreach List<int> (1000 items)", ForeachListLoop, 3, 15, ForeachRepeats);
            Bench.Measure("ch1.foreach", Chapter, "foreach IEnumerable<int> (1000 items)", ForeachEnumerableLoop, 3, 15, ForeachRepeats);
            Bench.MeasureAlloc("ch1.foreach.alloc", Chapter, "foreach List<int>", () => Bench.SinkInt = SumList(ForeachList), 1_000);
            Bench.MeasureAlloc("ch1.foreach.alloc", Chapter, "foreach IEnumerable<int>", () => Bench.SinkInt = SumEnumerable(ForeachList), 1_000);
        }

        // ---------- CalcV1() || CalcV2() vs both computed first ----------

        private static int shortCircuitCounter;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CalcV1(int i) {
            // в ~90% случаев true: тогда дорогой CalcV2 не нужен
            return (i % 10) != 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CalcV2(int i) {
            // «дорогая» проверка: ~200 итераций
            var x = (uint)i * 2654435761u;
            for (int k = 0; k < 200; ++k) {
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
            }
            return (x & 1) == 0;
        }

        private const int ShortCircuitCalls = 200_000;

        private static void ShortCircuitLoop() {
            var count = 0;
            for (int i = 0; i < ShortCircuitCalls; ++i) {
                if (CalcV1(i) || CalcV2(i)) ++count;
            }
            shortCircuitCounter = count;
            Bench.SinkInt = count;
        }

        private static void BothFirstLoop() {
            var count = 0;
            for (int i = 0; i < ShortCircuitCalls; ++i) {
                var v1 = CalcV1(i);
                var v2 = CalcV2(i);
                if (v1 || v2) ++count;
            }
            Bench.SinkInt = count + shortCircuitCounter;
        }

        private static void RunShortCircuit() {
            Bench.Measure("ch1.shortcircuit", Chapter, "CalcV1() || CalcV2() (V1 true 90%)", ShortCircuitLoop, 3, 15, ShortCircuitCalls);
            Bench.Measure("ch1.shortcircuit", Chapter, "v1 = CalcV1(); v2 = CalcV2(); v1 || v2", BothFirstLoop, 3, 15, ShortCircuitCalls);
        }
    }
}
