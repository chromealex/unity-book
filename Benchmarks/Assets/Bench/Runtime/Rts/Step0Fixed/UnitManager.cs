using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Pool;

// Шаг 0, исправленная версия (rts_0.md). Изменения для стенда помечены [bench].
namespace BookBench.Rts.Step0Fixed {

    public class UnitManager : MonoBehaviour {

        private static readonly ProfilerMarker MoveMarker = new ProfilerMarker("RTS.Move");
        private static readonly ProfilerMarker CombatMarker = new ProfilerMarker("RTS.Combat");
        private static readonly ProfilerMarker CleanupMarker = new ProfilerMarker("RTS.Cleanup");
        private static readonly ProfilerMarker LabelsMarker = new ProfilerMarker("RTS.Labels");

        private static readonly string[] HpStrings = BuildHpStrings();

        public readonly List<Unit> units = new List<Unit>(10_000);

        private static string[] BuildHpStrings() {
            var result = new string[UnitConfig.MaxHp + 1];
            for (int i = 0; i < result.Length; ++i) {
                result[i] = "HP: " + i;
            }
            return result;
        }

        public void Register(Unit unit) {
            this.units.Add(unit);
        }

        // [bench] public: кадр вызывает драйвер
        public void Update() {
            var dt = Time.deltaTime;
            var count = this.units.Count;
            var sepSq = UnitConfig.SeparationRadius * UnitConfig.SeparationRadius;
            var attackSq = UnitConfig.AttackRadius * UnitConfig.AttackRadius;

            using (MoveMarker.Auto()) {
                var tSplit = RtsSplit.Begin(); // [bench]
                for (int i = 0; i < count; ++i) {
                    var unit = this.units[i];
                    var tr = unit.transform;
                    var pos = tr.position;
                    var separation = Vector3.zero;
                    for (int j = 0; j < count; ++j) {
                        if (i == j) continue;
                        var d = pos - this.units[j].transform.position;
                        var distSq = d.sqrMagnitude;
                        if (distSq < sepSq && distSq > 1e-6f) separation += d / distSq;
                    }
                    var dir = (unit.target - pos).normalized + separation;
                    tr.position = pos + dir.normalized * (UnitConfig.Speed * dt);
                }
                RtsSplit.End(RtsSplit.Move, tSplit); // [bench]
            }

            using (CombatMarker.Auto()) {
                var tSplit = RtsSplit.Begin(); // [bench]
                for (int i = 0; i < count; ++i) {
                    var unit = this.units[i];
                    unit.cooldown -= dt;
                    if (unit.hp <= 0 || unit.cooldown > 0f) continue;

                    var pos = unit.transform.position;
                    Unit best = null;
                    var bestSq = attackSq;
                    for (int j = 0; j < count; ++j) {
                        var other = this.units[j];
                        if (other.team == unit.team || other.hp <= 0) continue;
                        var distSq = (other.transform.position - pos).sqrMagnitude;
                        if (distSq < bestSq) {
                            bestSq = distSq;
                            best = other;
                        }
                    }
                    if (best != null) {
                        best.hp -= UnitConfig.Damage;
                        unit.cooldown = UnitConfig.AttackCooldown;
                    }
                }
                RtsSplit.End(RtsSplit.Combat, tSplit); // [bench]
            }

            using (CleanupMarker.Auto()) {
                var tSplit = RtsSplit.Begin(); // [bench]
                using (ListPool<Unit>.Get(out var dead)) {
                    for (int i = count - 1; i >= 0; --i) {
                        var unit = this.units[i];
                        if (unit.hp > 0) continue;
                        dead.Add(unit);
                        // swap-remove: O(1) вместо сдвига хвоста списка
                        var last = this.units.Count - 1;
                        this.units[i] = this.units[last];
                        this.units.RemoveAt(last);
                    }
                    foreach (var unit in dead) {
                        Destroy(unit.gameObject);
                    }
                }
                RtsSplit.End(RtsSplit.Cleanup, tSplit); // [bench]
            }

            using (LabelsMarker.Auto()) {
                var tSplit = RtsSplit.Begin(); // [bench]
                for (int i = 0; i < this.units.Count; ++i) {
                    var unit = this.units[i];
                    if (unit.shownHp == unit.hp) continue;
                    unit.shownHp = unit.hp;
                    unit.hpLabel.text = HpStrings[Mathf.Clamp(unit.hp, 0, UnitConfig.MaxHp)];
                }
                RtsSplit.End(RtsSplit.Labels, tSplit); // [bench]
            }
        }

        // [bench] отложенный Destroy, см. RtsDestroyQueue
        private static new void Destroy(UnityEngine.Object obj) {
            RtsDestroyQueue.Enqueue(obj);
        }
    }
}
