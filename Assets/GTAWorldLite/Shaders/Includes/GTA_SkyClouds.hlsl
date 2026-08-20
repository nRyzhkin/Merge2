#ifndef GTA_SKY_CLOUDS_INCLUDED
#define GTA_SKY_CLOUDS_INCLUDED

/// <summary>
/// Ultra-cheap procedural sky clouds driven by _GTA_Weather (0..1).
/// Single white layer, lightly tinted by the current sky palette.
/// </summary>

float GTA_CloudHash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float GTA_CloudValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = GTA_CloudHash21(i);
    float b = GTA_CloudHash21(i + float2(1.0, 0.0));
    float c = GTA_CloudHash21(i + float2(0.0, 1.0));
    float d = GTA_CloudHash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float GTA_CloudFbm(float2 p)
{
    float v = 0.0;
    v += 0.50 * GTA_CloudValueNoise(p);
    v += 0.30 * GTA_CloudValueNoise(p * 2.13 + float2(17.1, 9.4));
    v += 0.20 * GTA_CloudValueNoise(p * 4.27 + float2(5.2, 31.7));
    return v;
}

half GTA_CloudDensity(float n, half threshold, half skyMask)
{
    half dens = saturate(((half)n - threshold) / max(1.0h - threshold, 0.05h));
    dens = dens * dens * (3.0h - 2.0h * dens);
    return dens * skyMask;
}

half4 GTAEvaluateSkyClouds(
    float3 viewDirWS,
    half3 zenithColor,
    half3 horizonColor,
    float weather)
{
    half w = saturate((half)weather);
    half cover = saturate(w * 1.45h);
    if (cover < 1e-3h)
        return half4(0.0h, 0.0h, 0.0h, 0.0h);

    float3 V = normalize(viewDirWS);
    half skyMask = smoothstep(0.02h, 0.14h, (half)V.y);
    skyMask *= 1.0h - smoothstep(0.72h, 1.05h, (half)V.y) * lerp(0.55h, 0.15h, cover);
    if (skyMask < 1e-3h)
        return half4(0.0h, 0.0h, 0.0h, 0.0h);

    float y = max(V.y, 0.05);
    float2 uv = V.xz * (1.15 / y);
    uv *= 0.95;

    float t = _TimeParameters.x;
    float ang = t * 0.012;
    float sa, ca;
    sincos(ang, sa, ca);
    uv = float2(uv.x * ca - uv.y * sa, uv.x * sa + uv.y * ca);

    half threshold = lerp(0.78h, 0.22h, cover);
    half storm = smoothstep(0.42h, 1.0h, w);
    half sheet = smoothstep(0.55h, 1.0h, w);

    float nBody = GTA_CloudFbm(uv);
    half dens = GTA_CloudDensity(nBody, threshold, skyMask);
    half densHard = smoothstep(0.05h, 0.55h, dens);
    densHard = lerp(dens, densHard * densHard * (3.0h - 2.0h * densHard), 0.55h);

    half alpha = densHard * lerp(0.62h, 0.82h, cover);
    alpha = saturate(alpha + sheet * densHard * 0.10h);

    half3 skyTint = lerp(zenithColor, horizonColor, saturate((half)V.y * 0.65h + 0.35h));
    half skyLum = max(dot(skyTint, half3(0.299h, 0.587h, 0.114h)), 0.02h);
    half3 cloudCol = lerp(half3(1.02h, 1.03h, 1.05h), skyTint * 1.12h, 0.42h);
    half3 stormGrey = half3(skyLum, skyLum, skyLum) * 1.08h;
    cloudCol = lerp(cloudCol, stormGrey, storm * 0.55h);

    return half4(cloudCol, alpha);
}

#endif // GTA_SKY_CLOUDS_INCLUDED
