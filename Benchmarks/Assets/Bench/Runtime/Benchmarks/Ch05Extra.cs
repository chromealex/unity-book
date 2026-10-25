using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace BookBench {

    /// <summary>
    /// Глава 5, дополнительные замеры:
    /// ch5.list.* — где видна лишняя косвенность NativeList (m_ListData — указатель на UnsafeList&lt;T&gt;);
    /// ch5.bitcase — «Разбор из практики: поиск установленных бит в 13 раз быстрее», код «до» и «после».
    /// </summary>
    public static unsafe class Ch05Extra {

        private const string Chapter = "5";
        private const int Million = 1 << 20;

        public static void Run() {
            RunListIndirection();
            RunBitCase();
        }

        // =====================================================================
        // ch5.list: NativeList vs UnsafeList, стоимость одного обращения
        // =====================================================================
        //
        // Collections: NativeList<T> хранит только `UnsafeList<T>* m_ListData` (+ m_Safety при ENABLE_UNITY_COLLECTIONS_CHECKS),
        // индексатор — `return (*m_ListData)[index];`, Length — `m_ListData->Length`.
        // UnsafeList<T> хранит `T* Ptr` и `int m_length` прямо в себе.
        // Чтение NativeList: job -> m_ListData -> Ptr -> элемент (две зависимые загрузки до данных);
        // UnsafeList: job -> Ptr -> элемент (одна). В обычном цикле Burst выносит загрузку Ptr из цикла, и разница исчезает.

        public enum ListMode {
            NativeList,
            UnsafeList,
            RawPointer,
        }

        /// <summary>(a) Burst, чтение по заранее перемешанным индексам.</summary>
        [BurstCompile(CompileSynchronously = true)]
        public struct ListRandomReadJob : IJob {
            [ReadOnly] public NativeList<int> native;
            [ReadOnly] public NativeArray<int> indices;
            public UnsafeList<int> unsafeList;
            [NativeDisableUnsafePtrRestriction] public int* raw;
            public NativeArray<long> result;
            public ListMode mode;

            public void Execute() {
                long sum = 0;
                if (this.mode == ListMode.NativeList) {
                    for (int i = 0; i < this.indices.Length; ++i) {
                        sum += this.native[this.indices[i]];
                    }
                } else if (this.mode == ListMode.UnsafeList) {
                    for (int i = 0; i < this.indices.Length; ++i) {
                        sum += this.unsafeList[this.indices[i]];
                    }
                } else {
                    for (int i = 0; i < this.indices.Length; ++i) {
                        sum += this.raw[this.indices[i]];
                    }
                }
                this.result[0] = sum;
            }
        }

        /// <summary>(c) Один элемент за вызов; функции не инлайнятся, поэтому разыменование m_ListData не вынести из цикла вызывающего.</summary>
        public static class ListReaders {

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static int ReadNative(NativeList<int> list, int index) {
                return list[index];
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static int ReadNativeRef(ref NativeList<int> list, int index) {
                return list[index];
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static int ReadUnsafe(UnsafeList<int> list, int index) {
                return list[index];
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static int ReadUnsafeRef(ref UnsafeList<int> list, int index) {
                return list[index];
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static int ReadRaw(int* ptr, int index) {
                return ptr[index];
            }
        }

        public enum CallMode {
            NativeByValue,
            NativeByRef,
            UnsafeByValue,
            UnsafeByRef,
            RawPointer,
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct ListCallReadJob : IJob {
            [ReadOnly] public NativeList<int> native;
            public UnsafeList<int> unsafeList;
            [NativeDisableUnsafePtrRestriction] public int* raw;
            public NativeArray<long> result;
            public CallMode mode;
            public int count;

            public void Execute() {
                long sum = 0;
                var mask = Million - 1;
                if (this.mode == CallMode.NativeByValue) {
                    for (int i = 0; i < this.count; ++i) {
                        sum += ListReaders.ReadNative(this.native, i & mask);
                    }
                } else if (this.mode == CallMode.NativeByRef) {
                    for (int i = 0; i < this.count; ++i) {
                        sum += ListReaders.ReadNativeRef(ref this.native, i & mask);
                    }
                } else if (this.mode == CallMode.UnsafeByValue) {
                    for (int i = 0; i < this.count; ++i) {
                        sum += ListReaders.ReadUnsafe(this.unsafeList, i & mask);
                    }
                } else if (this.mode == CallMode.UnsafeByRef) {
                    for (int i = 0; i < this.count; ++i) {
                        sum += ListReaders.ReadUnsafeRef(ref this.unsafeList, i & mask);
                    }
                } else {
                    for (int i = 0; i < this.count; ++i) {
                        sum += ListReaders.ReadRaw(this.raw, i & mask);
                    }
                }
                this.result[0] = sum;
            }
        }

        // (b) управляемый код без Burst: тот же цикл, что в NativeListSumJob / UnsafeListSumJob

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long ManagedSumNative(NativeList<int> list) {
            long sum = 0;
            for (int i = 0; i < list.Length; ++i) {
                sum += list[i];
            }
            return sum;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long ManagedSumUnsafe(UnsafeList<int> list) {
            long sum = 0;
            for (int i = 0; i < list.Length; ++i) {
                sum += list[i];
            }
            return sum;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long ManagedSumRaw(int* ptr, int length) {
            long sum = 0;
            for (int i = 0; i < length; ++i) {
                sum += ptr[i];
            }
            return sum;
        }

        private static void RunListIndirection() {
            var native = new NativeList<int>(Million, Allocator.Persistent);
            var unsafeList = new UnsafeList<int>(Million, Allocator.Persistent);
            var indices = new NativeArray<int>(Million, Allocator.Persistent);
            var result = new NativeArray<long>(1, Allocator.Persistent);
            try {
                var rnd = new Unity.Mathematics.Random(15);
                for (int i = 0; i < Million; ++i) {
                    var v = rnd.NextInt(1000);
                    native.Add(v);
                    unsafeList.Add(v);
                }
                // перемешанные индексы 0..1M-1 (Фишер — Йейтс)
                for (int i = 0; i < Million; ++i) {
                    indices[i] = i;
                }
                for (int i = Million - 1; i > 0; --i) {
                    var j = rnd.NextInt(i + 1);
                    var t = indices[i];
                    indices[i] = indices[j];
                    indices[j] = t;
                }
                var raw = unsafeList.Ptr;
                const string layout = "NativeList = UnsafeList<T>* m_ListData (+m_Safety with checks); indexer (*m_ListData)[i]: job->m_ListData->Ptr->[i]; UnsafeList: job->Ptr->[i]";

                // (a) Burst, случайные индексы
                const string randomNote = "Burst, 1M reads at precomputed shuffled indices (4 MB data); ns per read. Loop has no stores, so Burst can still hoist m_ListData/Ptr loads; this shows data-access cost. " + layout;
                var job = new ListRandomReadJob {
                    native = native,
                    indices = indices,
                    unsafeList = unsafeList,
                    raw = raw,
                    result = result,
                };
                job.mode = ListMode.NativeList;
                var r = Bench.Measure("ch5.list.random", Chapter, "Burst NativeList<int> list[idx[i]] 1M random", () => job.Run(), 3, 21, Million);
                r.notes = randomNote + "; sum=" + result[0];
                job.mode = ListMode.UnsafeList;
                r = Bench.Measure("ch5.list.random", Chapter, "Burst UnsafeList<int> list[idx[i]] 1M random", () => job.Run(), 3, 21, Million);
                r.notes = randomNote + "; sum=" + result[0];
                job.mode = ListMode.RawPointer;
                r = Bench.Measure("ch5.list.random", Chapter, "Burst int* ptr[idx[i]] 1M random", () => job.Run(), 3, 21, Million);
                r.notes = randomNote + "; sum=" + result[0];

                // (b) управляемый код
                var safety = UnityEngine.Application.isEditor ? "Editor: ENABLE_UNITY_COLLECTIONS_CHECKS on, NativeList indexer and Length call AtomicSafetyHandle.CheckReadAndThrow every access" : "player: collections checks compiled out";
                var managedNote = "managed C# (no Burst), for (i < list.Length) sum += list[i], 1M; ns per read; " + safety + ". " + layout;
                r = Bench.Measure("ch5.list.managed", Chapter, "managed NativeList<int> indexer sum 1M", () => {
                    Bench.SinkLong = ManagedSumNative(native);
                }, 3, 21, Million);
                r.notes = managedNote;
                r = Bench.Measure("ch5.list.managed", Chapter, "managed UnsafeList<int> indexer sum 1M", () => {
                    Bench.SinkLong = ManagedSumUnsafe(unsafeList);
                }, 3, 21, Million);
                r.notes = managedNote;
                r = Bench.Measure("ch5.list.managed", Chapter, "managed int* ptr[i] sum 1M", () => {
                    Bench.SinkLong = ManagedSumRaw(raw, Million);
                }, 3, 21, Million);
                r.notes = managedNote;

                // (c) Burst, одно чтение на вызов NoInlining-функции
                const string callNote = "Burst, 1M calls of a [MethodImpl(NoInlining)] reader, one element per call (sequential index); ns per call incl. call overhead. Inside the callee the chain m_ListData->Ptr->[i] cannot be hoisted out of the caller's loop. " + layout;
                var callJob = new ListCallReadJob {
                    native = native,
                    unsafeList = unsafeList,
                    raw = raw,
                    result = result,
                    count = Million,
                };
                callJob.mode = CallMode.NativeByValue;
                r = Bench.Measure("ch5.list.call", Chapter, "Burst NoInlining Read(NativeList<int> by value, i) x1M", () => callJob.Run(), 3, 21, Million);
                r.notes = callNote + "; sum=" + result[0];
                callJob.mode = CallMode.NativeByRef;
                r = Bench.Measure("ch5.list.call", Chapter, "Burst NoInlining Read(ref NativeList<int>, i) x1M", () => callJob.Run(), 3, 21, Million);
                r.notes = callNote + "; ref adds one more load: &list -> m_ListData -> Ptr -> [i]; sum=" + result[0];
                callJob.mode = CallMode.UnsafeByValue;
                r = Bench.Measure("ch5.list.call", Chapter, "Burst NoInlining Read(UnsafeList<int> by value, i) x1M", () => callJob.Run(), 3, 21, Million);
                r.notes = callNote + "; sum=" + result[0];
                callJob.mode = CallMode.UnsafeByRef;
                r = Bench.Measure("ch5.list.call", Chapter, "Burst NoInlining Read(ref UnsafeList<int>, i) x1M", () => callJob.Run(), 3, 21, Million);
                r.notes = callNote + "; sum=" + result[0];
                callJob.mode = CallMode.RawPointer;
                r = Bench.Measure("ch5.list.call", Chapter, "Burst NoInlining Read(int* ptr, i) x1M", () => callJob.Run(), 3, 21, Million);
                r.notes = callNote + "; baseline: call + one load; sum=" + result[0];
            } finally {
                native.Dispose();
                unsafeList.Dispose();
                indices.Dispose();
                result.Dispose();
            }
        }

        // =====================================================================
        // ch5.bitcase: «Разбор из практики: поиск установленных бит в 13 раз быстрее»
        // =====================================================================
        //
        // Книга: индексы установленных бит из ulong[]; «до» — наивная проверка каждого бита,
        // «после» — пустое слово пропускается, полное копируется MemCpy из заранее подготовленного массива индексов,
        // иначе tzcnt + val &= val - 1; плюс развертка по 4 слова (код ProcessWordInline из главы 5).
        // Данные кейса: 1000 ulong со случайными значениями.

        private const int CaseWords = 1000;
        private const int BitsInUlong = 64;
        private const int CaseRepeats = 200;

        public static class BitCase {

            /// <summary>«До»: наивная проверка каждого из 64 бит каждого слова.</summary>
            public static int Before(ulong* data, int wordCount, uint* destPtr) {
                var idx = 0;
                for (int i = 0; i < wordCount; ++i) {
                    var val = data[i];
                    var offset = (uint)(i * BitsInUlong);
                    for (int b = 0; b < BitsInUlong; ++b) {
                        if ((val & (1UL << b)) != 0) {
                            destPtr[idx++] = offset + (uint)b;
                        }
                    }
                }
                return idx;
            }

            /// <summary>«После»: код из книги (GetTrueBitsTempFast / ProcessWordInline), развертка по 4 слова.</summary>
            public static int After(ulong* data, int wordCount, uint* destPtr, uint* fullFillBits, int fillBitsCount) {
                var idx = 0;

                // читаем по 4 ulong
                var i = 0;
                var limit4 = wordCount & ~3;
                for (; i < limit4; i += 4) {
                    ProcessWordInline(data[i + 0], destPtr, ref idx, (uint)((i + 0) * BitsInUlong), fullFillBits, fillBitsCount);
                    ProcessWordInline(data[i + 1], destPtr, ref idx, (uint)((i + 1) * BitsInUlong), fullFillBits, fillBitsCount);
                    ProcessWordInline(data[i + 2], destPtr, ref idx, (uint)((i + 2) * BitsInUlong), fullFillBits, fillBitsCount);
                    ProcessWordInline(data[i + 3], destPtr, ref idx, (uint)((i + 3) * BitsInUlong), fullFillBits, fillBitsCount);
                }

                // оставшиеся ulong, не кратные 4
                for (i = limit4; i < wordCount; ++i) {
                    ProcessWordInline(data[i], destPtr, ref idx, (uint)(i * BitsInUlong), fullFillBits, fillBitsCount);
                }

                return idx;
            }

            /// <summary>
            /// ProcessWordInline из книги. Отличие только в том, что FullFillBits (у автора — статическое поле)
            /// передается параметрами: fullFillBits[k] = k, fillBitsCount — его длина.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void ProcessWordInline(ulong val, uint* destPtr, ref int idx, uint offset, uint* fullFillBits, int fillBitsCount) {
                // все нули — сразу выходим
                if (val == 0UL) return;
                // все единицы — копируем заранее подготовленный массив индексов
                if (val == ulong.MaxValue) {
                    if (offset + BitsInUlong <= (uint)fillBitsCount) {
                        var src = fullFillBits;
                        UnsafeUtility.MemCpy(destPtr + idx, src + offset, sizeof(uint) * BitsInUlong);
                        idx += BitsInUlong;
                        return;
                    }
                }
                // иначе: tzcnt дает номер младшего установленного бита, записываем его и сбрасываем бит
                while (val != 0UL) {
                    var bit = math.tzcnt(val);
                    destPtr[idx++] = offset + (uint)bit;
                    val &= val - 1UL;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct BitCaseJob : IJob {
            [NativeDisableUnsafePtrRestriction] public ulong* data;
            [NativeDisableUnsafePtrRestriction] public uint* dest;
            [NativeDisableUnsafePtrRestriction] public uint* fullFillBits;
            [NativeDisableUnsafePtrRestriction] public int* resultCount;
            public int wordCount;
            public int fillBitsCount;
            public int repeats;
            public bool after;

            public void Execute() {
                var count = 0;
                for (int i = 0; i < this.repeats; ++i) {
                    if (this.after) {
                        count = BitCase.After(this.data, this.wordCount, this.dest, this.fullFillBits, this.fillBitsCount);
                    } else {
                        count = BitCase.Before(this.data, this.wordCount, this.dest);
                    }
                }
                *this.resultCount = count;
            }
        }

        private static void RunBitCase() {
            const string id = "ch5.bitcase";
            var fillBitsCount = CaseWords * BitsInUlong;
            var random = (ulong*)UnsafeUtility.Malloc(CaseWords * sizeof(ulong), 64, Allocator.Persistent);
            var sparse = (ulong*)UnsafeUtility.Malloc(CaseWords * sizeof(ulong), 64, Allocator.Persistent);
            var mixed = (ulong*)UnsafeUtility.Malloc(CaseWords * sizeof(ulong), 64, Allocator.Persistent);
            var dest = (uint*)UnsafeUtility.Malloc(fillBitsCount * sizeof(uint), 64, Allocator.Persistent);
            var check = (uint*)UnsafeUtility.Malloc(fillBitsCount * sizeof(uint), 64, Allocator.Persistent);
            var fullFillBits = (uint*)UnsafeUtility.Malloc(fillBitsCount * sizeof(uint), 64, Allocator.Persistent);
            var resultCount = (int*)UnsafeUtility.Malloc(sizeof(int), 4, Allocator.Persistent);
            try {
                for (int i = 0; i < fillBitsCount; ++i) {
                    fullFillBits[i] = (uint)i;
                }
                // те же генераторы, что у ch5.bitscan (сид 14): random ~50%, sparse ~3% (AND пяти случайных), mixed 1/3 пустых, 1/3 полных, 1/3 случайных
                var rnd = new Unity.Mathematics.Random(14);
                for (int i = 0; i < CaseWords; ++i) {
                    var value = ((ulong)rnd.NextUInt() << 32) | rnd.NextUInt();
                    random[i] = value;
                    var kind = rnd.NextInt(3);
                    mixed[i] = kind == 0 ? 0UL : (kind == 1 ? ulong.MaxValue : value);
                    var s = value;
                    for (int k = 0; k < 4; ++k) {
                        s &= ((ulong)rnd.NextUInt() << 32) | rnd.NextUInt();
                    }
                    sparse[i] = s;
                }

                RunBitCaseSet(id, "random ~50% (book's case data)", random, dest, check, fullFillBits, fillBitsCount, resultCount);
                RunBitCaseSet(id, "sparse ~3%", sparse, dest, check, fullFillBits, fillBitsCount, resultCount);
                RunBitCaseSet(id, "mixed 1/3 empty, 1/3 full, 1/3 random", mixed, dest, check, fullFillBits, fillBitsCount, resultCount);
            } finally {
                UnsafeUtility.Free(random, Allocator.Persistent);
                UnsafeUtility.Free(sparse, Allocator.Persistent);
                UnsafeUtility.Free(mixed, Allocator.Persistent);
                UnsafeUtility.Free(dest, Allocator.Persistent);
                UnsafeUtility.Free(check, Allocator.Persistent);
                UnsafeUtility.Free(fullFillBits, Allocator.Persistent);
                UnsafeUtility.Free(resultCount, Allocator.Persistent);
            }
        }

        private static void RunBitCaseSet(string id, string dataName, ulong* data, uint* dest, uint* check, uint* fullFillBits, int fillBitsCount, int* resultCount) {
            var total = CaseWords * CaseRepeats;
            const string baseNote = "1000 ulong, output = uint indices of set bits; ns per ulong word (x1000 = one scan of the book's 1000 ulong; book: before ~20 ms, after ~1.5 ms per scan)";

            // эталон для проверки: «до» в управляемом коде
            var expected = BitCase.Before(data, CaseWords, check);

            // Burst
            var job = new BitCaseJob {
                data = data,
                dest = dest,
                fullFillBits = fullFillBits,
                resultCount = resultCount,
                wordCount = CaseWords,
                fillBitsCount = fillBitsCount,
                repeats = CaseRepeats,
            };
            job.after = false;
            var r = Bench.Measure(id, Chapter, "Burst before: naive per-bit loop, " + dataName, () => job.Run(), 3, 21, total);
            r.notes = baseNote + "; " + VerifyBits(*resultCount, expected, dest, check);
            job.after = true;
            r = Bench.Measure(id, Chapter, "Burst after: 0 skip + full MemCpy + tzcnt, unroll x4, " + dataName, () => job.Run(), 3, 21, total);
            r.notes = baseNote + "; " + VerifyBits(*resultCount, expected, dest, check);

            // управляемый код (IL2CPP / Mono): автор мог мерить и так, 20 мс на 1000 слов больше похоже на не-Burst
            r = Bench.Measure(id, Chapter, "managed before: naive per-bit loop, " + dataName, () => {
                var count = 0;
                for (int i = 0; i < CaseRepeats; ++i) {
                    count = BitCase.Before(data, CaseWords, dest);
                }
                *resultCount = count;
            }, 3, 21, total);
            r.notes = baseNote + "; " + VerifyBits(*resultCount, expected, dest, check);
            r = Bench.Measure(id, Chapter, "managed after: 0 skip + full MemCpy + tzcnt, unroll x4, " + dataName, () => {
                var count = 0;
                for (int i = 0; i < CaseRepeats; ++i) {
                    count = BitCase.After(data, CaseWords, dest, fullFillBits, fillBitsCount);
                }
                *resultCount = count;
            }, 3, 21, total);
            r.notes = baseNote + "; " + VerifyBits(*resultCount, expected, dest, check);
        }

        private static string VerifyBits(int count, int expected, uint* dest, uint* check) {
            if (count != expected) return "MISMATCH: count " + count + " vs " + expected;
            var same = UnsafeUtility.MemCmp(dest, check, (long)count * sizeof(uint)) == 0;
            return "set bits " + count + ", output " + (same ? "matches naive" : "MISMATCH");
        }
    }
}
