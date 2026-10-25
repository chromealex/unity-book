using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

// Шаг 3 (rts_3.md), без изменений.
namespace BookBench.Rts.Step3 {

    [BurstCompile]
    public struct MoveJob : IJobParallelFor {
        [ReadOnly] public NativeArray<float2> position;
        [ReadOnly] public NativeArray<float2> target;
        [ReadOnly] public NativeArray<ulong> aliveMask;
        [ReadOnly] public NativeArray<int> cellStart;
        [ReadOnly] public NativeArray<int> cellItems;
        [WriteOnly] public NativeArray<float2> nextPosition;
        [WriteOnly] public NativeArray<float2> velocity;
        public GridInfo grid;
        public float dt;

        public void Execute(int i) {
            var pos = this.position[i];
            if (Hint.Unlikely(AliveMask.IsAlive(this.aliveMask, i) == false)) {
                this.nextPosition[i] = pos;
                this.velocity[i] = float2.zero;
                return;
            }

            const float sepSq = UnitConfig.SeparationRadius * UnitConfig.SeparationRadius;
            var cell = this.grid.CellOf(pos);
            var min = math.max(cell - 1, int2.zero);
            var max = math.min(cell + 1, new int2(this.grid.width - 1, this.grid.height - 1));
            var separation = float2.zero;

            for (int y = min.y; y <= max.y; ++y) {
                for (int x = min.x; x <= max.x; ++x) {
                    var c = y * this.grid.width + x;
                    var end = this.cellStart[c + 1];
                    for (int k = this.cellStart[c]; k < end; ++k) {
                        var d = pos - this.position[this.cellItems[k]];
                        var distSq = math.lengthsq(d);
                        // Для себя distSq == 0, d / 0 даст NaN, но select его отбросит
                        separation += math.select(float2.zero, d / distSq, distSq < sepSq & distSq > 1e-6f);
                    }
                }
            }

            var dir = math.normalizesafe(this.target[i] - pos) + separation;
            var v = math.normalizesafe(dir) * UnitConfig.Speed;
            this.velocity[i] = v;
            this.nextPosition[i] = pos + v * this.dt;
        }
    }
}
