Shader "GTA/Foliage"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor]   _BaseColor("Color", Color) = (1, 1, 1, 1)
        _OpacityMap("Opacity (R)", 2D) = "white" {}

        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.35
        _DitherScale("Dither Softness", Range(0.05, 1)) = 0.35

        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1
        _Smoothness("Smoothness", Range(0, 1)) = 0.2
        _ReflectionStrength("Reflection Strength", Range(0, 1)) = 0.2

        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)
        _EmissionMap("Emission", 2D) = "white" {}

        _WindDirection("Wind Direction", Vector) = (1, 0, 0.3, 0)
        _WindSpeed("Wind Speed", Range(0, 5)) = 1.2
        _WindStrength("Wind Strength", Range(0, 2)) = 0.12
        _WindFrequency("Wind Frequency", Range(0, 4)) = 1.5
        _WindMinInfluence("Wind Min Influence", Range(0, 1)) = 0.45
        _WindRadialScale("Wind Radial Scale", Float) = 0.35

        _ColorVariationMin("Color Variation Min", Color) = (0.85, 0.9, 0.8, 1)
        _ColorVariationMax("Color Variation Max", Color) = (1.1, 1.05, 0.95, 1)
        _InstanceColorStrength("Instance Color Strength", Range(0, 1)) = 1

        _Wrap("Diffuse Wrap", Range(0, 1)) = 0.5
        _ShadowFloor("Shade Floor", Range(0, 1)) = 0.35
        _BacklightStrength("Backlight Strength", Range(0, 2)) = 0.4
        _BacklightPower("Backlight Power", Range(0.5, 8)) = 2.5
        _BacklightThinPower("Thin Leaf Power", Range(0.5, 8)) = 2.0

        _DistanceFadeStart("Distance Fade Start", Float) = 40
        _DistanceFadeEnd("Distance Fade End", Float) = 60

        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 1
        [Toggle(_OPACITYMAP)] _UseOpacityMap("Opacity Map", Float) = 0
        [Toggle(_WIND_ON)] _Wind("Vertex Wind", Float) = 1
        [Toggle(_NORMALMAP)] _UseNormalMap("Normal Map", Float) = 0
        [Toggle(_EMISSION)] _UseEmission("Emission", Float) = 0
        [Toggle(_DISTANCE_FADE_FAR)] _DistanceFadeFar("Fade Out Far", Float) = 0
        [Toggle(_DISTANCE_FADE_NEAR)] _DistanceFadeNear("Fade Out Near", Float) = 0
        [Toggle(_INSTANCE_COLOR)] _InstanceColor("Instance Color Variation", Float) = 1

        [HideInInspector] _QueueOffset("Queue Offset", Float) = 0
        [HideInInspector] _ShadowSoftness("", Float) = 0.85
        [HideInInspector] _ShadowContrast("", Float) = 1.35
        [HideInInspector] _ReceiveShadows("", Float) = 0

        // Idle / FoliageWind remaps
        [HideInInspector] _MainTex("Albedo", 2D) = "white" {}
        [HideInInspector] _Color("Tint", Color) = (1, 1, 1, 1)
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
        LOD 200
        Cull Off
        ZWrite On

        Pass
        {
            Name "ForwardLit"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _OPACITYMAP
            #pragma shader_feature_local _WIND_ON
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local _DISTANCE_FADE_FAR
            #pragma shader_feature_local _DISTANCE_FADE_NEAR
            #pragma shader_feature_local _INSTANCE_COLOR

            #include "UnityStandardUtils.cginc"
            #include "Includes/GTAWorldLighting.cginc"

            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            sampler2D _OpacityMap;
            sampler2D _BumpMap;
            sampler2D _EmissionMap;

            half _Cutoff;
            half _DitherScale;
            half _BumpScale;
            half _Smoothness;
            half _ReflectionStrength;
            half4 _EmissionColor;
            half4 _WindDirection;
            half _WindSpeed;
            half _WindStrength;
            half _WindFrequency;
            half _WindMinInfluence;
            half _WindRadialScale;
            half4 _ColorVariationMin;
            half4 _ColorVariationMax;
            half _InstanceColorStrength;
            half _Wrap;
            half _ShadowFloor;
            half _BacklightStrength;
            half _BacklightPower;
            half _BacklightThinPower;
            half _DistanceFadeStart;
            half _DistanceFadeEnd;

            // Idle model: breaks SRP Batcher → GPU instancing.
            UNITY_INSTANCING_BUFFER_START(GTAFoliageProps)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
            UNITY_INSTANCING_BUFFER_END(GTAFoliageProps)

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half4 color : TEXCOORD4;
                float4 screenPos : TEXCOORD5;
                UNITY_FOG_COORDS(6)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float ComputeWindInfluence(float3 localPos, half vertexAlpha)
            {
                float radial = length(localPos.xz) * _WindRadialScale;
                float vertical = max(localPos.y, 0.0) * 0.25;
                float influence = saturate(max(_WindMinInfluence, radial + vertical));
                // Prefer painted leaf-tip weight when present.
                return saturate(max(influence * 0.35, influence * vertexAlpha));
            }

            float2 ComputeWindOffset(float3 worldPos, float influence)
            {
                float3 windDir = normalize(_WindDirection.xyz + float3(1e-4, 0, 0));
                float time = _Time.y * _WindSpeed;
                float phase = dot(worldPos.xz, float2(1.1, 0.85)) * _WindFrequency;
                float waveA = sin(phase + time);
                float waveB = sin(phase * 1.73 + time * 1.35) * 0.45;
                float wind = (waveA + waveB) * _WindStrength * influence;
                return float2(windDir.x, windDir.z) * wind + float2(wind * 0.15, wind * 0.35);
            }

            half3 ProceduralColorVariation(float3 positionWS)
            {
                half h = frac(sin(dot(floor(positionWS.xz * 0.37h), half2(12.9898h, 78.233h))) * 43758.5453h);
                return lerp(_ColorVariationMin.rgb, _ColorVariationMax.rgb, h);
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float3 localPos = v.vertex.xyz;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

#if defined(_WIND_ON)
                float influence = ComputeWindInfluence(localPos, v.color.a);
                float2 windOffset = ComputeWindOffset(worldPos, influence);
                worldPos.x += windOffset.x;
                worldPos.z += windOffset.y;
#endif

                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.worldPos = worldPos;
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                half3 tangentWS = UnityObjectToWorldDir(v.tangent.xyz);
                half sign = v.tangent.w * unity_WorldTransformParams.w;
                o.tangentWS = half4(tangentWS, sign);
                o.color = v.color;
                o.screenPos = ComputeScreenPos(o.pos);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
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
                half3 openShade = (skyZ * 0.55h + skyH * 0.45h) * 0.32h;
                openShade = max(openShade, half3(0.06h, 0.07h, 0.11h));

                half floorAmt = saturate(_ShadowFloor);
                half3 shade = openShade * (0.55h + 0.45h * wrapped);
                half3 local = GtaSampleLightClusters(worldPos);
                half3 lit = sunCol * wrapped * shadow + local + shade * floorAmt * (1.0h - shadow * 0.35h);
                half3 color = albedo * lit;

                // Cheap sun specular for glossy leaves.
                if (_Smoothness > 1e-3h)
                {
                    float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos);
                    float3 halfDir = normalize(sunDir + viewDir);
                    half NdotH = saturate(dot(normal, halfDir));
                    half shininess = exp2(10.0h * saturate(_Smoothness) + 1.0h);
                    half spec = pow(NdotH, shininess) * (shininess * 0.04h + 0.5h);
                    color += sunCol * lerp(0.02h, 0.35h, _Smoothness) * spec * NdotL * shadow;
                }

                if (_BacklightStrength > 1e-4h)
                {
                    float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                    float lightBehindLeaf = saturate(dot(normal, -sunDir));
                    float lookingTowardSun = saturate(dot(viewDir, sunDir));
                    float thinLeaf = pow(1.0 - saturate(dot(normal, viewDir)), _BacklightThinPower);
                    half scatter = lightBehindLeaf * lookingTowardSun * thinLeaf;
                    scatter = pow(scatter, _BacklightPower) * _BacklightStrength;
                    color += sunCol * albedo * scatter * max(shadow, 0.35h);
                }

                return GtaApplyFog(color, worldPos);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

#if defined(LOD_FADE_CROSSFADE)
                UnityApplyDitherCrossFade(i.pos.xy);
#endif

                half4 baseColor = UNITY_ACCESS_INSTANCED_PROP(GTAFoliageProps, _BaseColor);
                half4 albedoAlpha = tex2D(_BaseMap, i.uv);
                half3 albedo = albedoAlpha.rgb * baseColor.rgb;
                half alpha = albedoAlpha.a * baseColor.a;

#if defined(_OPACITYMAP)
                alpha *= tex2D(_OpacityMap, i.uv).r;
#endif

                albedo *= i.color.rgb;

#if defined(_INSTANCE_COLOR)
                half3 variation = ProceduralColorVariation(i.worldPos);
                albedo *= lerp(half3(1, 1, 1), variation, saturate(_InstanceColorStrength));
#endif

#if defined(_ALPHATEST_ON)
                half scale = max(_DitherScale, 0.05h);
                half softStart = _Cutoff - scale * 0.5h;
                half ditherAlpha = saturate((alpha - softStart) / scale);
                GtaDitherClip(i.screenPos, ditherAlpha);
#else
                GtaDitherClip(i.screenPos, alpha);
#endif

#if defined(_DISTANCE_FADE_FAR) || defined(_DISTANCE_FADE_NEAR)
                float dist = length(i.worldPos - _WorldSpaceCameraPos.xyz);
#if defined(_DISTANCE_FADE_FAR)
                half fadeFar = 1.0h - saturate((dist - _DistanceFadeStart) / max(_DistanceFadeEnd - _DistanceFadeStart, 1e-3));
                GtaDitherClip(i.screenPos, fadeFar);
#endif
#if defined(_DISTANCE_FADE_NEAR)
                half fadeNear = saturate((dist - _DistanceFadeStart) / max(_DistanceFadeEnd - _DistanceFadeStart, 1e-3));
                GtaDitherClip(i.screenPos, fadeNear);
#endif
#endif

                half3 normalWS = normalize(i.normalWS);
#if defined(_NORMALMAP)
                half3 nTS = UnpackScaleNormal(tex2D(_BumpMap, i.uv), _BumpScale);
                half3 bitangent = i.tangentWS.w * cross(normalWS, i.tangentWS.xyz);
                half3x3 tbn = half3x3(i.tangentWS.xyz, bitangent, normalWS);
                normalWS = normalize(mul(nTS, tbn));
#endif

                half3 emission = 0;
#if defined(_EMISSION)
                emission = tex2D(_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
#endif

                half3 color = ApplyFoliageLighting(albedo, normalWS, i.worldPos);
                color += emission;

                UNITY_APPLY_FOG(i.fogCoord, color);
                return fixed4(color, alpha);
            }
            ENDCG
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull Off

            CGPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _OPACITYMAP
            #pragma shader_feature_local _WIND_ON

            #include "Includes/GTAWorldLighting.cginc"

            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            sampler2D _OpacityMap;
            half _Cutoff;
            half _DitherScale;
            half4 _WindDirection;
            half _WindSpeed;
            half _WindStrength;
            half _WindFrequency;
            half _WindMinInfluence;
            half _WindRadialScale;

            UNITY_INSTANCING_BUFFER_START(GTAFoliageDepthProps)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
            UNITY_INSTANCING_BUFFER_END(GTAFoliageDepthProps)

            struct depth_appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct depth_v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float ComputeWindInfluence(float3 localPos, half vertexAlpha)
            {
                float radial = length(localPos.xz) * _WindRadialScale;
                float vertical = max(localPos.y, 0.0) * 0.25;
                float influence = saturate(max(_WindMinInfluence, radial + vertical));
                return saturate(max(influence * 0.35, influence * vertexAlpha));
            }

            float2 ComputeWindOffset(float3 worldPos, float influence)
            {
                float3 windDir = normalize(_WindDirection.xyz + float3(1e-4, 0, 0));
                float time = _Time.y * _WindSpeed;
                float phase = dot(worldPos.xz, float2(1.1, 0.85)) * _WindFrequency;
                float waveA = sin(phase + time);
                float waveB = sin(phase * 1.73 + time * 1.35) * 0.45;
                float wind = (waveA + waveB) * _WindStrength * influence;
                return float2(windDir.x, windDir.z) * wind + float2(wind * 0.15, wind * 0.35);
            }

            depth_v2f depthVert(depth_appdata v)
            {
                depth_v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float3 localPos = v.vertex.xyz;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
#if defined(_WIND_ON)
                float influence = ComputeWindInfluence(localPos, v.color.a);
                float2 windOffset = ComputeWindOffset(worldPos, influence);
                worldPos.x += windOffset.x;
                worldPos.z += windOffset.y;
#endif
                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 depthFrag(depth_v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
#if defined(_ALPHATEST_ON)
                half4 baseColor = UNITY_ACCESS_INSTANCED_PROP(GTAFoliageDepthProps, _BaseColor);
                half alpha = tex2D(_BaseMap, i.uv).a * baseColor.a;
#if defined(_OPACITYMAP)
                alpha *= tex2D(_OpacityMap, i.uv).r;
#endif
                half scale = max(_DitherScale, 0.05h);
                half softStart = _Cutoff - scale * 0.5h;
                half ditherAlpha = saturate((alpha - softStart) / scale);
                GtaDitherClip(i.screenPos, ditherAlpha);
#endif
                return 0;
            }
            ENDCG
        }
    }

    FallBack Off
    CustomEditor "GTAShaderGUI"
}
