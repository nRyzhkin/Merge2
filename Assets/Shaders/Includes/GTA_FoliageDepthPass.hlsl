#ifndef GTA_FOLIAGE_DEPTH_PASS_INCLUDED
#define GTA_FOLIAGE_DEPTH_PASS_INCLUDED

#include "GTA_FoliageInput.hlsl"

struct GTA_FoliageDepthAttributes
{
    float4 positionOS : POSITION;
    float2 texcoord0  : TEXCOORD0;
    half4  color      : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct GTA_FoliageDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
#if defined(_ALPHATEST_ON)
    float2 uv0        : TEXCOORD1;
    half4  vertexColor : TEXCOORD2;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

GTA_FoliageDepthVaryings GTA_FoliageDepthVert(GTA_FoliageDepthAttributes input)
{
    GTA_FoliageDepthVaryings output = (GTA_FoliageDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    float3 positionOS = input.positionOS.xyz;
    GTAFoliageTransformPosition(positionOS, input.color);
    output.positionWS = TransformObjectToWorld(positionOS);
    output.positionCS = TransformObjectToHClip(positionOS);

#if defined(_ALPHATEST_ON)
    output.uv0 = TRANSFORM_TEX(input.texcoord0, _BaseMap);
    output.vertexColor = input.color;
#endif

    return output;
}

half4 GTA_FoliageDepthFrag(GTA_FoliageDepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);

#if defined(_ALPHATEST_ON)
    SurfaceData surfaceData;
    GTAInitializeFoliageSurfaceData(input.uv0, input.vertexColor, float3(0, 0, 0), surfaceData);
    GTAAlphaClipDither(surfaceData.alpha, _Cutoff, input.positionCS);
#endif

    GTAApplyAllDitherFades(input.positionWS, input.positionCS, _DistanceFadeStart, _DistanceFadeEnd);
    return 0.0h;
}

#endif // GTA_FOLIAGE_DEPTH_PASS_INCLUDED
