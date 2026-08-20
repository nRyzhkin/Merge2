#ifndef GTA_DEFAULT_META_PASS_INCLUDED
#define GTA_DEFAULT_META_PASS_INCLUDED

#include "GTA_DefaultInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalMetaPass.hlsl"

struct GTA_MetaAttributes
{
    float4 positionOS       : POSITION;
    float3 normalOS         : NORMAL;
    float2 texcoord0        : TEXCOORD0;
    float2 texcoord1        : TEXCOORD1;
    float2 staticLightmapUV : TEXCOORD2;
    half4  color            : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct GTA_MetaVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float2 uv1        : TEXCOORD1;
    half4  vertexColor : TEXCOORD2;
};

GTA_MetaVaryings GTA_MetaVert(GTA_MetaAttributes input)
{
    GTA_MetaVaryings output = (GTA_MetaVaryings)0;
    output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.staticLightmapUV, input.staticLightmapUV);
    output.uv = TRANSFORM_TEX(input.texcoord0, _BaseMap);
    output.uv1 = input.texcoord1;
    output.vertexColor = input.color;
    return output;
}

half4 GTA_MetaFrag(GTA_MetaVaryings input) : SV_Target
{
    SurfaceData surfaceData;
    GTAInitializeDefaultSurfaceData(input.uv, input.uv1, input.vertexColor, surfaceData);

    BRDFData brdfData;
    GTAInitializeBRDFData(surfaceData, brdfData);

    MetaInput metaInput;
    metaInput.Albedo = brdfData.diffuse + brdfData.specular * brdfData.roughness * 0.5h;
    metaInput.Emission = surfaceData.emission;

    Varyings metaVaryings = (Varyings)0;
    metaVaryings.positionCS = input.positionCS;
    metaVaryings.uv = input.uv;
    return UniversalFragmentMeta(metaVaryings, metaInput);
}

#endif // GTA_DEFAULT_META_PASS_INCLUDED
