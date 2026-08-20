#ifndef GTA_WATER_LITE_PASS_INCLUDED
#define GTA_WATER_LITE_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"
#include "GTAFog.hlsl"
#include "GTA_SkySun.hlsl"
#include "GTA_WaterAtmosphere.hlsl"
#include "../../../Shaders/Includes/GTAReflections.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

TEXTURE2D(_WaveNormalMap);
SAMPLER(sampler_WaveNormalMap);
TEXTURE2D(_FoamNoiseMap);
SAMPLER(sampler_FoamNoiseMap);
TEXTURE2D(_CausticMap);
SAMPLER(sampler_CausticMap);

CBUFFER_START(UnityPerMaterial)
    half4 _EdgeColor;
    half4 _FoamColor;
    half4 _Color;
    float4 _WaterSurfaceBounds;
    float _WaterSurfaceDepthMax;
    float4 _WaterSurfaceMap_TexelSize;
    float4 _WaveNormalMap_ST;
    float4 _FoamNoiseMap_ST;
    float4 _CausticMap_ST;
    float _WaveFrequency;
    float _WaveSpeed;
    float _WaveDist;
    float _EdgeDepth;
    float _MaxWaveDist;
    float _NormalStrength;
    float _Speed;
    float _Tiling;
    float _Transparency;
    float _Caustic_Strength;
    float _UseFoam;
    float _FoamOpacity;
    float _FoamCoverage;
    float _FoamThickness;
    float _FoamDayBoost;
    float _FoamShoreWobble;
    float _FoamShoreScale;
    float _FoamBeachDepth;
    float _ShallowClearDepth;
    UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

#include "GTA_WaterBakedSurface.hlsl"
#include "GTA_ShoreFoam.hlsl"

struct WaterLiteAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct WaterLiteVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
    float4 screenPos  : TEXCOORD2;
    half   fogFactor  : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

half3 WaterLiteTimeOfDayTint(half3 color)
{
    half exposure = max((half)_GTA_SkyExposure, 0.5h);
    half3 ambient = (half3)_GTA_AmbientColor.rgb;
    half3 horizon = GTAEvaluateHorizonSky(float3(0.0, 0.04, 1.0));
    half3 tint = lerp(ambient, horizon, 0.45h) * exposure;
    tint += (half3)_GTA_SunColor.rgb * 0.06h;
    return color * (tint + 0.18h);
}

float WaterLiteFoamNoise(float2 worldXZ, float time)
{
    return GTAShoreFoamNoise(worldXZ, time, _WaveSpeed, _FoamNoiseMap_ST, TEXTURE2D_ARGS(_FoamNoiseMap, sampler_FoamNoiseMap));
}

float WaterLiteShoreDistort(float2 worldXZ, float time)
{
    return GTAShoreShoreDistort(worldXZ, time, _FoamShoreScale, _WaveSpeed);
}

void WaterLiteFoam(float3 worldPos, float foamDepth, float shoreDistort, float time, out float staticWash, out float animatedFoam)
{
    GTAShoreFoamEvaluate(
        worldPos,
        foamDepth,
        shoreDistort,
        time,
        _WaveFrequency,
        _WaveSpeed,
        _WaveDist,
        _FoamNoiseMap_ST,
        TEXTURE2D_ARGS(_FoamNoiseMap, sampler_FoamNoiseMap),
        staticWash,
        animatedFoam);
}

half WaterLiteFoamWeight(float foamRaw, float nearFactor)
{
    return GTAShoreFoamWeight(foamRaw, nearFactor, _UseFoam, _FoamCoverage, _FoamThickness);
}

