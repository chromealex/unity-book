using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

namespace UnityBook.Bits {

    /// <summary>
    /// Перебор установленных битов в битовой маске из слов ulong.
    /// Вместо проверки каждого из 64 бит слово обрабатывается через tzcnt:
    /// цикл делает столько шагов, сколько в слове единиц.
    /// Пустые слова пропускаются сразу, полностью заполненные пишутся одним проходом без tzcnt.
    /// </summary>
    public static unsafe class BitScanner {

        public const int BITS_IN_ULONG = 64;

        /// <summary>
        /// Сколько слов ulong нужно, чтобы хранить bitLength бит.
        /// </summary>
        [INLINE(256)]
        public static int GetWordCount(int bitLength) {
            return (bitLength + BITS_IN_ULONG - 1) / BITS_IN_ULONG;
        }

        /// <summary>
        /// Количество установленных битов среди первых bitLength.
        /// </summary>
        [INLINE(256)]
        public static int GetTrueBitsCount(ulong* data, int bitLength) {
            var wordCount = GetWordCount(bitLength);
            var count = 0;
            for (int i = 0; i < wordCount; ++i) {
                var value = data[i];
                if (i == wordCount - 1) {
                    value &= GetLastWordMask(bitLength);
                }
                count += math.countbits(value);
            }
            return count;
        }

        /// <summary>
        /// Пишет индексы всех установленных битов в destination и возвращает их количество.
        /// В destination должно быть место под bitLength значений: столько получится, если установлены все биты.
        /// </summary>
        [INLINE(256)]
        public static int GetTrueBits(ulong* data, int bitLength, uint* destination) {
            var fullWords = bitLength / BITS_IN_ULONG;
            var count = 0;

            // Основной цикл развёрнут на 4 слова: меньше накладных расходов на сам цикл
            var i = 0;
            var limit4 = fullWords & ~3;
            for (; i < limit4; i += 4) {
                ProcessWord(data[i + 0], destination, ref count, (uint)((i + 0) * BITS_IN_ULONG));
                ProcessWord(data[i + 1], destination, ref count, (uint)((i + 1) * BITS_IN_ULONG));
                ProcessWord(data[i + 2], destination, ref count, (uint)((i + 2) * BITS_IN_ULONG));
                ProcessWord(data[i + 3], destination, ref count, (uint)((i + 3) * BITS_IN_ULONG));
            }
            for (; i < fullWords; ++i) {
                ProcessWord(data[i], destination, ref count, (uint)(i * BITS_IN_ULONG));
            }

            ProcessTail(data, bitLength, destination, ref count);
            return count;
        }

        /// <summary>
        /// То же, что GetTrueBits, но результат — новый список с указанным аллокатором.
        /// </summary>
        public static UnsafeList<uint> GetTrueBits(ulong* data, int bitLength, Allocator allocator) {
            var result = new UnsafeList<uint>(bitLength, allocator);
            result.Length = GetTrueBits(data, bitLength, result.Ptr);
            return result;
        }

        /// <summary>
        /// Версия для NativeArray: удобна в джобах. Индексы дописываются в result.
        /// </summary>
        public static void GetTrueBits(NativeArray<ulong> words, int bitLength, ref NativeList<uint> result) {
            var start = result.Length;
            result.ResizeUninitialized(start + bitLength);
            var count = GetTrueBits((ulong*)words.GetUnsafeReadOnlyPtr(), bitLength, result.GetUnsafePtr() + start);
            result.ResizeUninitialized(start + count);
        }

        /// <summary>
        /// Постраничная выдача: пропускает первые startIndex установленных битов и пишет не больше count следующих.
        /// Слова, целиком лежащие до startIndex, пропускаются по countbits, без перебора битов.
        /// </summary>
        [INLINE(256)]
        public static int WriteTrueBits(ulong* data, int bitLength, uint* destination, int startIndex, int count) {
            if (count <= 0) {
                return 0;
            }

            var wordCount = GetWordCount(bitLength);
            var sourceIndex = 0;
            var destinationIndex = 0;
            for (int i = 0; i < wordCount; ++i) {
                var value = data[i];
                if (i == wordCount - 1) {
                    value &= GetLastWordMask(bitLength);
                }
                if (value == 0UL) {
                    continue;
                }

                var bitsCount = math.countbits(value);
                if (sourceIndex + bitsCount <= startIndex) {
                    sourceIndex += bitsCount;
                    continue;
                }

                var offset = (uint)(i * BITS_IN_ULONG);
                while (value != 0UL) {
                    var bit = math.tzcnt(value);
                    if (sourceIndex >= startIndex) {
                        destination[destinationIndex++] = offset + (uint)bit;
                        if (destinationIndex == count) {
                            return destinationIndex;
                        }
                    }
                    ++sourceIndex;
                    value &= value - 1UL;
                }
            }

            return destinationIndex;
        }

        /// <summary>
        /// Обработка одного полного слова: offset — индекс его нулевого бита во всей маске.
        /// </summary>
        [INLINE(256)]
        internal static void ProcessWord(ulong value, uint* destination, ref int count, uint offset) {
            // все нули — сразу выходим
            if (value == 0UL) {
                return;
            }
            // все единицы — пишем 64 подряд идущих индекса; такой цикл Burst векторизует
            if (value == ulong.MaxValue) {
                WriteFullWord(destination + count, offset);
                count += BITS_IN_ULONG;
                return;
            }
            // иначе: tzcnt даёт номер младшего установленного бита, записываем его и сбрасываем бит
            while (value != 0UL) {
                var bit = math.tzcnt(value);
                destination[count++] = offset + (uint)bit;
                value &= value - 1UL;
            }
        }

        [INLINE(256)]
        internal static void WriteFullWord(uint* destination, uint offset) {
            for (uint i = 0; i < BITS_IN_ULONG; ++i) {
                destination[i] = offset + i;
            }
        }

        /// <summary>
        /// Последнее неполное слово: биты за пределами bitLength отбрасываются маской.
        /// </summary>
        [INLINE(256)]
        internal static void ProcessTail(ulong* data, int bitLength, uint* destination, ref int count) {
            var remainder = bitLength & (BITS_IN_ULONG - 1);
            if (remainder == 0) {
                return;
            }
            var index = bitLength / BITS_IN_ULONG;
            var value = data[index] & ((1UL << remainder) - 1UL);
            var offset = (uint)(index * BITS_IN_ULONG);
            while (value != 0UL) {
                var bit = math.tzcnt(value);
                destination[count++] = offset + (uint)bit;
                value &= value - 1UL;
            }
        }

        [INLINE(256)]
        private static ulong GetLastWordMask(int bitLength) {
            var remainder = bitLength & (BITS_IN_ULONG - 1);
            return remainder == 0 ? ulong.MaxValue : (1UL << remainder) - 1UL;
        }

    }

}
