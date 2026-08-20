// Shared WorldController lighting for CG foliage/default shaders (GPU-instancing path).
#ifndef GTA_WORLD_LIGHTING_CG_INCLUDED
#define GTA_WORLD_LIGHTING_CG_INCLUDED

#include "UnityCG.cginc"

float4 _GTA_AmbientColor;
float4 _GTA_SunColor;
float4 _GTA_SunDirection;
float4 _GTA_FogColor;
float4 _GTA_SkyZenith;
float4 _GTA_SkyHorizon;
float  _GTA_FogStart;
float  _GTA_FogEnd;
float  _GTA_FogEnabled;
float  _GTA_ReflectionIntensity;
float  _GTA_Wetness;
float  _GTA_WetMaxSmoothness;

// Baked local light clusters (GTALightClusterController globals).
float  _GTA_LCCount;
float4 _GTA_LCOpacity;
float4 _GTA_LC0_Min;
float4 _GTA_LC0_InvSize;
float4 _GTA_LC1_Min;
float4 _GTA_LC1_InvSize;
float4 _GTA_LC2_Min;
float4 _GTA_LC2_InvSize;
float4 _GTA_LC3_Min;
float4 _GTA_LC3_InvSize;
sampler3D _GTA_LC0;
sampler3D _GTA_LC1;
sampler3D _GTA_LC2;
sampler3D _GTA_LC3;

#ifndef GTA_LOCAL_LIGHT_MAX
#define GTA_LOCAL_LIGHT_MAX 2.0h
#endif

float4x4 _GTA_WorldToShadow;
float4x4 _GTA_WorldToShadowPrev;
float4 _GTA_ShadowParams;
float4 _GTA_ShadowSoftParams;
float  _GTA_ShadowBlend;
sampler2D _GTA_ShadowMap;
sampler2D _GTA_ShadowMapPrev;

float IsGtaDithered(float2 screenUV, float alpha)
{
    float2 pos = screenUV * _ScreenParams.xy;
    float4x4 thresholdMatrix =
    {
        1.0 / 17.0,  9.0 / 17.0,  3.0 / 17.0, 11.0 / 17.0,
        13.0 / 17.0, 5.0 / 17.0, 15.0 / 17.0,  7.0 / 17.0,
         4.0 / 17.0, 12.0 / 17.0,  2.0 / 17.0, 10.0 / 17.0,
        16.0 / 17.0, 8.0 / 17.0, 14.0 / 17.0,  6.0 / 17.0
    };
    return alpha - thresholdMatrix[fmod(pos.x, 4)][fmod(pos.y, 4)];
}

void GtaDitherClip(float4 screenPos, float alpha)
{
    clip(IsGtaDithered(screenPos.xy / max(screenPos.w, 1e-5), alpha));
}

half GtaShadowLitAmountTex(sampler2D shadowMap, float2 uv, float receiverZ, half bias, half softWidth)
{
    half closest = tex2Dlod(shadowMap, float4(uv, 0, 0)).r;
    half mid = closest + bias;
    half w = max(softWidth, 1e-4h);
    return (half)(1.0 - smoothstep(mid - w, mid + w, receiverZ));
}

// Dense Vogel disk — avoids the "9 hard ghost shadows" look of a sparse cross kernel.
half GtaSampleShadowAtlas(sampler2D shadowMap, float4x4 worldToShadow, float3 positionWS, half bias, half softWidth, float radius, half edgeFadeParam)
{
    float4 shadowCoord = mul(worldToShadow, float4(positionWS, 1.0));
    float3 uvz = shadowCoord.xyz;

    if (any(uvz.xy < 0.0) || any(uvz.xy > 1.0))
        return 1.0h;

    if (uvz.z <= 0.0 || uvz.z >= 1.0)
        return 1.0h;

    float halfFade = max((float)edgeFadeParam, 0.05);
    float2 edgeDist = min(uvz.xy, 1.0 - uvz.xy);
    float edgeMargin = max(halfFade * 0.5, 1e-3);
    half insideFade = (half)saturate(min(edgeDist.x, edgeDist.y) / edgeMargin);
    if (insideFade <= 1e-3h)
        return 1.0h;

    float2 uv = uvz.xy;
    float r = max(radius, 1e-5);

    // Screen-stable rotation breaks banding without temporal flicker.
    float2 pix = floor(uv * 2048.0);
    float golden = 2.39996323; // 2π / φ²
    float rot = frac(sin(dot(pix, float2(12.9898, 78.233))) * 43758.5453) * 6.2831853;
    float cosR = cos(rot);
    float sinR = sin(rot);

    const int kTaps = 16;
    half lit = 0.0h;
    [unroll]
    for (int i = 0; i < kTaps; i++)
    {
        float fi = (float)i + 0.5;
        float dist = r * sqrt(fi / (float)kTaps);
        float ang = fi * golden;
        float s, c;
        sincos(ang, s, c);
        float2 offset = float2(c * cosR - s * sinR, c * sinR + s * cosR) * dist;
        lit += GtaShadowLitAmountTex(shadowMap, uv + offset, uvz.z, bias, softWidth);
    }
    lit *= (1.0h / (half)kTaps);

    return lerp(1.0h, lit, insideFade);
}

