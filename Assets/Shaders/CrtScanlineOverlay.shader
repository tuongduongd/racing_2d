Shader "RacingGame/CrtScanlineOverlay"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0.02, 0.05, 0.08, 1.0)
        _ScanlineIntensity ("Scanline Intensity", Range(0, 0.5)) = 0.16
        _ScanlineDensity ("Scanline Density", Range(120, 720)) = 320.0
        _VignetteStrength ("Vignette Strength", Range(0, 0.6)) = 0.16
        _Opacity ("Overall Opacity", Range(0, 1)) = 1.0
        _ScrollSpeed ("Scanline Drift Speed", Range(0, 4)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _ScanlineIntensity;
                float _ScanlineDensity;
                float _VignetteStrength;
                float _Opacity;
                float _ScrollSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Subtle horizontal CRT scanlines computed procedurally (0 texture samples)
                float phase = input.uv.y * _ScanlineDensity + _Time.y * _ScrollSpeed;
                float scanWave = 0.5 - 0.5 * cos(phase * 6.2831853);
                float scanlineMask = scanWave * _ScanlineIntensity;

                // Soft corner vignette that frames HUD without darkening center road visibility
                float2 centered = input.uv - 0.5;
                float distSq = dot(centered, centered) * 2.2;
                float vignetteMask = saturate(distSq * distSq) * _VignetteStrength;

                half alpha = (half)saturate((scanlineMask + vignetteMask) * _Opacity) * _Color.a * input.color.a;
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
