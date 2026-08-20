#ifndef GTA_LIGHTING_INCLUDED
#define GTA_LIGHTING_INCLUDED

#include "GTACommon.hlsl"
#include "GTAWorldGlobals.hlsl"
#include "GTAReflections.hlsl"
#include "GTAShadowCache.hlsl"
#include "GTALightClusters.hlsl"

// Lightweight forward shading for WebGL — driven by GTAWorldController globals.
// Sun shadows come from runtime GTAShadowCache (not URP shadow maps).

struct GTAInputData
{
    float3 positionWS;
    half3  normalWS;
    half3  viewDirectionWS;
    half   fogCoord;
};

void GTAInitializeInputData(
    float3 positionWS,
    float4 positionCS,
    half3 normalWS,
    half4 tangentWS,
    half3 normalTS,
    half fogFactor,
    out GTAInputData inputData)
{
    inputData.positionWS = positionWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
    inputData.fogCoord = GTAInitializeFogCoord(positionWS, fogFactor);
    inputData.normalWS = GTATransformNormalTS(normalTS, normalWS, tangentWS);
}

void GTAApplyWorldWetness(inout half3 albedo, inout half smoothness)
{
    half wet = saturate((half)_GTA_Wetness);
    if (wet <= 1e-4h)
        return;

    half wetSmoothTarget = saturate((half)_GTA_WetMaxSmoothness);
    wetSmoothTarget = clamp(wetSmoothTarget, 0.2h, 0.75h);
    if (wetSmoothTarget < 1e-3h)
        wetSmoothTarget = 0.52h;

    smoothness = saturate(lerp(smoothness, wetSmoothTarget, wet));
    albedo *= lerp(1.0h, 0.86h, wet * 0.75h);
}

half3 GTAShadeSurfaceLit(
    GTAInputData inputData,
    half3 albedo,
    half smoothness,
    half3 emission,
    half occlusion,
    half alpha,
    half reflectionStrength)
{
    albedo *= occlusion;
    GTAApplyWorldWetness(albedo, smoothness);

    half3 sunDir = normalize((half3)_GTA_SunDirection.xyz);
    half NdotL = saturate(dot(inputData.normalWS, sunDir));
    half wrapped = NdotL * 0.5h + 0.5h;
    half shadow = GTASampleShadowCache(inputData.positionWS);

    // Open shade: sky-tinted fill in shadow so Gamma doesn't crush to pure black.
    half3 skyZ = saturate((half3)_GTA_SkyZenith.rgb);
    half3 skyH = saturate((half3)_GTA_SkyHorizon.rgb);
    half3 openShade = (skyZ * 0.55h + skyH * 0.45h) * 0.32h;
    // Moonlit floor when sky HDR is still dark.
    openShade = max(openShade, half3(0.06h, 0.07h, 0.11h));
    half inShade = 1.0h - shadow;

    half3 sun = (half3)_GTA_SunColor.rgb * wrapped * shadow;
    half3 shade = openShade * (0.55h + 0.45h * wrapped) * inShade;
    half3 local = GTASampleLightClusters(inputData.positionWS);
    half3 color = albedo * (sun + shade + local);

    half3 halfDir = SafeNormalize(sunDir + inputData.viewDirectionWS);
    half NdotH = saturate(dot(inputData.normalWS, halfDir));
    half shininess = exp2(10.0h * saturate(smoothness) + 1.0h);
    half spec = pow(NdotH, shininess) * (shininess * 0.04h + 0.5h);
    half wet = saturate((half)_GTA_Wetness);
    half specAmt = lerp(0.04h, 0.5h, smoothness) * spec * NdotL * shadow;
    specAmt *= lerp(1.0h, 1.25h, wet);
    color += (half3)_GTA_SunColor.rgb * specAmt;

    color += GTACalcEnvironmentReflection(
        inputData.normalWS,
        inputData.viewDirectionWS,
        inputData.positionWS,
        smoothness,
        reflectionStrength);

    color += emission;
    color = GTAApplyFog(color, inputData.positionWS);
    return color;
}

half4 GTAShadeSurfacePBR(GTAInputData inputData, SurfaceData surfaceData, half reflectionStrength)
{
    half3 color = GTAShadeSurfaceLit(
        inputData,
        surfaceData.albedo,
        surfaceData.smoothness,
        surfaceData.emission,
        surfaceData.occlusion,
        surfaceData.alpha,
        reflectionStrength);

    return half4(color, surfaceData.alpha);
}

// Kept for forward-pass struct compatibility — no-op in lite mode.
#define GTA_DECLARE_GI_VARYINGS(lmIndex, shIndex)

void GTAApplyBakedGI(float2 lightmapUV, half3 vertexSH, half3 normalWS, inout GTAInputData inputData)
{
}

#endif // GTA_LIGHTING_INCLUDED
