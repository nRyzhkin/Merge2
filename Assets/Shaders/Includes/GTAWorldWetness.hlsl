#ifndef GTA_WORLD_WETNESS_INCLUDED
#define GTA_WORLD_WETNESS_INCLUDED

#include "GTAWorldGlobals.hlsl"

// Wetness helpers — implemented in GTALighting.hlsl for surface shaders.
void GTAApplyWorldWetness(inout half3 albedo, inout half smoothness);

#endif // GTA_WORLD_WETNESS_INCLUDED
