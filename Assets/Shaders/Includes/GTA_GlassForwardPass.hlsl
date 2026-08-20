#ifndef GTA_GLASS_FORWARD_PASS_INCLUDED
#define GTA_GLASS_FORWARD_PASS_INCLUDED

#include "GTA_GlassInput.hlsl"
#include "GTAReflections.hlsl"
#include "GTAFog.hlsl"

struct GTA_GlassAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 texcoord0  : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct GTA_GlassVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv0        : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3  normalWS   : TEXCOORD2;
    half4  tangentWS  : TEXCOORD3;
    half   fogFactor  : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

GTA_GlassVaryings GTA_GlassVert(GTA_GlassAttributes input)
{
    GTA_GlassVaryings output = (GTA_GlassVaryings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.positionCS = vertexInput.positionCS;
    output.positionWS = vertexInput.positionWS;
    output.normalWS = normalInput.normalWS;
    output.uv0 = TRANSFORM_TEX(input.texcoord0, _BaseMap);

    half sign = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalInput.tangentWS, sign);

#if !defined(_FOG_FRAGMENT)
    output.fogFactor = GTAComputeFogFactor(vertexInput.positionCS.z);
#else
    output.fogFactor = 0.0h;
#endif

    return output;
}

half4 GTA_GlassFrag(GTA_GlassVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    GTAApplyAllDitherFades(input.positionWS, input.positionCS, _DistanceFadeStart, _DistanceFadeEnd);

    half4 albedoAlpha = SampleAlbedoAlpha(input.uv0, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    half alphaBase = GTAResolveAlpha(albedoAlpha.a, _BaseColor.a, input.uv0);
    half3 tint = albedoAlpha.rgb * _BaseColor.rgb;

    half3 normalTS = SampleNormal(input.uv0, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    half3 normalWS = GTATransformNormalTS(normalTS, input.normalWS, input.tangentWS);

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half NdotV = saturate(dot(normalWS, viewDirWS));
    half fresnel = GTAGlassFresnel(NdotV, _FresnelPower, _FresnelBias);

    half3 ambient = (half3)_GTA_AmbientColor.rgb * tint;
    half3 sunDir = normalize((half3)_GTA_SunDirection.xyz);
    half3 specular = GTAGlassDirectSpecular(
        (half3)_GTA_SunColor.rgb,
        sunDir,
        normalWS,
        viewDirWS,
        _Smoothness,
        _SpecularColor.rgb,
        _SpecularIntensity,
        1.0h) * fresnel;

    half dirt = GTASampleDirt(input.uv0);
    half dirtAmt = saturate(dirt * _DirtStrength);

    half3 color = lerp(ambient, tint, fresnel * 0.65h);
    color += specular * (1.0h - dirtAmt);
    color += GTACalcEnvironmentReflection(
        normalWS,
        viewDirWS,
        input.positionWS,
        _Smoothness,
        _CubemapReflectionStrength) * (1.0h - dirtAmt);
    color = lerp(color, tint * _DirtColor.rgb, dirtAmt);

    half alpha = saturate(alphaBase * max(_MinAlpha, fresnel) + dirtAmt * _DirtOpacity);
    color = GTAApplyFog(color, input.positionWS);

    return half4(color, alpha);
}

#endif // GTA_GLASS_FORWARD_PASS_INCLUDED
