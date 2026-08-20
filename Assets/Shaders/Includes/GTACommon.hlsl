#ifndef GTA_COMMON_INCLUDED
#define GTA_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
// Keep texture streaming metadata in UnityPerMaterial (SRP Batcher requirement).
#undef UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX
#define UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(tex)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

#include "GTANoise.hlsl"
#include "GTALodFade.hlsl"
#include "GTAFog.hlsl"

// Optional separate opacity mask (R). Declared once for the whole GTA family.
TEXTURE2D(_OpacityMap);
SAMPLER(sampler_OpacityMap);

// ---------------------------------------------------------------------------
// Opacity
// ---------------------------------------------------------------------------

half GTASampleOpacity(float2 uv)
{
#if defined(_OPACITYMAP)
    return SAMPLE_TEXTURE2D(_OpacityMap, sampler_OpacityMap, uv).r;
#else
    return 1.0h;
#endif
}

half GTAResolveAlpha(half albedoAlpha, half baseColorAlpha, float2 uv)
{
#if defined(_OPACITYMAP)
    return GTASampleOpacity(uv) * baseColorAlpha;
#else
    return albedoAlpha * baseColorAlpha;
#endif
}

// ---------------------------------------------------------------------------
// Tangent space
// ---------------------------------------------------------------------------

half3 GTATransformNormalTS(half3 normalTS, half3 normalWS, half4 tangentWS)
{
    half sgn = tangentWS.w;
    half3 bitangent = sgn * cross(normalWS, tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(tangentWS.xyz, bitangent, normalWS);
    return NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));
}

half3 GTAGetViewDirectionTS(half4 tangentWS, half3 normalWS, half3 viewDirWS)
{
    half sgn = tangentWS.w;
    half3 bitangent = sgn * cross(normalWS, tangentWS.xyz);
    half3x3 worldToTangent = half3x3(tangentWS.xyz, bitangent, normalWS);
    return mul(worldToTangent, viewDirWS);
}

// ---------------------------------------------------------------------------
// Alpha clip (opaque queue)
// ---------------------------------------------------------------------------

void GTAAlphaClip(half alpha, half cutoff)
{
#if defined(_ALPHATEST_ON)
    clip(alpha - cutoff);
#endif
}

void GTAAlphaClipDither(half alpha, half cutoff, float4 positionCS)
{
#if defined(_ALPHATEST_ON)
    // Dithered alpha test (opaque queue): keeps correct average cutoff.
    // dither in [0..1) -> subtract (dither - 0.5) so alpha==cutoff passes ~50% pixels.
    half dither = GTASampleDither(positionCS);
    clip((alpha - cutoff) - (dither - 0.5h));
#endif
}

// ---------------------------------------------------------------------------
// Detail albedo blend (overlay-style multiply, RAGE-like)
// ---------------------------------------------------------------------------

half3 GTABlendDetailAlbedo(half3 baseAlbedo, half3 detailAlbedo, half mask)
{
    if (mask <= 1e-4h)
        return baseAlbedo;

    // Missing / unset detail textures sample as black — treat as neutral gray.
    half detailLum = Luminance(detailAlbedo);
    if (detailLum <= 0.02h)
        return baseAlbedo;

    half3 blended = baseAlbedo * detailAlbedo * 2.0h;
    return lerp(baseAlbedo, blended, mask);
}

// B/W facture centered at 0.5 — no change at mid-gray, never zeroes albedo.
half3 GTAApplyDetailFacture(half3 baseAlbedo, half facture, half strength)
{
    if (strength <= 1e-4h)
        return baseAlbedo;

    if (facture <= 0.02h)
        facture = 0.5h;

    half factor = 1.0h + (facture - 0.5h) * 2.0h * strength;
    return saturate(baseAlbedo * factor);
}

// ---------------------------------------------------------------------------
// Macro texture — large-scale albedo variation
// ---------------------------------------------------------------------------

half3 GTAApplyMacroAlbedo(half3 albedo, half3 macroSample, half intensity)
{
    return lerp(albedo, albedo * macroSample, intensity);
}

// ---------------------------------------------------------------------------
// Vertex color
// ---------------------------------------------------------------------------

half3 GTAApplyVertexColor(half3 albedo, half4 vertexColor)
{
#if defined(_VERTEXCOLOR)
    return albedo * vertexColor.rgb;
#else
    return albedo;
#endif
}

half GTAApplyVertexAlpha(half alpha, half4 vertexColor)
{
#if defined(_VERTEXCOLOR)
    return alpha * vertexColor.a;
#else
    return alpha;
#endif
}

#endif // GTA_COMMON_INCLUDED
