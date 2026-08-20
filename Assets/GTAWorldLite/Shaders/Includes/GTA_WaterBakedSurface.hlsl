#ifndef GTA_WATER_BAKED_SURFACE_INCLUDED
#define GTA_WATER_BAKED_SURFACE_INCLUDED

TEXTURE2D(_WaterSurfaceMap);
SAMPLER(sampler_WaterSurfaceMap);

float2 WaterSurfaceWorldToUV(float2 worldXZ)
{
    return (worldXZ - _WaterSurfaceBounds.xy) / max(_WaterSurfaceBounds.zw, float2(1e-5, 1e-5));
}

// Soft 5-tap sample. Call once per pixel and reuse via FromDepth helpers.
float WaterSurfaceSampleDepthNormalized(float3 worldPos)
{
    float2 uv = saturate(WaterSurfaceWorldToUV(worldPos.xz));
    float2 soft = _WaterSurfaceMap_TexelSize.xy * 1.5;
    float c = SAMPLE_TEXTURE2D(_WaterSurfaceMap, sampler_WaterSurfaceMap, uv).r;
    float n = SAMPLE_TEXTURE2D(_WaterSurfaceMap, sampler_WaterSurfaceMap, saturate(uv + float2(0, soft.y))).r;
    float s = SAMPLE_TEXTURE2D(_WaterSurfaceMap, sampler_WaterSurfaceMap, saturate(uv - float2(0, soft.y))).r;
    float e = SAMPLE_TEXTURE2D(_WaterSurfaceMap, sampler_WaterSurfaceMap, saturate(uv + float2(soft.x, 0))).r;
    float w = SAMPLE_TEXTURE2D(_WaterSurfaceMap, sampler_WaterSurfaceMap, saturate(uv - float2(soft.x, 0))).r;
    return (c * 2.0 + n + s + e + w) * (1.0 / 6.0);
}

float WaterSurfaceSampleDepth(float3 worldPos)
{
    return WaterSurfaceSampleDepthNormalized(worldPos) * _WaterSurfaceDepthMax;
}

float WaterBakedEdgeFromDepth_float(float depth, float offset)
{
    float t = saturate(depth / max(offset, 1e-5));
    return smoothstep(0.0, 1.0, t);
}

float WaterBakedShallowFromDepth_float(float depth, float shallowRange)
{
    return 1.0 - smoothstep(0.0, max(shallowRange, 1e-5), depth);
}

float WaterBakedEdgeDistance_float(float3 worldPos, float offset)
{
    return WaterBakedEdgeFromDepth_float(WaterSurfaceSampleDepth(worldPos), offset);
}

float WaterBakedShallowMask_float(float3 worldPos, float shallowRange)
{
    return WaterBakedShallowFromDepth_float(WaterSurfaceSampleDepth(worldPos), shallowRange);
}

float WaterCheapHash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float WaterCheapNoise2D(float2 uv)
{
    float2 i = floor(uv);
    float2 f = frac(uv);
    f = f * f * (3.0 - 2.0 * f);
    float a = WaterCheapHash21(i);
    float b = WaterCheapHash21(i + float2(1, 0));
    float c = WaterCheapHash21(i + float2(0, 1));
    float d = WaterCheapHash21(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

void WaterCheapFoamFromDepth_float(
    float3 worldPos,
    float depth,
    float time,
    float waveFrequency,
    float waveSpeed,
    float waveDist,
    out float waves,
    out float foam)
{
    float edge = WaterBakedEdgeFromDepth_float(depth, waveDist);
    float anim = time * waveSpeed;

    float n1 = WaterCheapNoise2D(worldPos.xz * 0.28 + float2(anim * 0.1, -anim * 0.06));
    float n2 = WaterCheapNoise2D(worldPos.xz * 1.1 + float2(-anim * 0.3, anim * 0.18));
    float noise = n1 * 0.6 + n2 * 0.4;

    float offset = (noise - 0.5) * 0.08;
    float phase = (edge + anim + offset) * max(waveFrequency, 1e-3);
    float fl = floor(phase);
    float fr = frac(phase);

    float waveBody = saturate((fl + pow(fr, 20.0)) / max(waveFrequency, 1e-3) - anim - offset);
    waves = saturate(2.0 * waveBody);

    float shoreMask = 1.0 - step(0.999, edge);
    float edgeBand = edge * smoothstep(0.0, 1.0, 1.0 - edge);

    float crest = pow(saturate(1.0 - fr), 7.0) * edgeBand * shoreMask;
    float wash = pow(saturate(1.0 - edge), 1.6) * shoreMask * 0.55;
    wash *= lerp(0.5, 1.0, noise);

    float clump = lerp(0.7, 1.15, noise);
    foam = saturate(wash * clump + crest * 1.35 * clump);
    waves = max(waves, foam * 0.55);
}

void WaterCheapFoam_float(
    float3 worldPos,
    float time,
    float waveFrequency,
    float waveSpeed,
    float waveDist,
    out float waves,
    out float foam)
{
    WaterCheapFoamFromDepth_float(
        worldPos,
        WaterSurfaceSampleDepth(worldPos),
        time,
        waveFrequency,
        waveSpeed,
        waveDist,
        waves,
        foam);
}

#endif
