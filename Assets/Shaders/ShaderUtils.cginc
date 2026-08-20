#ifndef SHADER_UTILS
#define SHADER_UTILS

#include "UnityCG.cginc"

float IsDithered(float2 pos, float alpha)
{
    pos *= _ScreenParams.xy;

    float4x4 thresholdMatrix =
    {
        1.0/17.0,  9.0/17.0,  3.0/17.0,  11.0/17.0,
        13.0/17.0, 5.0/17.0,  15.0/17.0, 7.0/17.0,
        4.0/17.0,  12.0/17.0, 2.0/17.0,  10.0/17.0,
        16.0/17.0, 8.0/17.0,  14.0/17.0, 6.0/17.0
    };
    return alpha - thresholdMatrix[pos.x % 4][pos.y % 4];
}

inline void DitherClip(float4 screen_pos, float alpha)
{
    clip(IsDithered(screen_pos.xy / screen_pos.w, alpha));
}

half Fit(half value, half from1, half to1, half from2, half to2) {
    half c = (value - from1) / (to1 - from1) * (to2 - from2) + from2;
    return saturate(c);
}


half Overlay(half a, half b)
{
    return a < 0.5 ? 2*a*b : 1 - 2*(1-a)*(1-b); 
}

half3 Overlay(half3 a, half3 b)
{
    half rr = a.r < 0.5 ? 2*a.r*b.r : 1 - 2*(1-a.r)*(1-b.r);
    half gg = a.g < 0.5 ? 2*a.g*b.g : 1 - 2*(1-a.g)*(1-b.g); 
    half bb = a.b < 0.5 ? 2*a.b*b.b : 1 - 2*(1-a.b)*(1-b.b); 
    return half3(rr, gg, bb);
}

half3 OverlayBlend(half3 a, half3 b, half mask)
{
    half3 result = 0;
    result.r = a.r < 0.5 ? (2.0 * a.r * b.r) : (1.0 - 2.0 * (1.0 - a.r) * (1.0 - b.r));
    result.g = a.g < 0.5 ? (2.0 * a.g * b.g) : (1.0 - 2.0 * (1.0 - a.g) * (1.0 - b.g));
    result.b = a.b < 0.5 ? (2.0 * a.b * b.b) : (1.0 - 2.0 * (1.0 - a.b) * (1.0 - b.b));
    return lerp(a, result, mask);
}

half3 ComputeBlend(half3 normal)
{
    half3 blend = pow(abs(normal.xyz), 2);
    return  blend / dot(blend, half3(1,1,1));
}

inline half3 NormalBlend(half3 x, half3 y, half3 z, half3 w)
{
    return x * w.x + y * w.y + z * w.z;
} 

#endif