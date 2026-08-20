Shader "GTA/Sprite Opaque"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _AlphaTex("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha("Enable External Alpha", Float) = 0
        [MainColor] _Color("Tint", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.2
        _Smoothness("Smoothness", Range(0,1)) = 0.05
        _Occlusion("Occlusion", Range(0,1)) = 1
        _ReflectionStrength("Reflection Strength", Range(0,1)) = 0.1
        [HDR] _EmissionColor("Emission", Color) = (0,0,0,0)

        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip("Flip", Vector) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite On
        Blend Off

        Pass
        {
            Name "Forward"
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "UnitySprites.cginc"
            #include "Includes/GTAWorldLighting.cginc"

            half _Cutoff;
            half _Smoothness;
            half _Occlusion;
            half _ReflectionStrength;
            half4 _EmissionColor;

            struct v2f_lit
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f_lit vert(appdata_t v)
            {
                v2f_lit o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4 flipped = UnityFlipSprite(v.vertex, _Flip);
                o.vertex = UnityObjectToClipPos(flipped);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color * _RendererColor;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normalWS = normalize(UnityObjectToWorldDir(float3(0, 0, -1)));

                #ifdef PIXELSNAP_ON
                o.vertex = UnityPixelSnap(o.vertex);
                #endif

                return o;
            }

            fixed4 frag(v2f_lit i) : SV_Target
            {
                fixed4 texColor = SampleSpriteTexture(i.texcoord);
                fixed4 tinted = texColor * i.color;

                clip(tinted.a - _Cutoff);

                half3 lit = GtaShadeLit(
                    tinted.rgb,
                    i.normalWS,
                    i.worldPos,
                    _Smoothness,
                    _EmissionColor.rgb,
                    _Occlusion,
                    _ReflectionStrength);

                return fixed4(lit, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
    CustomEditor "GTAShaderGUI"
}
