#ifndef SHADER_FUNCTIONS
#define SHADER_FUNCTIONS

#include "UnityCG.cginc"
#include "ShaderInputs.cginc"
#include "ShaderUtils.cginc"

struct VertexData
{
    float4 vertex : POSITION;
    float4 color : COLOR0;
    float2 uv : TEXCOORD0;
    float3 normal : NORMAL;
    
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Interpolators
{
    float4 pos : SV_POSITION;
    float4 color : COLOR0;
    float2 uv : TEXCOORD0;
    float3 world_normal : TEXCOORD1;
    float3 world_pos : TEXCOORD2;
    float4 screen_pos :TEXCOORD3;
    float3 view_dir : TEXCOORD4;
    
    #if defined(_TRIPLANAR)
        float2 proj_x : TEXCOORD5;
        float2 proj_y : TEXCOORD6;
        float2 proj_z : TEXCOORD7;
        UNITY_FOG_COORDS(8)
    #else
        UNITY_FOG_COORDS(5)
    #endif

    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Interpolators VertexProgram (VertexData v)
{
    Interpolators i;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, i);

    i.pos = UnityObjectToClipPos(v.vertex);
    i.color = v.color;
    i.uv = TRANSFORM_TEX(v.uv, _MainTex);
    i.world_normal = UnityObjectToWorldNormal(v.normal);
    i.world_pos = mul(unity_ObjectToWorld, v.vertex);
    i.screen_pos = ComputeScreenPos(i.pos);
    //i.view_dir = normalize(ObjSpaceViewDir(v.vertex)); //for perspective
    i.view_dir = mul((float3x3)unity_CameraToWorld, float3(0,0,-1)); // for ortho (position independent)
    
    #if defined(_TRIPLANAR)
        i.proj_x = i.world_pos.yz;
        i.proj_y = i.world_pos.xz;
        i.proj_z = i.world_pos.xy;
    #endif

    UNITY_TRANSFER_FOG(i, i.pos);

    return i;
}

struct Helpers
{
    half3 normal_blend;
};


Helpers GetHelpers(Interpolators i)
{
    Helpers h;
    h.normal_blend = ComputeBlend(i.world_normal);
    return h;
}

#if defined(_NORMAL_LIGHT)
half3 NormalLight(Helpers h)
{
    half3 normal_light = INST(_NormalLight);
    half3 color_x = INST(_NormalColorX) * normal_light.x;
    half3 color_y = INST(_NormalColorY) * normal_light.y;
    half3 color_z = INST(_NormalColorZ) * normal_light.z;

    half3 color = NormalBlend(color_x, color_y, color_z, h.normal_blend);
    return color;
}
#define NORMAL_LIGHT(src, helpers) (src*=NormalLight(helpers))
#endif


#if defined(_TEXTURE_LIGHT)
#define TEXTURE_LIGHT(src, mask) src*=lerp(INST(_ShadowColor), INST(_LightColor), mask)
#define TEXTURE_GI(src, mask) src+=INST(_IndirectColor) * INST(_Indirect) * mask;
#endif


#if defined(_TRIPLANAR)
float2 TexCoordRotation(float2 uv)
{
    float rot = _TexCoordRotation / 180.0 * UNITY_PI; 
    float s = sin(rot);
    float c = cos(rot);
    float2x2 rotMatrix = float2x2(c, -s, s, c);
    uv = mul(uv, rotMatrix);
    return uv;
}
#define TRANSFORM_ST(uv, st) (TexCoordRotation(uv.xy) * st.xy + st.zw)

half3 TriplanarTex(Interpolators i, Helpers h, sampler2D tex, float4 st)
{    
    half3 tex_x = tex2D(tex, TRANSFORM_ST(i.proj_x, st));
    half3 tex_y = tex2D(tex, TRANSFORM_ST(i.proj_y, st));
    half3 tex_z = tex2D(tex, TRANSFORM_ST(i.proj_z, st));
    return NormalBlend(tex_x, tex_y, tex_z, h.normal_blend);
}
#define TRIPLANAR(i, h, tex) TriplanarTex(i, h, tex, tex##_ST)
#endif


#if defined(_AMBIENT_LIGHT)
half3 AmbientLight(Helpers h, half3 source, half tex_mask)
{
    half3 ambient = source * unity_AmbientSky;
    half4 mask = INST(_AmbientMask);

    half3 color_x = lerp(source, ambient, mask.x);
    half3 color_y = lerp(source, ambient, mask.y);
    half3 color_z = lerp(source, ambient, mask.z);
    half3 color = NormalBlend(color_x, color_y, color_z, h.normal_blend);
    color = lerp(color, ambient, (1-tex_mask) * mask.w);
    color = lerp(source, color, INST(_Ambient));

    return color;
}
#define AMBIENT_LIGHT(src, mask, h) src=AmbientLight(h, src, mask)
#endif


#if defined(_EMISSION)
half3 Emission(half3 src, Interpolators i)
{
    half3 emission = tex2D(_EmissionTex, i.uv)*INST(_Emission);
    return pow(pow(src, 2.2) + pow(emission, 2.2), 1.0/2.2);
}
//#define EMISSION(src, i) src+=tex2D(_EmissionTex, i.uv)*INST(_Emission)
#define EMISSION(src, i) src = Emission(src, i)
#endif


#if defined(_CUSTOM_LIGHT0) || defined(_CUSTOM_LIGHT1)
half3 CustomLight(Interpolators i, half3 light_pos, half3 light_color, half light_range)
{
    half3 light_dir = normalize(light_pos - i.world_pos);
    half light_dist = distance(light_pos, i.world_pos);
    //half atten = 1 / (1 + pow(dot(light_dist, light_dist), 16)) * light_range;
    half atten = Fit(light_dist, light_range-1, light_range, 1, 0);
    half ndotl = dot(light_dir, i.world_normal);
    return saturate(ndotl) * light_color * atten;
}
#if defined(_CUSTOM_LIGHT0)
#define LIGHT0 CustomLight(i, _CustomLightPos0, _CustomLightColor0, _CustomLightRange0)
#endif
#if defined(_CUSTOM_LIGHT1)
#define LIGHT1 CustomLight(i, _CustomLightPos1, _CustomLightColor1, _CustomLightRange1)
#endif
#endif

#endif