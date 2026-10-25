using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

// Шаг 2 (rts_2.md): менеджер шага 1 + три изменения из книги. Изменения для стенда помечены [bench].
namespace BookBench.Rts.Step2 {

    public class UnitManager : MonoBehaviour {

        private static readonly ProfilerMarker GridMarker = new ProfilerMarker("RTS.Grid");
        private static readonly ProfilerMarker MoveMarker = new ProfilerMarker("RTS.Move");
        private static readonly ProfilerMarker CombatMarker = new ProfilerMarker("RTS.Combat");
        private static readonly ProfilerMarker SyncMarker = new ProfilerMarker("RTS.SyncTransforms");

        public GameObject unitPrefab;
        public int unitCount = 10_000;
        // [bench] -1 = как в книге: processorCount - 2 (минимум 1)
        public int backgroundThreads = -1;

        private UnitData data;
        private SpatialGrid grid;
        private Transform[] transforms;
        private readonly List<int> died = new List<int>(256);
        private WorkerPool pool;
        private ParallelSimulation simulation;

        // [bench] доступ к данным для контрольной суммы и уборки
        public UnitData Data => this.data;
        public Transform[] Transforms => this.transforms;
        public int WorkerCount => this.pool != null ? this.pool.WorkerCount : 0;

        // [bench] public: жизненный цикл вызывает драйвер
        public void Start() {
            this.data = new UnitData(this.unitCount);
            this.grid = new SpatialGrid(UnitConfig.WorldSize, UnitConfig.CellSize, this.unitCount);
            this.transforms = new Transform[this.unitCount];

            var half = UnitConfig.WorldSize * 0.4f;
            for (int n = 0; n < this.unitCount; ++n) {
                var team = (byte)(n & 1);
                var side = team == 0 ? -1f : 1f;
                var pos = new Vector2(side * half * Random.value, Random.Range(-half, half));
                var target = new Vector2(-side * half, pos.y);
                var i = this.data.Add(pos, target, team);
                this.transforms[i] = Instantiate(this.unitPrefab).transform;
            }

            var threads = this.backgroundThreads >= 0 ? this.backgroundThreads : Mathf.Max(SystemInfo.processorCount - 2, 1);
            this.pool = new WorkerPool(threads);
            this.simulation = new ParallelSimulation(this.data, this.grid, this.pool);
        }

        public void OnDestroy() {
            this.pool?.Dispose();
            this.pool = null; // [bench] повторный вызов безопасен
        }

        public void Update() {
            var dt = Time.deltaTime;
            using (GridMarker.Auto()) {
                var tSplit = RtsSplit.Begin(); // [bench]
                this.grid.Build(this.data);
                RtsSplit.End(RtsSplit.Grid, tSplit); // [bench]
            }
            using (MoveMarker.Auto()) {
                var tSplit = RtsSplit.Begin(); // [bench]
                this.simulation.Move(dt);
                RtsSplit.End(RtsSplit.Move, tSplit); // [bench]
            }
            using (CombatMarker.Auto()) {
                var tSplit = RtsSplit.Begin(); // [bench]
                this.died.Clear();
                this.simulation.Combat(dt, this.died);
                RtsSplit.End(RtsSplit.Combat, tSplit); // [bench]
            }
            using (SyncMarker.Auto()) {
                var tSplit = RtsSplit.Begin(); // [bench]
                // как в шаге 1
                for (int k = 0; k < this.died.Count; ++k) {
                    this.transforms[this.died[k]].gameObject.SetActive(false);
                }
                for (int i = 0; i < this.data.count; ++i) {
                    if (this.data.IsAlive(i) == false) continue;
                    var p = this.data.position[i];
                    var v = this.data.velocity[i];
                    var rotation = v.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(new Vector3(v.x, 0f, v.y)) : this.transforms[i].rotation;
                    this.transforms[i].SetPositionAndRotation(new Vector3(p.x, 0f, p.y), rotation);
                }
                RtsSplit.End(RtsSplit.Sync, tSplit); // [bench]
            }
        }
    }
}
