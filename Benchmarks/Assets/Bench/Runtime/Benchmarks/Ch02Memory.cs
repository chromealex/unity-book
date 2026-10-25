using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace BookBench {

    /// <summary>Глава 2: память.</summary>
    public static class Ch02Memory {

        private const string Chapter = "2";

        public static void Run() {
            RunToString();
            RunReturnList();
            RunEmptyArray();
            RunNodeLayout();
        }

        // ---------- health.ToString() vs cached table ----------

        private const int MaxHp = 100;
        private static readonly string[] HpStrings = BuildHpStrings();

        private static string[] BuildHpStrings() {
            var result = new string[MaxHp + 1];
            for (int i = 0; i < result.Length; ++i) {
                result[i] = i.ToString();
            }
            return result;
        }

        private const int StringCalls = 100_000;

        private static void ToStringLoop() {
            var len = 0;
            for (int i = 0; i < StringCalls; ++i) {
                var health = i % (MaxHp + 1);
                len += health.ToString().Length;
            }
            Bench.SinkInt = len;
        }

        private static void CachedLoop() {
            var len = 0;
            for (int i = 0; i < StringCalls; ++i) {
                var health = i % (MaxHp + 1);
                len += HpStrings[health].Length;
            }
            Bench.SinkInt = len;
        }

        private static int toStringCounter;

        private static void RunToString() {
            Bench.Measure("ch2.tostring", Chapter, "health.ToString()", ToStringLoop, 3, 15, StringCalls);
            Bench.Measure("ch2.tostring", Chapter, "cached string table", CachedLoop, 3, 15, StringCalls);
            Bench.MeasureAlloc("ch2.tostring.alloc", Chapter, "health.ToString()", () => {
                toStringCounter = (toStringCounter + 1) % (MaxHp + 1);
                Bench.SinkObject = (toStringCounter + 10).ToString();
            }, 10_000);
            Bench.MeasureAlloc("ch2.tostring.alloc", Chapter, "cached string table", () => {
                toStringCounter = (toStringCounter + 1) % (MaxHp + 1);
                Bench.SinkObject = HpStrings[toStringCounter];
            }, 10_000);
            Bench.SinkObject = null;
        }

        // ---------- return new List vs fill passed list ----------

        private static readonly int[] Source = CreateSource(256);

        private static int[] CreateSource(int count) {
            var rnd = new Random(2);
            var result = new int[count];
            for (int i = 0; i < count; ++i) {
                result[i] = rnd.Next(1000);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static List<int> GetEvenNew() {
            var result = new List<int>();
            for (int i = 0; i < Source.Length; ++i) {
                if ((Source[i] & 1) == 0) result.Add(Source[i]);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void GetEvenFill(List<int> result) {
            result.Clear();
            for (int i = 0; i < Source.Length; ++i) {
                if ((Source[i] & 1) == 0) result.Add(Source[i]);
            }
        }

        private static readonly List<int> ReusedList = new List<int>(256);

        private static void RunReturnList() {
            Bench.MeasureAlloc("ch2.returnlist.alloc", Chapter, "return new List<int> (256 src)", () => Bench.SinkInt = GetEvenNew().Count, 1_000);
            Bench.MeasureAlloc("ch2.returnlist.alloc", Chapter, "fill passed List<int>", () => {
                GetEvenFill(ReusedList);
                Bench.SinkInt = ReusedList.Count;
            }, 1_000);
            Bench.Measure("ch2.returnlist", Chapter, "return new List<int> (256 src)", () => {
                var acc = 0;
                for (int i = 0; i < 1_000; ++i) {
                    acc += GetEvenNew().Count;
                }
                Bench.SinkInt = acc;
            }, 3, 15, 1_000);
            Bench.Measure("ch2.returnlist", Chapter, "fill passed List<int>", () => {
                var acc = 0;
                for (int i = 0; i < 1_000; ++i) {
                    GetEvenFill(ReusedList);
                    acc += ReusedList.Count;
                }
                Bench.SinkInt = acc;
            }, 3, 15, 1_000);
        }

        // ---------- new T[0] vs Array.Empty<T>() ----------

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int[] EmptyNew() {
            return new int[0];
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int[] EmptyCached() {
            return Array.Empty<int>();
        }

        private static void RunEmptyArray() {
            Bench.MeasureAlloc("ch2.emptyarray.alloc", Chapter, "new int[0]", () => Bench.SinkObject = EmptyNew(), 10_000);
            Bench.MeasureAlloc("ch2.emptyarray.alloc", Chapter, "Array.Empty<int>()", () => Bench.SinkObject = EmptyCached(), 10_000);
            Bench.SinkObject = null;
        }

        // ---------- node layout: interleaved vs two passes ----------

        public sealed class Node {
            public int value;
            public List<int> children;
        }

        private const int NodeCount = 100_000;
        private static Node[] interleaved;
        private static Node[] twoPass;

        private static void BuildNodes() {
            var rnd = new Random(3);
            interleaved = new Node[NodeCount];
            for (int i = 0; i < NodeCount; ++i) {
                // узел и его список выделяются вперемешку: узлы разбросаны по куче
                var node = new Node { value = rnd.Next(100) };
                node.children = new List<int>(8);
                for (int k = 0; k < 4; ++k) {
                    node.children.Add(k);
                }
                interleaved[i] = node;
            }

            rnd = new Random(3);
            twoPass = new Node[NodeCount];
            // первый проход: только узлы — лежат в куче подряд
            for (int i = 0; i < NodeCount; ++i) {
                twoPass[i] = new Node { value = rnd.Next(100) };
            }
            // второй проход: списки
            for (int i = 0; i < NodeCount; ++i) {
                var list = new List<int>(8);
                for (int k = 0; k < 4; ++k) {
                    list.Add(k);
                }
                twoPass[i].children = list;
            }
        }

        private static long SumNodes(Node[] nodes) {
            long result = 0;
            for (int i = 0; i < nodes.Length; ++i) {
                result += nodes[i].value;
            }
            return result;
        }

        private static void RunNodeLayout() {
            BuildNodes();
            try {
                Bench.Measure("ch2.nodelayout", Chapter, "iterate nodes: allocated interleaved with child lists", () => Bench.SinkLong = SumNodes(interleaved), 5, 31, NodeCount);
                Bench.Measure("ch2.nodelayout", Chapter, "iterate nodes: allocated in two passes", () => Bench.SinkLong = SumNodes(twoPass), 5, 31, NodeCount);
            } finally {
                interleaved = null;
                twoPass = null;
            }
        }
    }
}