half3 WaterLiteLitFoam(half3 geomNormalWS)
{
    half3 white = saturate(_FoamColor.rgb);
    half3 N = normalize(geomNormalWS);
    half exposure = max((half)_GTA_SkyExposure, 0.45h);

    float3 sunDir = _GTA_SunDirection.xyz;
    float sunLen2 = dot(sunDir, sunDir);
    half sunUp = 0.5h;
    half diffuseSun = 0.0h;
    half3 sunCol = (half3)_GTA_SunColor.rgb;
    if (sunLen2 > 1e-6)
    {
        sunDir *= rsqrt(sunLen2);
        sunUp = saturate((half)sunDir.y * 2.5h + 0.1h);
        diffuseSun = saturate(dot(N, (half3)sunDir));
    }

    half weatherDim = lerp(1.0h, 0.82h, saturate((half)_GTA_Weather));
    half3 skyFill = saturate((half3)_GTA_SkyZenith.rgb) * 0.40h
                  + saturate((half3)_GTA_SkyHorizon.rgb) * 0.30h;
    half3 sunLit = sunCol * (0.40h + 0.60h * diffuseSun) * max(sunUp, 0.15h);

    // Independent of ambient (which is black) — foam must read as white.
    half3 lit = white * (skyFill + sunLit) * exposure * weatherDim;
    lit *= 1.0h + sunUp * _FoamDayBoost * 0.55h;

    half foamFloor = lerp(0.28h, 0.82h, sunUp);
    lit = max(lit, white * foamFloor * exposure * weatherDim);
    return lit;
}

half3 WaterLiteBrightSandFoam(half3 geomNormalWS, half3 litFoam, half foamShow)
{
    half3 white = saturate(_FoamColor.rgb);

    float3 sunDir = _GTA_SunDirection.xyz;
    float sunLen2 = dot(sunDir, sunDir);
    half sunUp = 0.5h;
    if (sunLen2 > 1e-6)
        sunUp = saturate((half)(sunDir.y * rsqrt(sunLen2)) * 2.5h + 0.1h);

    half exposure = max((half)_GTA_SkyExposure, 0.5h);
    half weatherDim = lerp(1.0h, 0.85h, saturate((half)_GTA_Weather));

    half3 bright = white * (0.55h + 0.45h * sunUp) * exposure * weatherDim;
    bright *= 1.0h + sunUp * _FoamDayBoost * 0.85h;
    bright = max(bright, white * lerp(0.35h, 0.95h, sunUp) * exposure);
    bright = max(bright, litFoam);

    return lerp(litFoam, bright, foamShow);
}

float3 WaterLiteNormalLayerWeights(float2 worldXZ, float time)
{
  // Slow-varying weights — breaks uniform two-layer sliding without a new texture.
  float t = time * _Speed * 0.11;
  float2 np = worldXZ * 0.065 + float2(t * 0.37, -t * 0.29);

  float w1 = WaterCheapNoise2D(np) * 0.62 + 0.19;
  float w2 = WaterCheapNoise2D(np * 1.73 + float2(4.1, 2.8)) * 0.62 + 0.19;
  float w3 = WaterCheapNoise2D(np * 0.47 + float2(9.2, 5.1)) * 0.62 + 0.19;
  float inv = rcp(max(w1 + w2 + w3, 1e-4));
  return float3(w1, w2, w3) * inv;
}

float3 WaterLiteBlendNormalsUDN(float3 n1, float3 n2, float3 n3, float3 weights)
{
  float3 tN;
  tN.xy = n1.xy * weights.x + n2.xy * weights.y + n3.xy * weights.z;
  tN.z = n1.z * weights.x + n2.z * weights.y + n3.z * weights.z;
  return normalize(tN);
}

float3 WaterLiteWorldNormal(float3 geomNormalWS, float3 worldPos, float time)
{
  float t = time * _Speed;
  float2 xz = worldPos.xz * _Tiling * _WaveNormalMap_ST.xy + _WaveNormalMap_ST.zw;

  // Three misaligned layers — irrational scales / rotations hide tile repetition.
  const float2x2 rotA = float2x2(0.809017, -0.587785, 0.587785, 0.809017);
  const float2x2 rotB = float2x2(-0.123601, -0.992332, 0.992332, -0.123601);

  float wob = 0.038;
  float2 flow1 = float2(t * 0.041, t * 0.027) + float2(sin(t * 0.71), cos(t * 0.53)) * wob;
  float2 flow2 = float2(-t * 0.033, t * 0.049) + float2(cos(t * 0.67), sin(t * 0.89)) * wob;
  float2 flow3 = float2(t * 0.019, -t * 0.044) + float2(sin(t * 0.43 + 1.7), cos(t * 0.59 + 2.3)) * wob * 0.75;

  float3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_WaveNormalMap, sampler_WaveNormalMap, xz * 1.00 + flow1));
  float3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_WaveNormalMap, sampler_WaveNormalMap, mul(rotA, xz) * 1.31 + flow2 + 3.7));
  float3 n3 = UnpackNormal(SAMPLE_TEXTURE2D(_WaveNormalMap, sampler_WaveNormalMap, mul(rotB, xz) * 0.78 + flow3 + 11.3));

  float3 weights = WaterLiteNormalLayerWeights(worldPos.xz, time);
  float3 tN = WaterLiteBlendNormalsUDN(n1, n2, n3, weights);
  tN.xy *= _NormalStrength;

  float3 n = normalize(geomNormalWS);
  float3 tangentWS = float3(1.0, 0.0, 0.0) - n.x * n;
  float3 bitangentWS = float3(0.0, 0.0, 1.0) - n.z * n;
  tangentWS = normalize(tangentWS);
  bitangentWS = normalize(bitangentWS - dot(bitangentWS, tangentWS) * tangentWS);
  tangentWS = normalize(cross(bitangentWS, n));

  return NormalizeNormalPerPixel(TransformTangentToWorld(tN, float3x3(tangentWS, bitangentWS, n)));
}

