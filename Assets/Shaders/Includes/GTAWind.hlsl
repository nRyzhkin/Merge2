#ifndef GTA_WIND_INCLUDED
#define GTA_WIND_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Vertex wind — layered sine sway, height-weighted via vertex color alpha.
// Wind parameters are passed in explicitly so this file has no material CBUFFER dependency.

float3 GTAApplyVertexWind(
    float3 positionOS,
    half windWeight,
    half instanceWindOffset,
    half windSpeed,
    half windStrength,
    half windFrequency,
    half3 windDirection)
{
#if defined(_WIND_ON)
    if (windWeight <= 1e-4h)
        return positionOS;

    float3 positionWS = TransformObjectToWorld(positionOS);
    float time = _Time.y * windSpeed + instanceWindOffset;

    float2 phase = positionWS.xz * windFrequency + time;
    half swayA = sin(phase.x) + sin(phase.y * 1.37h + time * 0.63h) * 0.5h;
    half swayB = cos(phase.y * 0.91h + time * 0.41h) + sin(phase.x * 1.13h) * 0.35h;

    float3 windDir = windDirection;
    float len = max(length(windDir), 1e-4);
    windDir /= len;

    float3 windRight = normalize(cross(float3(0.0, 1.0, 0.0), windDir));
    float3 displacementWS = (windRight * swayA + windDir * swayB) * (windStrength * windWeight);

    float3 displacementOS = mul((float3x3)GetWorldToObjectMatrix(), displacementWS);
    positionOS += displacementOS;
#endif
    return positionOS;
}

#endif // GTA_WIND_INCLUDED
