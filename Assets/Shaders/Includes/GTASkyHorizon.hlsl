#ifndef GTA_SKY_HORIZON_INCLUDED
#define GTA_SKY_HORIZON_INCLUDED

#include "GTAWorldGlobals.hlsl"

// Sky palette mirrored from GTA/Sky material — set by GTAWorldController.
float4 _GTA_SkyZenith;
float4 _GTA_SkyHorizon;
float4 _GTA_SkyGround;
float4 _GTA_SkyHaze;
float  _GTA_HorizonHaze;
float  _GTA_WeatherGrey;

// Matches GTAEvaluateSky in GTA_Sky.shader (without sun disc — fog is a broad band).
half3 GTAEvaluateHorizonSky(float3 viewDir)
{
    float3 V = normalize(viewDir);
    float3 S = normalize(_GTA_SunDirection.xyz);
    half y = V.y;

    half belowHorizon = saturate((-y) * 3.0h);
    half horizonBlend = 1.0h - saturate(abs(y) * 4.5h);
    half zenithBlend = saturate(y * 2.35h);
    half3 sky = lerp(_GTA_SkyHorizon.rgb, _GTA_SkyZenith.rgb, zenithBlend);
    sky = lerp(sky, _GTA_SkyHorizon.rgb, belowHorizon);

    half sunDot = dot(V, S);
    half towardSun = saturate(sunDot);
    half antiSun = saturate(-sunDot);

    half sunsetStrength = saturate(1.0h - abs(S.y) * 2.2h);
    sunsetStrength *= saturate(S.y * 6.0h + 0.85h);

    half3 coolSky = _GTA_SkyZenith.rgb * half3(0.55h, 0.65h, 0.95h);
    sky = lerp(sky, coolSky, antiSun * antiSun * lerp(0.25h, 0.55h, sunsetStrength));

    half sunAzimuthWeight = towardSun * towardSun * 0.65h + 0.35h;
    half haze = pow(horizonBlend, 1.25h) * _GTA_HorizonHaze * sunAzimuthWeight;
    half3 hazeCol = lerp(_GTA_SkyHaze.rgb, _GTA_SkyHorizon.rgb, sunsetStrength * 0.65h);
    sky = lerp(sky, hazeCol, saturate(haze));

    half sunsetCore = pow(towardSun, 3.8h);
    half3 warm = _GTA_HaloColor.rgb;
    half warmVis = saturate(_GTA_HaloVisibility);
    sky = lerp(sky, warm, sunsetCore * sunsetStrength * 0.35h * warmVis);
    sky += warm * (sunsetCore * sunsetStrength * 0.08h * warmVis);

    // No broad daytime halo wash in fog/horizon band — keep sky chroma.
    half glow = pow(towardSun, 4.5h);
    sky += warm * (glow * _GTA_HaloIntensity * 0.12h * saturate(S.y * 3.0h + 0.4h) * warmVis);

    half grey = dot(sky, half3(0.299h, 0.587h, 0.114h));
    sky = lerp(sky, half3(grey, grey, grey) * lerp(0.85h, 1.15h, y * 0.5h + 0.5h), _GTA_WeatherGrey);

    return sky * _GTA_SkyExposure;
}

float3 GTAFogViewDirection(float3 positionWS)
{
    float3 dir = positionWS - _WorldSpaceCameraPos.xyz;
    float len2 = dot(dir, dir);
    if (len2 < 1e-6)
        return float3(0.0, 0.04, 1.0);

    dir *= rsqrt(len2);
    dir.y = clamp(dir.y, 0.02, 0.14);
    return normalize(dir);
}

#endif // GTA_SKY_HORIZON_INCLUDED
