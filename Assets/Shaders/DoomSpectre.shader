Shader "Doom/Spectre"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _DistortStrength ("Distort", Float) = 0.02
        _AlphaScale ("Alpha", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _DistortStrength;
                half _AlphaScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float2 warp = float2(
                    sin(t * 2.1 + input.uv.y * 9.0) * _DistortStrength,
                    cos(t * 1.7 + input.uv.x * 7.0) * (_DistortStrength * 0.75));
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + warp);
                clip(tex.a - 0.05h);

                // Visible translucent spectre: alpha stays in ~0.45–0.55.
                half alpha = clamp(saturate(_AlphaScale) * tex.a, 0.45h, 0.55h);

#if !UNITY_COLORSPACE_GAMMA
                half3 rgb = SRGBToLinear(LinearToSRGB(tex.rgb) * input.color.rgb);
#else
                half3 rgb = tex.rgb * input.color.rgb;
#endif
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
