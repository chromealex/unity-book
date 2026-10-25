using System;
using System.Collections.Generic;

namespace BookBench {

    /// <summary>Глава 10: алгоритмы.</summary>
    public static class Ch10Algorithms {

        private const string Chapter = "10";

        public static void Run() {
            RunContains();
            RunSort();
            RunDfs();
        }

        // ---------- List.Contains vs HashSet.Contains ----------

        private static void RunContains() {
            const int size = 1000;
            const int queries = 100_000;
            var rnd = new Random(30);
            var list = new List<int>(size);
            var set = new HashSet<int>();
            while (list.Count < size) {
                var v = rnd.Next(0, 1_000_000);
                if (set.Add(v)) list.Add(v);
            }
            // половина запросов попадает, половина нет
            var probes = new int[queries];
            for (int i = 0; i < queries; ++i) {
                probes[i] = (i & 1) == 0 ? list[rnd.Next(size)] : rnd.Next(1_000_000, 2_000_000);
            }
            Bench.Measure("ch10.contains", Chapter, "List<int>.Contains (1000 items, 50% hit)", () => {
                var hits = 0;
                for (int i = 0; i < queries; ++i) {
                    if (list.Contains(probes[i])) ++hits;
                }
                Bench.SinkInt = hits;
            }, 2, 11, queries);
            Bench.Measure("ch10.contains", Chapter, "HashSet<int>.Contains (1000 items, 50% hit)", () => {
                var hits = 0;
                for (int i = 0; i < queries; ++i) {
                    if (set.Contains(probes[i])) ++hits;
                }
                Bench.SinkInt = hits;
            }, 2, 11, queries);
        }

        // ---------- Array.Sort vs LSD radix ----------

        private const int SortCount = 1 << 20;

        public static void RadixSort(uint[] data, uint[] temp) {
            var counts = new int[256];
            var src = data;
            var dst = temp;
            for (int shift = 0; shift < 32; shift += 8) {
                Array.Clear(counts, 0, counts.Length);
                for (int i = 0; i < src.Length; ++i) {
                    ++counts[(src[i] >> shift) & 0xFF];
                }
                var total = 0;
                for (int b = 0; b < 256; ++b) {
                    var c = counts[b];
                    counts[b] = total;
                    total += c;
                }
                for (int i = 0; i < src.Length; ++i) {
                    var v = src[i];
                    dst[counts[(v >> shift) & 0xFF]++] = v;
                }
                var swap = src;
                src = dst;
                dst = swap;
            }
            // 4 прохода — четное число, результат снова в data
        }

        private static void RunSort() {
            var rnd = new Random(31);
            var source = new uint[SortCount];
            for (int i = 0; i < SortCount; ++i) {
                source[i] = (uint)rnd.Next() ^ ((uint)rnd.Next() << 16);
            }
            var work = new uint[SortCount];
            var temp = new uint[SortCount];

            // проверка корректности radix один раз
            Array.Copy(source, work, SortCount);
            RadixSort(work, temp);
            var check = (uint[])source.Clone();
            Array.Sort(check);
            for (int i = 0; i < SortCount; ++i) {
                if (work[i] != check[i]) throw new Exception("Radix sort mismatch at " + i);
            }

            // В замер входит Array.Copy исходника (одинаково в обоих вариантах); его время записано отдельно
            Bench.Measure("ch10.sort", Chapter, "Array.Copy only (baseline, subtract)", () => {
                Array.Copy(source, work, SortCount);
                Bench.SinkLong = work[7];
            }, 2, 9, SortCount);
            Bench.Measure("ch10.sort", Chapter, "Array.Sort uint[] 1M", () => {
                Array.Copy(source, work, SortCount);
                Array.Sort(work);
                Bench.SinkLong = work[7];
            }, 2, 9, SortCount);
            Bench.Measure("ch10.sort", Chapter, "LSD radix sort (base 256) uint[] 1M", () => {
                Array.Copy(source, work, SortCount);
                RadixSort(work, temp);
                Bench.SinkLong = work[7];
            }, 2, 9, SortCount);
        }

        // ---------- recursive DFS vs explicit stack ----------

        private const int TreeSize = 100_000;

        public sealed class TreeNode {
            public int value;
            public List<TreeNode> children = new List<TreeNode>(4);
        }

        private static TreeNode BuildTree(out int depth) {
            var rnd = new Random(32);
            var nodes = new TreeNode[TreeSize];
            var depths = new int[TreeSize];
            nodes[0] = new TreeNode { value = 0 };
            depth = 0;
            for (int i = 1; i < TreeSize; ++i) {
                // родитель среди последних 64 узлов: дерево получается глубоким, но не вырожденным
                var parent = Math.Max(0, i - 1 - rnd.Next(64));
                var node = new TreeNode { value = rnd.Next(100) };
                nodes[parent].children.Add(node);
                nodes[i] = node;
                depths[i] = depths[parent] + 1;
                if (depths[i] > depth) depth = depths[i];
            }
            return nodes[0];
        }

        private static long DfsRecursive(TreeNode node) {
            long sum = node.value;
            var children = node.children;
            for (int i = 0; i < children.Count; ++i) {
                sum += DfsRecursive(children[i]);
            }
            return sum;
        }

        private static long DfsStack(TreeNode root, Stack<TreeNode> stack) {
            long sum = 0;
            stack.Clear();
            stack.Push(root);
            while (stack.Count > 0) {
                var node = stack.Pop();
                sum += node.value;
                var children = node.children;
                for (int i = children.Count - 1; i >= 0; --i) {
                    stack.Push(children[i]);
                }
            }
            return sum;
        }

        private static void RunDfs() {
            var root = BuildTree(out var depth);
            var stack = new Stack<TreeNode>(1024);
            if (DfsRecursive(root) != DfsStack(root, stack)) throw new Exception("DFS mismatch");
            var notes = "tree depth = " + depth;
            var r1 = Bench.Measure("ch10.dfs", Chapter, "recursive DFS (100k nodes)", () => Bench.SinkLong = DfsRecursive(root), 3, 21, TreeSize);
            r1.notes = notes;
            var r2 = Bench.Measure("ch10.dfs", Chapter, "explicit Stack<T> DFS (100k nodes, reused stack)", () => Bench.SinkLong = DfsStack(root, stack), 3, 21, TreeSize);
            r2.notes = notes;
        }
    }
}
