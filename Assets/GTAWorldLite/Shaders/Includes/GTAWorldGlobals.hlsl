#ifndef GTA_WORLD_GLOBALS_INCLUDED
#define GTA_WORLD_GLOBALS_INCLUDED

// Globals driven by GTAWorldController (Shader.SetGlobal*).
float  _GTA_TimeOfDay;
float  _GTA_Weather;
float  _GTA_Wetness;
float  _GTA_WetMaxSmoothness;
float  _GTA_FogStart;
float  _GTA_FogEnd;
float  _GTA_FogDensity;
float  _GTA_FogEnabled;
float  _GTA_ReflectionIntensity;
float4 _GTA_AmbientColor;
float4 _GTA_SunColor;
float4 _GTA_SunDirection;
float4 _GTA_HaloColor;
float4 _GTA_FogColor;
float  _GTA_HaloIntensity;
float  _GTA_SkyExposure;
float  _GTA_SunSize;
float  _GTA_HaloSize;
float  _GTA_SunVisibility;
float  _GTA_HaloVisibility;

// Runtime shadow cache (GTAShadowCache).
float4x4 _GTA_WorldToShadow;
float4x4 _GTA_WorldToShadowPrev;
float4 _GTA_ShadowParams;       // x=bias, y=strength (weather-scaled), z=edgeFade, w=enabled
float4 _GTA_ShadowSoftParams;   // x=softDepthWidth, y=filterRadiusTexels, z=texelSize, w=unused
float4 _GTA_ShadowCenter;       // xyz=center, w=orthoHalf
float  _GTA_ShadowNear;
float  _GTA_ShadowFar;
float  _GTA_ShadowBlend;        // 0=prev atlas, 1=current atlas

// Baked local light clusters (GTALightCluster). Up to 4 volumes.
float  _GTA_LCCount;
float4 _GTA_LCOpacity;          // per-cluster opacity (x..w)
float4 _GTA_LC0_Min;
float4 _GTA_LC0_InvSize;
float4 _GTA_LC1_Min;
float4 _GTA_LC1_InvSize;
float4 _GTA_LC2_Min;
float4 _GTA_LC2_InvSize;
float4 _GTA_LC3_Min;
float4 _GTA_LC3_InvSize;

#endif // GTA_WORLD_GLOBALS_INCLUDED
