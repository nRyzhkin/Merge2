#ifndef GTA_TERRAIN_SHADOW_PASS_INCLUDED
#define GTA_TERRAIN_SHADOW_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float3 _LightDirection;
float3 _LightPosition;

struct GTA_TerrainShadowAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct GTA_TerrainShadowVaryings
{
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

float4 GTA_TerrainGetShadowPositionHClip(GTA_TerrainShadowAttributes input)
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

GTA_TerrainShadowVaryings GTA_TerrainShadowVert(GTA_TerrainShadowAttributes input)
{
    GTA_TerrainShadowVaryings output = (GTA_TerrainShadowVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    output.positionCS = GTA_TerrainGetShadowPositionHClip(input);
    return output;
}

half4 GTA_TerrainShadowFrag(GTA_TerrainShadowVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    return 0.0h;
}

#endif // GTA_TERRAIN_SHADOW_PASS_INCLUDED
