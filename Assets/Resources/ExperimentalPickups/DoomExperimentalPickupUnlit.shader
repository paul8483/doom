Shader "Doom/ExperimentalPickupUnlit"
{
    Properties
    {
        [MainTexture] _MainTex ("Albedo", 2D) = "white" {}
        _EmissionMask ("Emission Mask", 2D) = "black" {}
        _Exposure ("Exposure", Range(0.25, 8)) = 1
        _EmissionStrength ("Emission", Range(0, 2)) = 0
        _PulseStrength ("Pulse Strength", Range(0, 3)) = 0
        _PulseSpeed ("Pulse Speed", Range(0, 16)) = 8
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Back
        ZWrite On

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_EmissionMask);
            SAMPLER(sampler_EmissionMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Exposure;
                half _EmissionStrength;
                half _PulseStrength;
                half _PulseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
                half mask = SAMPLE_TEXTURE2D(
                    _EmissionMask, sampler_EmissionMask, input.uv).r;
                half pulse = 0.5h + 0.5h * sin(_Time.y * _PulseSpeed);
                half3 color = albedo * (_Exposure + _EmissionStrength);
                color += mask * _PulseStrength * pulse;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
