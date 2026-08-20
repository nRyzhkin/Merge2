Shader "GTA/WaterLite"
{
    Properties
    {
        _EdgeColor("EdgeColor", Color) = (0.2509804, 0.3294118, 0.2862745, 1)
        _EdgeDepth("Edge Depth (m)", Float) = 5
        _FoamColor("FoamColor", Color) = (1, 1, 1, 1)
        _FoamOpacity("Foam Opacity", Range(0, 1)) = 0.88
        _FoamCoverage("Foam Coverage", Range(0.5, 3)) = 1.35
        _FoamThickness("Foam Thickness", Range(0.3, 2)) = 1.3
        _FoamDayBoost("Foam Day Boost", Range(0, 2)) = 1.2
        _FoamShoreWobble("Foam Shore Wobble (m)", Range(0, 12)) = 3.5
        _FoamShoreScale("Foam Shore Scale", Range(0.01, 0.25)) = 0.055
        _FoamBeachDepth("Foam Beach Depth (m)", Range(0.05, 3)) = 0.8
        _ShallowClearDepth("Clear Depth (m)", Range(0.5, 20)) = 4
        _WaveFrequency("WaveFrequency", Float) = 3
        _WaveSpeed("WaveSpeed", Float) = 0.1
        _WaveDist("WaveDist", Float) = 5
        _MaxWaveDist("MaxWaveDist", Float) = 250
        _WaterSurfaceMap("Water Surface Map", 2D) = "white" {}
        [HideInInspector]_WaterSurfaceBounds("Water Surface Bounds", Vector) = (0,0,2000,2000)
        [HideInInspector]_WaterSurfaceDepthMax("Water Surface Depth Max", Float) = 50
        _Color("Color", Color) = (0.1921569, 0.2392157, 0.2627451, 1)
        _NormalStrength("NormalStrength", Float) = 0.1
        _Speed("Speed", Float) = 0.2
        _Tiling("Tiling", Float) = 0.2
        _Transparency("Transparency", Range(0, 1)) = 0.95
        _Caustic_Strength("Caustic_Strength", Range(0, 2)) = 2
        [ToggleUI]_UseFoam("UseFoam", Float) = 1
        _WaveNormalMap("Wave Normal (256)", 2D) = "bump" {}
        _FoamNoiseMap("Foam Noise (256)", 2D) = "gray" {}
        _CausticMap("Caustic (256)", 2D) = "black" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "UniversalMaterialType" = "GTA"
            "IgnoreProjector" = "True"
        }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WaterLiteVert
            #pragma fragment WaterLiteFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #define _FOG_FRAGMENT 1
            #include "Includes/GTA_WaterLitePass.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
