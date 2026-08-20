Shader "IslandCleaner/FoliageWind"
{
    // Compatibility alias — use GTA/Foliage (Idle CG + WorldController lighting).
    // Kept so existing material references keep compiling; prefer GTA/Foliage going forward.
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.35
        _DitherScale ("Dither Softness", Range(0.05, 1)) = 0.35
        _WindSpeed ("Wind Speed", Float) = 1.2
        _WindStrength ("Wind Strength", Float) = 0.12
        _WindFrequency ("Wind Frequency", Float) = 1.5
        _WindMinInfluence ("Wind Min Influence", Range(0, 1)) = 0.45
        _WindRadialScale ("Wind Radial Scale", Float) = 0.35
        _Wrap ("Diffuse Wrap", Range(0, 1)) = 0.5
        _ShadowFloor ("Shade Floor", Range(0, 1)) = 0.35
        _BacklightStrength ("Backlight Strength", Range(0, 2)) = 0.4
        _BacklightPower ("Backlight Power", Range(0.5, 8)) = 2.5
        _BacklightThinPower ("Thin Leaf Power", Range(0.5, 8)) = 2.0
        [HideInInspector] _BaseMap ("Base Map", 2D) = "white" {}
        [HideInInspector] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
            "IgnoreProjector" = "True"
            "DisableBatching" = "True"
        }
        Cull Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "ShaderFunctions.cginc"
            #include "Includes/GTAWorldLighting.cginc"

            float _Cutoff;
            float _DitherScale;
            float _WindSpeed;
            float _WindStrength;
            float _WindFrequency;
            float _WindMinInfluence;
            float _WindRadialScale;
            half _Wrap;
            half _ShadowFloor;
            half _BacklightStrength;
            half _BacklightPower;
            half _BacklightThinPower;

            struct foliage_v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 world_normal : TEXCOORD1;
                float3 world_pos : TEXCOORD2;
                float4 screen_pos : TEXCOORD3;
                UNITY_FOG_COORDS(4)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float ComputeInfluence(float3 localPos)
            {
                float radial = length(localPos.xz) * _WindRadialScale;
                float vertical = max(localPos.y, 0.0) * 0.25;
                return saturate(max(_WindMinInfluence, radial + vertical));
            }

            float2 ComputeWindOffset(float3 worldPos, float influence)
            {
                float time = _Time.y * _WindSpeed;
                float phase = dot(worldPos.xz, float2(1.1, 0.85)) * _WindFrequency;
                float waveA = sin(phase + time);
                float waveB = sin(phase * 1.73 + time * 1.35) * 0.45;
                float wind = (waveA + waveB) * _WindStrength * influence;
                return float2(wind, wind * 0.35);
            }

            foliage_v2f vert(VertexData input)
            {
                foliage_v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 localPos = input.vertex.xyz;
                float3 worldPos = mul(unity_ObjectToWorld, input.vertex).xyz;
                float influence = ComputeInfluence(localPos);
                float2 windOffset = ComputeWindOffset(worldPos, influence);
                worldPos.x += windOffset.x;
                worldPos.z += windOffset.y;

                output.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.world_normal = UnityObjectToWorldNormal(input.normal);
                output.world_pos = worldPos;
                output.screen_pos = ComputeScreenPos(output.pos);
                UNITY_TRANSFER_FOG(output, output.pos);
                return output;
            }

            half3 ApplyFoliageLighting(half3 albedo, float3 worldNormal, float3 worldPos)
            {
                float3 normal = normalize(worldNormal);
                float3 sunDir = normalize(_GTA_SunDirection.xyz);
                half3 sunCol = (half3)_GTA_SunColor.rgb;
                half NdotL = saturate(dot(normal, sunDir));
                half wrap = saturate(_Wrap);
                half wrapped = saturate((NdotL + wrap) / max(1.0h + wrap, 1e-3h));
                half shadow = GtaSampleShadowCache(worldPos);
                half3 skyZ = saturate((half3)_GTA_SkyZenith.rgb);
                half3 skyH = saturate((half3)_GTA_SkyHorizon.rgb);
                half3 openShade = max((skyZ * 0.55h + skyH * 0.45h) * 0.32h, half3(0.06h, 0.07h, 0.11h));
                half3 local = GtaSampleLightClusters(worldPos);
                half3 lit = sunCol * wrapped * shadow + local + openShade * saturate(_ShadowFloor) * (1.0h - shadow * 0.35h);
                half3 color = albedo * lit;
                if (_BacklightStrength > 1e-4h)
                {
                    float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                    half scatter = saturate(dot(normal, -sunDir)) * saturate(dot(viewDir, sunDir));
                    scatter *= pow(1.0 - saturate(dot(normal, viewDir)), _BacklightThinPower);
                    scatter = pow(scatter, _BacklightPower) * _BacklightStrength;
                    color += sunCol * albedo * scatter * max(shadow, 0.35h);
                }
                return GtaApplyFog(color, worldPos);
            }

            half ComputeDitherAlpha(half texAlpha)
            {
                half scale = max(_DitherScale, 0.05h);
                return saturate((texAlpha - (_Cutoff - scale * 0.5h)) / scale);
            }

            fixed4 frag(foliage_v2f input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 mainTex = tex2D(_MainTex, input.uv);
                GtaDitherClip(input.screen_pos, ComputeDitherAlpha(mainTex.a));
                half3 color = ApplyFoliageLighting(mainTex.rgb * INST(_Color), input.world_normal, input.world_pos);
                UNITY_APPLY_FOG(input.fogCoord, color);
                return fixed4(color, mainTex.a);
            }
            ENDCG
        }
    }
    Fallback "GTA/Foliage"
}
