Shader "GTA/Default"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor]   _BaseColor("Color", Color) = (1, 1, 1, 1)
        _OpacityMap("Opacity (R)", 2D) = "white" {}
        _Opacity("Opacity", Range(0, 1)) = 1

        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _DitherScale("Dither Softness", Range(0.05, 1)) = 0.35

        [HideInInspector] _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        _ReflectionStrength("Reflection Strength", Range(0, 1)) = 0.35
        _MetallicGlossMap("Smoothness (A)", 2D) = "white" {}

        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1

        _OcclusionMap("Occlusion", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1

        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)
        _EmissionMap("Emission", 2D) = "white" {}

        _DetailAlbedoMap("Detail Albedo", 2D) = "gray" {}
        _DetailAlbedoScale("Detail Albedo Scale", Range(0, 2)) = 1
        [Normal] _DetailNormalMap("Detail Normal", 2D) = "bump" {}
        _DetailNormalScale("Detail Normal Scale", Range(0, 2)) = 1

        _MacroMap("Macro Texture", 2D) = "white" {}
        _MacroIntensity("Macro Intensity", Range(0, 1)) = 0.5
        _MacroScale("Macro Tiling", Float) = 0.01

        _DistanceFadeStart("Distance Fade Start", Float) = 50
        _DistanceFadeEnd("Distance Fade End", Float) = 80

        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        [Toggle(_NORMALMAP)] _UseNormalMap("Normal Map", Float) = 0
        [Toggle(_METALLICMAP)] _UseMetallicMap("Smoothness Map", Float) = 0
        [Toggle(_OCCLUSIONMAP)] _UseOcclusionMap("Occlusion Map", Float) = 0
        [Toggle(_EMISSION)] _UseEmission("Emission", Float) = 0
        [Toggle(_DETAIL_ALBEDO)] _UseDetailAlbedo("Detail Albedo", Float) = 0
        [Toggle(_DETAIL_NORMAL)] _UseDetailNormal("Detail Normal", Float) = 0
        [Toggle(_MACRO_MAP)] _UseMacroMap("Macro Map", Float) = 0
        [Toggle(_OPACITYMAP)] _UseOpacityMap("Opacity Map", Float) = 0
        [Toggle(_VERTEXCOLOR)] _UseVertexColor("Vertex Color", Float) = 0
        [Toggle(_DISTANCE_FADE_FAR)] _DistanceFadeFar("Fade Out Far", Float) = 0
        [Toggle(_DISTANCE_FADE_NEAR)] _DistanceFadeNear("Fade Out Near", Float) = 0

        [HideInInspector] _DetailAlbedoMap_ST("", Vector) = (1, 1, 0, 0)
        [HideInInspector] _MacroMap_ST("", Vector) = (1, 1, 0, 0)
        [HideInInspector] _Cull("", Float) = 2
        [HideInInspector] _QueueOffset("Queue Offset", Float) = 0
        [HideInInspector] _ShadowSoftness("", Float) = 0.85
        [HideInInspector] _ShadowContrast("", Float) = 1.35
        [HideInInspector] _ReceiveShadows("", Float) = 0

        // Idle/Standard / URP remaps
        [HideInInspector] _MainTex("Color Tex", 2D) = "white" {}
        [HideInInspector] _Color("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
        }
        LOD 300
        Cull [_Cull]
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _OPACITYMAP
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _METALLICMAP
            #pragma shader_feature_local _OCCLUSIONMAP
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local _DETAIL_ALBEDO
            #pragma shader_feature_local _DETAIL_NORMAL
            #pragma shader_feature_local _MACRO_MAP
            #pragma shader_feature_local _VERTEXCOLOR
            #pragma shader_feature_local _DISTANCE_FADE_FAR
            #pragma shader_feature_local _DISTANCE_FADE_NEAR

            #include "UnityStandardUtils.cginc"
            #include "Includes/GTAWorldLighting.cginc"

            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            sampler2D _OpacityMap;
            sampler2D _BumpMap;
            sampler2D _MetallicGlossMap;
            sampler2D _OcclusionMap;
            sampler2D _EmissionMap;
            sampler2D _DetailAlbedoMap;
            float4 _DetailAlbedoMap_ST;
            sampler2D _DetailNormalMap;
            sampler2D _MacroMap;
            float4 _MacroMap_ST;

            half _Cutoff;
            half _DitherScale;
            half _Smoothness;
            half _BumpScale;
            half _OcclusionStrength;
            half _DetailAlbedoScale;
            half _DetailNormalScale;
            half _MacroIntensity;
            half _MacroScale;
            half _DistanceFadeStart;
            half _DistanceFadeEnd;
            half _ReflectionStrength;
            half4 _EmissionColor;

            // Breaks SRP Batcher → URP uses GPU instancing (Idle/Standard model).
            UNITY_INSTANCING_BUFFER_START(GTADefaultProps)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(half, _Opacity)
            UNITY_INSTANCING_BUFFER_END(GTADefaultProps)

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                half3 normalWS : TEXCOORD3;
                half4 tangentWS : TEXCOORD4;
                half4 color : TEXCOORD5;
                float4 screenPos : TEXCOORD6;
                UNITY_FOG_COORDS(7)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.uv1 = v.uv1;
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

            half3 ApplyNormalMap(half3 normalWS, half4 tangentWS, float2 uv)
            {
#if defined(_NORMALMAP)
                half3 nTS = UnpackScaleNormal(tex2D(_BumpMap, uv), _BumpScale);
#if defined(_DETAIL_NORMAL)
                float2 detailUV = uv * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
                half3 detailTS = UnpackScaleNormal(tex2D(_DetailNormalMap, detailUV), _DetailNormalScale);
                nTS = normalize(half3(nTS.xy + detailTS.xy, nTS.z * detailTS.z));
#endif
                half3 bitangent = tangentWS.w * cross(normalWS, tangentWS.xyz);
                half3x3 tbn = half3x3(tangentWS.xyz, bitangent, normalWS);
                return normalize(mul(nTS, tbn));
#else
                return normalize(normalWS);
#endif
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

#if defined(LOD_FADE_CROSSFADE)
                UnityApplyDitherCrossFade(i.pos.xy);
#endif

                half4 baseColor = UNITY_ACCESS_INSTANCED_PROP(GTADefaultProps, _BaseColor);
                half opacity = UNITY_ACCESS_INSTANCED_PROP(GTADefaultProps, _Opacity);

                half4 albedoAlpha = tex2D(_BaseMap, i.uv);
                half3 albedo = albedoAlpha.rgb * baseColor.rgb;
                half alpha = albedoAlpha.a * baseColor.a * opacity;

#if defined(_OPACITYMAP)
                alpha *= tex2D(_OpacityMap, i.uv).r;
#endif

#if defined(_VERTEXCOLOR)
                albedo *= i.color.rgb;
                alpha *= i.color.a;
#endif

#if defined(_DETAIL_ALBEDO)
                float2 detailUV = i.uv * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
                half3 detail = tex2D(_DetailAlbedoMap, detailUV).rgb;
                albedo = lerp(albedo, albedo * detail * unity_ColorSpaceDouble.rgb, saturate(_DetailAlbedoScale));
#endif

#if defined(_MACRO_MAP)
                float2 macroUV = i.uv1 * _MacroScale + _MacroMap_ST.zw;
                half3 macroSample = tex2D(_MacroMap, macroUV).rgb;
                albedo = lerp(albedo, albedo * macroSample, saturate(_MacroIntensity));
#endif

#if defined(_ALPHATEST_ON)
                half scale = max(_DitherScale, 0.05h);
                half softStart = _Cutoff - scale * 0.5h;
                half ditherAlpha = saturate((alpha - softStart) / scale);
                GtaDitherClip(i.screenPos, ditherAlpha);
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

                half3 normalWS = ApplyNormalMap(i.normalWS, i.tangentWS, i.uv);

                half smoothness = _Smoothness;
#if defined(_METALLICMAP)
                smoothness *= tex2D(_MetallicGlossMap, i.uv).a;
#endif

                half occlusion = 1.0h;
#if defined(_OCCLUSIONMAP)
                half occ = tex2D(_OcclusionMap, i.uv).g;
                occlusion = lerp(1.0h, occ, saturate(_OcclusionStrength));
#endif

                half3 emission = 0;
#if defined(_EMISSION)
                emission = tex2D(_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
#endif

                half3 color = GtaShadeLit(
                    albedo,
                    normalWS,
                    i.worldPos,
                    smoothness,
                    emission,
                    occlusion,
                    _ReflectionStrength);

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
            Cull [_Cull]

            CGPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _OPACITYMAP
            #pragma shader_feature_local _VERTEXCOLOR

            #include "Includes/GTAWorldLighting.cginc"

            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            sampler2D _OpacityMap;
            half _Cutoff;
            half _DitherScale;

            UNITY_INSTANCING_BUFFER_START(GTADefaultDepthProps)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(half, _Opacity)
            UNITY_INSTANCING_BUFFER_END(GTADefaultDepthProps)

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
                half4 color : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            depth_v2f depthVert(depth_appdata v)
            {
                depth_v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 depthFrag(depth_v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
#if defined(_ALPHATEST_ON)
                half4 baseColor = UNITY_ACCESS_INSTANCED_PROP(GTADefaultDepthProps, _BaseColor);
                half opacity = UNITY_ACCESS_INSTANCED_PROP(GTADefaultDepthProps, _Opacity);
                half alpha = tex2D(_BaseMap, i.uv).a * baseColor.a * opacity;
#if defined(_OPACITYMAP)
                alpha *= tex2D(_OpacityMap, i.uv).r;
#endif
#if defined(_VERTEXCOLOR)
                alpha *= i.color.a;
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
