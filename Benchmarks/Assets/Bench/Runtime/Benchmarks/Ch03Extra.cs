using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace BookBench {

    /// <summary>
    /// Глава 3, дополнительные замеры:
    /// ch3.ptrvsclass — «работа с указателями примерно в два раза быстрее, чем стандартный путь: создали класс в куче...»;
    /// ch3.diff — разбор «дифф состояния игры»: побайтово, по 8 байт (long), по 32 байта (v256, Burst);
    /// ch3.fixedloop — цикл p[i] внутри fixed против обычного arr[i].
    /// Всё, кроме Burst-вариантов диффа, — управляемый код (IL2CPP / Mono), как в книге.
    /// </summary>
    public static unsafe class Ch03Extra {

        private const string Chapter = "3";

        public static void Run() {
            RunPtrVsClass();
            RunDiff();
            RunFixedLoop();
        }

        // =====================================================================
        // ch3.ptrvsclass
        // =====================================================================

        /// <summary>Та же «A» из книги (a, b, c), только как класс.</summary>
        public sealed class AClass {
            public int a;
            public int b;
            public int c;
        }

        public struct A {
            public int a;
            public int b;
            public int c;
        }

        public sealed class NodeClass {
            public int value;
            public NodeClass next;
        }

        /// <summary>Как в книге, раздел «Ссылка на себя внутри структуры».</summary>
        public struct Node {
            public int value;
            public Node* next;
        }

        private const int LifeCount = 100_000;
        private const int AccessCalls = 1_000_000;
        private const int ListNodes = 100_000;

        // ----- жизненный цикл: создать, поработать, уничтожить -----

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long LifecycleClass(int count) {
            long sum = 0;
            for (int i = 0; i < count; ++i) {
                var obj = new AClass();
                obj.a = i;
                obj.b = obj.a * 2;
                obj.c = obj.a + obj.b;
                sum += obj.c;
                // ссылка уходит: объект соберет GC
            }
            return sum;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long LifecycleMalloc(int count) {
            long sum = 0;
            var size = UnsafeUtility.SizeOf<A>();
            var align = UnsafeUtility.AlignOf<A>();
            for (int i = 0; i < count; ++i) {
                var ptr = (A*)UnsafeUtility.Malloc(size, align, Allocator.Persistent);
                ptr->a = i;
                ptr->b = ptr->a * 2;
                ptr->c = ptr->a + ptr->b;
                sum += ptr->c;
                UnsafeUtility.Free(ptr, Allocator.Persistent);
            }
            return sum;
        }

        // ----- объект создан один раз, «что-то с ним делаем» через метод -----

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UpdateClass(AClass obj, int i) {
            obj.a += i;
            obj.b ^= obj.a;
            obj.c = obj.a + obj.b;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UpdatePtr(A* ptr, int i) {
            ptr->a += i;
            ptr->b ^= ptr->a;
            ptr->c = ptr->a + ptr->b;
        }

        // ----- связный список: ноды-классы против Node* -----

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long SumClassList(NodeClass head) {
            long sum = 0;
            for (var n = head; n != null; n = n.next) {
                sum += n.value;
            }
            return sum;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long SumPtrList(Node* head) {
            long sum = 0;
            for (var n = head; n != null; n = n->next) {
                sum += n->value;
            }
            return sum;
        }

        private static int[] Shuffled(int count, int seed) {
            var order = new int[count];
            for (int i = 0; i < count; ++i) {
                order[i] = i;
            }
            var rnd = new Random(seed);
            for (int i = count - 1; i > 0; --i) {
                var j = rnd.Next(i + 1);
                var t = order[i];
                order[i] = order[j];
                order[j] = t;
            }
            return order;
        }

        private static void RunPtrVsClass() {
            const string id = "ch3.ptrvsclass";
            const string lifeNote = "book: create, work, destroy; one object per op, 3 int fields. Class path includes amortized GC (GC enabled), Malloc path calls UnsafeUtility.Malloc+Free(Persistent) per op";

            var r = Bench.Measure(id, Chapter, "lifecycle: new AClass(), use, drop (GC)", () => {
                Bench.SinkLong = LifecycleClass(LifeCount);
            }, 3, 21, LifeCount);
            r.notes = lifeNote;
            r = Bench.Measure(id, Chapter, "lifecycle: Malloc(A), use via ptr->, Free", () => {
                Bench.SinkLong = LifecycleMalloc(LifeCount);
            }, 3, 21, LifeCount);
            r.notes = lifeNote;
            Bench.MeasureAlloc(id + ".alloc", Chapter, "lifecycle: new AClass()", () => Bench.SinkLong = LifecycleClass(1), 10_000);
            Bench.MeasureAlloc(id + ".alloc", Chapter, "lifecycle: Malloc/Free (expect 0 managed bytes)", () => Bench.SinkLong = LifecycleMalloc(1), 10_000);

            // объект живет долго, работаем с ним через вызовы
            const string accessNote = "object created once; 1M non-inlined calls that read/write 3 fields (class reference vs A*)";
            var obj = new AClass();
            var ptr = (A*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<A>(), UnsafeUtility.AlignOf<A>(), Allocator.Persistent);
            UnsafeUtility.MemClear(ptr, UnsafeUtility.SizeOf<A>());
            try {
                r = Bench.Measure(id, Chapter, "long-lived AClass: Update(obj, i) x1M", () => {
                    for (int i = 0; i < AccessCalls; ++i) {
                        UpdateClass(obj, i);
                    }
                    Bench.SinkInt = obj.c;
                }, 3, 21, AccessCalls);
                r.notes = accessNote;
                r = Bench.Measure(id, Chapter, "long-lived A* (Malloc): Update(ptr, i) x1M", () => {
                    for (int i = 0; i < AccessCalls; ++i) {
                        UpdatePtr(ptr, i);
                    }
                    Bench.SinkInt = ptr->c;
                }, 3, 21, AccessCalls);
                r.notes = accessNote;
            } finally {
                UnsafeUtility.Free(ptr, Allocator.Persistent);
            }

            // связный список: книга сравнивает ноды-классы и Node* (раздел «Ссылка на себя» и «Кэш-линии»)
            var classNodes = new NodeClass[ListNodes];
            var ptrNodes = new IntPtr[ListNodes];
            for (int i = 0; i < ListNodes; ++i) {
                classNodes[i] = new NodeClass { value = i & 1023 };
                var node = (Node*)UnsafeUtility.Malloc(sizeof(Node), UnsafeUtility.AlignOf<Node>(), Allocator.Persistent);
                UnsafeUtility.MemClear(node, sizeof(Node));
                node->value = i & 1023;
                ptrNodes[i] = (IntPtr)node;
            }
            try {
                // порядок выделения
                LinkClass(classNodes, null);
                LinkPtr(ptrNodes, null);
                var classHead = classNodes[0];
                var ptrHead = (Node*)ptrNodes[0];
                const string listNote = "100K nodes, each a separate allocation (new / Malloc), traversal sum; ns per node";
                r = Bench.Measure(id, Chapter, "linked list traverse: class nodes, allocation order", () => {
                    Bench.SinkLong = SumClassList(classHead);
                }, 3, 21, ListNodes);
                r.notes = listNote;
                r = Bench.Measure(id, Chapter, "linked list traverse: Node* (Malloc each), allocation order", () => {
                    Bench.SinkLong = SumPtrList(ptrHead);
                }, 3, 21, ListNodes);
                r.notes = listNote;

                // случайный порядок связей: next прыгает по памяти
                var order = Shuffled(ListNodes, 31);
                LinkClass(classNodes, order);
                LinkPtr(ptrNodes, order);
                classHead = classNodes[order[0]];
                ptrHead = (Node*)ptrNodes[order[0]];
                r = Bench.Measure(id, Chapter, "linked list traverse: class nodes, shuffled links", () => {
                    Bench.SinkLong = SumClassList(classHead);
                }, 3, 21, ListNodes);
                r.notes = listNote + "; next points to a random node (cache misses)";
                r = Bench.Measure(id, Chapter, "linked list traverse: Node* (Malloc each), shuffled links", () => {
                    Bench.SinkLong = SumPtrList(ptrHead);
                }, 3, 21, ListNodes);
                r.notes = listNote + "; next points to a random node (cache misses)";
            } finally {
                for (int i = 0; i < ListNodes; ++i) {
                    UnsafeUtility.Free((void*)ptrNodes[i], Allocator.Persistent);
                }
            }
        }

        /// <summary>order == null: по порядку массива, иначе в порядке order.</summary>
        private static void LinkClass(NodeClass[] nodes, int[] order) {
            for (int i = 0; i < nodes.Length; ++i) {
                var current = order == null ? i : order[i];
                var next = i + 1 < nodes.Length ? (order == null ? i + 1 : order[i + 1]) : -1;
                nodes[current].next = next >= 0 ? nodes[next] : null;
            }
        }

        private static void LinkPtr(IntPtr[] nodes, int[] order) {
            for (int i = 0; i < nodes.Length; ++i) {
                var current = order == null ? i : order[i];
                var next = i + 1 < nodes.Length ? (order == null ? i + 1 : order[i + 1]) : -1;
                ((Node*)nodes[current])->next = next >= 0 ? (Node*)nodes[next] : null;
            }
        }

        // =====================================================================
        // ch3.diff
        // =====================================================================
        //
        // Формат диффа (книга, глава 16 «Patch»): [int смещение][int количество байт][байты из текущего состояния].
        // Отличающиеся подряд байты (слова, блоки) продлевают текущую запись, смещение второй раз не пишется.

        public static class Differ {

            /// <summary>«Первое решение»: идем по байтам и сравниваем со вторым массивом.</summary>
            public static int DiffBytes(byte* prev, byte* curr, int length, byte* output) {
                var pos = 0;
                var i = 0;
                while (i < length) {
                    if (prev[i] == curr[i]) {
                        ++i;
                        continue;
                    }
                    var header = pos;
                    *(int*)(output + header) = i;
                    pos += 8;
                    var start = i;
                    while (i < length && prev[i] != curr[i]) {
                        output[pos++] = curr[i];
                        ++i;
                    }
                    *(int*)(output + header + 4) = i - start;
                }
                return pos;
            }

            /// <summary>Читаем не по 1 байту, а по 8 (long). Хвост, не кратный 8, — побайтово.</summary>
            public static int DiffLongs(byte* prev, byte* curr, int length, byte* output) {
                var pos = 0;
                var i = 0;
                var limit = length & ~7;
                while (i < limit) {
                    if (*(long*)(prev + i) == *(long*)(curr + i)) {
                        i += 8;
                        continue;
                    }
                    var header = pos;
                    *(int*)(output + header) = i;
                    pos += 8;
                    var start = i;
                    while (i < limit && *(long*)(prev + i) != *(long*)(curr + i)) {
                        *(long*)(output + pos) = *(long*)(curr + i);
                        pos += 8;
                        i += 8;
                    }
                    *(int*)(output + header + 4) = i - start;
                }
                if (limit < length) {
                    pos += DiffTail(prev, curr, limit, length, output + pos);
                }
                return pos;
            }

            /// <summary>Читаем по 256 бит (v256) и сравниваем функцией из книги. Хвост, не кратный 32, — побайтово.</summary>
            public static int DiffV256(byte* prev, byte* curr, int length, byte* output) {
                var pos = 0;
                var i = 0;
                var limit = length & ~31;
                while (i < limit) {
                    if (Equal(*(v256*)(prev + i), *(v256*)(curr + i))) {
                        i += 32;
                        continue;
                    }
                    var header = pos;
                    *(int*)(output + header) = i;
                    pos += 8;
                    var start = i;
                    while (i < limit && Equal(*(v256*)(prev + i), *(v256*)(curr + i)) == false) {
                        // выход после 8-байтного заголовка не выровнен на 32: копируем через MemCpy, а не *(v256*)
                        UnsafeUtility.MemCpy(output + pos, curr + i, 32);
                        pos += 32;
                        i += 32;
                    }
                    *(int*)(output + header + 4) = i - start;
                }
                if (limit < length) {
                    pos += DiffTail(prev, curr, limit, length, output + pos);
                }
                return pos;
            }

            private static int DiffTail(byte* prev, byte* curr, int from, int length, byte* output) {
                var pos = 0;
                var i = from;
                while (i < length) {
                    if (prev[i] == curr[i]) {
                        ++i;
                        continue;
                    }
                    var header = pos;
                    *(int*)(output + header) = i;
                    pos += 8;
                    var start = i;
                    while (i < length && prev[i] != curr[i]) {
                        output[pos++] = curr[i];
                        ++i;
                    }
                    *(int*)(output + header + 4) = i - start;
                }
                return pos;
            }

            /// <summary>Сравнение двух v256 — код из книги (глава 16, «Patch»), без изменений по логике.</summary>
            public static bool Equal(v256 a, v256 b) {
                // сначала первый и последний байт — быстрый выход
                if (a.Byte0 != b.Byte0 || a.Byte31 != b.Byte31) {
                    return false;
                }
                // x86 с AVX2
                if (Unity.Burst.Intrinsics.X86.Avx2.IsAvx2Supported) {
                    var xor = Unity.Burst.Intrinsics.X86.Avx2.mm256_xor_si256(a, b);
                    return Unity.Burst.Intrinsics.X86.Avx.mm256_testz_si256(xor, xor) != 0;
                }
                // ARM: две половины по 128 бит
                if (Unity.Burst.Intrinsics.Arm.Neon.IsNeonSupported) {
                    var cmpLo = Unity.Burst.Intrinsics.Arm.Neon.vceqq_u32(a.Lo128, b.Lo128);
                    var cmpHi = Unity.Burst.Intrinsics.Arm.Neon.vceqq_u32(a.Hi128, b.Hi128);
                    ulong maskLo = Unity.Burst.Intrinsics.Arm.Neon.vminvq_u32(cmpLo);
                    ulong maskHi = Unity.Burst.Intrinsics.Arm.Neon.vminvq_u32(cmpHi);
                    return maskLo == uint.MaxValue && maskHi == uint.MaxValue;
                }
                // иначе — побайтовая проверка
                var pa = (byte*)UnsafeUtility.AddressOf(ref a);
                var pb = (byte*)UnsafeUtility.AddressOf(ref b);
                for (var i = 0; i < 32; ++i) {
                    if (pa[i] != pb[i]) return false;
                }
                return true;
            }

            /// <summary>Применяет дифф к копии prev (для проверки корректности, не меряется).</summary>
            public static void Apply(byte* state, byte* diff, int diffLength) {
                var pos = 0;
                while (pos < diffLength) {
                    var offset = *(int*)(diff + pos);
                    var count = *(int*)(diff + pos + 4);
                    pos += 8;
                    UnsafeUtility.MemCpy(state + offset, diff + pos, count);
                    pos += count;
                }
            }
        }

        public enum DiffMode {
            Bytes,
            Longs,
            V256,
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct DiffJob : IJob {
            [NativeDisableUnsafePtrRestriction] public byte* prev;
            [NativeDisableUnsafePtrRestriction] public byte* curr;
            [NativeDisableUnsafePtrRestriction] public byte* output;
            [NativeDisableUnsafePtrRestriction] public int* outputLength;
            public int length;
            public DiffMode mode;

            public void Execute() {
                if (this.mode == DiffMode.Bytes) {
                    *this.outputLength = Differ.DiffBytes(this.prev, this.curr, this.length, this.output);
                } else if (this.mode == DiffMode.Longs) {
                    *this.outputLength = Differ.DiffLongs(this.prev, this.curr, this.length, this.output);
                } else {
                    *this.outputLength = Differ.DiffV256(this.prev, this.curr, this.length, this.output);
                }
            }
        }

        private const int StateSize = 1 << 20; // 1 МБ: размер стейта книга не называет (вопрос автору)

        private static void RunDiff() {
            const string id = "ch3.diff";
            var prev = (byte*)UnsafeUtility.Malloc(StateSize, 64, Allocator.Persistent);
            var curr = (byte*)UnsafeUtility.Malloc(StateSize, 64, Allocator.Persistent);
            var check = (byte*)UnsafeUtility.Malloc(StateSize, 64, Allocator.Persistent);
            // худший случай: чередование «отличается / совпадает» по байту дает 8 + 1 байт на 2 байта стейта
            var outputCapacity = StateSize * 5 + 64;
            var output = (byte*)UnsafeUtility.Malloc(outputCapacity, 64, Allocator.Persistent);
            var outputLength = (int*)UnsafeUtility.Malloc(sizeof(int), 4, Allocator.Persistent);
            try {
                var rnd = new Random(32);
                for (int i = 0; i < StateSize; ++i) {
                    prev[i] = (byte)rnd.Next(256);
                }

                // 1) состояния совпадают: чистая скорость сравнения
                UnsafeUtility.MemCpy(curr, prev, StateSize);
                RunDiffSet(id, "identical", "no changes", prev, curr, check, output, outputLength);

                // 2) мало изменений: 64 участка по 1..64 байта в случайных местах (типичный «дифф к подключению»)
                UnsafeUtility.MemCpy(curr, prev, StateSize);
                for (int i = 0; i < 64; ++i) {
                    var len = rnd.Next(1, 65);
                    var at = rnd.Next(StateSize - len);
                    for (int k = 0; k < len; ++k) {
                        curr[at + k] = (byte)(prev[at + k] ^ (1 + rnd.Next(255)));
                    }
                }
                RunDiffSet(id, "64 runs", "64 changed runs of 1..64 bytes", prev, curr, check, output, outputLength);

                // 3) много разбросанных изменений: ~1% байт, по одному
                UnsafeUtility.MemCpy(curr, prev, StateSize);
                for (int i = 0; i < StateSize / 100; ++i) {
                    var at = rnd.Next(StateSize);
                    curr[at] = (byte)(prev[at] ^ (1 + rnd.Next(255)));
                }
                RunDiffSet(id, "1% scattered", "~1% of bytes changed, single scattered bytes", prev, curr, check, output, outputLength);
            } finally {
                UnsafeUtility.Free(prev, Allocator.Persistent);
                UnsafeUtility.Free(curr, Allocator.Persistent);
                UnsafeUtility.Free(check, Allocator.Persistent);
                UnsafeUtility.Free(output, Allocator.Persistent);
                UnsafeUtility.Free(outputLength, Allocator.Persistent);
            }
        }

        private static void RunDiffSet(string id, string dataName, string dataNote, byte* prev, byte* curr, byte* check, byte* output, int* outputLength) {
            const string baseNote = "1 MB state, ns per whole diff (= per MB); format [int offset][int count][bytes]";

            // управляемый код (IL2CPP / Mono)
            var r = Bench.Measure(id, Chapter, dataName + ": managed, byte by byte", () => {
                *outputLength = Differ.DiffBytes(prev, curr, StateSize, output);
            }, 2, 11, 1);
            r.notes = baseNote + "; " + dataNote + "; " + Verify(prev, curr, check, output, *outputLength);
            r = Bench.Measure(id, Chapter, dataName + ": managed, 8 bytes (long)", () => {
                *outputLength = Differ.DiffLongs(prev, curr, StateSize, output);
            }, 2, 11, 1);
            r.notes = baseNote + "; " + dataNote + "; " + Verify(prev, curr, check, output, *outputLength);

            // Burst
            var job = new DiffJob {
                prev = prev,
                curr = curr,
                output = output,
                outputLength = outputLength,
                length = StateSize,
            };
            job.mode = DiffMode.Bytes;
            r = Bench.Measure(id, Chapter, dataName + ": Burst, byte by byte", () => job.Run(), 2, 11, 1);
            r.notes = baseNote + "; " + dataNote + "; " + Verify(prev, curr, check, output, *outputLength);
            job.mode = DiffMode.Longs;
            r = Bench.Measure(id, Chapter, dataName + ": Burst, 8 bytes (long)", () => job.Run(), 2, 11, 1);
            r.notes = baseNote + "; " + dataNote + "; " + Verify(prev, curr, check, output, *outputLength);
            job.mode = DiffMode.V256;
            r = Bench.Measure(id, Chapter, dataName + ": Burst, 32 bytes (v256 + AVX2/NEON compare from the book)", () => job.Run(), 2, 11, 1);
            r.notes = baseNote + "; " + dataNote + "; " + Verify(prev, curr, check, output, *outputLength) + "; on ARM64 the Neon branch is taken (vceqq_u32 + vminvq_u32)";
        }

        /// <summary>Применяет последний дифф к копии prev и сравнивает с curr; возвращает строку для notes.</summary>
        private static string Verify(byte* prev, byte* curr, byte* check, byte* output, int outputLength) {
            UnsafeUtility.MemCpy(check, prev, StateSize);
            Differ.Apply(check, output, outputLength);
            var ok = UnsafeUtility.MemCmp(check, curr, StateSize) == 0;
            return "diff size " + outputLength + " B, apply " + (ok ? "ok" : "MISMATCH");
        }

        // =====================================================================
        // ch3.fixedloop
        // =====================================================================

        private const int FixedCount = 1 << 20;

        /// <summary>Обычный цикл: граница — arr.Length, JIT/IL2CPP могут сами убрать проверку.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long SumIndexLength(int[] arr) {
            long sum = 0;
            for (int i = 0; i < arr.Length; ++i) {
                sum += arr[i];
            }
            return sum;
        }

        /// <summary>Граница — отдельный параметр: проверку границ компилятор убрать не может.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long SumIndexCount(int[] arr, int count) {
            long sum = 0;
            for (int i = 0; i < count; ++i) {
                sum += arr[i];
            }
            return sum;
        }

        /// <summary>Книга: fixed (int* p = &amp;arr[0]) и p[i] в цикле.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long SumFixedIndex(int[] arr, int count) {
            long sum = 0;
            fixed (int* p = &arr[0]) {
                for (int i = 0; i < count; ++i) {
                    sum += p[i];
                }
            }
            return sum;
        }

        /// <summary>Набросок из книги дословно: fixed (int* p = arr) и указатель-итератор.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long SumFixedBook(int[] arr) {
            long sum = 0;
            fixed (int* p = arr) {
                int* end = p + arr.Length;
                for (int* it = p; it < end; ++it) {
                    sum += *it;
                }
            }
            return sum;
        }

        private static void RunFixedLoop() {
            const string id = "ch3.fixedloop";
            var arr = new int[FixedCount];
            var rnd = new Random(33);
            for (int i = 0; i < FixedCount; ++i) {
                arr[i] = rnd.Next(1000);
            }
            var count = FixedCount;
            const string note = "int[] 1M sum, managed (no Burst); ns per element";
            var r = Bench.Measure(id, Chapter, "for (i < arr.Length) sum += arr[i]", () => {
                Bench.SinkLong = SumIndexLength(arr);
            }, 3, 21, FixedCount);
            r.notes = note + "; bound is arr.Length, bounds check may be elided";
            r = Bench.Measure(id, Chapter, "for (i < count) sum += arr[i] (count passed in)", () => {
                Bench.SinkLong = SumIndexCount(arr, count);
            }, 3, 21, FixedCount);
            r.notes = note + "; bound is a separate int, bounds check stays on every access";
            r = Bench.Measure(id, Chapter, "fixed (int* p = &arr[0]) for (i < count) sum += p[i]", () => {
                Bench.SinkLong = SumFixedIndex(arr, count);
            }, 3, 21, FixedCount);
            r.notes = note + "; the book's form";
            r = Bench.Measure(id, Chapter, "fixed (int* p = arr) for (it < end) sum += *it (book sketch)", () => {
                Bench.SinkLong = SumFixedBook(arr);
            }, 3, 21, FixedCount);
            r.notes = note + "; the book's Sum(int[]) sketch verbatim";
        }
    }
}
