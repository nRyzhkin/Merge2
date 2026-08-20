#ifndef GTA_TERRAIN_META_PASS_INCLUDED
#define GTA_TERRAIN_META_PASS_INCLUDED

#include "GTA_TerrainInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalMetaPass.hlsl"

struct GTA_TerrainMetaAttributes
{
    float4 positionOS       : POSITION;
    float3 normalOS         : NORMAL;
    float2 texcoord0        : TEXCOORD0;
    float2 staticLightmapUV : TEXCOORD1;
    half4  color            : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct GTA_TerrainMetaVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv0        : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half4  vertexColor : TEXCOORD2;
};

GTA_TerrainMetaVaryings GTA_TerrainMetaVert(GTA_TerrainMetaAttributes input)
{
    GTA_TerrainMetaVaryings output = (GTA_TerrainMetaVaryings)0;
    output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.staticLightmapUV, input.staticLightmapUV);
    output.uv0 = TRANSFORM_TEX(input.texcoord0, _BaseMap);
    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.vertexColor = input.color;
    return output;
}

half4 GTA_TerrainMetaFrag(GTA_TerrainMetaVaryings input) : SV_Target
{
    SurfaceData surfaceData;
    GTAInitializeTerrainSurfaceData(input.uv0, input.positionWS, input.vertexColor.rgb, surfaceData);

    BRDFData brdfData;
    GTAInitializeBRDFData(surfaceData, brdfData);

    MetaInput metaInput;
    metaInput.Albedo = brdfData.diffuse + brdfData.specular * brdfData.roughness * 0.5h;
    metaInput.Emission = half3(0.0h, 0.0h, 0.0h);

    Varyings metaVaryings = (Varyings)0;
    metaVaryings.positionCS = input.positionCS;
    metaVaryings.uv = input.uv0;
    return UniversalFragmentMeta(metaVaryings, metaInput);
}

#endif // GTA_TERRAIN_META_PASS_INCLUDED
