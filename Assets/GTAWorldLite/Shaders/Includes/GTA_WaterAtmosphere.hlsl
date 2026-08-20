#ifndef GTA_WATER_ATMOSPHERE_INCLUDED
#define GTA_WATER_ATMOSPHERE_INCLUDED

#include "GTAWorldGlobals.hlsl"
#include "GTA_SkySun.hlsl"

// Water receives the sky sun/halo at this fraction of sky brightness.
#ifndef GTA_WATER_SUN_HALO_SCALE
#define GTA_WATER_SUN_HALO_SCALE 0.5h
#endif

// Wave normals drive glitter breakup (near 1 = scattered path like reference sunset).
#ifndef GTA_WATER_SUN_WAVE_BLEND
#define GTA_WATER_SUN_WAVE_BLEND 0.92h
#endif

/// <summary>
/// Sky sun disc (+ dusk/dawn halo) mirrored into water with wave-driven glitter.
/// Midday (TimeOfDay ~0.27–0.7): disc only — no dirty halo wash.
/// </summary>
void GTAWaterApplyAtmosphere(
    inout float3 baseColor,
    inout float3 emission,
    inout float smoothness,
    float3 geomNormalWS,
    float3 detailNormalWS,
    float3 viewDirWS)
{
    // Direct GGX from the sun is disabled via _SPECULARHIGHLIGHTS_OFF.
    smoothness = min(smoothness, 0.88);

    float3 V = normalize(viewDirWS);
    float3 Ngeom = normalize(geomNormalWS);
    float3 Ndetail = normalize(detailNormalWS);

    // Mild upright bias — allow wave tilt so the path breaks into glints.
    Ngeom = normalize(float3(Ngeom.x * 0.25, max(Ngeom.y, 0.35), Ngeom.z * 0.25));
    Ndetail = normalize(float3(Ndetail.x, max(Ndetail.y, 0.08), Ndetail.z));

    float3 sunDir = _GTA_SunDirection.xyz;
    float sunLen2 = dot(sunDir, sunDir);
    if (sunLen2 > 1e-6)
        sunDir *= rsqrt(sunLen2);
    else
        sunDir = float3(0.35, 0.55, 0.2);

    half3 sunColor = (half3)max(_GTA_SunColor.rgb, float3(0.15, 0.12, 0.08)) * 1.35h;
    half3 haloColor = (half3)_GTA_HaloColor.rgb;
    if (dot(haloColor, haloColor) < 1e-4h)
        haloColor = sunColor * half3(1.0h, 0.75h, 0.45h);

    half sunSize = (half)_GTA_SunSize;
    half haloSize = (half)_GTA_HaloSize;
    half haloIntensity = (half)_GTA_HaloIntensity;
    half exposure = (half)_GTA_SkyExposure;
    if (sunSize < 1e-4h) sunSize = 0.02h;
    if (haloSize < 1e-4h) haloSize = 0.18h;
    if (haloIntensity < 1e-4h) haloIntensity = 1.2h;
    if (exposure < 1e-4h) exposure = 1.0h;

    // ~25% smaller than previous water disc (was * 1.45).
    sunSize *= 1.0875h;

    // Halo only outside midday window (~0.27 … 0.7), scaled by weather overcast.
    float tod = saturate(_GTA_TimeOfDay);
    float midday = smoothstep(0.25, 0.27, tod) * (1.0 - smoothstep(0.70, 0.73, tod));
    half haloAmount = (half)(1.0 - midday) * (half)saturate(_GTA_HaloVisibility);
    half sunVis = (half)saturate(_GTA_SunVisibility);
    half haloVis = (half)saturate(_GTA_HaloVisibility);

    // Strong wave blend → glitter path along normals (reference sunset look).
    float3 Nglint = normalize(lerp(Ngeom, Ndetail, (float)GTA_WATER_SUN_WAVE_BLEND));
    float3 R = reflect(-V, Nglint);

    half3 sun = GTAEvaluateSunHalo(
        R, sunDir, sunColor, haloColor,
        sunSize, haloSize * 1.15h, haloIntensity, exposure, haloAmount,
        sunVis, haloVis);

    // Extra scattered sparkle from fuller wave normal (disc-only, tight).
    float3 Nscatter = normalize(lerp(Ngeom, Ndetail, min(1.0, (float)GTA_WATER_SUN_WAVE_BLEND + 0.06)));
    float3 Rscatter = reflect(-V, Nscatter);
    half3 sparkle = GTAEvaluateSunHalo(
        Rscatter, sunDir, sunColor, haloColor,
        sunSize * 0.85h, haloSize, haloIntensity, exposure, 0.0h,
        sunVis, 0.0h);

    half NdotV = saturate(dot(Nglint, V));
    half fresnel = exp2((-5.55473h * NdotV - 6.98316h) * NdotV);
    half reflectWeight = lerp(0.45h, 1.0h, fresnel);

    emission += (float3)((sun * 0.75h + sparkle * 0.45h) * (GTA_WATER_SUN_HALO_SCALE * reflectWeight));

    // Body tint only when halo is allowed (dawn / dusk) and sky is visible.
    if (haloAmount > 0.05h && sunVis > 0.05h)
    {
        float elev = sunDir.y;
        float lowSun = saturate(1.0 - abs(elev) * 2.6) * saturate(elev * 6.0 + 0.85);
        float mood = lowSun * (float)haloAmount;
        if (mood > 1e-4)
        {
            float3 tint = lerp(_GTA_FogColor.rgb, _GTA_SunColor.rgb, 0.4) + _GTA_AmbientColor.rgb * 0.15;
            baseColor = lerp(baseColor, baseColor * (tint * 1.25) + tint * 0.06, mood * 0.55);
        }
    }
}

#endif // GTA_WATER_ATMOSPHERE_INCLUDED
