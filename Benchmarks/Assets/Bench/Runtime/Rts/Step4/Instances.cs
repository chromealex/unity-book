using Unity.Mathematics;
using UnityEngine;

// Шаг 4 (rts_4.md), без изменений.
namespace BookBench.Rts.Step4 {

    public struct UnitInstance {
        public Matrix4x4 objectToWorld;
    }

    // 16 байт, раскладка совпадает с HLSL-структурой в шейдере
    public struct BarInstance {
        public float3 position;
        public float hp01;
    }
}
