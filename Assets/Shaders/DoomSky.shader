Shader "Doom/Sky"
{
    Properties
    {
        _MainTex ("Sky", 2D) = "white" {}
        _YawOffset ("Yaw Offset", Float) = 0
        _PitchOffset ("Pitch Offset", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Background"
            "Queue" = "Background"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Unlit"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _YawOffset;
                float _PitchOffset;
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
                // Keep sky infinitely far so depth tests open through F_SKY1 holes.
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 viewPos = TransformWorldToView(posWS);
                // Push far but stay inside the far plane.
                viewPos.z = min(viewPos.z, -_ProjectionParams.z * 0.95);
                output.positionCS = TransformWViewToHClip(viewPos);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.uv.x += _YawOffset;
                output.uv.y += _PitchOffset;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
#if !UNITY_COLORSPACE_GAMMA
                return half4(SRGBToLinear(tex.rgb), 1.0h);
#else
                return half4(tex.rgb, 1.0h);
#endif
            }
            ENDHLSL
        }
    }

    FallBack Off
}
