#ifndef GTA_FOLIAGE_FORWARD_PASS_INCLUDED
#define GTA_FOLIAGE_FORWARD_PASS_INCLUDED

#include "GTA_FoliageInput.hlsl"
#include "GTALighting.hlsl"

struct GTA_FoliageAttributes
{
    float4 positionOS     : POSITION;
    float3 normalOS       : NORMAL;
    float4 tangentOS      : TANGENT;
    float2 texcoord0      : TEXCOORD0;
    half4  color          : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct GTA_FoliageVaryings
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

GTA_FoliageVaryings GTA_FoliageVert(GTA_FoliageAttributes input)
{
    GTA_FoliageVaryings output = (GTA_FoliageVaryings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionOS = input.positionOS.xyz;
    GTAFoliageTransformPosition(positionOS, input.color);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS);
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

half4 GTA_FoliageFrag(GTA_FoliageVaryings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    SurfaceData surfaceData;
    GTAInitializeFoliageSurfaceData(input.uv0, input.vertexColor, input.positionWS, surfaceData);

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

    inputData.normalWS = GTAFlipNormalForDoubleSided(inputData.normalWS, facing);

    half4 color = GTAShadeSurfacePBR(inputData, surfaceData, _ReflectionStrength);

    half3 sunDir = normalize((half3)_GTA_SunDirection.xyz);
    half backLit = saturate(-dot(inputData.normalWS, sunDir));
    color.rgb += surfaceData.albedo * (half3)_GTA_SunColor.rgb * (backLit * backLit * 0.4h);

    half3 skyFloor = surfaceData.albedo * (half3)_GTA_AmbientColor.rgb * 0.18h;
    color.rgb = max(color.rgb, skyFloor);

    return color;
}

#endif // GTA_FOLIAGE_FORWARD_PASS_INCLUDED