WaterLiteVaryings WaterLiteVert(WaterLiteAttributes input)
{
    WaterLiteVaryings output = (WaterLiteVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, float4(1, 0, 0, 1));

    output.positionCS = posInputs.positionCS;
    output.positionWS = posInputs.positionWS;
    output.normalWS = normInputs.normalWS;
    output.screenPos = ComputeScreenPos(output.positionCS);
#if !defined(_FOG_FRAGMENT)
    output.fogFactor = GTAComputeFogFactor(output.positionCS.z);
#else
    output.fogFactor = 0.0h;
#endif
    return output;
}

half4 WaterLiteFrag(WaterLiteVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);

    float3 worldPos = input.positionWS;
    float3 geomN = normalize(input.normalWS);
    float3 viewDir = GetWorldSpaceNormalizeViewDir(worldPos);
    float time = _Time.y;

    half3 waterColor = WaterLiteTimeOfDayTint(_Color.rgb);
    half3 edgeColor = WaterLiteTimeOfDayTint(_EdgeColor.rgb);

    float depth = WaterSurfaceSampleDepth(worldPos);
    float edgeDepthRange = max(_EdgeDepth, 0.01);
    float edgeDepth = WaterBakedEdgeFromDepth_float(depth, edgeDepthRange);
    float edge01 = WaterBakedEdgeFromDepth_float(depth, 0.1);
    float clearRange = max(_ShallowClearDepth, max(_FoamBeachDepth, 0.5));
    float shallowClear = WaterBakedShallowFromDepth_float(depth, clearRange);

    float shoreDistort = WaterLiteShoreDistort(worldPos.xz, time);
    float foamDepth = max(depth + shoreDistort * _FoamShoreWobble, 0.0);

    float staticWash, animatedFoam;
    WaterLiteFoam(worldPos, foamDepth, shoreDistort, time, staticWash, animatedFoam);
    float foamOnWater = staticWash + animatedFoam;

    float caustic = 0.0;
    if (_Caustic_Strength > 0.001 && (1.0 - edgeDepth) > 0.001)
    {
        float2 cuv = worldPos.xz * 0.5 * _CausticMap_ST.xy + _CausticMap_ST.zw + time * _Speed * float2(0.12, 0.08);
        float c = SAMPLE_TEXTURE2D(_CausticMap, sampler_CausticMap, cuv).r;
        caustic = pow(c, 3.0) * _Caustic_Strength * (1.0 - edgeDepth);
    }

    float3 worldN = WaterLiteWorldNormal(geomN, worldPos, time);

    float screenW = input.screenPos.w;
    float nearFactor = 1.0 - saturate(screenW / max(_MaxWaveDist, 1.0));

    float oneMinusEdgeDepth = 1.0 - edgeDepth;
    float edgeSpread = pow(saturate(1.0 - oneMinusEdgeDepth * nearFactor), 3.2);
    half3 edgeLerp = lerp(edgeColor, waterColor, edgeSpread);
    float fresnel = pow(saturate(1.0 - dot(worldN, viewDir)), 1.0);
    half3 bodyColor = edgeLerp * 0.5h * (1.0h + fresnel);

    half foamWeight = WaterLiteFoamWeight(foamOnWater, nearFactor);
    half foamBlend = foamWeight * (half)_FoamOpacity;

    float beachDepth = max(_FoamBeachDepth, 0.08);
    float beachProx = 1.0 - WaterBakedEdgeFromDepth_float(depth, beachDepth);
    half sandZone = GTAShoreSandZoneMask(depth, edge01, shoreDistort, _FoamBeachDepth);

    // Opaque water foam — never on sand (sand uses transparent foam pass below).
    half waveFoamBlend = foamBlend * (1.0h - sandZone);

    // Animated white foam on sand — entire beach band, crests wash onto dry sand.
    half boostedAnim = saturate((half)animatedFoam * 2.4h);
    half sandFoamWeight = WaterLiteFoamWeight(boostedAnim, max((half)nearFactor, 0.88h));
    half sandFoamAlpha = saturate(sandFoamWeight * 1.2h) * (half)_FoamOpacity * sandZone;
    half sandFoamShow = saturate(pow(sandFoamWeight, 0.72h) * 1.35h) * sandZone;

    half3 shallowTint = waterColor * (1.0h + caustic * (1.0h - (half)edgeDepth));
    float beachFade = saturate(beachProx);
    float colorBlend = (1.0 - nearFactor * (1.0 - edgeDepth)) * _Transparency;
    colorBlend = max(colorBlend, staticWash * 0.65 * (1.0 - beachFade));
    half3 finalColor = lerp(shallowTint, bodyColor, colorBlend);

    // Sky / probe reflection — fixed LOD, raw RGB + box projection.
    half3 reflectDir = reflect(-viewDir, worldN);
    half3 envReflection = GTASampleSpecularCubemap(reflectDir, worldPos);
    half fresnelRefl = pow(1.0h - saturate(dot(worldN, viewDir)), 3.0h);
    half reflWeight = lerp(0.18h, 1.0h, fresnelRefl) * saturate((half)_GTA_ReflectionIntensity) * lerp(0.5h, 1.0h, nearFactor);
    finalColor += envReflection * reflWeight;

    // Broad sun glint from wave normals (visible at all times of day, not only sunset halo).
    float3 sunDir = _GTA_SunDirection.xyz;
    float sunLen2 = dot(sunDir, sunDir);
    if (sunLen2 > 1e-6)
    {
        sunDir *= rsqrt(sunLen2);
        half3 reflectSun = reflect(-viewDir, worldN);
        half sunGlint = pow(saturate(dot(reflectSun, sunDir)), 256.0h);
        finalColor += (half3)_GTA_SunColor.rgb * sunGlint * 0.35h * saturate(sunDir.y * 4.0h + 0.35h);
    }

    half3 ambientSH = SampleSH(worldN);
    finalColor = finalColor * 0.82h + ambientSH * waterColor * 0.28h * max((half)_GTA_SkyExposure, 0.5h);

    float baseAlpha = 1.0 - nearFactor * (1.0 - edge01);
    float alpha = max(baseAlpha, (float)foamBlend * 0.95);

    float clearTrail = saturate(shallowClear * (1.0 - (float)foamWeight) * (1.0 - staticWash));
    alpha = lerp(alpha, alpha * 0.12, clearTrail * 0.65);
    finalColor = lerp(finalColor, waterColor * (1.0h + caustic), clearTrail * 0.65);

    float smoothness = 0.88;
    half3 emission = half3(0.0, 0.0, 0.0);
    GTAWaterApplyAtmosphere(finalColor, emission, smoothness, geomN, worldN, viewDir);
    finalColor += emission;

    half3 foamRGB = WaterLiteLitFoam(geomN);

    finalColor = lerp(finalColor, foamRGB, waveFoamBlend);
    alpha = max(alpha * (1.0 - (float)sandZone), (float)waveFoamBlend * 0.95);

    // Sand: fully transparent except animated foam patches (no water color / normals).
    half3 sandFoamRGB = WaterLiteBrightSandFoam(geomN, foamRGB, sandFoamShow);
    half3 sandComposite = sandFoamRGB * sandFoamShow;
    finalColor = lerp(finalColor, sandComposite, sandZone);
    alpha = lerp(alpha, sandFoamAlpha, sandZone);

    finalColor = GTAApplyFog(finalColor, worldPos);

    return half4(finalColor, saturate(alpha));
}

#endif
