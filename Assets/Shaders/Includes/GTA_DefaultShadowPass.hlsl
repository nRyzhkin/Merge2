#ifndef GTA_DEFAULT_SHADOW_PASS_INCLUDED
#define GTA_DEFAULT_SHADOW_PASS_INCLUDED

#include "GTA_DefaultInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float3 _LightDirection;
float3 _LightPosition;

struct GTA_ShadowAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 texcoord0  : TEXCOORD0;
    half4  color      : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct GTA_ShadowVaryings
{
    float4 positionCS : SV_POSITION;
#if defined(_ALPHATEST_ON)
    float2 uv0        : TEXCOORD0;
    half4  vertexColor : TEXCOORD1;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

float4 GTA_GetShadowPositionHClip(GTA_ShadowAttributes input)
{
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    return ApplyShadowClamping(positionCS);
}

GTA_ShadowVaryings GTA_ShadowVert(GTA_ShadowAttributes input)
{
    GTA_ShadowVaryings output = (GTA_ShadowVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

#if defined(_ALPHATEST_ON)
    output.uv0 = TRANSFORM_TEX(input.texcoord0, _BaseMap);
    output.vertexColor = input.color;
#endif

    output.positionCS = GTA_GetShadowPositionHClip(input);
    return output;
}

half4 GTA_ShadowFrag(GTA_ShadowVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);

#if defined(_ALPHATEST_ON)
    half4 albedoAlpha = SampleAlbedoAlpha(input.uv0, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    half alpha = GTAResolveAlpha(albedoAlpha.a, _BaseColor.a, input.uv0);
    alpha = GTAApplyVertexAlpha(alpha, input.vertexColor);
    GTAAlphaClip(alpha, _Cutoff);
#endif

    GTAApplyLodDitherFade(input.positionCS);
    return 0.0h;
}

#endif // GTA_DEFAULT_SHADOW_PASS_INCLUDED
