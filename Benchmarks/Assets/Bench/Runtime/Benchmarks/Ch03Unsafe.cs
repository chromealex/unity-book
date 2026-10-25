using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace BookBench {

    /// <summary>Глава 3: unsafe и структуры.</summary>
    public static unsafe class Ch03Unsafe {

        private const string Chapter = "3";

        public static void Run() {
            RunSizes();
            RunMemCpy();
            RunPointerWrite();
            RunHasFlag();
            RunDictionaryKeys();
        }

        // ---------- sizes ----------

        public struct MyStruct {
            public int a;
            public byte b;
            public int c;
            public byte d;
        }

        public struct MyStructReordered {
            public int a;
            public int c;
            public byte b;
            public byte d;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct MyStructPack1 {
            public int a;
            public byte b;
            public int c;
            public byte d;
        }

        // Из шага 1 сквозного примера
        public enum UnitStateB : byte {
            Moving = 0,
        }

        public enum UnitFlagsB : byte {
            None = 0,
        }

        public struct UnitAosBad {
            public byte team;
            public UnityEngine.Vector2 position;
            public UnitStateB state;
            public float cooldown;
            public UnitFlagsB flags;
            public int hp;
            public UnityEngine.Vector2 velocity;
            public UnityEngine.Vector2 target;
        }

        public struct UnitAos {
            public UnityEngine.Vector2 position;
            public UnityEngine.Vector2 velocity;
            public UnityEngine.Vector2 target;
            public float cooldown;
            public int hp;
            public byte team;
            public UnitStateB state;
            public UnitFlagsB flags;
        }

        private static void RunSizes() {
            Bench.Info("ch3.sizeof", Chapter, "MyStruct {int,byte,int,byte} default", UnsafeUtility.SizeOf<MyStruct>(), "bytesPerOp = size in bytes; sizeof=" + sizeof(MyStruct));
            Bench.Info("ch3.sizeof", Chapter, "MyStruct reordered {int,int,byte,byte}", UnsafeUtility.SizeOf<MyStructReordered>(), "bytesPerOp = size in bytes; sizeof=" + sizeof(MyStructReordered));
            Bench.Info("ch3.sizeof", Chapter, "MyStruct Pack=1", UnsafeUtility.SizeOf<MyStructPack1>(), "bytesPerOp = size in bytes; sizeof=" + sizeof(MyStructPack1));
            Bench.Info("ch3.sizeof", Chapter, "rts UnitAosBad", UnsafeUtility.SizeOf<UnitAosBad>(), "bytesPerOp = size in bytes; book says 44");
            Bench.Info("ch3.sizeof", Chapter, "rts UnitAos", UnsafeUtility.SizeOf<UnitAos>(), "bytesPerOp = size in bytes; book says 36");
        }

        // ---------- MemCpy vs MemMove ----------

        private const int MemSize = 1 << 20;

        private static void RunMemCpy() {
            var src = (byte*)UnsafeUtility.Malloc(MemSize, 64, Allocator.Persistent);
            var dst = (byte*)UnsafeUtility.Malloc(MemSize, 64, Allocator.Persistent);
            try {
                for (int i = 0; i < MemSize; ++i) {
                    src[i] = (byte)i;
                }
                const int repeats = 50;
                Bench.Measure("ch3.memcpy", Chapter, "UnsafeUtility.MemCpy 1 MB", () => {
                    for (int r = 0; r < repeats; ++r) {
                        UnsafeUtility.MemCpy(dst, src, MemSize);
                    }
                    Bench.SinkInt = dst[MemSize - 1];
                }, 3, 21, repeats);
                Bench.Measure("ch3.memcpy", Chapter, "UnsafeUtility.MemMove 1 MB (non-overlapping)", () => {
                    for (int r = 0; r < repeats; ++r) {
                        UnsafeUtility.MemMove(dst, src, MemSize);
                    }
                    Bench.SinkInt = dst[MemSize - 1];
                }, 3, 21, repeats);
            } finally {
                UnsafeUtility.Free(src, Allocator.Persistent);
                UnsafeUtility.Free(dst, Allocator.Persistent);
            }
        }

        // ---------- *(T*)ptr vs byte-by-byte ----------

        private const int WriteCount = 1 << 18; // 256K int -> 1 MB

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WritePointer(byte* buffer, int count) {
            for (int i = 0; i < count; ++i) {
                *(int*)(buffer + i * 4) = i;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteBytes(byte* buffer, int count) {
            for (int i = 0; i < count; ++i) {
                var p = buffer + i * 4;
                // как BitConverter/ручная сериализация: побайтно, little endian
                p[0] = (byte)i;
                p[1] = (byte)(i >> 8);
                p[2] = (byte)(i >> 16);
                p[3] = (byte)(i >> 24);
            }
        }

        private static void RunPointerWrite() {
            var buffer = (byte*)UnsafeUtility.Malloc(WriteCount * 4, 64, Allocator.Persistent);
            try {
                Bench.Measure("ch3.ptrwrite", Chapter, "*(int*)ptr = value", () => {
                    WritePointer(buffer, WriteCount);
                    Bench.SinkInt = buffer[100];
                }, 3, 21, WriteCount);
                Bench.Measure("ch3.ptrwrite", Chapter, "byte-by-byte (shift & mask)", () => {
                    WriteBytes(buffer, WriteCount);
                    Bench.SinkInt = buffer[100];
                }, 3, 21, WriteCount);
                var managed = new byte[WriteCount * 4];
                Bench.Measure("ch3.ptrwrite", Chapter, "BitConverter.TryWriteBytes(Span)", () => {
                    var span = new Span<byte>(managed);
                    for (int i = 0; i < WriteCount; ++i) {
                        BitConverter.TryWriteBytes(span.Slice(i * 4, 4), i);
                    }
                    Bench.SinkInt = managed[100];
                }, 3, 21, WriteCount);
            } finally {
                UnsafeUtility.Free(buffer, Allocator.Persistent);
            }
        }

        // ---------- HasFlag vs mask ----------

        [Flags]
        public enum Abilities {
            None = 0,
            Fly = 1 << 0,
            Swim = 1 << 1,
            Run = 1 << 2,
            Climb = 1 << 3,
        }

        private const int FlagCount = 1 << 16;
        private static readonly Abilities[] FlagData = CreateFlags();

        private static Abilities[] CreateFlags() {
            var rnd = new Random(4);
            var result = new Abilities[FlagCount];
            for (int i = 0; i < FlagCount; ++i) {
                result[i] = (Abilities)rnd.Next(16);
            }
            return result;
        }

        private static void RunHasFlag() {
            Bench.Measure("ch3.hasflag", Chapter, "Enum.HasFlag", () => {
                var count = 0;
                var data = FlagData;
                for (int i = 0; i < data.Length; ++i) {
                    if (data[i].HasFlag(Abilities.Swim)) ++count;
                }
                Bench.SinkInt = count;
            }, 3, 21, FlagCount);
            Bench.Measure("ch3.hasflag", Chapter, "(flags & mask) != 0", () => {
                var count = 0;
                var data = FlagData;
                for (int i = 0; i < data.Length; ++i) {
                    if ((data[i] & Abilities.Swim) != 0) ++count;
                }
                Bench.SinkInt = count;
            }, 3, 21, FlagCount);
            var flags = Abilities.Fly | Abilities.Swim;
            Bench.MeasureAlloc("ch3.hasflag.alloc", Chapter, "Enum.HasFlag", () => Bench.SinkInt = flags.HasFlag(Abilities.Swim) ? 1 : 0, 10_000);
        }

        // ---------- Dictionary<long,int> vs Dictionary<(int,int),int> ----------

        private const int DictSize = 10_000;
        private const int Lookups = 200_000;

        private static void RunDictionaryKeys() {
            var rnd = new Random(5);
            var longDict = new Dictionary<long, int>(DictSize);
            var tupleDict = new Dictionary<(int, int), int>(DictSize);
            var keysA = new int[DictSize];
            var keysB = new int[DictSize];
            for (int i = 0; i < DictSize; ++i) {
                int a;
                int b;
                do {
                    a = rnd.Next(1, 100_000);
                    b = rnd.Next(1, 100_000);
                } while (tupleDict.ContainsKey((a, b)));
                keysA[i] = a;
                keysB[i] = b;
                longDict[((long)a << 32) | (uint)b] = i;
                tupleDict[(a, b)] = i;
            }
            var order = new int[Lookups];
            for (int i = 0; i < Lookups; ++i) {
                order[i] = rnd.Next(DictSize);
            }

            Bench.Measure("ch3.dictkey", Chapter, "Dictionary<long,int> (two ints packed)", () => {
                long acc = 0;
                for (int i = 0; i < Lookups; ++i) {
                    var k = order[i];
                    var key = ((long)keysA[k] << 32) | (uint)keysB[k];
                    if (longDict.TryGetValue(key, out var v)) acc += v;
                }
                Bench.SinkLong = acc;
            }, 3, 15, Lookups);
            Bench.Measure("ch3.dictkey", Chapter, "Dictionary<(int,int),int>", () => {
                long acc = 0;
                for (int i = 0; i < Lookups; ++i) {
                    var k = order[i];
                    if (tupleDict.TryGetValue((keysA[k], keysB[k]), out var v)) acc += v;
                }
                Bench.SinkLong = acc;
            }, 3, 15, Lookups);
        }
    }
}
