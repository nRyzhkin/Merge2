// Per-renderer baked dirt volumes in world space (up to two zones via MaterialPropertyBlock).
#ifndef GTA_DIRT_MASK_CG_INCLUDED
#define GTA_DIRT_MASK_CG_INCLUDED

sampler3D _GTADirtMask0;
sampler3D _GTADirtMask1;
float4 _GTADirtBoundsMin0;
float4 _GTADirtBoundsMin1;
float4 _GTADirtInvSize0;
float4 _GTADirtInvSize1;
half _GTADirtEdgePadding0;
half _GTADirtEdgePadding1;
half _GTADirtEnabled0;
half _GTADirtEnabled1;

half GtaSampleDirtMaskSlot(
    sampler3D maskTex,
    float4 boundsMin,
    float4 invSize,
    half edgePadding,
    half enabled,
    float3 positionWS)
{
    if (enabled < 0.5h)
        return 0.0h;

    float3 uvw = (positionWS - boundsMin.xyz) * invSize.xyz;
    if (any(uvw < 0.0) || any(uvw > 1.0))
        return 0.0h;

    half volume = tex3Dlod(maskTex, float4(uvw, 0.0)).r;
    if (volume <= 1e-4h)
        return 0.0h;

    float3 edgeDist = min(uvw, 1.0 - uvw);
    half edgeFade = saturate(min(edgeDist.x, min(edgeDist.y, edgeDist.z)) / max(edgePadding, 1e-4h));
    return volume * edgeFade;
}

half GtaSampleDirtMask(float3 positionWS)
{
    half dirt0 = GtaSampleDirtMaskSlot(
        _GTADirtMask0, _GTADirtBoundsMin0, _GTADirtInvSize0, _GTADirtEdgePadding0, _GTADirtEnabled0, positionWS);
    half dirt1 = GtaSampleDirtMaskSlot(
        _GTADirtMask1, _GTADirtBoundsMin1, _GTADirtInvSize1, _GTADirtEdgePadding1, _GTADirtEnabled1, positionWS);
    return max(dirt0, dirt1);
}

void GtaApplyDirtLayer(
    inout half3 albedo,
    inout half smoothness,
    inout half reflectionStrength,
    half3 dirtyAlbedo,
    float3 worldPos)
{
    half dirt = GtaSampleDirtMask(worldPos);
    if (dirt <= 1e-4h)
        return;

    albedo = lerp(albedo, dirtyAlbedo, dirt);
    smoothness = lerp(smoothness, 0.0h, dirt);
    reflectionStrength = lerp(reflectionStrength, 0.0h, dirt);
}

#endif
