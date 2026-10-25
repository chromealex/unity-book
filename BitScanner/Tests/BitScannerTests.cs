using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace UnityBook.Bits.Tests {

    public unsafe class BitScannerTests {

        // Плотности масок: пустая, разреженная, половина, почти полная, полная.
        private static readonly double[] DENSITIES = { 0.0, 0.03, 0.5, 0.97, 1.0 };
        // Длины специально некратны 64 и 256, чтобы проверить хвосты.
        private static readonly int[] LENGTHS = { 1, 63, 64, 65, 255, 256, 1000, 4099 };

        [Test]
        public void Scalar_MatchesNaive() {
            var random = new System.Random(1);
            foreach (var density in DENSITIES) {
                foreach (var length in LENGTHS) {
                    var words = CreateMask(random, length, density);
                    var expected = Naive(words, length);
                    var actual = new uint[length];
                    int count;
                    fixed (ulong* data = words) {
                        fixed (uint* destination = actual) {
                            count = BitScanner.GetTrueBits(data, length, destination);
                            Assert.AreEqual(expected.Length, BitScanner.GetTrueBitsCount(data, length));
                        }
                    }
                    AssertSame(expected, actual, count, density, length);
                }
            }
        }

        [Test]
        public void WriteTrueBits_ReturnsPage() {
            var random = new System.Random(2);
            var length = 1000;
            var words = CreateMask(random, length, 0.3);
            var expected = Naive(words, length);
            var page = new uint[10];
            fixed (ulong* data = words) {
                fixed (uint* destination = page) {
                    var count = BitScanner.WriteTrueBits(data, length, destination, 20, page.Length);
                    Assert.AreEqual(page.Length, count);
                }
            }
            for (int i = 0; i < page.Length; ++i) {
                Assert.AreEqual(expected[20 + i], page[i]);
            }
        }

        [Test]
        public void Simd_InBurstJob_MatchesNaive() {
            var random = new System.Random(3);
            foreach (var density in DENSITIES) {
                foreach (var length in LENGTHS) {
                    var words = CreateMask(random, length, density);
                    var expected = Naive(words, length);
                    var data = new NativeArray<ulong>(words, Allocator.TempJob);
                    var result = new NativeArray<uint>(length, Allocator.TempJob);
                    var count = new NativeArray<int>(1, Allocator.TempJob);
                    new SimdJob {
                        data = data,
                        result = result,
                        count = count,
                        bitLength = length,
                    }.Run();
                    AssertSame(expected, result.ToArray(), count[0], density, length);
                    data.Dispose();
                    result.Dispose();
                    count.Dispose();
                }
            }
        }

        // Burst-компиляция нужна, иначе интринсики недоступны и тест проверит только скалярный путь.
        [BurstCompile(CompileSynchronously = true)]
        private struct SimdJob : IJob {

            [ReadOnly] public NativeArray<ulong> data;
            public NativeArray<uint> result;
            public NativeArray<int> count;
            public int bitLength;

            public void Execute() {
                this.count[0] = BitScannerSimd.GetTrueBits((ulong*)this.data.GetUnsafeReadOnlyPtr(), this.bitLength, (uint*)this.result.GetUnsafePtr());
            }

        }

        private static ulong[] CreateMask(System.Random random, int length, double density) {
            var words = new ulong[BitScanner.GetWordCount(length)];
            for (int i = 0; i < length; ++i) {
                if (random.NextDouble() < density) {
                    words[i / 64] |= 1UL << (i % 64);
                }
            }
            // мусор за пределами length: сканер обязан его игнорировать
            if (words.Length > 0 && (length & 63) != 0) {
                words[words.Length - 1] |= ~0UL << (length & 63);
            }
            return words;
        }

        // Эталон: проверяем каждый бит по очереди.
        private static uint[] Naive(ulong[] words, int length) {
            var list = new System.Collections.Generic.List<uint>();
            for (int i = 0; i < length; ++i) {
                if ((words[i / 64] & (1UL << (i % 64))) != 0UL) {
                    list.Add((uint)i);
                }
            }
            return list.ToArray();
        }

        private static void AssertSame(uint[] expected, uint[] actual, int count, double density, int length) {
            Assert.AreEqual(expected.Length, count, $"density {density}, length {length}");
            for (int i = 0; i < count; ++i) {
                Assert.AreEqual(expected[i], actual[i], $"density {density}, length {length}, index {i}");
            }
        }

    }

}
