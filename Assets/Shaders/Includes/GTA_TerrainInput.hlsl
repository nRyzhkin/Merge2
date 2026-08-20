#ifndef GTA_TERRAIN_INPUT_INCLUDED
#define GTA_TERRAIN_INPUT_INCLUDED

#include "GTACommon.hlsl"

#if defined(_SHORE_FOAM)
#include "GTAWorldGlobals.hlsl"

float4 _WaterSurfaceBounds;
float _WaterSurfaceDepthMax;
#include "GTA_WaterBakedSurface.hlsl"
#include "GTA_ShoreFoam.hlsl"

TEXTURE2D(_GTA_FoamNoiseMap);
SAMPLER(sampler_GTA_FoamNoiseMap);
float4 _GTA_FoamNoiseMap_ST;
float _GTA_WaveFrequency;
float _GTA_WaveSpeed;
float _GTA_WaveDist;
float _GTA_FoamShoreWobble;
float _GTA_FoamShoreScale;
float _GTA_FoamBeachDepth;
float _GTA_FoamCoverage;
float _GTA_FoamThickness;
float _GTA_UseFoam;
float _GTA_WaterLevel;
half4 _GTA_FoamColor;
half _GTA_FoamDayBoost;
#endif

#include "GTA_TerrainMaterial.hlsl"

TEXTURE2D(_SandHeightMap);   SAMPLER(sampler_SandHeightMap);
TEXTURE2D(_DetailMaskMap);    SAMPLER(sampler_DetailMaskMap);
TEXTURE2D(_DetailGrassMap);   SAMPLER(sampler_DetailGrassMap);
TEXTURE2D(_DetailSandMap);    SAMPLER(sampler_DetailSandMap);
TEXTURE2D(_DetailGravelMap);  SAMPLER(sampler_DetailGravelMap);

half GTAComputeHeightSandMask(float worldY)
{
    half blend = max(_SandHeightBlend, 1e-3h);
    return 1.0h - smoothstep(_SandHeightMax - blend, _SandHeightMax, worldY);
}

void GTAComputeTerrainDetailWeights(float2 uv0, half3 vertexColor, out half grassW, out half sandW, out half gravelW)
{
#if defined(_DETAIL_MASK_MAP)
    half3 mask = SAMPLE_TEXTURE2D(_DetailMaskMap, sampler_DetailMaskMap, uv0).rgb;
    grassW = mask.r;
    sandW = mask.g;
    gravelW = mask.b;
#elif defined(_DETAIL_VERTEX_COLOR)
    sandW = saturate(min(vertexColor.r, vertexColor.g) - vertexColor.b);
    grassW = saturate(vertexColor.g - max(vertexColor.r, vertexColor.b));
    gravelW = saturate(1.0h - sandW - grassW);
#else
    grassW = 0.0h;
    sandW = 0.0h;
    gravelW = 0.0h;
    return;
#endif

    half wSum = grassW + sandW + gravelW;
    if (wSum < 1e-4h)
    {
        grassW = 0.0h;
        sandW = 0.0h;
        gravelW = 0.0h;
        return;
    }

    grassW /= wSum;
    sandW /= wSum;
    gravelW /= wSum;
}

half GTASampleDetailFacture(Texture2D tex, SamplerState samp, float2 uv)
{
    // B/W detail maps: use luminance as surface breakup only (color comes from base layers).
    half3 sample = SAMPLE_TEXTURE2D(tex, samp, uv).rgb;
    return Luminance(sample);
}

#if defined(_SHORE_FOAM)
half3 GTAShoreTerrainFoamColor(half3 normalWS)
{
    half3 white = saturate(_GTA_FoamColor.rgb);

    float3 sunDir = _GTA_SunDirection.xyz;
    float sunLen2 = dot(sunDir, sunDir);
    half sunUp = 0.5h;
    half diffuseSun = 0.0h;
    if (sunLen2 > 1e-6)
    {
        sunDir *= rsqrt(sunLen2);
        sunUp = saturate((half)sunDir.y * 2.5h + 0.1h);
        diffuseSun = saturate(dot(normalize(normalWS), sunDir));
    }

    half exposure = max((half)_GTA_SkyExposure, 0.5h);
    half weatherDim = lerp(1.0h, 0.8h, saturate((half)_GTA_Weather));
    half3 ambient = (half3)_GTA_AmbientColor.rgb;

    half3 sceneLight = ambient * 1.05h + (half3)_GTA_SunColor.rgb * diffuseSun * 0.55h;
    half3 bright = white * sceneLight * exposure * weatherDim;
    bright *= 1.0h + sunUp * _GTA_FoamDayBoost * 0.85h;
    bright = lerp(bright, white * exposure * (0.85h + sunUp * 0.35h), sunUp * 0.8h);

    return max(bright, white * exposure * 0.75h);
}

