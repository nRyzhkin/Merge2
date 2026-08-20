Shader "Hidden/GTA/ShadowCacheDepth"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "ShadowCacheDepth"
            ZWrite On
            ZTest LEqual
            Cull Back
            ColorMask R

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float _GTA_ShadowNear;
            float _GTA_ShadowFar;

            // Per-draw via MaterialPropertyBlock (CommandBuffer snapshots MPB).
            float4 _BaseMap_ST;
            half _Cutoff;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float depth01 : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;

                float3 positionVS = TransformWorldToView(positionWS);
                float z = -positionVS.z;
                float denom = max(_GTA_ShadowFar - _GTA_ShadowNear, 1e-4);
                output.depth01 = saturate((z - _GTA_ShadowNear) / denom);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Always clip: opaque casters use white map + cutoff ≤ 0 so they never discard.
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
                clip(a - _Cutoff);
                return half4(input.depth01, 0, 0, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
