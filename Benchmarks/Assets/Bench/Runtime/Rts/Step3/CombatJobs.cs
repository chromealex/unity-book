using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

// Шаг 3 (rts_3.md), без изменений.
namespace BookBench.Rts.Step3 {

    [BurstCompile]
    public struct CombatJob : IJobParallelFor {
        [ReadOnly] public NativeArray<float2> position;
        [ReadOnly] public NativeArray<byte> team;
        [ReadOnly] public NativeArray<ulong> aliveMask;
        [ReadOnly] public NativeArray<int> cellStart;
        [ReadOnly] public NativeArray<int> cellItems;
        public NativeArray<float> cooldown;
        [WriteOnly] public NativeArray<int> targetEnemy;
        [WriteOnly] public NativeArray<UnitState> state;
        public NativeQueue<int>.ParallelWriter hits;
        public GridInfo grid;
        public float dt;

        public void Execute(int i) {
            if (Hint.Unlikely(AliveMask.IsAlive(this.aliveMask, i) == false)) return;

            const float attackSq = UnitConfig.AttackRadius * UnitConfig.AttackRadius;
            var pos = this.position[i];
            var myTeam = this.team[i];
            var cell = this.grid.CellOf(pos);
            var min = math.max(cell - 1, int2.zero);
            var max = math.min(cell + 1, new int2(this.grid.width - 1, this.grid.height - 1));
            var best = -1;
            var bestSq = attackSq;

            for (int y = min.y; y <= max.y; ++y) {
                for (int x = min.x; x <= max.x; ++x) {
                    var c = y * this.grid.width + x;
                    var end = this.cellStart[c + 1];
                    for (int k = this.cellStart[c]; k < end; ++k) {
                        var j = this.cellItems[k];
                        var distSq = math.lengthsq(this.position[j] - pos);
                        var better = distSq < bestSq & this.team[j] != myTeam;
                        bestSq = math.select(bestSq, distSq, better);
                        best = math.select(best, j, better);
                    }
                }
            }

            this.targetEnemy[i] = best;
            this.state[i] = best >= 0 ? UnitState.Attacking : UnitState.Moving;

            var cd = this.cooldown[i] - this.dt;
            if (best >= 0 & cd <= 0f) {
                this.hits.Enqueue(best);
                cd = UnitConfig.AttackCooldown;
            }
            this.cooldown[i] = cd;
        }
    }

    [BurstCompile]
    public struct DamageMergeJob : IJob {
        public NativeQueue<int> hits;
        public NativeArray<int> hp;
        public NativeArray<ulong> aliveMask;
        public NativeArray<UnitState> state;
        public NativeArray<UnitFlags> flags;
        public NativeList<int> died;

        public void Execute() {
            this.died.Clear();
            var list = this.hits.ToArray(Allocator.Temp);
            this.hits.Clear();

            for (int h = 0; h < list.Length; ++h) {
                this.hp[list[h]] -= UnitConfig.Damage;
            }
            for (int h = 0; h < list.Length; ++h) {
                var t = list[h];
                if (this.hp[t] > 0 || AliveMask.IsAlive(this.aliveMask, t) == false) continue;
                this.aliveMask[t >> 6] &= ~(1UL << (t & 63));
                this.flags[t] &= ~UnitFlags.Alive;
                this.state[t] = UnitState.Dead;
                this.died.Add(t);
            }
            list.Dispose();
        }
    }
}
