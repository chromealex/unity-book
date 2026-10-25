using System.Threading;
using Unity.Mathematics;
using UnityEngine;

// Шаг 2 (rts_2.md). Изменения для стенда помечены [bench] (только замеры времени).
namespace BookBench.Rts.Step2 {

    public sealed class ParallelSimulation {

        public const int ChunkSize = 256;

        private readonly UnitData data;
        private readonly SpatialGrid grid;
        private readonly WorkerPool pool;

        private readonly int[][] hitBuffers;
        private readonly PerThreadCounters[] counters;

        // Делегаты создаются один раз, иначе каждый Run аллоцировал бы
        private readonly System.Action<int> moveJob;
        private readonly System.Action<int> combatJob;

        private float dt;
        private int chunkCount;
        private int nextChunk;

        public ParallelSimulation(UnitData data, SpatialGrid grid, WorkerPool pool) {
            this.data = data;
            this.grid = grid;
            this.pool = pool;
            this.hitBuffers = new int[pool.WorkerCount][];
            for (int w = 0; w < this.hitBuffers.Length; ++w) {
                // Один юнит дает не больше одного попадания за кадр
                this.hitBuffers[w] = new int[data.capacity];
            }
            this.counters = new PerThreadCounters[pool.WorkerCount];
            this.moveJob = this.MoveWorker;
            this.combatJob = this.CombatWorker;
        }

        public void Move(float dt) {
            this.dt = dt;
            this.Dispatch(this.moveJob);
            this.data.SwapPositions();
        }

        public void Combat(float dt, System.Collections.Generic.List<int> died) {
            this.dt = dt;
            for (int w = 0; w < this.counters.Length; ++w) {
                this.counters[w].hitCount = 0;
                this.counters[w].attacks = 0;
            }
            var tDispatch = RtsSplit.Begin(); // [bench]
            this.Dispatch(this.combatJob);
            RtsSplit.End(RtsSplit.CombatDispatch, tDispatch); // [bench]
            var tMerge = RtsSplit.Begin(); // [bench]
            this.MergeDamage(died);
            RtsSplit.End(RtsSplit.Merge, tMerge); // [bench]
        }

        private void Dispatch(System.Action<int> job) {
            this.chunkCount = (this.data.count + ChunkSize - 1) / ChunkSize;
            this.nextChunk = 0;
            this.pool.Run(job);
        }

        private bool TryTakeChunk(out int from, out int to) {
            var chunk = Interlocked.Increment(ref this.nextChunk) - 1;
            from = chunk * ChunkSize;
            to = math.min(from + ChunkSize, this.data.count);
            return chunk < this.chunkCount;
        }

        private void MoveWorker(int worker) {
            var data = this.data;
            var grid = this.grid;
            var read = data.position;      // только чтение
            var write = data.nextPosition; // только свои индексы
            var sepSq = UnitConfig.SeparationRadius * UnitConfig.SeparationRadius;

            while (this.TryTakeChunk(out var from, out var to)) {
                for (int i = from; i < to; ++i) {
                    var pos = read[i];
                    if (data.IsAlive(i) == false) {
                        write[i] = pos;
                        continue;
                    }
                    var separation = Vector2.zero;
                    var cx = grid.CellX(pos.x);
                    var cy = grid.CellY(pos.y);
                    for (int y = math.max(cy - 1, 0); y <= math.min(cy + 1, grid.height - 1); ++y) {
                        for (int x = math.max(cx - 1, 0); x <= math.min(cx + 1, grid.width - 1); ++x) {
                            var c = y * grid.width + x;
                            for (int k = grid.cellStart[c]; k < grid.cellStart[c + 1]; ++k) {
                                var d = pos - read[grid.cellItems[k]];
                                var distSq = d.sqrMagnitude;
                                if (distSq < sepSq && distSq > 1e-6f) separation += d / distSq;
                            }
                        }
                    }
                    var dir = (data.target[i] - pos).normalized + separation;
                    var velocity = dir.normalized * UnitConfig.Speed;
                    data.velocity[i] = velocity;
                    write[i] = pos + velocity * this.dt;
                }
            }
        }

        private void CombatWorker(int worker) {
            var data = this.data;
            var grid = this.grid;
            var hits = this.hitBuffers[worker];
            var attackSq = UnitConfig.AttackRadius * UnitConfig.AttackRadius;
            var hitCount = 0;
            var attacks = 0;

            while (this.TryTakeChunk(out var from, out var to)) {
                for (int i = from; i < to; ++i) {
                    if (data.IsAlive(i) == false) continue;
                    data.cooldown[i] -= this.dt;

                    var pos = data.position[i];
                    var myTeam = data.team[i];
                    var best = -1;
                    var bestSq = attackSq;
                    var cx = grid.CellX(pos.x);
                    var cy = grid.CellY(pos.y);
                    for (int y = math.max(cy - 1, 0); y <= math.min(cy + 1, grid.height - 1); ++y) {
                        for (int x = math.max(cx - 1, 0); x <= math.min(cx + 1, grid.width - 1); ++x) {
                            var c = y * grid.width + x;
                            for (int k = grid.cellStart[c]; k < grid.cellStart[c + 1]; ++k) {
                                var j = grid.cellItems[k];
                                if (data.team[j] == myTeam || data.IsAlive(j) == false) continue;
                                var distSq = (data.position[j] - pos).sqrMagnitude;
                                if (distSq < bestSq) {
                                    bestSq = distSq;
                                    best = j;
                                }
                            }
                        }
                    }

                    data.targetEnemy[i] = best;
                    data.state[i] = best >= 0 ? UnitState.Attacking : UnitState.Moving;
                    if (best < 0 || data.cooldown[i] > 0f) continue;

                    data.cooldown[i] = UnitConfig.AttackCooldown;
                    hits[hitCount++] = best; // в свой буфер, не в чужой hp
                    ++attacks;
                }
            }

            // Пишем в общий массив один раз, в конце, и в свою кэш-линию
            this.counters[worker].hitCount = hitCount;
            this.counters[worker].attacks = attacks;
        }

        private void MergeDamage(System.Collections.Generic.List<int> died) {
            for (int w = 0; w < this.hitBuffers.Length; ++w) {
                var hits = this.hitBuffers[w];
                var count = this.counters[w].hitCount;
                for (int h = 0; h < count; ++h) {
                    this.data.hp[hits[h]] -= UnitConfig.Damage;
                }
            }
            for (int w = 0; w < this.hitBuffers.Length; ++w) {
                var hits = this.hitBuffers[w];
                var count = this.counters[w].hitCount;
                for (int h = 0; h < count; ++h) {
                    var target = hits[h];
                    if (this.data.hp[target] > 0 || this.data.IsAlive(target) == false) continue;
                    this.data.Kill(target);
                    died.Add(target);
                }
            }
        }
    }
}
