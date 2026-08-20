#ifndef GTA_SKY_SUN_INCLUDED
#define GTA_SKY_SUN_INCLUDED

/// <summary>
/// Artistic sun disc + compact colored corona — shared by GTA/Sky and water reflection.
/// Keep halo small/saturated so the sky stays blue instead of washing to white.
/// </summary>
half3 GTAEvaluateSunHalo(
    float3 viewDirWS,
    float3 sunDirWS,
    half3 sunColor,
    half3 haloColor,
    half sunSize,
    half haloSize,
    half haloIntensity,
    half exposure,
    half haloAmount,
    half sunVisibility,
    half haloVisibility)
{
    float3 V = normalize(viewDirWS);
    float3 S = normalize(sunDirWS);
    half towardSun = saturate(dot(V, S));
    half sunVis = saturate(sunVisibility);
    half haloVis = saturate(haloVisibility);
    half sunVisibilityElev = saturate(S.y * 4.0h + 0.35h);

    half3 c = half3(0.0h, 0.0h, 0.0h);

    half ha = saturate(haloAmount) * haloVis;
    if (ha > 1e-4h)
    {
        // Narrow warm glow — high power kills the huge pastel bloom.
        half glow = pow(towardSun, 4.5h);
        c += haloColor * (glow * haloIntensity * 0.22h * saturate(S.y * 3.0h + 0.4h) * ha);

        half hs = max(min(haloSize, 0.12h), 1e-3h);
        half halo = pow(saturate((towardSun - (1.0h - hs)) / hs), 2.4h);
        c += haloColor * (halo * haloIntensity * 0.85h * sunVisibilityElev * ha);
    }

    half sunCore = smoothstep(1.0h - sunSize * 0.35h, 1.0h - sunSize * 0.02h, towardSun);
    c += sunColor * (sunCore * 2.8h * sunVisibilityElev * sunVis);

    return c * exposure;
}

#endif // GTA_SKY_SUN_INCLUDED
