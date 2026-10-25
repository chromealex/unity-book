using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

// Шаг 3 (rts_3.md), без изменений.
namespace BookBench.Rts.Step3 {

    public struct GridInfo {
        public float2 origin;
        public float invCellSize;
        public int width;
        public int height;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int2 CellOf(float2 p) {
            var c = (int2)math.floor((p - this.origin) * this.invCellSize);
            return math.clamp(c, int2.zero, new int2(this.width - 1, this.height - 1));
        }
    }

    public static class AliveMask {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAlive(in NativeArray<ulong> mask, int i) {
            return (mask[i >> 6] & (1UL << (i & 63))) != 0;
        }
    }
}
