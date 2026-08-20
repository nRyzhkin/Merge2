#ifndef GTA_DEFAULT_INPUT_INCLUDED
#define GTA_DEFAULT_INPUT_INCLUDED

#include "GTACommon.hlsl"
#include "GTA_MaterialCBufferTail.hlsl"

// SRP Batcher: never ifdef properties inside UnityPerMaterial.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseMap_TexelSize;
    float4 _DetailAlbedoMap_ST;
    float4 _MacroMap_ST;
    half4  _BaseColor;
    half4  _EmissionColor;
    half   _Cutoff;
    half   _Smoothness;
    half   _Metallic;
    half   _BumpScale;
    half   _OcclusionStrength;
    half   _DetailAlbedoScale;
    half   _DetailNormalScale;
    half   _MacroIntensity;
    half   _MacroScale;
    half   _DistanceFadeStart;
    half   _DistanceFadeEnd;
    half   _ShadowSoftness;
    half   _ShadowContrast;
    half   _ReflectionStrength;
    half   _Cull;
    half   _QueueOffset;
    half   _ReceiveShadows;
    GTA_MATERIAL_CBUFFER_STREAMING_TAIL
CBUFFER_END

TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_OcclusionMap);       SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_DetailAlbedoMap);    SAMPLER(sampler_DetailAlbedoMap);
TEXTURE2D(_DetailNormalMap);    SAMPLER(sampler_DetailNormalMap);
TEXTURE2D(_MacroMap);           SAMPLER(sampler_MacroMap);

half3 GTASampleDetailNormal(float2 uv, half scale)
{
#if defined(_DETAIL_NORMAL)
    half4 n = SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, uv);
    return UnpackNormalScale(n, scale);
#else
    return half3(0.0h, 0.0h, 1.0h);
#endif
}

half3 GTABlendDetailNormals(half3 baseNormalTS, half3 detailNormalTS)
{
#if defined(_DETAIL_NORMAL)
    return normalize(half3(baseNormalTS.xy + detailNormalTS.xy, baseNormalTS.z * detailNormalTS.z));
#else
    return baseNormalTS;
#endif
}

void GTAInitializeDefaultSurfaceData(
    float2 uv0,
    float2 uv1,
    half4 vertexColor,
    out SurfaceData surfaceData)
{
    surfaceData = (SurfaceData)0;

    half4 albedoAlpha = SampleAlbedoAlpha(uv0, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    half3 albedo = albedoAlpha.rgb * _BaseColor.rgb;
    half alpha = GTAResolveAlpha(albedoAlpha.a, _BaseColor.a, uv0);

    albedo = GTAApplyVertexColor(albedo, vertexColor);
    alpha = GTAApplyVertexAlpha(alpha, vertexColor);

#if defined(_DETAIL_ALBEDO)
    half3 detailAlbedo = SAMPLE_TEXTURE2D(_DetailAlbedoMap, sampler_DetailAlbedoMap, uv0 * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw).rgb;
    albedo = GTABlendDetailAlbedo(albedo, detailAlbedo, _DetailAlbedoScale);
#endif

#if defined(_MACRO_MAP)
    float2 macroUV = uv1 * _MacroScale + _MacroMap_ST.zw;
    half3 macroSample = SAMPLE_TEXTURE2D(_MacroMap, sampler_MacroMap, macroUV).rgb;
    albedo = GTAApplyMacroAlbedo(albedo, macroSample, _MacroIntensity);
#endif

    surfaceData.albedo = albedo;
    surfaceData.alpha = alpha;
    surfaceData.normalTS = SampleNormal(uv0, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);

#if defined(_DETAIL_NORMAL)
    float2 detailUV = uv0 * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
    half3 detailNormalTS = GTASampleDetailNormal(detailUV, _DetailNormalScale);
    surfaceData.normalTS = GTABlendDetailNormals(surfaceData.normalTS, detailNormalTS);
#endif

    surfaceData.metallic = 0.0h;
#if defined(_METALLICMAP)
    half4 metallicGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv0);
    surfaceData.smoothness = metallicGloss.a * _Smoothness;
#else
    surfaceData.smoothness = _Smoothness;
#endif

    surfaceData.specular = half3(0.0h, 0.0h, 0.0h);

#if defined(_OCCLUSIONMAP)
    half occ = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv0).g;
    surfaceData.occlusion = LerpWhiteTo(occ, _OcclusionStrength);
#else
    surfaceData.occlusion = 1.0h;
#endif

    surfaceData.emission = SampleEmission(uv0, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
    surfaceData.clearCoatMask = 0.0h;
    surfaceData.clearCoatSmoothness = 0.0h;
}

#endif // GTA_DEFAULT_INPUT_INCLUDED
