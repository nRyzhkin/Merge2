#ifndef GTA_BRDF_INCLUDED
#define GTA_BRDF_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"

// Thin wrapper around URP PBR — shared BRDF math, no duplication.

inline void GTAInitializeBRDFData(SurfaceData surfaceData, out BRDFData brdfData)
{
    InitializeBRDFData(surfaceData, brdfData);
}

inline void GTAInitializeBRDFDataDirect(half3 albedo, half metallic, half smoothness, inout half alpha, out BRDFData brdfData)
{
    InitializeBRDFData(albedo, metallic, half3(0.0h, 0.0h, 0.0h), smoothness, alpha, brdfData);
}

#endif // GTA_BRDF_INCLUDED
