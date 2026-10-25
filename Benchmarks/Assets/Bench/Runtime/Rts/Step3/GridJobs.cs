using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

// Шаг 3 (rts_3.md). FillHashGridJob — альтернатива из текста, в симуляции не используется.
namespace BookBench.Rts.Step3 {

    [BurstCompile]
    public struct FillHashGridJob : IJobParallelFor {
        [ReadOnly] public NativeArray<int> unitCell;
        public NativeParallelMultiHashMap<int, int>.ParallelWriter map;

        public void Execute(int i) {
            this.map.Add(this.unitCell[i], i);
        }
    }

    [BurstCompile]
    public struct CellIndexJob : IJobParallelFor {
        [ReadOnly] public NativeArray<float2> position;
        [WriteOnly] public NativeArray<int> unitCell;
        public GridInfo grid;

        public void Execute(int i) {
            var c = this.grid.CellOf(this.position[i]);
            this.unitCell[i] = c.y * this.grid.width + c.x;
        }
    }

    [BurstCompile]
    public struct GridBuildJob : IJob {
        [ReadOnly] public NativeArray<int> unitCell;
        [ReadOnly] public NativeArray<ulong> aliveMask;
        public NativeArray<int> cellStart;
        public NativeArray<int> cellItems;
        public NativeArray<int> cellCursor;
        public int count;

        public void Execute() {
            var cells = this.cellCursor.Length;
            for (int c = 0; c <= cells; ++c) {
                this.cellStart[c] = 0;
            }
            for (int i = 0; i < this.count; ++i) {
                if (AliveMask.IsAlive(this.aliveMask, i) == false) continue;
                ++this.cellStart[this.unitCell[i] + 1];
            }
            for (int c = 1; c <= cells; ++c) {
                this.cellStart[c] += this.cellStart[c - 1];
            }
            for (int c = 0; c < cells; ++c) {
                this.cellCursor[c] = this.cellStart[c];
            }
            for (int i = 0; i < this.count; ++i) {
                if (AliveMask.IsAlive(this.aliveMask, i) == false) continue;
                var c = this.unitCell[i];
                this.cellItems[this.cellCursor[c]] = i;
                ++this.cellCursor[c];
            }
        }
    }
}
