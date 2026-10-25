using Unity.Burst.Intrinsics;
using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

namespace UnityBook.Bits {

    /// <summary>
    /// Варианты BitScanner на интринсиках. SIMD здесь не ищет биты, а сортирует слова:
    /// за одну инструкцию сравнения выясняем, какие из 2–4 слов пустые, а какие заполнены целиком,
    /// и только остальные отдаём в цикл с tzcnt.
    /// Интринсики работают только в коде, скомпилированном Burst. В обычном C# проверки IsXxxSupported
    /// возвращают false, и вызов уходит в скалярный BitScanner.GetTrueBits.
    /// </summary>
    public static unsafe class BitScannerSimd {

        private const int BITS_IN_ULONG = BitScanner.BITS_IN_ULONG;

        /// <summary>
        /// Выбирает лучший доступный вариант для текущего процессора.
        /// </summary>
        public static int GetTrueBits(ulong* data, int bitLength, uint* destination) {
            if (X86.Avx2.IsAvx2Supported == true) {
                return GetTrueBitsAvx2(data, bitLength, destination);
            }
            if (Arm.Neon.IsNeonSupported == true) {
                return GetTrueBitsNeon(data, bitLength, destination);
            }
            if (X86.Sse2.IsSse2Supported == true) {
                return GetTrueBitsSse2(data, bitLength, destination);
            }
            return BitScanner.GetTrueBits(data, bitLength, destination);
        }

        /// <summary>
        /// AVX2: четыре слова за раз через 256-битное сравнение по 64-битным дорожкам.
        /// </summary>
        public static int GetTrueBitsAvx2(ulong* data, int bitLength, uint* destination) {
            var fullWords = bitLength / BITS_IN_ULONG;
            var count = 0;
            var i = 0;
            if (X86.Avx2.IsAvx2Supported == true) {
                var zero = X86.Avx.mm256_setzero_si256();
                var ones = X86.Avx.mm256_set1_epi64x(-1L);
                var limit4 = fullWords & ~3;
                for (; i < limit4; i += 4) {
                    var vec = X86.Avx.mm256_loadu_si256(data + i);
                    // movemask_epi8 даёт по биту на байт: у 64-битной дорожки k это биты 8k..8k+7
                    var zeroMask = X86.Avx2.mm256_movemask_epi8(X86.Avx2.mm256_cmpeq_epi64(vec, zero));
                    var onesMask = X86.Avx2.mm256_movemask_epi8(X86.Avx2.mm256_cmpeq_epi64(vec, ones));
                    for (int lane = 0; lane < 4; ++lane) {
                        var shift = lane * 8;
                        if (((zeroMask >> shift) & 0xFF) == 0xFF) {
                            continue;
                        }
                        var offset = (uint)((i + lane) * BITS_IN_ULONG);
                        if (((onesMask >> shift) & 0xFF) == 0xFF) {
                            BitScanner.WriteFullWord(destination + count, offset);
                            count += BITS_IN_ULONG;
                            continue;
                        }
                        BitScanner.ProcessWord(data[i + lane], destination, ref count, offset);
                    }
                }
            }
            return Finish(data, bitLength, destination, i, count);
        }

        /// <summary>
        /// SSE2: два слова за раз. Сравнения 64-битных дорожек в SSE2 нет (оно появилось в SSE4.1),
        /// поэтому сравниваем по 32 бита: дорожка пустая или полная, только если обе её половины такие же.
        /// </summary>
        public static int GetTrueBitsSse2(ulong* data, int bitLength, uint* destination) {
            var fullWords = bitLength / BITS_IN_ULONG;
            var count = 0;
            var i = 0;
            if (X86.Sse2.IsSse2Supported == true) {
                var zero = X86.Sse2.setzero_si128();
                var ones = X86.Sse2.set1_epi32(-1);
                var limit2 = fullWords & ~1;
                for (; i < limit2; i += 2) {
                    var vec = X86.Sse2.loadu_si128(data + i);
                    var zeroMask = X86.Sse2.movemask_epi8(X86.Sse2.cmpeq_epi32(vec, zero));
                    var onesMask = X86.Sse2.movemask_epi8(X86.Sse2.cmpeq_epi32(vec, ones));
                    for (int lane = 0; lane < 2; ++lane) {
                        var shift = lane * 8;
                        if (((zeroMask >> shift) & 0xFF) == 0xFF) {
                            continue;
                        }
                        var offset = (uint)((i + lane) * BITS_IN_ULONG);
                        if (((onesMask >> shift) & 0xFF) == 0xFF) {
                            BitScanner.WriteFullWord(destination + count, offset);
                            count += BITS_IN_ULONG;
                            continue;
                        }
                        BitScanner.ProcessWord(data[i + lane], destination, ref count, offset);
                    }
                }
            }
            return Finish(data, bitLength, destination, i, count);
        }

        /// <summary>
        /// NEON (ARM64, Apple Silicon, телефоны): два слова за раз через vceqq_u64.
        /// </summary>
        public static int GetTrueBitsNeon(ulong* data, int bitLength, uint* destination) {
            var fullWords = bitLength / BITS_IN_ULONG;
            var count = 0;
            var i = 0;
            if (Arm.Neon.IsNeonSupported == true) {
                var zero = new v128(0UL);
                var ones = new v128(ulong.MaxValue);
                var limit2 = fullWords & ~1;
                for (; i < limit2; i += 2) {
                    var vec = Arm.Neon.vld1q_u64(data + i);
                    // vceqq_u64 пишет в дорожку все единицы, если значения равны, и нули, если нет
                    var isZero = Arm.Neon.vceqq_u64(vec, zero);
                    var isOnes = Arm.Neon.vceqq_u64(vec, ones);
                    ProcessNeonLane(vec.ULong0, isZero.ULong0, isOnes.ULong0, destination, ref count, (uint)((i + 0) * BITS_IN_ULONG));
                    ProcessNeonLane(vec.ULong1, isZero.ULong1, isOnes.ULong1, destination, ref count, (uint)((i + 1) * BITS_IN_ULONG));
                }
            }
            return Finish(data, bitLength, destination, i, count);
        }

        [INLINE(256)]
        private static void ProcessNeonLane(ulong value, ulong isZero, ulong isOnes, uint* destination, ref int count, uint offset) {
            if (isZero == ulong.MaxValue) {
                return;
            }
            if (isOnes == ulong.MaxValue) {
                BitScanner.WriteFullWord(destination + count, offset);
                count += BITS_IN_ULONG;
                return;
            }
            BitScanner.ProcessWord(value, destination, ref count, offset);
        }

        /// <summary>
        /// Хвост: полные слова, не вошедшие в SIMD-блоки, и последнее неполное слово.
        /// </summary>
        [INLINE(256)]
        private static int Finish(ulong* data, int bitLength, uint* destination, int from, int count) {
            var fullWords = bitLength / BITS_IN_ULONG;
            for (int i = from; i < fullWords; ++i) {
                BitScanner.ProcessWord(data[i], destination, ref count, (uint)(i * BITS_IN_ULONG));
            }
            BitScanner.ProcessTail(data, bitLength, destination, ref count);
            return count;
        }

    }

}
