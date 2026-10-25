using BookBench.Rts.Step3;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

// Шаг 4 (rts_4.md), без изменений (AliveMask берется из шага 3).
namespace BookBench.Rts.Step4 {

    [BurstCompile]
    public struct CullAndBuildInstancesJob : IJobParallelFor {
        [ReadOnly] public NativeArray<float2> position;
        [ReadOnly] public NativeArray<float2> velocity;
        [ReadOnly] public NativeArray<int> hp;
        [ReadOnly] public NativeArray<ulong> aliveMask;
        [ReadOnly] public NativeArray<float4> frustumPlanes; // 6 плоскостей: xyz — нормаль, w — расстояние

        public NativeList<UnitInstance>.ParallelWriter units;
        public NativeList<BarInstance>.ParallelWriter bars;

        public float radius;
        public float barHeight;

        public void Execute(int i) {
            if (AliveMask.IsAlive(this.aliveMask, i) == false) return;

            var p = this.position[i];
            var center = new float3(p.x, 0f, p.y);

            var visible = true;
            for (int k = 0; k < 6; ++k) {
                var plane = this.frustumPlanes[k];
                visible &= math.dot(plane.xyz, center) + plane.w > -this.radius;
            }
            if (visible == false) return;

            var v = this.velocity[i];
            var rotation = quaternion.LookRotationSafe(new float3(v.x, 0f, v.y), math.up());
            this.units.AddNoResize(new UnitInstance {
                objectToWorld = float4x4.TRS(center, rotation, new float3(1f)),
            });

            var health = this.hp[i];
            if (health < UnitConfig.MaxHp) {
                this.bars.AddNoResize(new BarInstance {
                    position = center + new float3(0f, this.barHeight, 0f),
                    hp01 = math.saturate(health / (float)UnitConfig.MaxHp),
                });
            }
        }
    }
}
