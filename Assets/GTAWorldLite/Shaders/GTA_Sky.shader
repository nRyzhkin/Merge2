Shader "GTA/Sky"
{
    Properties
    {
        [HDR] _ZenithColor("Zenith", Color) = (0.15, 0.35, 0.75, 1)
        [HDR] _HorizonColor("Horizon", Color) = (0.55, 0.7, 0.9, 1)
        [HDR] _GroundColor("Ground", Color) = (0.08, 0.08, 0.1, 1)
        [HDR] _SunColor("Sun Color", Color) = (1, 0.95, 0.85, 1)
        [HDR] _HaloColor("Halo Color", Color) = (1, 0.7, 0.35, 1)
        [HDR] _HazeColor("Haze Color", Color) = (0.8, 0.85, 0.95, 1)

        _SunDirection("Sun Direction", Vector) = (0.3, 0.7, 0.2, 0)
        _SunSize("Sun Size", Range(0.001, 0.1)) = 0.02
        _HaloSize("Halo Size", Range(0.01, 0.6)) = 0.18
        _HaloIntensity("Halo Intensity", Range(0, 4)) = 1.2
        _HorizonHaze("Horizon Haze", Range(0, 2)) = 0.65
        _Exposure("Exposure", Range(0.1, 4)) = 1
        _WeatherGrey("Weather Grey", Range(0, 1)) = 0
        [HideInInspector] _SunVisibility("Sun Visibility", Range(0, 1)) = 1
        [HideInInspector] _HaloVisibility("Halo Visibility", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        // URP draws sky after opaques — only fill pixels where depth is still far.
        ZTest LEqual

        Pass
        {
            Name "Sky"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex GTA_SkyVert
            #pragma fragment GTA_SkyFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Includes/GTAWorldGlobals.hlsl"
            #include "Includes/GTA_SkySun.hlsl"
            #include "Includes/GTA_SkyClouds.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half4 _SunColor;
                half4 _HaloColor;
                half4 _HazeColor;
                float4 _SunDirection;
                half _SunSize;
                half _HaloSize;
                half _HaloIntensity;
                half _HorizonHaze;
                half _Exposure;
                half _WeatherGrey;
                half _SunVisibility;
                half _HaloVisibility;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirWS  : TEXCOORD0;
            };

            Varyings GTA_SkyVert(Attributes input)
            {
                Varyings output;

                // Direction through the sky (Unity positions the sky mesh around the camera).
                float3 viewDirWS = mul((float3x3)unity_ObjectToWorld, input.positionOS.xyz);

                // Project via full object→clip so the mesh stays locked to the camera.
                float4 clip = TransformObjectToHClip(input.positionOS.xyz);

                // Force far-plane depth. URP DrawSkybox runs after opaque; near-plane depth
                // would paint the sky over all geometry (especially on Metal / reversed-Z).
            #if UNITY_REVERSED_Z
                clip.z = 0.0;           // far = 0
            #else
                clip.z = clip.w;        // far = w → NDC z = 1
            #endif

                output.positionCS = clip;
                output.viewDirWS = viewDirWS;
                return output;
            }

            half3 GTAEvaluateSky(float3 viewDir)
            {
                float3 V = normalize(viewDir);
                float3 S = normalize(_SunDirection.xyz);
                half y = V.y;

                // Base vertical: thin horizon band → zenith takes over quickly above it.
                half belowHorizon = saturate((-y) * 3.0h);
                half horizonBlend = 1.0h - saturate(abs(y) * 4.5h);
                half zenithBlend = saturate(y * 2.35h);
                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, zenithBlend);
                sky = lerp(sky, _HorizonColor.rgb, belowHorizon);

                // Angle to sun — drives directional (not full-sphere) coloring
                half sunDot = dot(V, S);
                half towardSun = saturate(sunDot);
                half antiSun = saturate(-sunDot);

                // Stronger artistic sunset when sun is near the horizon
                half sunsetStrength = saturate(1.0h - abs(S.y) * 2.2h);
                sunsetStrength *= saturate(S.y * 6.0h + 0.85h); // kill tint when deep below horizon

                // Opposite sky stays cool / deep blue (GTA V style)
                half3 coolSky = _ZenithColor.rgb * half3(0.55h, 0.65h, 0.95h);
                sky = lerp(sky, coolSky, antiSun * antiSun * lerp(0.25h, 0.55h, sunsetStrength));

                // Horizon haze — sky palette only (never sun halo orange on the full horizon band)
                half sunAzimuthWeight = towardSun * towardSun * 0.65h + 0.35h;
                half haze = pow(horizonBlend, 1.25h) * _HorizonHaze * sunAzimuthWeight;
                half3 hazeCol = lerp(_HazeColor.rgb, _HorizonColor.rgb, sunsetStrength * 0.65h);
                sky = lerp(sky, hazeCol, saturate(haze));

                // Sunset Tint — tight cone around the sun
                half sunsetCore = pow(towardSun, 3.8h);
                half3 warm = _HaloColor.rgb;
                half warmVis = _HaloVisibility;
                sky = lerp(sky, warm, sunsetCore * sunsetStrength * 0.35h * warmVis);
                sky += warm * (sunsetCore * sunsetStrength * 0.08h * warmVis);

                // Weather grey (base sky) before clouds so storm deck stays intentional.
                half grey = dot(sky, half3(0.299h, 0.587h, 0.114h));
                sky = lerp(sky, half3(grey, grey, grey) * lerp(0.85h, 1.15h, y * 0.5h + 0.5h), _WeatherGrey);

                half4 clouds = GTAEvaluateSkyClouds(
                    V, _ZenithColor.rgb, _HorizonColor.rgb, _GTA_Weather);

                half storm = smoothstep(0.42h, 1.0h, saturate((half)_GTA_Weather));
                half cloudCover = clouds.a;

                // Sun / halo peek through gaps; storm nearly kills the disc.
                half sunThrough = 1.0h - cloudCover * lerp(0.45h, 0.97h, storm);
                sky += GTAEvaluateSunHalo(
                    V, S, _SunColor.rgb, _HaloColor.rgb,
                    _SunSize, min(_HaloSize, 0.12h), _HaloIntensity, 1.0h, 1.0h,
                    _SunVisibility, _HaloVisibility) * sunThrough;

                sky = lerp(sky, clouds.rgb, clouds.a);

                return sky * _Exposure;
            }

            half4 GTA_SkyFrag(Varyings input) : SV_Target
            {
                return half4(GTAEvaluateSky(input.viewDirWS), 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
