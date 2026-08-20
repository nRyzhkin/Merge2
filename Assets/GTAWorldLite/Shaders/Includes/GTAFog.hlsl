#ifndef GTA_FOG_INCLUDED
#define GTA_FOG_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "GTAWorldGlobals.hlsl"
#include "GTASkyHorizon.hlsl"

half GTAComputeDistanceFog(float3 positionWS)
{
    if (_GTA_FogEnabled < 0.5)
        return 0.0h;

    float dist = length(positionWS - _WorldSpaceCameraPos.xyz);
    return saturate((dist - _GTA_FogStart) / max(_GTA_FogEnd - _GTA_FogStart, 1e-3));
}

half GTAComputeFogFactor(float positionCSZ)
{
    return 0.0h;
}

half GTAInitializeFogCoord(float3 positionWS, half fogFactorVertex)
{
    return GTAComputeDistanceFog(positionWS);
}

half3 GTAApplyFog(half3 color, float3 positionWS)
{
    if (_GTA_FogEnabled < 0.5)
        return color;

    half fog = GTAComputeDistanceFog(positionWS);
    if (fog <= 1e-4h)
        return color;

    half3 fogColor = GTAEvaluateHorizonSky(GTAFogViewDirection(positionWS));
    return lerp(color, fogColor, fog);
}

half3 GTAApplyFog(half3 color, half fogFactor)
{
    if (_GTA_FogEnabled < 0.5)
        return color;

    if (fogFactor <= 1e-4h)
        return color;
    return lerp(color, _GTA_FogColor.rgb, saturate(fogFactor));
}

#endif // GTA_FOG_INCLUDED
