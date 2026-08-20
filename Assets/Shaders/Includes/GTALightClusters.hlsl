#ifndef GTA_LIGHT_CLUSTERS_INCLUDED
#define GTA_LIGHT_CLUSTERS_INCLUDED

#include "GTAWorldGlobals.hlsl"

TEXTURE3D(_GTA_LC0);
TEXTURE3D(_GTA_LC1);
TEXTURE3D(_GTA_LC2);
TEXTURE3D(_GTA_LC3);
SAMPLER(sampler_GTA_LC0);

// Matches GTALocalLight.MaxIntensity — keeps albedo*local from blowing out.
#ifndef GTA_LOCAL_LIGHT_MAX
#define GTA_LOCAL_LIGHT_MAX 2.0h
#endif

half3 GTASampleLightClusterVolume(
    TEXTURE3D_PARAM(vol, samp),
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

    half3 rgb = SAMPLE_TEXTURE3D_LOD(vol, samp, uvw, 0).rgb;
    return min(rgb, (half3)GTA_LOCAL_LIGHT_MAX) * opacity;
}

// Baked local lights (street / interior / FX clusters). Cost ≈ up to 4 volume taps.
half3 GTASampleLightClusters(float3 positionWS)
{
    half count = (half)_GTA_LCCount;
    if (count < 0.5h)
        return 0.0h;

    half4 op = _GTA_LCOpacity;
    half3 sum = 0.0h;

    sum += GTASampleLightClusterVolume(
        TEXTURE3D_ARGS(_GTA_LC0, sampler_GTA_LC0),
        positionWS, _GTA_LC0_Min.xyz, _GTA_LC0_InvSize.xyz, op.x);

    if (count >= 1.5h)
    {
        sum += GTASampleLightClusterVolume(
            TEXTURE3D_ARGS(_GTA_LC1, sampler_GTA_LC0),
            positionWS, _GTA_LC1_Min.xyz, _GTA_LC1_InvSize.xyz, op.y);
    }

    if (count >= 2.5h)
    {
        sum += GTASampleLightClusterVolume(
            TEXTURE3D_ARGS(_GTA_LC2, sampler_GTA_LC0),
            positionWS, _GTA_LC2_Min.xyz, _GTA_LC2_InvSize.xyz, op.z);
    }

    if (count >= 3.5h)
    {
        sum += GTASampleLightClusterVolume(
            TEXTURE3D_ARGS(_GTA_LC3, sampler_GTA_LC0),
            positionWS, _GTA_LC3_Min.xyz, _GTA_LC3_InvSize.xyz, op.w);
    }

    // Soft ceiling after stacking clusters (night + interior, etc.).
    half peak = max(sum.r, max(sum.g, sum.b));
    if (peak > GTA_LOCAL_LIGHT_MAX)
        sum *= GTA_LOCAL_LIGHT_MAX / peak;

    return sum;
}

#endif // GTA_LIGHT_CLUSTERS_INCLUDED
