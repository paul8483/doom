// MF_SHADOW analog for the stop-motion 3D path: the spectre shares the
// demon's meshes, so the ghost look must come from the material — the mesh
// equivalent of the billboard's Doom/Spectre (UV shimmer + ~0.5 alpha).
//
// A translucent MESH needs one extra trick a quad does not: with plain alpha
// blending every interior triangle blends its own layer and the body turns
// into overlapping shells. The depth prepass (ColorMask 0) primes the depth
// buffer with the nearest surface, and the color pass draws with ZTest Equal,
// so exactly one layer of the mesh blends over the scene.
Shader "Doom/ExperimentalSpectre"
{
    Properties
    {
        [MainTexture] _MainTex ("Albedo", 2D) = "white" {}
        _Exposure ("Exposure", Range(0.25, 8)) = 1
        _AlphaScale ("Alpha", Range(0, 1)) = 0.5
        // Atlas UVs: keep the warp an order smaller than the billboard's
        // 0.02 or the shimmer drags texels across UV-island borders.
        _DistortStrength ("Distort", Float) = 0.006
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

        Cull Back

        // Depth prepass: nearest-surface depth only, no color.
        Pass
        {
            Name "SpectreDepthPrime"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Exposure;
                half _AlphaScale;
                half _DistortStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        // Color pass: blends the single primed layer over the scene.
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite Off
            ZTest Equal
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Exposure;
                half _AlphaScale;
                half _DistortStrength;
            CBUFFER_END

            // SectorFogSystem globals (same contract as the pickup shader).
            float4 _DoomFogColor;
            float4 _DoomFogParams; // x=density y=start z=end w=enabled

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            half3 ApplyDoomFog(half3 color, float3 positionWS)
            {
                if (_DoomFogParams.w < 0.5) return color;
                float dist = distance(GetCameraPositionWS(), positionWS);
                float start = _DoomFogParams.y;
                float end = max(_DoomFogParams.z, start + 0.01);
                float t = saturate((dist - start) / (end - start));
                t = 1.0 - exp(-_DoomFogParams.x * t * t * 8.0);
                return lerp(color, _DoomFogColor.rgb, t);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Same shimmer cadence as the billboard's Doom/Spectre.
                float t = _Time.y;
                float2 warp = float2(
                    sin(t * 2.1 + input.uv.y * 9.0) * _DistortStrength,
                    cos(t * 1.7 + input.uv.x * 7.0) * (_DistortStrength * 0.75));
                half3 albedo = SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex, input.uv + warp).rgb;
                half3 color = ApplyDoomFog(albedo * _Exposure, input.positionWS);
                return half4(color, saturate(_AlphaScale));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
