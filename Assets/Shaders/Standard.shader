Shader "Idle/Standard"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _Opacity("Opacity", Range(0,1)) = 1
        _MainTex("Color Tex", 2D) = "white" {}
        
        _EmissionTex("Emission Map", 2D) = "black" {}
        _Emission("Emission", Float) = 1
        
        _NormalLight("Light", Vector) = (1,1,1,1)
        _NormalColorX("Color X", Color) = (1,1,1,1)
        _NormalColorY("Color Y", Color) = (1,1,1,1)
        _NormalColorZ("Color Z", Color) = (1,1,1,1)
        
        _Ambient("Ambient", Range(0,1)) = 1
        _AmbientMask("Ambient Mask", Vector) = (1,1,1,0)

        // Accept URP SimpleLit/Lit property writes from FBX MaterialDescription remaps.
        [HideInInspector] _BaseMap("Base Map", 2D) = "white" {}
        [HideInInspector] _BaseColor("Base Color", Color) = (1,1,1,1)
        [HideInInspector] _BumpMap("Normal Map", 2D) = "bump" {}
        [HideInInspector] _BumpScale("Scale", Float) = 1
        [HideInInspector] _EmissionMap("Emission Map", 2D) = "white" {}
        [HideInInspector] _EmissionColor("Color", Color) = (0,0,0)
        [HideInInspector] _SpecColor("Specular", Color) = (0.2,0.2,0.2)
        [HideInInspector] _SpecGlossMap("Specular", 2D) = "white" {}
        [HideInInspector] _Cutoff("Alpha Clip", Range(0,1)) = 0.5
        [HideInInspector] _Smoothness("Smoothness", Range(0,1)) = 0.5
        [HideInInspector] _SmoothnessSource("Source", Float) = 0
        [HideInInspector] _SpecularHighlights("Specular Highlights", Float) = 1
        [HideInInspector] _Shininess("Shininess", Range(0.01,1)) = 0.5
        [HideInInspector] _GlossinessSource("Glossiness Source", Float) = 0
        [HideInInspector] _SpecSource("Specular Source", Float) = 0
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _Blend("Blend", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _AlphaClip("Alpha Clip", Float) = 0
        [HideInInspector] _SrcBlend("Src", Float) = 1
        [HideInInspector] _DstBlend("Dst", Float) = 0
        [HideInInspector] _SrcBlendAlpha("Src Alpha", Float) = 1
        [HideInInspector] _DstBlendAlpha("Dst Alpha", Float) = 0
        [HideInInspector] _ZWrite("ZWrite", Float) = 1
        [HideInInspector] _BlendModePreserveSpecular("Preserve Specular", Float) = 1
        [HideInInspector] _AlphaToMask("Alpha To Mask", Float) = 0
        [HideInInspector] _AddPrecomputedVelocity("Add Precomputed Velocity", Float) = 0
        [HideInInspector] _XRMotionVectorsPass("XR Motion Vectors", Float) = 1
        [HideInInspector] _ReceiveShadows("Receive Shadows", Float) = 1
        [HideInInspector] _QueueOffset("Queue Offset", Float) = 0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        
        Pass
        {
            CGPROGRAM
            
            #pragma vertex VertexProgram
            #pragma fragment FragmentProgram
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog

            #define _NORMAL_LIGHT
            #define _AMBIENT_LIGHT
            #define _EMISSION

            #include "ShaderFunctions.cginc"

            fixed4 FragmentProgram(Interpolators i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                #if defined(LOD_FADE_CROSSFADE)
                    UnityApplyDitherCrossFade(i.pos.xy);
                #endif

                half4 main_tex = tex2D(_MainTex, i.uv);
                
                DitherClip(i.screen_pos, main_tex.a * INST(_Opacity));
                Helpers h = GetHelpers(i);

                // apply lighting
                NORMAL_LIGHT(main_tex.rgb, h);

                // apply emissive
                AMBIENT_LIGHT(main_tex.rgb, 1, h);
                EMISSION(main_tex.rgb, i);

                // tint
                main_tex.rgb *= INST(_Color);

                UNITY_APPLY_FOG(i.fogCoord, main_tex);
                
                return main_tex;
            }

            ENDCG
        }
    }
    FallBack "Diffuse"
}
