#ifndef GTA_LOD_FADE_INCLUDED
#define GTA_LOD_FADE_INCLUDED

#include "GTANoise.hlsl"

// LODGroup fade + optional distance dither fade (opaque queue, no alpha blend).
//
// Far fade: fully visible near camera, fades out between Start→End, invisible past End.
// Near fade: invisible near camera, fades in between Start→End, fully visible past End.
// Both can be enabled at once (soft visibility band).

half GTAComputeDistanceFadeT(float3 positionWS, half fadeStart, half fadeEnd)
{
    half range = max(fadeEnd - fadeStart, 1e-4h);
    half dist = distance(positionWS, GetCameraPositionWS());
    return saturate((dist - fadeStart) / range);
}

void GTAApplyLodDitherFade(float4 positionCS)
{
#if defined(LOD_FADE_CROSSFADE)
    half lodFade = unity_LODFade.x;
    if (abs(lodFade) <= 1e-4h)
        return;

    half dither = GTASampleDither(positionCS);
    half d = lodFade - GTACopySign(dither, lodFade);
    clip(d);
#endif
}

void GTAApplyDistanceDitherFade(float3 positionWS, float4 positionCS, half fadeStart, half fadeEnd)
{
    half t = GTAComputeDistanceFadeT(positionWS, fadeStart, fadeEnd);
    half visibility = 1.0h;

#if defined(_DISTANCE_FADE_FAR)
    // 1 near FadeStart, 0 at/after FadeEnd
    visibility = min(visibility, 1.0h - t);
#endif

#if defined(_DISTANCE_FADE_NEAR)
    // 0 near FadeStart, 1 at/after FadeEnd
    visibility = min(visibility, t);
#endif

#if defined(_DISTANCE_FADE_FAR) || defined(_DISTANCE_FADE_NEAR)
    half dither = GTASampleDither(positionCS);
    clip(visibility - dither);
#endif
}

void GTAApplyAllDitherFades(float3 positionWS, float4 positionCS, half fadeStart, half fadeEnd)
{
    GTAApplyLodDitherFade(positionCS);
    GTAApplyDistanceDitherFade(positionWS, positionCS, fadeStart, fadeEnd);
}

#endif // GTA_LOD_FADE_INCLUDED
