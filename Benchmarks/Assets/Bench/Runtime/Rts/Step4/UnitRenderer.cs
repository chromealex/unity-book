using System;
using BookBench.Rts.Step3;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

// Шаг 4 (rts_4.md). UnitBuffers берется из шага 3. Изменения для стенда помечены [bench].
namespace BookBench.Rts.Step4 {

    public sealed class UnitRenderer : IDisposable {

        // Две матрицы на инстанс по умолчанию, см. документацию RenderMeshInstanced
        private const int MaxInstancesPerCall = 511;

        private readonly Mesh unitMesh;
        private readonly Mesh barMesh;
        private RenderParams unitParams;
        private RenderParams barParams;

        private readonly Plane[] managedPlanes = new Plane[6];
        private NativeArray<float4> frustumPlanes;
        private NativeList<UnitInstance> units;
        private NativeList<BarInstance> bars;

        private readonly GraphicsBuffer barBuffer;
        private readonly GraphicsBuffer barArgs;
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] argsData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        private static readonly int BarsId = Shader.PropertyToID("_Bars");

        // [bench] для контрольной суммы и замеров
        public int VisibleUnits => this.units.Length;
        public int VisibleBars => this.bars.Length;

        public UnitRenderer(Mesh unitMesh, Material unitMaterial, Mesh barMesh, Material barMaterial, int capacity) {
            this.unitMesh = unitMesh;
            this.barMesh = barMesh;
            var bounds = new Bounds(Vector3.zero, new Vector3(UnitConfig.WorldSize, 50f, UnitConfig.WorldSize));

            this.unitParams = new RenderParams(unitMaterial) {
                worldBounds = bounds,
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true,
            };

            this.barBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 16);
            this.barArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            this.barParams = new RenderParams(barMaterial) {
                worldBounds = bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                matProps = new MaterialPropertyBlock(),
            };
            this.barParams.matProps.SetBuffer(BarsId, this.barBuffer);

            this.frustumPlanes = new NativeArray<float4>(6, Allocator.Persistent);
            this.units = new NativeList<UnitInstance>(capacity, Allocator.Persistent);
            this.bars = new NativeList<BarInstance>(capacity, Allocator.Persistent);
        }

        public JobHandle Schedule(Camera camera, UnitBuffers data, JobHandle dependsOn) {
            GeometryUtility.CalculateFrustumPlanes(camera, this.managedPlanes);
            for (int k = 0; k < 6; ++k) {
                var plane = this.managedPlanes[k];
                this.frustumPlanes[k] = new float4(plane.normal, plane.distance);
            }
            this.units.Clear();
            this.bars.Clear();

            return new CullAndBuildInstancesJob {
                position = data.nextPosition,
                velocity = data.velocity,
                hp = data.hp,
                aliveMask = data.aliveMask,
                frustumPlanes = this.frustumPlanes,
                units = this.units.AsParallelWriter(),
                bars = this.bars.AsParallelWriter(),
                radius = 1f,
                barHeight = 2.2f,
            }.Schedule(data.count, 128, dependsOn);
        }

        // Вызывается после Complete, на главном потоке
        public void Draw() {
            var instances = this.units.AsArray();
            for (int start = 0; start < instances.Length; start += MaxInstancesPerCall) {
                var n = math.min(MaxInstancesPerCall, instances.Length - start);
                Graphics.RenderMeshInstanced(this.unitParams, this.unitMesh, 0, instances, n, start);
            }

            var barCount = this.bars.Length;
            if (barCount == 0) return;
            this.barBuffer.SetData(this.bars.AsArray(), 0, 0, barCount);
            this.argsData[0] = new GraphicsBuffer.IndirectDrawIndexedArgs {
                indexCountPerInstance = this.barMesh.GetIndexCount(0),
                instanceCount = (uint)barCount,
                startIndex = this.barMesh.GetIndexStart(0),
                baseVertexIndex = this.barMesh.GetBaseVertex(0),
                startInstance = 0,
            };
            this.barArgs.SetData(this.argsData);
            Graphics.RenderMeshIndirect(this.barParams, this.barMesh, this.barArgs);
        }

        public void Dispose() {
            this.barBuffer.Dispose();
            this.barArgs.Dispose();
            this.frustumPlanes.Dispose();
            this.units.Dispose();
            this.bars.Dispose();
        }
    }
}
