using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace UnityBook.ILAnalysis.Tests {

    public class CallAnalyzerTests {

        // Подопытные методы. NoInlining не влияет на IL, но не даёт JIT склеить их при отладке.
        private static class Samples {

            public static int counter;

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void Target() {
                ++counter;
            }

            public static void Direct() {
                Target();
                Target();
            }

            public static void InLoop(int count) {
                for (int i = 0; i < count; ++i) {
                    Target();
                }
            }

            public static void InWhile(int count) {
                while (count-- > 0) {
                    Target();
                }
            }

            public static void Nested() {
                Helper();
                Helper();
            }

            public static void NestedInLoop(int count) {
                Target();
                for (int i = 0; i < count; ++i) {
                    Helper();
                }
            }

            public static void Branch(bool flag) {
                if (flag == true) {
                    Target();
                }
            }

            public static void Recursive(int depth) {
                Target();
                if (depth > 0) {
                    Recursive(depth - 1);
                }
            }

            public static void Ignored() {
                IgnoredHelper();
            }

            private static void Helper() {
                Target();
            }

            [CallAnalyzerIgnore]
            private static void IgnoredHelper() {
                Target();
            }

        }

        private static MethodInfo Get(string name) {
            return typeof(Samples).GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        }

        private static CallCount Count(string name) {
            return CallAnalyzer.Count(Get(name), Get(nameof(Samples.Target)));
        }

        [Test]
        public void DirectCalls() {
            var result = Count(nameof(Samples.Direct));
            Assert.AreEqual(2, result.total);
            Assert.AreEqual(0, result.insideLoop);
        }

        [Test]
        public void CallInForLoop() {
            var result = Count(nameof(Samples.InLoop));
            Assert.AreEqual(1, result.total);
            Assert.AreEqual(1, result.insideLoop);
        }

        [Test]
        public void CallInWhileLoop() {
            var result = Count(nameof(Samples.InWhile));
            Assert.AreEqual(1, result.total);
            Assert.AreEqual(1, result.insideLoop);
        }

        [Test]
        public void CallThroughNestedMethod() {
            var result = Count(nameof(Samples.Nested));
            Assert.AreEqual(2, result.total);
            Assert.AreEqual(0, result.insideLoop);
        }

        [Test]
        public void NestedCallInsideLoop() {
            var result = Count(nameof(Samples.NestedInLoop));
            Assert.AreEqual(2, result.total);
            Assert.AreEqual(1, result.insideLoop);
        }

        [Test]
        public void ConditionIsNotLoop() {
            var result = Count(nameof(Samples.Branch));
            Assert.AreEqual(1, result.total);
            Assert.AreEqual(0, result.insideLoop);
        }

        [Test]
        public void RecursionStops() {
            var result = Count(nameof(Samples.Recursive));
            Assert.AreEqual(1, result.total);
        }

        [Test]
        public void IgnoredMethodIsSkipped() {
            var result = Count(nameof(Samples.Ignored));
            Assert.AreEqual(0, result.total);
        }

    }

}
