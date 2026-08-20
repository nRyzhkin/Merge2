Shader "GTA/Terrain"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Albedo", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Header(Height Sand)]
        _SandHeightMap("Height Sand Albedo", 2D) = "white" {}
        [Toggle(_HEIGHT_SAND)] _UseHeightSand("Use Height Sand", Float) = 1
        _SandHeightMax("Height Sand Max (world Y)", Float) = 7
        _SandHeightBlend("Height Sand Blend Width", Range(0.01, 8)) = 1.5
        _SandHeightTiling("Height Sand World Tiling", Float) = 0.15

        [Header(Detail Materials)]
        _DetailMaskMap("Detail Mask (R Grass, G Sand, B Gravel)", 2D) = "black" {}
        [Toggle(_DETAIL_MASK_MAP)] _UseDetailMask("Use Detail Mask", Float) = 1
        [Toggle(_DETAIL_VERTEX_COLOR)] _UseDetailVertexColor("Use Vertex Color Instead", Float) = 0
        [Space(4)]
        _DetailGrassMap("Detail Grass (B/W facture)", 2D) = "gray" {}
        _DetailSandMap("Detail Sand (B/W facture)", 2D) = "gray" {}
        _DetailGravelMap("Detail Gravel (B/W facture)", 2D) = "gray" {}
        _DetailStrength("Detail Blend Strength", Range(0, 2)) = 1

        [Header(Shore Foam)]
        [Toggle(_SHORE_FOAM)] _UseShoreFoam("Sync Shore Foam With Water", Float) = 1
        _ShoreFoamStrength("Shore Foam Strength", Range(0, 2)) = 1

        [Header(Surface)]
        [HideInInspector] _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        _ReflectionStrength("Reflection Strength", Range(0, 1)) = 0.35
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1

        [Toggle(_NORMALMAP)] _UseNormalMap("Normal Map", Float) = 0

        [HideInInspector] _Cull("", Float) = 2
        [HideInInspector] _QueueOffset("Queue Offset", Float) = 0
        [HideInInspector] _ShadowSoftness("", Float) = 0.85
        [HideInInspector] _ShadowContrast("", Float) = 1.35
        [HideInInspector] _ReceiveShadows("", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "GTA"
            "IgnoreProjector" = "True"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One Zero
            ZWrite On
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex GTA_TerrainVert
            #pragma fragment GTA_TerrainFrag

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _HEIGHT_SAND
            #pragma shader_feature_local _DETAIL_MASK_MAP
            #pragma shader_feature_local _DETAIL_VERTEX_COLOR
            #pragma shader_feature_local _SHORE_FOAM

            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Includes/GTA_TerrainForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex GTA_TerrainDepthVert
            #pragma fragment GTA_TerrainDepthFrag
            #pragma multi_compile_instancing

            #include "Includes/GTA_TerrainDepthPass.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
    CustomEditor "GTAShaderGUI"
}
