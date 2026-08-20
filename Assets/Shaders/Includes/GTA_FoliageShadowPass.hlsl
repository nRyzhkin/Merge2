#ifndef GTA_FOLIAGE_SHADOW_PASS_INCLUDED
#define GTA_FOLIAGE_SHADOW_PASS_INCLUDED

#include "GTA_FoliageInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float3 _LightDirection;
float3 _LightPosition;

struct GTA_FoliageShadowAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 texcoord0  : TEXCOORD0;
    half4  color      : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct GTA_FoliageShadowVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
#if defined(_ALPHATEST_ON)
    float2 uv0        : TEXCOORD1;
    half4  vertexColor : TEXCOORD2;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

float4 GTA_FoliageShadowPositionHClip(GTA_FoliageShadowAttributes input)
{
    float3 positionOS = input.positionOS.xyz;
    GTAFoliageTransformPosition(positionOS, input.color);

    float3 positionWS = TransformObjectToWorld(positionOS);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    return ApplyShadowClamping(positionCS);
}

GTA_FoliageShadowVaryings GTA_FoliageShadowVert(GTA_FoliageShadowAttributes input)
{
    GTA_FoliageShadowVaryings output = (GTA_FoliageShadowVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    float3 positionOS = input.positionOS.xyz;
    GTAFoliageTransformPosition(positionOS, input.color);
    output.positionWS = TransformObjectToWorld(positionOS);

#if defined(_ALPHATEST_ON)
    output.uv0 = TRANSFORM_TEX(input.texcoord0, _BaseMap);
    output.vertexColor = input.color;
#endif

    output.positionCS = GTA_FoliageShadowPositionHClip(input);
    return output;
}

half4 GTA_FoliageShadowFrag(GTA_FoliageShadowVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);

#if defined(_ALPHATEST_ON)
    SurfaceData surfaceData;
    GTAInitializeFoliageSurfaceData(input.uv0, input.vertexColor, float3(0, 0, 0), surfaceData);
    GTAAlphaClip(surfaceData.alpha, _Cutoff);
#endif

    GTAApplyAllDitherFades(input.positionWS, input.positionCS, _DistanceFadeStart, _DistanceFadeEnd);
    return 0.0h;
}

#endif // GTA_FOLIAGE_SHADOW_PASS_INCLUDED
