#ifndef GTA_DEFAULT_DEPTH_PASS_INCLUDED
#define GTA_DEFAULT_DEPTH_PASS_INCLUDED

#include "GTA_DefaultInput.hlsl"

struct GTA_DepthAttributes
{
    float4 positionOS : POSITION;
    float2 texcoord0  : TEXCOORD0;
    half4  color      : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct GTA_DepthVaryings
{
    float4 positionCS : SV_POSITION;
#if defined(_ALPHATEST_ON)
    float2 uv0        : TEXCOORD0;
    half4  vertexColor : TEXCOORD1;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

GTA_DepthVaryings GTA_DepthVert(GTA_DepthAttributes input)
{
    GTA_DepthVaryings output = (GTA_DepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

#if defined(_ALPHATEST_ON)
    output.uv0 = TRANSFORM_TEX(input.texcoord0, _BaseMap);
    output.vertexColor = input.color;
#endif

    return output;
}

half4 GTA_DepthFrag(GTA_DepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);

#if defined(_ALPHATEST_ON)
    half4 albedoAlpha = SampleAlbedoAlpha(input.uv0, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    half alpha = GTAResolveAlpha(albedoAlpha.a, _BaseColor.a, input.uv0);
    alpha = GTAApplyVertexAlpha(alpha, input.vertexColor);
    GTAAlphaClipDither(alpha, _Cutoff, input.positionCS);
#endif

    GTAApplyLodDitherFade(input.positionCS);
    return 0.0h;
}

#endif // GTA_DEFAULT_DEPTH_PASS_INCLUDED
