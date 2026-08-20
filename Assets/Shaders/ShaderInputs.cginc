#ifndef SHADER_INPUTS
#define SHADER_INPUTS

#include "UnityCG.cginc"

// base input
uniform sampler2D _MainTex;
uniform float4 _MainTex_ST;

// instancing inputs
UNITY_INSTANCING_BUFFER_START(Props)

UNITY_DEFINE_INSTANCED_PROP(half3, _Color)
UNITY_DEFINE_INSTANCED_PROP(half, _Opacity)

#if defined(_EMISSION)
UNITY_DEFINE_INSTANCED_PROP(half, _Emission)
#endif

#if defined(_NORMAL_LIGHT)
UNITY_DEFINE_INSTANCED_PROP(half3, _NormalLight)
UNITY_DEFINE_INSTANCED_PROP(half3, _NormalColorX)
UNITY_DEFINE_INSTANCED_PROP(half3, _NormalColorY)
UNITY_DEFINE_INSTANCED_PROP(half3, _NormalColorZ)
#endif

#if defined(_TEXTURE_LIGHT)
UNITY_DEFINE_INSTANCED_PROP(half3, _LightColor)
UNITY_DEFINE_INSTANCED_PROP(half3, _ShadowColor)
UNITY_DEFINE_INSTANCED_PROP(half3, _IndirectColor)
UNITY_DEFINE_INSTANCED_PROP(half, _Indirect)
#endif

#if defined(_COVERAGE_TEX)
UNITY_DEFINE_INSTANCED_PROP(half, _Coverage)
UNITY_DEFINE_INSTANCED_PROP(float3, _CoveragePos)
#endif

#if defined(_AMBIENT_LIGHT)
UNITY_DEFINE_INSTANCED_PROP(half, _Ambient)
UNITY_DEFINE_INSTANCED_PROP(half4, _AmbientMask)
#endif

#if defined(_LIGHT_RAY)
UNITY_DEFINE_INSTANCED_PROP(half, _Width)
UNITY_DEFINE_INSTANCED_PROP(half, _HalfWidth)
UNITY_DEFINE_INSTANCED_PROP(half, _TopPosition)
UNITY_DEFINE_INSTANCED_PROP(half, _TopPower)
UNITY_DEFINE_INSTANCED_PROP(half, _BottomPosition)
UNITY_DEFINE_INSTANCED_PROP(half, _BottomPower)
#endif

#if defined(_RIM)
UNITY_DEFINE_INSTANCED_PROP(half, _Rim)
UNITY_DEFINE_INSTANCED_PROP(half, _RimPower)
#endif

#if defined(_IDX)
UNITY_DEFINE_INSTANCED_PROP(half, _Idx)
#endif

#if defined(_RECT)
UNITY_DEFINE_INSTANCED_PROP(half3, _PosMin)
UNITY_DEFINE_INSTANCED_PROP(half3, _PosMax)
#endif

#if defined(_PULSE)
UNITY_DEFINE_INSTANCED_PROP(half, _Pulse)
#endif

UNITY_INSTANCING_BUFFER_END(Props)

// global inputs
#if defined(_CUSTOM_LIGHT0)
half3 _CustomLightPos0;
half3 _CustomLightColor0;
half _CustomLightRange0;
#endif

#if defined(_CUSTOM_LIGHT1)
half3 _CustomLightPos1;
half3 _CustomLightColor1;
half _CustomLightRange1;
#endif

// texture inputs
#if defined(_TRIPLANAR)
half _TexCoordRotation;
#endif

#if defined(_EMISSION)
   sampler2D _EmissionTex;
#endif

#if defined(_TEXTURE_LIGHT)
    sampler2D _LightTex;
    float4 _LightTex_ST;
#endif

#if defined(_COVERAGE_TEX)
    sampler2D _CoverageTex;
    float4 _CoverageTex_ST;
    sampler2D _CoverageMaskTex;
    float4 _CoverageMaskTex_ST;
#endif

// access
#if defined(INSTANCING_ON)
    #define INST(val) UNITY_ACCESS_INSTANCED_PROP(Props, val)
#else
    #define INST(val) val
#endif

#define TINT4 fixed4(INST(_Color), 1)



#endif