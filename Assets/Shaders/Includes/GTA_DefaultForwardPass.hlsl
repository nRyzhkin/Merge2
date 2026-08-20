#ifndef GTA_DEFAULT_FORWARD_PASS_INCLUDED
#define GTA_DEFAULT_FORWARD_PASS_INCLUDED

#include "GTA_DefaultInput.hlsl"
#include "GTALighting.hlsl"

struct GTA_DefaultAttributes
{
    float4 positionOS     : POSITION;
    float3 normalOS       : NORMAL;
    float4 tangentOS      : TANGENT;
    float2 texcoord0      : TEXCOORD0;
    float2 texcoord1      : TEXCOORD1;
    half4  color          : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct GTA_DefaultVaryings
{
    float4 positionCS     : SV_POSITION;
    float2 uv0            : TEXCOORD0;
    float2 uv1            : TEXCOORD1;
    float3 positionWS     : TEXCOORD2;
    half3  normalWS       : TEXCOORD3;
    half4  tangentWS      : TEXCOORD4;
    half4  vertexColor    : TEXCOORD5;
    half   fogFactor      : TEXCOORD6;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

GTA_DefaultVaryings GTA_DefaultVert(GTA_DefaultAttributes input)
{
    GTA_DefaultVaryings output = (GTA_DefaultVaryings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.positionCS = vertexInput.positionCS;
    output.positionWS = vertexInput.positionWS;
    output.normalWS = normalInput.normalWS;
    output.uv0 = TRANSFORM_TEX(input.texcoord0, _BaseMap);
    output.uv1 = input.texcoord1;
    output.vertexColor = input.color;

    half sign = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalInput.tangentWS, sign);

#if !defined(_FOG_FRAGMENT)
    output.fogFactor = GTAComputeFogFactor(vertexInput.positionCS.z);
#else
    output.fogFactor = 0.0h;
#endif

    return output;
}

half4 GTA_DefaultFrag(GTA_DefaultVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    SurfaceData surfaceData;
    GTAInitializeDefaultSurfaceData(input.uv0, input.uv1, input.vertexColor, surfaceData);

    GTAAlphaClipDither(surfaceData.alpha, _Cutoff, input.positionCS);
    GTAApplyAllDitherFades(input.positionWS, input.positionCS, _DistanceFadeStart, _DistanceFadeEnd);

    GTAInputData inputData;
    GTAInitializeInputData(
        input.positionWS,
        input.positionCS,
        input.normalWS,
        input.tangentWS,
        surfaceData.normalTS,
        input.fogFactor,
        inputData);

    return GTAShadeSurfacePBR(inputData, surfaceData, _ReflectionStrength);
}

#endif // GTA_DEFAULT_FORWARD_PASS_INCLUDED
