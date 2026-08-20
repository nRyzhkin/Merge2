#ifndef GTA_GLASS_INPUT_INCLUDED
#define GTA_GLASS_INPUT_INCLUDED

// Cheap glass material data: tint, fresnel, dirt.

#include "GTACommon.hlsl"
#include "GTA_MaterialCBufferTail.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseMap_TexelSize;
    half4  _BaseColor;
    half4  _DirtColor;
    half4  _SpecularColor;
    half   _Smoothness;
    half   _SpecularIntensity;
    half   _FresnelPower;
    half   _FresnelBias;
    half   _MinAlpha;
    half   _DirtStrength;
    half   _DirtOpacity;
    half   _BumpScale;
    half   _DistanceFadeStart;
    half   _DistanceFadeEnd;
    half   _CubemapReflectionStrength;
    half   _ShadowSoftness;
    half   _ShadowContrast;
    half   _Cull;
    half   _ReceiveShadows;
    GTA_MATERIAL_CBUFFER_STREAMING_TAIL
CBUFFER_END

TEXTURE2D(_DirtMap);
SAMPLER(sampler_DirtMap);

half GTASampleDirt(float2 uv)
{
#if defined(_DIRT_MAP)
    return SAMPLE_TEXTURE2D(_DirtMap, sampler_DirtMap, uv).r;
#else
    return 0.0h;
#endif
}

half GTAGlassFresnel(half NdotV, half power, half bias)
{
    half inv = 1.0h - saturate(NdotV);
    half f = inv * inv;
    f *= f;
    if (power > 5.5h)
        f *= inv;
    return saturate(bias + (1.0h - bias) * f);
}

// Cheap Blinn-Phong specular for glass highlights (no full PBR BRDF).
half3 GTAGlassDirectSpecular(
    half3 lightColor,
    half3 lightDirWS,
    half3 normalWS,
    half3 viewDirWS,
    half smoothness,
    half3 specularColor,
    half specularIntensity,
    half attenuation)
{
    half3 halfDir = SafeNormalize(lightDirWS + viewDirWS);
    half NdotH = saturate(dot(normalWS, halfDir));
    half NdotL = saturate(dot(normalWS, lightDirWS));

    // Map smoothness → Phong exponent (cheap & punchy for glass)
    half shininess = exp2(10.0h * saturate(smoothness) + 1.0h);
    half spec = pow(NdotH, shininess) * (shininess * 0.125h + 1.0h);

    return lightColor * (attenuation * NdotL * spec * specularIntensity) * specularColor;
}

#endif // GTA_GLASS_INPUT_INCLUDED
