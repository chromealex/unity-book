using System.Runtime.InteropServices;

// Шаг 2 (rts_2.md), без изменений.
namespace BookBench.Rts.Step2 {

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct PerThreadCounters {
        [FieldOffset(64)] public int hitCount;
        [FieldOffset(68)] public int attacks;
    }
}
