#ifndef GTA_SHADOWS_INCLUDED
#define GTA_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

// Shadow coordinate helpers shared across GTA shaders.

float4 GTAGetShadowCoord(float3 positionWS, float4 positionCS, float4 shadowCoordInterpolated)
{
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    return shadowCoordInterpolated;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    return TransformWorldToShadowCoord(positionWS);
#else
    return float4(0.0, 0.0, 0.0, 0.0);
#endif
}

float4 GTAGetVertexShadowCoord(VertexPositionInputs vertexInput)
{
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    return GetShadowCoord(vertexInput);
#else
    return float4(0.0, 0.0, 0.0, 0.0);
#endif
}

// ---------------------------------------------------------------------------
// Visual shadow softening (NOT shadow-map filtering)
//
// Post-processes the scalar shadowAttenuation after URP shadow lookup.
// No extra shadow-map taps, no UV offsets — cannot produce ghost silhouettes.
// Requires _ShadowSoftness / _ShadowContrast in UnityPerMaterial (each GTA shader).
// ---------------------------------------------------------------------------

half GTAApplyVisualShadowSoftening(half shadowAttenuation, half softness, half contrast)
{
    half s = saturate(softness);
    if (s <= 1e-4h)
        return shadowAttenuation;

    // Toe lift: raise the darkest shadow values to hide alpha-clip speckle / noise inside shadow.
    half shadowFloor = lerp(0.0h, 0.22h, s);
    half lifted = lerp(shadowFloor, 1.0h, saturate(shadowAttenuation));

    // Smoothstep remapping: softer perceived transition without spatial filtering.
    half edge0 = lerp(0.0h, 0.12h, s);
    half edge1 = lerp(1.0h, 0.88h, s);
    half softened = smoothstep(edge0, edge1, lifted);

    // Contrast < 1 lifts midtones further; > 1 deepens (artist tweak).
    half c = max(contrast, 0.01h);
    softened = pow(softened, rcp(c));

    return saturate(softened);
}

#endif // GTA_SHADOWS_INCLUDED
