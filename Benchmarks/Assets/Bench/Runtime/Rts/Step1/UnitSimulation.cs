using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// Шаг 1 (rts_1.md), без изменений.
namespace BookBench.Rts.Step1 {

    public static class UnitSimulation {

        public static void Move(UnitData data, SpatialGrid grid, float dt) {
            var sepSq = UnitConfig.SeparationRadius * UnitConfig.SeparationRadius;
            var words = data.aliveMask.Length;

            for (int w = 0; w < words; ++w) {
                var bits = data.aliveMask[w];
                while (bits != 0) {
                    var i = (w << 6) + math.tzcnt(bits);
                    bits &= bits - 1; // сбрасываем младший установленный бит

                    var pos = data.position[i];
                    var separation = Vector2.zero;
                    var cx = grid.CellX(pos.x);
                    var cy = grid.CellY(pos.y);

                    for (int y = math.max(cy - 1, 0); y <= math.min(cy + 1, grid.height - 1); ++y) {
                        for (int x = math.max(cx - 1, 0); x <= math.min(cx + 1, grid.width - 1); ++x) {
                            var c = y * grid.width + x;
                            for (int k = grid.cellStart[c]; k < grid.cellStart[c + 1]; ++k) {
                                var j = grid.cellItems[k];
                                var d = pos - data.position[j];
                                var distSq = d.sqrMagnitude;
                                if (distSq < sepSq && distSq > 1e-6f) separation += d / distSq;
                            }
                        }
                    }

                    var toTarget = data.target[i] - pos;
                    var dir = toTarget.normalized + separation;
                    var velocity = dir.normalized * UnitConfig.Speed;
                    data.velocity[i] = velocity;
                    data.position[i] = pos + velocity * dt;
                }
            }
        }

        public static void Combat(UnitData data, SpatialGrid grid, float dt, List<int> died) {
            var attackSq = UnitConfig.AttackRadius * UnitConfig.AttackRadius;

            for (int i = 0; i < data.count; ++i) {
                if (data.IsAlive(i) == false) continue;
                data.cooldown[i] -= dt;

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
                data.hp[best] -= UnitConfig.Damage;
                if (data.hp[best] <= 0) {
                    data.Kill(best);
                    died.Add(best);
                }
            }
        }
    }
}
