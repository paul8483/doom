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
            "IgnoreProjector" = "True"
        }

        Cull Front
        ZWrite Off
        ZTest LEqual
        Blend Off

        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            TEXTURE2D(_MainTex);
            // Independent sampler keeps the panorama at nearest LOD0 without
            // mutating the shared SKY1 texture used by world materials.

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _YawOffset;
                float _PitchOffset;
            CBUFFER_END

            // 1 / (2 * PI)
            static const float kInvTwoPi = 0.15915494309189535;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float4 clipPos = TransformWorldToHClip(posWS);

                // Standard skybox far-plane depth (works with reversed Z in URP).
                // Previous view-space z rewrite left the sphere unshaded on GPU.
#if UNITY_REVERSED_Z
                clipPos.z = clipPos.w * 1.0e-7;
#else
                clipPos.z = clipPos.w * 0.999999;
#endif
                output.positionCS = clipPos;

                // Direction from camera through this sky vertex (camera-centered mesh).
                output.viewDirWS = posWS - GetCameraPositionWS();
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.viewDirWS);

                // Cylindrical panorama: U = yaw, V = altitude (SKY1 mountains→sky).
                float u = atan2(dir.x, dir.z) * kInvTwoPi + 0.5 + _YawOffset;
                float v = saturate(dir.y * 0.5 + 0.5 + _PitchOffset);
                float2 uv = TRANSFORM_TEX(float2(u, v), _MainTex);

                half4 tex = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_PointRepeat, uv, 0.0);
#if !UNITY_COLORSPACE_GAMMA
                return half4(SRGBToLinear(tex.rgb), 1.0);
#else
                return half4(tex.rgb, 1.0);
#endif
            }
            ENDHLSL
        }
    }

    FallBack Off
}