void GTAApplyShoreFoam(inout half3 albedo, float3 positionWS, half3 normalWS, half sandMask)
{
    if (_ShoreFoamStrength <= 1e-4h || _GTA_UseFoam < 0.5)
        return;

    half waveMask = GTAShoreTerrainFoamShow(
        positionWS,
        _GTA_WaveFrequency,
        _GTA_WaveSpeed,
        _GTA_WaveDist,
        _GTA_FoamShoreWobble,
        _GTA_FoamShoreScale,
        _GTA_FoamBeachDepth,
        _GTA_FoamCoverage,
        _GTA_FoamThickness,
        _GTA_UseFoam,
        _GTA_WaterLevel,
        _GTA_FoamNoiseMap_ST,
        TEXTURE2D_ARGS(_GTA_FoamNoiseMap, sampler_GTA_FoamNoiseMap)) * _ShoreFoamStrength;

    if (sandMask > 0.01h)
        waveMask *= lerp(0.82h, 1.0h, saturate(sandMask));

    half texMask = GTAShoreSampleFoamMask(
        positionWS,
        _Time.y,
        _GTA_WaveSpeed,
        _GTA_FoamNoiseMap_ST,
        TEXTURE2D_ARGS(_GTA_FoamNoiseMap, sampler_GTA_FoamNoiseMap));

    half foamBlend = saturate(waveMask * texMask);
    if (foamBlend <= 1e-4h)
        return;

    half3 foamColor = GTAShoreTerrainFoamColor(normalWS);
    albedo = lerp(albedo, foamColor, foamBlend);
}
#endif

half3 GTAComposeTerrainAlbedo(float2 uv0, float3 positionWS, half3 vertexColor, half3 normalWS)
{
    half3 baseAlbedo = SampleAlbedoAlpha(uv0, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).rgb * _BaseColor.rgb;
    half sandMask = 0.0h;

#if defined(_HEIGHT_SAND)
    float2 sandHeightUV = positionWS.xz * _SandHeightTiling;
    half3 sandHeightAlbedo = SAMPLE_TEXTURE2D(_SandHeightMap, sampler_SandHeightMap, sandHeightUV).rgb;
    half heightSandMask = GTAComputeHeightSandMask(positionWS.y);
    baseAlbedo = lerp(baseAlbedo, sandHeightAlbedo, heightSandMask);
    sandMask = max(sandMask, heightSandMask);
#endif

    half grassW, sandW, gravelW;
    GTAComputeTerrainDetailWeights(uv0, vertexColor, grassW, sandW, gravelW);
    sandMask = max(sandMask, sandW);

    half wSum = grassW + sandW + gravelW;
    if (wSum > 1e-4h && _DetailStrength > 1e-4h)
    {
        float2 grassUV = uv0 * _DetailGrassMap_ST.xy + _DetailGrassMap_ST.zw;
        float2 sandUV = uv0 * _DetailSandMap_ST.xy + _DetailSandMap_ST.zw;
        float2 gravelUV = uv0 * _DetailGravelMap_ST.xy + _DetailGravelMap_ST.zw;

        half detailFacture =
            grassW * GTASampleDetailFacture(TEXTURE2D_ARGS(_DetailGrassMap, sampler_DetailGrassMap), grassUV) +
            sandW * GTASampleDetailFacture(TEXTURE2D_ARGS(_DetailSandMap, sampler_DetailSandMap), sandUV) +
            gravelW * GTASampleDetailFacture(TEXTURE2D_ARGS(_DetailGravelMap, sampler_DetailGravelMap), gravelUV);

        baseAlbedo = GTAApplyDetailFacture(baseAlbedo, detailFacture, _DetailStrength);
    }

#if defined(_SHORE_FOAM)
    GTAApplyShoreFoam(baseAlbedo, positionWS, normalWS, sandMask);
#endif

    return baseAlbedo;
}

void GTAInitializeTerrainSurfaceData(
    float2 uv0,
    float3 positionWS,
    half3 vertexColor,
    half3 normalWS,
    out SurfaceData surfaceData)
{
    surfaceData = (SurfaceData)0;
    surfaceData.albedo = GTAComposeTerrainAlbedo(uv0, positionWS, vertexColor, normalWS);
    surfaceData.alpha = _BaseColor.a;
    surfaceData.normalTS = SampleNormal(uv0, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    surfaceData.metallic = 0.0h;
    surfaceData.smoothness = _Smoothness;
    surfaceData.specular = half3(0.0h, 0.0h, 0.0h);
    surfaceData.occlusion = 1.0h;
    surfaceData.emission = half3(0.0h, 0.0h, 0.0h);
    surfaceData.clearCoatMask = 0.0h;
    surfaceData.clearCoatSmoothness = 0.0h;
}

#endif // GTA_TERRAIN_INPUT_INCLUDED
