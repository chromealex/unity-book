using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

// Шаг 3 (rts_3.md). Изменения для стенда помечены [bench].
namespace BookBench.Rts.Step3 {

    public class UnitManager : MonoBehaviour {

        private const int UnitsPerRoot = 256;

        public GameObject unitPrefab;
        public int unitCount = 10_000;
        // [bench] true: каждая задача Complete сразу после Schedule (замер времени каждой задачи отдельно)
        public bool completeEachJob;

        private UnitBuffers data;
        private GridInfo grid;
        private Transform[] transforms;
        private TransformAccessArray transformAccess;
        private JobHandle frameHandle;

        // [bench] доступ к данным для контрольной суммы и уборки
        public UnitBuffers Data => this.data;
        public Transform[] Transforms => this.transforms;

        // [bench] public: жизненный цикл вызывает драйвер
        public void Start() {
            var width = (int)math.ceil(UnitConfig.WorldSize / UnitConfig.CellSize);
            this.grid = new GridInfo {
                origin = new float2(-UnitConfig.WorldSize * 0.5f),
                invCellSize = 1f / UnitConfig.CellSize,
                width = width,
                height = width,
            };
            this.data = new UnitBuffers(this.unitCount, width * width);
            this.transforms = new Transform[this.unitCount];
            this.transformAccess = new TransformAccessArray(this.unitCount);

            Transform root = null;
            var half = UnitConfig.WorldSize * 0.4f;
            for (int n = 0; n < this.unitCount; ++n) {
                if (n % UnitsPerRoot == 0) root = new GameObject("Units " + n / UnitsPerRoot).transform;
                var team = (byte)(n & 1);
                var side = team == 0 ? -1f : 1f;
                // Unity.Mathematics тоже объявляет Random, поэтому имя пишем полностью
                var pos = new float2(side * half * UnityEngine.Random.value, UnityEngine.Random.Range(-half, half));
                var i = this.data.Add(pos, new float2(-side * half, pos.y), team);
                var tr = Instantiate(this.unitPrefab, root).transform;
                this.transforms[i] = tr;
                this.transformAccess.Add(tr); // индекс в TransformAccessArray == индекс юнита
            }
        }

        public void Update() {
            var tUpdate = RtsSplit.Begin(); // [bench]
            var count = this.data.count;
            var dt = Time.deltaTime;

            var tJob = RtsSplit.Begin(); // [bench]
            var handle = new CellIndexJob {
                position = this.data.position,
                unitCell = this.data.unitCell,
                grid = this.grid,
            }.Schedule(count, 256);
            handle = RtsSplit.Job(RtsSplit.JobCellIndex, tJob, handle, this.completeEachJob); // [bench]

            tJob = RtsSplit.Begin(); // [bench]
            handle = new GridBuildJob {
                unitCell = this.data.unitCell,
                aliveMask = this.data.aliveMask,
                cellStart = this.data.cellStart,
                cellItems = this.data.cellItems,
                cellCursor = this.data.cellCursor,
                count = count,
            }.Schedule(handle);
            handle = RtsSplit.Job(RtsSplit.JobGridBuild, tJob, handle, this.completeEachJob); // [bench]

            tJob = RtsSplit.Begin(); // [bench]
            handle = new MoveJob {
                position = this.data.position,
                target = this.data.target,
                aliveMask = this.data.aliveMask,
                cellStart = this.data.cellStart,
                cellItems = this.data.cellItems,
                nextPosition = this.data.nextPosition,
                velocity = this.data.velocity,
                grid = this.grid,
                dt = dt,
            }.Schedule(count, 64, handle);
            handle = RtsSplit.Job(RtsSplit.JobMove, tJob, handle, this.completeEachJob); // [bench]

            tJob = RtsSplit.Begin(); // [bench]
            handle = new CombatJob {
                position = this.data.nextPosition,
                team = this.data.team,
                aliveMask = this.data.aliveMask,
                cellStart = this.data.cellStart,
                cellItems = this.data.cellItems,
                cooldown = this.data.cooldown,
                targetEnemy = this.data.targetEnemy,
                state = this.data.state,
                hits = this.data.hits.AsParallelWriter(),
                grid = this.grid,
                dt = dt,
            }.Schedule(count, 64, handle);
            handle = RtsSplit.Job(RtsSplit.JobCombat, tJob, handle, this.completeEachJob); // [bench]

            tJob = RtsSplit.Begin(); // [bench]
            handle = new DamageMergeJob {
                hits = this.data.hits,
                hp = this.data.hp,
                aliveMask = this.data.aliveMask,
                state = this.data.state,
                flags = this.data.flags,
                died = this.data.died,
            }.Schedule(handle);
            handle = RtsSplit.Job(RtsSplit.JobDamageMerge, tJob, handle, this.completeEachJob); // [bench]

            tJob = RtsSplit.Begin(); // [bench]
            this.frameHandle = new SyncTransformsJob {
                position = this.data.nextPosition,
                velocity = this.data.velocity,
                aliveMask = this.data.aliveMask,
            }.Schedule(this.transformAccess, handle);
            this.frameHandle = RtsSplit.Job(RtsSplit.JobSyncTransforms, tJob, this.frameHandle, this.completeEachJob); // [bench]

            JobHandle.ScheduleBatchedJobs();
            RtsSplit.End(RtsSplit.UpdateTotal, tUpdate); // [bench]
        }

        public void LateUpdate() {
            var tLate = RtsSplit.Begin(); // [bench]
            this.frameHandle.Complete();
            RtsSplit.End(RtsSplit.CompleteWait, tLate); // [bench]
            tLate = RtsSplit.Begin(); // [bench]
            for (int k = 0; k < this.data.died.Length; ++k) {
                this.transforms[this.data.died[k]].gameObject.SetActive(false);
            }
            this.data.SwapPositions();
            RtsSplit.End(RtsSplit.LateRest, tLate); // [bench]
        }

        public void OnDestroy() {
            this.frameHandle.Complete();
            if (this.transformAccess.isCreated) this.transformAccess.Dispose();
            this.data?.Dispose();
            this.data = null; // [bench] повторный вызов безопасен
        }
    }
}
