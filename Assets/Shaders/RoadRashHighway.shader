Shader "RacingGame/RoadRashHighway"
{
    Properties
    {
        _MainTex ("Grain Texture", 2D) = "white" {}
        _GrainScale ("Grain Scale", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

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
                float2 uv1 : TEXCOORD1;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _GrainScale;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _GrainScale;
                output.uv1 = input.uv1;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 col = input.color;
                float grainStrength = input.uv1.x;
                if (grainStrength > 0.001)
                {
                    half texSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r;
                    half modulation = lerp(1.0h, texSample, (half)grainStrength);
                    col.rgb *= modulation;
                }
                return col;
            }
            ENDHLSL
        }
    }
}
