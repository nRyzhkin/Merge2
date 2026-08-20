#ifndef GTA_SHADOW_CACHE_INCLUDED
#define GTA_SHADOW_CACHE_INCLUDED

#include "GTAWorldGlobals.hlsl"

TEXTURE2D(_GTA_ShadowMap);
SAMPLER(sampler_GTA_ShadowMap);
TEXTURE2D(_GTA_ShadowMapPrev);
SAMPLER(sampler_GTA_ShadowMapPrev);

half GTAShadowLitAmountTex(TEXTURE2D_PARAM(map, samp), float2 uv, float receiverZ, half bias, half softWidth)
{
    half closest = SAMPLE_TEXTURE2D_LOD(map, samp, uv, 0).r;
    half mid = closest + bias;
    half w = max(softWidth, 1e-4h);
    return (half)(1.0 - smoothstep(mid - w, mid + w, receiverZ));
}

half GTASampleShadowAtlas(
    TEXTURE2D_PARAM(map, samp),
    float4x4 worldToShadow,
    float3 positionWS,
    half bias,
    half softWidth,
    float radius,
    half edgeFadeParam)
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

    float2 pix = floor(uv * 2048.0);
    float golden = 2.39996323;
    float rot = frac(sin(dot(pix, float2(12.9898, 78.233))) * 43758.5453) * 6.2831853;
    float cosR = cos(rot);
    float sinR = sin(rot);

    const int kTaps = 16;
    half lit = 0.0h;
    UNITY_UNROLL
    for (int i = 0; i < kTaps; i++)
    {
        float fi = (float)i + 0.5;
        float dist = r * sqrt(fi / (float)kTaps);
        float ang = fi * golden;
        float s, c;
        sincos(ang, s, c);
        float2 offset = float2(c * cosR - s * sinR, c * sinR + s * cosR) * dist;
        lit += GTAShadowLitAmountTex(TEXTURE2D_ARGS(map, samp), uv + offset, uvz.z, bias, softWidth);
    }
    lit *= (1.0h / (half)kTaps);

    return lerp(1.0h, lit, insideFade);
}

half GTASampleShadowCache(float3 positionWS)
{
    half enabled = (half)_GTA_ShadowParams.w;
    if (enabled < 0.5h)
        return 1.0h;

    half strength = saturate((half)_GTA_ShadowParams.y);
    if (strength <= 1e-4h)
        return 1.0h;

    half bias = (half)_GTA_ShadowParams.x;
    half softWidth = max((half)_GTA_ShadowSoftParams.x, 0.0h);
    float texel = max((float)_GTA_ShadowSoftParams.z, 1e-4);
    float radiusTexels = clamp((float)_GTA_ShadowSoftParams.y, 0.35, 3.5);
    float radius = radiusTexels * texel;
    half edgeFadeParam = (half)_GTA_ShadowParams.z;
    half blend = saturate((half)_GTA_ShadowBlend);

    half litNew = GTASampleShadowAtlas(
        TEXTURE2D_ARGS(_GTA_ShadowMap, sampler_GTA_ShadowMap),
        _GTA_WorldToShadow,
        positionWS,
        bias,
        softWidth,
        radius,
        edgeFadeParam);

    half lit = litNew;
    if (blend < 0.999h)
    {
        half litOld = GTASampleShadowAtlas(
            TEXTURE2D_ARGS(_GTA_ShadowMapPrev, sampler_GTA_ShadowMapPrev),
            _GTA_WorldToShadowPrev,
            positionWS,
            bias,
            softWidth,
            radius,
            edgeFadeParam);
        lit = lerp(litOld, litNew, blend);
    }

    return lerp(1.0h, lit, strength);
}

#endif // GTA_SHADOW_CACHE_INCLUDED
