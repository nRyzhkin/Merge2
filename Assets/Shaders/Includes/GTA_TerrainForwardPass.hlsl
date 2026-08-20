#ifndef GTA_TERRAIN_FORWARD_PASS_INCLUDED
#define GTA_TERRAIN_FORWARD_PASS_INCLUDED

#include "GTA_TerrainInput.hlsl"
#include "GTALighting.hlsl"

struct GTA_TerrainAttributes
{
    float4 positionOS     : POSITION;
    float3 normalOS       : NORMAL;
    float4 tangentOS      : TANGENT;
    float2 texcoord0      : TEXCOORD0;
    half4  color          : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct GTA_TerrainVaryings
{
    float4 positionCS     : SV_POSITION;
    float2 uv0            : TEXCOORD0;
    float3 positionWS     : TEXCOORD1;
    half3  normalWS       : TEXCOORD2;
    half4  tangentWS      : TEXCOORD3;
    half4  vertexColor    : TEXCOORD4;
    half   fogFactor      : TEXCOORD5;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

GTA_TerrainVaryings GTA_TerrainVert(GTA_TerrainAttributes input)
{
    GTA_TerrainVaryings output = (GTA_TerrainVaryings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.positionCS = vertexInput.positionCS;
    output.positionWS = vertexInput.positionWS;
    output.normalWS = normalInput.normalWS;
    output.uv0 = TRANSFORM_TEX(input.texcoord0, _BaseMap);
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

half4 GTA_TerrainFrag(GTA_TerrainVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    SurfaceData surfaceData;
    GTAInitializeTerrainSurfaceData(input.uv0, input.positionWS, input.vertexColor.rgb, input.normalWS, surfaceData);

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

#endif // GTA_TERRAIN_FORWARD_PASS_INCLUDED
