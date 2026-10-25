using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Jobs;

// Шаг 3 (rts_3.md), без изменений.
namespace BookBench.Rts.Step3 {

    [BurstCompile]
    public struct SyncTransformsJob : IJobParallelForTransform {
        [ReadOnly] public NativeArray<float2> position;
        [ReadOnly] public NativeArray<float2> velocity;
        [ReadOnly] public NativeArray<ulong> aliveMask;

        public void Execute(int index, TransformAccess transform) {
            if (AliveMask.IsAlive(this.aliveMask, index) == false) return;
            var p = this.position[index];
            transform.position = new float3(p.x, 0f, p.y);
            var v = this.velocity[index];
            if (math.lengthsq(v) > 1e-6f) transform.rotation = quaternion.LookRotationSafe(new float3(v.x, 0f, v.y), math.up());
        }
    }
}
