#ifndef GTA_TERRAIN_DEPTH_PASS_INCLUDED
#define GTA_TERRAIN_DEPTH_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "GTA_TerrainMaterial.hlsl"

struct GTA_TerrainDepthAttributes
{
    float4 positionOS : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct GTA_TerrainDepthVaryings
{
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

GTA_TerrainDepthVaryings GTA_TerrainDepthVert(GTA_TerrainDepthAttributes input)
{
    GTA_TerrainDepthVaryings output = (GTA_TerrainDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    return output;
}

half4 GTA_TerrainDepthFrag(GTA_TerrainDepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    return 0.0h;
}

#endif // GTA_TERRAIN_DEPTH_PASS_INCLUDED
