#ifndef GTA_MATERIAL_CBUFFER_TAIL_INCLUDED
#define GTA_MATERIAL_CBUFFER_TAIL_INCLUDED

// SurfaceInput normally declares these outside UnityPerMaterial; keep them in the buffer for SRP Batcher.
#define GTA_MATERIAL_CBUFFER_STREAMING_TAIL \
    float4 _BaseMap_MipInfo; \
    float4 _BaseMap_StreamInfo; \
    UNITY_TEXTURE_STREAMING_DEBUG_VARS;

#endif // GTA_MATERIAL_CBUFFER_TAIL_INCLUDED