half GtaSampleShadowCache(float3 positionWS)
{
    if (_GTA_ShadowParams.w < 0.5)
        return 1.0h;

    half strength = saturate(_GTA_ShadowParams.y);
    if (strength <= 1e-4h)
        return 1.0h;

    half bias = _GTA_ShadowParams.x;
    // Soft Depth Width: contact / acne soften only — keep small.
    half softWidth = max(_GTA_ShadowSoftParams.x, 0.0h);
    float texel = max((float)_GTA_ShadowSoftParams.z, 1e-4);
    // Soft Filter Radius (texels): penumbra width. Cap so the kernel stays dense.
    float radiusTexels = clamp((float)_GTA_ShadowSoftParams.y, 0.35, 3.5);
    float radius = radiusTexels * texel;
    half edgeFadeParam = _GTA_ShadowParams.z;
    half blend = saturate(_GTA_ShadowBlend);

    half litNew = GtaSampleShadowAtlas(
        _GTA_ShadowMap, _GTA_WorldToShadow, positionWS, bias, softWidth, radius, edgeFadeParam);
    half lit = litNew;
    if (blend < 0.999h)
    {
        half litPrev = GtaSampleShadowAtlas(
            _GTA_ShadowMapPrev, _GTA_WorldToShadowPrev, positionWS, bias, softWidth, radius, edgeFadeParam);
        lit = lerp(litPrev, litNew, blend);
    }

    return lerp(1.0h, lit, strength);
}

half3 GtaApplyFog(half3 color, float3 worldPos)
{
    if (_GTA_FogEnabled < 0.5)
        return color;

    float dist = length(worldPos - _WorldSpaceCameraPos.xyz);
    half fog = saturate((dist - _GTA_FogStart) / max(_GTA_FogEnd - _GTA_FogStart, 1e-3));
    if (fog <= 1e-4h)
        return color;

    return lerp(color, (half3)_GTA_FogColor.rgb, fog);
}

half3 GtaSampleLightClusterVolume(
    sampler3D vol,
    float3 positionWS,
    float3 boundsMin,
    float3 invSize,
    half opacity)
{
    opacity = saturate(opacity);
    if (opacity <= 1e-4h)
        return 0.0h;

    float3 uvw = (positionWS - boundsMin) * invSize;
    if (any(uvw < 0.0) || any(uvw > 1.0))
        return 0.0h;

    half3 rgb = tex3Dlod(vol, float4(uvw, 0.0)).rgb;
    return min(rgb, (half3)GTA_LOCAL_LIGHT_MAX) * opacity;
}

// Matches HLSL GTASampleLightClusters — street / interior / FX volumes.
half3 GtaSampleLightClusters(float3 positionWS)
{
    half count = (half)_GTA_LCCount;
    if (count < 0.5h)
        return 0.0h;

    half4 op = (half4)_GTA_LCOpacity;
    half3 sum = 0.0h;

    sum += GtaSampleLightClusterVolume(
        _GTA_LC0, positionWS, _GTA_LC0_Min.xyz, _GTA_LC0_InvSize.xyz, op.x);

    if (count >= 1.5h)
    {
        sum += GtaSampleLightClusterVolume(
            _GTA_LC1, positionWS, _GTA_LC1_Min.xyz, _GTA_LC1_InvSize.xyz, op.y);
    }

    if (count >= 2.5h)
    {
        sum += GtaSampleLightClusterVolume(
            _GTA_LC2, positionWS, _GTA_LC2_Min.xyz, _GTA_LC2_InvSize.xyz, op.z);
    }

    if (count >= 3.5h)
    {
        sum += GtaSampleLightClusterVolume(
            _GTA_LC3, positionWS, _GTA_LC3_Min.xyz, _GTA_LC3_InvSize.xyz, op.w);
    }

    half peak = max(sum.r, max(sum.g, sum.b));
    if (peak > GTA_LOCAL_LIGHT_MAX)
        sum *= GTA_LOCAL_LIGHT_MAX / peak;

    return sum;
}

