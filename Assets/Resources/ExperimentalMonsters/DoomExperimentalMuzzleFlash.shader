Shader "Doom/ExperimentalMuzzleFlash"
{
    // Shader-drawn muzzle flash for 3D monster fire frames. The mesh cannot
    // carry the flash (a baked fire stop-frame is a lump of geometry, and the
    // vanilla flash is light, not matter), so the fire frame shows a small
    // camera-facing quad instead. The texture is an irregular pixel-art
    // burst (ragged core, radial streaks, loose sparks) baked by
    // Tools/make_muzzle_flash.py strictly from the native fire frame's own
    // flash texels — a smooth radial disc failed the gate («огонь не может
    // быть ровным кругом», 2026-08-27), and a photoreal texture would break
    // authenticity. Alpha-tested for the hard pixel-art edge; fullbright by
    // design (a flash IS light), fog still applies so it does not glow
    // through a fogged sector.
    Properties
    {
        [MainTexture] _MainTex ("Flash Burst", 2D) = "white" {}
        _Exposure ("Exposure", Range(0.25, 8)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
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

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Exposure;
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
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 sample = SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex, input.uv);
                clip(sample.a - 0.5h);
                half3 color = sample.rgb * _Exposure;
                color = ApplyDoomFog(color, input.positionWS);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
