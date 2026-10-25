using BookBench.Rts.Step3;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

// Шаг 4 (rts_4.md). Пропущенные в книге части ("как в шаге 3") восстановлены из шага 3.
// Изменения для стенда помечены [bench].
namespace BookBench.Rts.Step4 {

    public class UnitManager : MonoBehaviour {

        public Mesh unitMesh;
        public Material unitMaterial; // любой шейдер с включенным Enable GPU Instancing
        public Mesh barMesh;          // стандартный Quad
        public Material barMaterial;  // шейдер RTS/HealthBar
        public Camera mainCamera;
        public int unitCount = 10_000;

        private UnitBuffers data;
        private GridInfo grid;
        private UnitRenderer unitRenderer;
        private JobHandle frameHandle;

        // [bench] доступ для контрольной суммы и замеров
        public UnitBuffers Data => this.data;
        public UnitRenderer Renderer => this.unitRenderer;
        public double LastDrawNs;

        // [bench] public: жизненный цикл вызывает драйвер
        public void Start() {
            // как в шаге 3, но без Instantiate
            var width = (int)math.ceil(UnitConfig.WorldSize / UnitConfig.CellSize);
            this.grid = new GridInfo {
                origin = new float2(-UnitConfig.WorldSize * 0.5f),
                invCellSize = 1f / UnitConfig.CellSize,
                width = width,
                height = width,
            };
            this.data = new UnitBuffers(this.unitCount, width * width);

            var half = UnitConfig.WorldSize * 0.4f;
            for (int n = 0; n < this.unitCount; ++n) {
                var team = (byte)(n & 1);
                var side = team == 0 ? -1f : 1f;
                var pos = new float2(side * half * UnityEngine.Random.value, UnityEngine.Random.Range(-half, half));
                this.data.Add(pos, new float2(-side * half, pos.y), team);
            }

            this.unitRenderer = new UnitRenderer(this.unitMesh, this.unitMaterial, this.barMesh, this.barMaterial, this.unitCount);
        }

        public void Update() {
            // CellIndexJob, GridBuildJob, MoveJob, CombatJob, DamageMergeJob как в шаге 3
            var tUpdate = RtsSplit.Begin(); // [bench]
            var count = this.data.count;
            var dt = Time.deltaTime;

            var handle = new CellIndexJob {
                position = this.data.position,
                unitCell = this.data.unitCell,
                grid = this.grid,
            }.Schedule(count, 256);

            handle = new GridBuildJob {
                unitCell = this.data.unitCell,
                aliveMask = this.data.aliveMask,
                cellStart = this.data.cellStart,
                cellItems = this.data.cellItems,
                cellCursor = this.data.cellCursor,
                count = count,
            }.Schedule(handle);

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

            handle = new DamageMergeJob {
                hits = this.data.hits,
                hp = this.data.hp,
                aliveMask = this.data.aliveMask,
                state = this.data.state,
                flags = this.data.flags,
                died = this.data.died,
            }.Schedule(handle);

            this.frameHandle = this.unitRenderer.Schedule(this.mainCamera, this.data, handle);
            JobHandle.ScheduleBatchedJobs();
            RtsSplit.End(RtsSplit.UpdateTotal, tUpdate); // [bench]
        }

        public void LateUpdate() {
            var tLate = RtsSplit.Begin(); // [bench]
            this.frameHandle.Complete();
            RtsSplit.End(RtsSplit.CompleteWait, tLate); // [bench]
            tLate = RtsSplit.Begin(); // [bench]
            // [bench] отдельный замер стоимости подачи вызовов отрисовки
            var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            this.unitRenderer.Draw();
            this.LastDrawNs = BookBench.Bench.TicksToNs(System.Diagnostics.Stopwatch.GetTimestamp() - t0);
            this.data.SwapPositions();
            RtsSplit.End(RtsSplit.LateRest, tLate); // [bench]
        }

        public void OnDestroy() {
            this.frameHandle.Complete();
            this.unitRenderer?.Dispose();
            this.data?.Dispose();
            this.unitRenderer = null; // [bench] повторный вызов безопасен
            this.data = null;
        }
    }
}
