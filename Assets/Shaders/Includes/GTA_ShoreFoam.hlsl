#ifndef GTA_SHORE_FOAM_INCLUDED
#define GTA_SHORE_FOAM_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

// Requires GTA_WaterBakedSurface.hlsl included before this file.

float GTAShoreFoamNoise(
    float2 worldXZ,
    float time,
    float waveSpeed,
    float4 foamNoiseST,
    TEXTURE2D_PARAM(foamNoiseMap, foamNoiseSampler))
{
    float anim = time * waveSpeed;
    float2 uv1 = worldXZ * 0.28 + float2(anim * 0.1, -anim * 0.06);
    float2 uv2 = worldXZ * 1.1 + float2(-anim * 0.3, anim * 0.18);
    float n1 = Luminance(SAMPLE_TEXTURE2D(foamNoiseMap, foamNoiseSampler, uv1 * foamNoiseST.xy + foamNoiseST.zw).rgb);
    float n2 = Luminance(SAMPLE_TEXTURE2D(foamNoiseMap, foamNoiseSampler, uv2 * foamNoiseST.xy + foamNoiseST.zw).rgb);
    return n1 * 0.6 + n2 * 0.4;
}

float GTAShoreShoreDistort(float2 worldXZ, float time, float shoreScale, float waveSpeed)
{
    float scale = max(shoreScale, 0.005);
    float t = time * waveSpeed * 0.4;
    float2 p = worldXZ * scale;
    float2 drift = float2(t * 0.35, -t * 0.22);

    float n1 = WaterCheapNoise2D(p + drift);
    float n2 = WaterCheapNoise2D(p * 0.52 + float2(4.7, 2.3) - drift * 0.6);
    float ripple = sin(p.x * 6.2831853 + t) * sin(p.y * 5.1 - t * 1.15);

    return (n1 * 0.5 + n2 * 0.35 + ripple * 0.15) * 2.0 - 1.0;
}

void GTAShoreFoamEvaluate(
    float3 worldPos,
    float foamDepth,
    float shoreDistort,
    float time,
    float waveFrequency,
    float waveSpeed,
    float waveDist,
    float4 foamNoiseST,
    TEXTURE2D_PARAM(foamNoiseMap, foamNoiseSampler),
    out float staticWash,
    out float animatedFoam)
{
    float edge = WaterBakedEdgeFromDepth_float(foamDepth, waveDist);
    float noise = GTAShoreFoamNoise(worldPos.xz, time, waveSpeed, foamNoiseST, TEXTURE2D_ARGS(foamNoiseMap, foamNoiseSampler));
    float offset = (noise - 0.5) * 0.14 + shoreDistort * 0.06;

    float waveFreq = max(waveFrequency, 1e-3);
    float spatial = edge * waveFreq + offset;
    float waveId = floor(spatial);

    float localT = frac(time * waveSpeed + waveId * 0.618033988 + offset * 0.11);
    float waveMotion = 1.0 - abs(localT * 2.0 - 1.0);
    waveMotion = waveMotion * waveMotion * (3.0 - 2.0 * waveMotion);
    float cycleFade = sin(localT * 3.14159265);
    cycleFade = cycleFade * cycleFade * (3.0 - 2.0 * cycleFade);
    float travelScale = 0.8 / waveFreq;

    float phase = (edge + waveMotion * travelScale + offset) * waveFreq;
    float fl = floor(phase);
    float fr = frac(phase);
    float waveBody = saturate((fl + pow(fr, 10.0)) / waveFreq - waveMotion * travelScale - offset);
    waveBody *= cycleFade;

    float shoreMask = 1.0 - step(0.999, edge);
    float edgeBand = edge * smoothstep(0.0, 1.0, 1.0 - edge);
    float crest = pow(saturate(1.0 - fr), 5.0) * edgeBand * shoreMask * cycleFade;
    waveBody *= edgeBand * shoreMask;

    float clump = lerp(0.7, 1.15, noise);
    animatedFoam = saturate((crest * 1.35 + waveBody * 0.5) * clump);

    float washBias = noise * 0.35 + shoreDistort * 0.25;
    float wash = pow(saturate(1.0 - edge), lerp(1.25, 2.1, washBias)) * shoreMask * 0.55;
    wash *= lerp(0.5, 1.0, noise);
    staticWash = saturate(wash * clump);
}

half GTAShoreFoamWeight(float foamRaw, float nearFactor, float useFoam, float foamCoverage, float foamThickness)
{
    if (useFoam < 0.5)
        return 0.0h;

    float f = saturate(foamRaw * nearFactor * foamCoverage);
    f = 1.0 - pow(saturate(1.0 - f), max(foamThickness, 0.3));
    return saturate(f);
}

half GTAShoreSandZoneMask(float depth, float edge01, float shoreDistort, float foamBeachDepth)
{
    float beachDepth = max(foamBeachDepth, 0.08);
    float sandBeachProx = 1.0 - WaterBakedEdgeFromDepth_float(depth, beachDepth * 2.8);
    half sandZone = (half)saturate(sandBeachProx * 2.0 + (1.0h - (half)edge01) * 0.5);
    sandZone = saturate(sandZone + (half)shoreDistort * 0.1h);
    return smoothstep(0.05h, 0.94h, sandZone);
}

