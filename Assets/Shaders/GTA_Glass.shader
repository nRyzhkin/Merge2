Shader "GTA/Glass"
{
    Properties
    {
        [MainTexture] _BaseMap("Tint / Opacity (A)", 2D) = "white" {}
        [MainColor]   _BaseColor("Color", Color) = (0.8, 0.9, 1.0, 0.15)
        _OpacityMap("Opacity (R)", 2D) = "white" {}

        _Smoothness("Reflection Smoothness", Range(0, 1)) = 0.92
        _CubemapReflectionStrength("Cubemap Reflection", Range(0, 1)) = 0.65
        [HDR] _SpecularColor("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularIntensity("Specular Intensity", Range(0, 4)) = 1.5
        _FresnelPower("Fresnel Power", Range(1, 8)) = 5
        _FresnelBias("Fresnel Bias", Range(0, 0.5)) = 0.04
        _MinAlpha("Min Alpha", Range(0, 1)) = 0.05

        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1

        _DirtMap("Dirt Mask (R)", 2D) = "black" {}
        _DirtColor("Dirt Color", Color) = (0.25, 0.22, 0.18, 1)
        _DirtStrength("Dirt Strength", Range(0, 1)) = 0.6
        _DirtOpacity("Dirt Opacity Add", Range(0, 1)) = 0.35

        _DistanceFadeStart("Distance Fade Start", Float) = 50
        _DistanceFadeEnd("Distance Fade End", Float) = 80

        [Toggle(_OPACITYMAP)] _UseOpacityMap("Opacity Map", Float) = 0
        [Toggle(_NORMALMAP)] _UseNormalMap("Normal Map", Float) = 0
        [Toggle(_DIRT_MAP)] _UseDirtMap("Dirt Mask", Float) = 0
        [Toggle(_DISTANCE_FADE_FAR)] _DistanceFadeFar("Fade Out Far", Float) = 0
        [Toggle(_DISTANCE_FADE_NEAR)] _DistanceFadeNear("Fade Out Near", Float) = 0

        [HideInInspector] _Cull("", Float) = 2
        [HideInInspector] _ShadowSoftness("", Float) = 0.85
        [HideInInspector] _ShadowContrast("", Float) = 1.35
        [HideInInspector] _ReceiveShadows("", Float) = 0
    }

    CustomEditor "GTAShaderGUI"

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "GTA"
            "IgnoreProjector" = "True"
        }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex GTA_GlassVert
            #pragma fragment GTA_GlassFrag

            #pragma shader_feature_local _OPACITYMAP
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _DIRT_MAP
            #pragma shader_feature_local _DISTANCE_FADE_FAR
            #pragma shader_feature_local _DISTANCE_FADE_NEAR

            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #include "Includes/GTA_GlassForwardPass.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
