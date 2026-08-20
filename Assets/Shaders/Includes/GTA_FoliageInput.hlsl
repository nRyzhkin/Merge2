#ifndef GTA_FOLIAGE_INPUT_INCLUDED
#define GTA_FOLIAGE_INPUT_INCLUDED

#include "GTACommon.hlsl"
#include "GTA_MaterialCBufferTail.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseMap_TexelSize;
    half4  _BaseColor;
    half4  _EmissionColor;
    half4  _ColorVariationMin;
    half4  _ColorVariationMax;
    half4  _WindDirection;
    half   _Cutoff;
    half   _Smoothness;
    half   _BumpScale;
    half   _WindSpeed;
    half   _WindStrength;
    half   _WindFrequency;
    half   _DistanceFadeStart;
    half   _DistanceFadeEnd;
    half   _InstanceColorStrength;
    half   _ShadowSoftness;
    half   _ShadowContrast;
    half   _ReflectionStrength;
    half   _QueueOffset;
    half   _ReceiveShadows;
    GTA_MATERIAL_CBUFFER_STREAMING_TAIL
CBUFFER_END

// Break SRP Batcher compatibility so URP can use GPU instancing for mass foliage
// (same trade-off as CG GTA/Default / IslandCleaner/FoliageWind).
UNITY_INSTANCING_BUFFER_START(GTAFoliageProps)
#if defined(_INSTANCE_COLOR)
    UNITY_DEFINE_INSTANCED_PROP(half4, _InstanceColorTint)
#endif
UNITY_INSTANCING_BUFFER_END(GTAFoliageProps)

#include "GTAWind.hlsl"

half GTAGetInstanceWindOffset()
{
    return 0.0h;
}

half4 GTAGetInstanceColorVariation()
{
#if defined(UNITY_INSTANCING_ENABLED) && defined(_INSTANCE_COLOR)
    return UNITY_ACCESS_INSTANCED_PROP(GTAFoliageProps, _InstanceColorTint);
#else
    return half4(1.0h, 1.0h, 1.0h, 1.0h);
#endif
}

half3 GTAProceduralColorVariation(float3 positionWS)
{
    half h = frac(sin(dot(floor(positionWS.xz * 0.37h), half2(12.9898h, 78.233h))) * 43758.5453h);
    return lerp(_ColorVariationMin.rgb, _ColorVariationMax.rgb, h);
}

half3 GTAGetFoliageColorTint(float3 positionWS, half4 vertexColor)
{
#if defined(_INSTANCE_COLOR)
    half4 instanceColor = GTAGetInstanceColorVariation();
    half3 procedural = GTAProceduralColorVariation(positionWS);
    half isDefaultInstance = step(0.999h, instanceColor.r * instanceColor.g * instanceColor.b);
    half3 variation = lerp(instanceColor.rgb, procedural, isDefaultInstance);
    variation = lerp(half3(1.0h, 1.0h, 1.0h), variation, saturate(_InstanceColorStrength));
    return vertexColor.rgb * variation;
#else
    return vertexColor.rgb;
#endif
}

half GTAGetFoliageWindWeight(half4 vertexColor)
{
    // Alpha = 0 at trunk/base, 1 at leaf tips (SpeedTree / URP grass convention).
    return saturate(vertexColor.a);
}

void GTAFoliageTransformPosition(inout float3 positionOS, half4 vertexColor)
{
    half windWeight = GTAGetFoliageWindWeight(vertexColor);
    half windOffset = GTAGetInstanceWindOffset();
    positionOS = GTAApplyVertexWind(
        positionOS,
        windWeight,
        windOffset,
        _WindSpeed,
        _WindStrength,
        _WindFrequency,
        _WindDirection.xyz);
}

void GTAInitializeFoliageSurfaceData(
    float2 uv,
    half4 vertexColor,
    float3 positionWS,
    out SurfaceData surfaceData)
{
    surfaceData = (SurfaceData)0;

    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    half3 tint = GTAGetFoliageColorTint(positionWS, vertexColor);
    half3 albedo = albedoAlpha.rgb * _BaseColor.rgb * tint;
    half alpha = GTAResolveAlpha(albedoAlpha.a, _BaseColor.a, uv);

    surfaceData.albedo = albedo;
    surfaceData.alpha = alpha;
    surfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    surfaceData.metallic = 0.0h;
    surfaceData.smoothness = _Smoothness;
    surfaceData.specular = half3(0.0h, 0.0h, 0.0h);
    surfaceData.occlusion = 1.0h;
    surfaceData.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
    surfaceData.clearCoatMask = 0.0h;
    surfaceData.clearCoatSmoothness = 0.0h;
}

half3 GTAFlipNormalForDoubleSided(half3 normalWS, FRONT_FACE_TYPE facing)
{
#if defined(_DOUBLESIDED_ON)
    normalWS *= IS_FRONT_VFACE(facing, 1.0h, -1.0h);
#endif
    return normalWS;
}

#endif // GTA_FOLIAGE_INPUT_INCLUDED
