#ifndef GTA_TERRAIN_MATERIAL_INCLUDED
#define GTA_TERRAIN_MATERIAL_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "GTA_MaterialCBufferTail.hlsl"

// SRP Batcher: identical layout in every Terrain pass; never ifdef inside this block.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseMap_TexelSize;
    float4 _DetailMaskMap_ST;
    float4 _DetailGrassMap_ST;
    float4 _DetailSandMap_ST;
    float4 _DetailGravelMap_ST;
    half4  _BaseColor;
    half   _Smoothness;
    half   _Metallic;
    half   _BumpScale;
    half   _SandHeightMax;
    half   _SandHeightBlend;
    half   _SandHeightTiling;
    half   _DetailStrength;
    half   _ShadowSoftness;
    half   _ShadowContrast;
    half   _ReflectionStrength;
    half   _ShoreFoamStrength;
    half   _Cull;
    half   _QueueOffset;
    half   _ReceiveShadows;
    GTA_MATERIAL_CBUFFER_STREAMING_TAIL
CBUFFER_END

#endif // GTA_TERRAIN_MATERIAL_INCLUDED
