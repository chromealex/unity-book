Shader "RTS/HealthBar" {
    Properties {
        _BarSize ("Bar Size", Vector) = (1.2, 0.15, 0, 0)
    }
    SubShader {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass {
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"

            struct BarInstance {
                float3 position;
                float hp01;
            };

            StructuredBuffer<BarInstance> _Bars;
            float4 _BarSize;

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float hp : TEXCOORD1;
            };

            v2f vert(appdata_base v, uint svInstanceID : SV_InstanceID) {
                InitIndirectDrawArgs(0);
                uint id = GetIndirectInstanceID(svInstanceID);
                BarInstance bar = _Bars[id];

                // Билборд: оси камеры из строк матрицы вида
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 world = bar.position + right * (v.vertex.x * _BarSize.x) + up * (v.vertex.y * _BarSize.y);

                v2f o;
                o.pos = mul(UNITY_MATRIX_VP, float4(world, 1.0));
                o.uv = v.texcoord.xy;
                o.hp = bar.hp01;
                return o;
            }

            half4 frag(v2f i) : SV_Target {
                return i.uv.x < i.hp ? half4(0.2, 0.9, 0.2, 1.0) : half4(0.1, 0.1, 0.1, 0.7);
            }
            ENDHLSL
        }
    }
}
