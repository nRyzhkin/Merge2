#ifndef GTA_REFLECTIONS_INCLUDED
#define GTA_REFLECTIONS_INCLUDED

#include "GTAWorldGlobals.hlsl"

// Same math as URP BoxProjectedCubemapDirection — local copy to avoid heavy GI includes.
half3 GTABoxProjectedCubemapDirection(
    half3 reflectionWS,
    float3 positionWS,
    float4 cubemapPositionWS,
    float4 boxMin,
    float4 boxMax)
{
    // ProbePosition.w > 0 means this probe uses box projection.
    if (cubemapPositionWS.w > 0.0h)
    {
        float3 boxMinMax = (reflectionWS > 0.0h) ? boxMax.xyz : boxMin.xyz;
        half3 rbMinMax = half3(boxMinMax - positionWS) / reflectionWS;
        half fa = min(min(rbMinMax.x, rbMinMax.y), rbMinMax.z);
        half3 localPos = half3(positionWS - cubemapPositionWS.xyz);
        return localPos + reflectionWS * fa;
    }

    return reflectionWS;
}

// Cheap env sample: fixed cubemap LOD, raw RGB (no HDR decode, no roughness mips).
half3 GTASampleSpecularCubemap(half3 reflectVector, float3 positionWS)
{
#if defined(_ENVIRONMENTREFLECTIONS_OFF)
    return _GlossyEnvironmentColor.rgb;
#else
    reflectVector = GTABoxProjectedCubemapDirection(
        reflectVector,
        positionWS,
        unity_SpecCube0_ProbePosition,
        unity_SpecCube0_BoxMin,
        unity_SpecCube0_BoxMax);

    return SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectVector, 0.0h).rgb;
#endif
}

half3 GTACalcEnvironmentReflection(
    half3 normalWS,
    half3 viewDirWS,
    float3 positionWS,
    half smoothness,
    half reflectionStrength)
{
    half strength = saturate(reflectionStrength) * saturate((half)_GTA_ReflectionIntensity);
    if (strength <= 1e-4h)
        return 0;

    half3 reflectVector = reflect(-viewDirWS, normalWS);
    half3 env = GTASampleSpecularCubemap(reflectVector, positionWS);

    half NdotV = saturate(dot(normalWS, viewDirWS));
    half fresnel = pow(1.0h - NdotV, lerp(2.0h, 5.0h, smoothness));
    return env * lerp(0.35h, 1.0h, fresnel) * strength;
}

#endif // GTA_REFLECTIONS_INCLUDED
