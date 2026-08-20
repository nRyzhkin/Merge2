#ifndef GTA_DECAL_COMMON_INCLUDED
#define GTA_DECAL_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

// Mesh decal depth bias — prevents z-fighting without transparent blending.

void GTADecalApplyViewBias(inout float3 positionOS, half viewBias)
{
    if (viewBias <= 1e-5h)
        return;

    float3 viewDirOS = GetObjectSpaceNormalizeViewDir(positionOS);
    positionOS += viewDirOS * viewBias;
}

void GTADecalApplyNormalBias(inout float3 positionOS, half3 normalOS, half normalBias)
{
    if (normalBias <= 1e-5h)
        return;

    positionOS += normalOS * normalBias;
}

void GTADecalApplyClipDepthBias(inout float4 positionCS, half depthBias)
{
    if (depthBias <= 1e-5h)
        return;

#if UNITY_REVERSED_Z
    positionCS.z -= depthBias;
#else
    positionCS.z += depthBias;
#endif
}

void GTADecalSceneDepthClip(float4 positionCS, half depthThreshold)
{
#if defined(_DEPTH_TEST_ON)
    float2 screenUV = positionCS.xy * _ScaledScreenParams.zw;
    float sceneDepth = SampleSceneDepth(screenUV);
    float meshDepth = positionCS.z / positionCS.w;
    half delta = abs(sceneDepth - meshDepth);
    clip(depthThreshold - delta);
#endif
}

half3 GTABlendNormalOverlay(half3 baseNormalWS, half3 overlayNormalWS, half strength)
{
    if (strength <= 1e-4h)
        return baseNormalWS;

    half3 t = baseNormalWS + half3(0.0h, 0.0h, 1.0h);
    half3 u = overlayNormalWS * half3(-1.0h, -1.0h, 1.0h);
    half3 rnm = normalize(t * dot(t, u) - u * t.z);
    return normalize(lerp(baseNormalWS, rnm, saturate(strength)));
}

half3 GTAOverlayAlbedo(half3 baseAlbedo, half3 decalAlbedo, half strength, half mask)
{
    half blend = saturate(strength * mask);
    return lerp(baseAlbedo, decalAlbedo, blend);
}

half GTAOverlayRoughness(half baseSmoothness, half decalSmoothness, half strength, half mask)
{
    return lerp(baseSmoothness, decalSmoothness, saturate(strength * mask));
}

#endif // GTA_DECAL_COMMON_INCLUDED