half GTAShoreSurfBandMask(float foamDepth, float depth, float waveDist)
{
    float shoreWashProx = 1.0 - WaterBakedEdgeFromDepth_float(foamDepth, waveDist);
    float shallowProx = WaterBakedShallowFromDepth_float(depth, max(waveDist, 0.5));
    return saturate((shoreWashProx - 0.22) * 1.75) * saturate((shallowProx - 0.18) * 1.65);
}

half GTAShoreWaterRunupMask(float worldY, float waterLevel, float waveDist)
{
    float runUp = max(waveDist * 0.28, 0.15);
    float above = worldY - waterLevel;
    return 1.0 - smoothstep(runUp * 0.2, runUp, above);
}

half GTAShoreAnimatedFoamShow(
    float3 worldPos,
    float waveFrequency,
    float waveSpeed,
    float waveDist,
    float foamShoreWobble,
    float foamShoreScale,
    float foamBeachDepth,
    float foamCoverage,
    float foamThickness,
    float useFoam,
    float4 foamNoiseST,
    TEXTURE2D_PARAM(foamNoiseMap, foamNoiseSampler))
{
    float time = _Time.y;
    float depth = WaterSurfaceSampleDepth(worldPos);
    float edge01 = WaterBakedEdgeFromDepth_float(depth, 0.1);
    float shoreDistort = GTAShoreShoreDistort(worldPos.xz, time, foamShoreScale, waveSpeed);
    float foamDepth = max(depth + shoreDistort * foamShoreWobble, 0.0);

    float staticWash, animatedFoam;
    GTAShoreFoamEvaluate(
        worldPos,
        foamDepth,
        shoreDistort,
        time,
        waveFrequency,
        waveSpeed,
        waveDist,
        foamNoiseST,
        TEXTURE2D_ARGS(foamNoiseMap, foamNoiseSampler),
        staticWash,
        animatedFoam);

    half sandZone = GTAShoreSandZoneMask(depth, edge01, shoreDistort, foamBeachDepth);
    half boostedAnim = saturate((half)animatedFoam * 2.4h);
    half sandFoamWeight = GTAShoreFoamWeight(boostedAnim, 1.0h, useFoam, foamCoverage, foamThickness);
    return saturate(pow(sandFoamWeight, 0.72h) * 1.35h) * sandZone;
}

// Terrain: same wave pattern as water, but only in the surf band and below wave run-up height.
half GTAShoreTerrainFoamShow(
    float3 worldPos,
    float waveFrequency,
    float waveSpeed,
    float waveDist,
    float foamShoreWobble,
    float foamShoreScale,
    float foamBeachDepth,
    float foamCoverage,
    float foamThickness,
    float useFoam,
    float waterLevel,
    float4 foamNoiseST,
    TEXTURE2D_PARAM(foamNoiseMap, foamNoiseSampler))
{
    float time = _Time.y;
    float depth = WaterSurfaceSampleDepth(worldPos);
    float shoreDistort = GTAShoreShoreDistort(worldPos.xz, time, foamShoreScale, waveSpeed);
    float foamDepth = max(depth + shoreDistort * foamShoreWobble, 0.0);

    float staticWash, animatedFoam;
    GTAShoreFoamEvaluate(
        worldPos,
        foamDepth,
        shoreDistort,
        time,
        waveFrequency,
        waveSpeed,
        waveDist,
        foamNoiseST,
        TEXTURE2D_ARGS(foamNoiseMap, foamNoiseSampler),
        staticWash,
        animatedFoam);

    half surfBand = GTAShoreSurfBandMask(foamDepth, depth, waveDist);
    half runupMask = GTAShoreWaterRunupMask(worldPos.y, waterLevel, waveDist);
    half boostedAnim = saturate((half)animatedFoam * 2.4h);
    half sandFoamWeight = GTAShoreFoamWeight(boostedAnim, 1.0h, useFoam, foamCoverage, foamThickness);
    return saturate(pow(sandFoamWeight, 0.72h) * 1.35h) * surfBand * runupMask;
}

half GTAShoreSampleFoamMask(
    float3 worldPos,
    float time,
    float waveSpeed,
    float4 foamNoiseST,
    TEXTURE2D_PARAM(foamNoiseMap, foamNoiseSampler))
{
    float anim = time * waveSpeed;
    float2 uv1 = worldPos.xz * 0.28 + float2(anim * 0.1, -anim * 0.06);
    float2 uv2 = worldPos.xz * 1.1 + float2(-anim * 0.3, anim * 0.18);
    half4 s1 = SAMPLE_TEXTURE2D(foamNoiseMap, foamNoiseSampler, uv1 * foamNoiseST.xy + foamNoiseST.zw);
    half4 s2 = SAMPLE_TEXTURE2D(foamNoiseMap, foamNoiseSampler, uv2 * foamNoiseST.xy + foamNoiseST.zw);
    half m1 = max(Luminance(s1.rgb), s1.a);
    half m2 = max(Luminance(s2.rgb), s2.a);
    half mask = (m1 + m2) * 0.5h;
    return smoothstep(0.18h, 0.55h, mask);
}

#endif // GTA_SHORE_FOAM_INCLUDED