// Same math as URP BoxProjectedCubemapDirection / GTAReflections.hlsl.
half3 GtaBoxProjectedCubemapDirection(
    half3 reflectionWS,
    float3 positionWS,
    float4 cubemapPositionWS,
    float4 boxMin,
    float4 boxMax)
{
    // ProbePosition.w > 0 means this probe uses box projection.
    if (cubemapPositionWS.w > 0.0h)
    {
        float3 boxMinMax = (reflectionWS > 0.0h) ? boxMax.xyz : boxMin.xyz;
        half3 rbMinMax = half3(boxMinMax - positionWS) / reflectionWS;
        half fa = min(min(rbMinMax.x, rbMinMax.y), rbMinMax.z);
        half3 localPos = half3(positionWS - cubemapPositionWS.xyz);
        return localPos + reflectionWS * fa;
    }

    return reflectionWS;
}

half3 GtaShadeLit(
    half3 albedo,
    float3 normalWS,
    float3 positionWS,
    half smoothness,
    half3 emission,
    half occlusion,
    half reflectionStrength)
{
    half wet = saturate((half)_GTA_Wetness);
    if (wet > 1e-4h)
    {
        half wetSmooth = clamp(saturate((half)_GTA_WetMaxSmoothness), 0.2h, 0.75h);
        if (wetSmooth < 1e-3h)
            wetSmooth = 0.52h;
        smoothness = saturate(lerp(smoothness, wetSmooth, wet));
        albedo *= lerp(1.0h, 0.86h, wet * 0.75h);
    }

    albedo *= occlusion;

    float3 normal = normalize(normalWS);
    float3 sunDir = normalize(_GTA_SunDirection.xyz);
    half3 sunCol = (half3)_GTA_SunColor.rgb;

    half NdotL = saturate(dot(normal, sunDir));
    half wrapped = NdotL * 0.5h + 0.5h;
    half shadow = GtaSampleShadowCache(positionWS);

    half3 skyZ = saturate((half3)_GTA_SkyZenith.rgb);
    half3 skyH = saturate((half3)_GTA_SkyHorizon.rgb);
    half3 openShade = (skyZ * 0.55h + skyH * 0.45h) * 0.32h;
    openShade = max(openShade, half3(0.06h, 0.07h, 0.11h));
    half inShade = 1.0h - shadow;

    half3 sun = sunCol * wrapped * shadow;
    half3 shade = openShade * (0.55h + 0.45h * wrapped) * inShade;
    half3 local = GtaSampleLightClusters(positionWS);
    half3 color = albedo * (sun + shade + local);

    float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - positionWS);
    float3 halfDir = normalize(sunDir + viewDir);
    half NdotH = saturate(dot(normal, halfDir));
    half shininess = exp2(10.0h * saturate(smoothness) + 1.0h);
    half spec = pow(NdotH, shininess) * (shininess * 0.04h + 0.5h);
    half specAmt = lerp(0.04h, 0.5h, smoothness) * spec * NdotL * shadow;
    specAmt *= lerp(1.0h, 1.25h, wet);
    color += sunCol * specAmt;

    half reflStrength = saturate(reflectionStrength) * saturate((half)_GTA_ReflectionIntensity);
    if (reflStrength > 1e-4h)
    {
        half3 reflectVec = reflect(-viewDir, normal);
        // Match glass / GTAReflections.hlsl: box-projected reflection probes.
        reflectVec = GtaBoxProjectedCubemapDirection(
            reflectVec,
            positionWS,
            unity_SpecCube0_ProbePosition,
            unity_SpecCube0_BoxMin,
            unity_SpecCube0_BoxMax);
        half4 envSample = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, reflectVec, 0);
        half3 env = DecodeHDR(envSample, unity_SpecCube0_HDR);
        half NdotV = saturate(dot(normal, viewDir));
        half fresnel = pow(1.0h - NdotV, lerp(2.0h, 5.0h, smoothness));
        color += env * lerp(0.35h, 1.0h, fresnel) * reflStrength;
    }

    color += emission;
    return GtaApplyFog(color, positionWS);
}

#endif // GTA_WORLD_LIGHTING_CG_INCLUDED
