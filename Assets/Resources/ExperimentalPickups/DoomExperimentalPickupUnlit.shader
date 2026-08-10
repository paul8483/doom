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
        // 0 = MEDIA0-style sine pulse; 1 = ARM1 A/B discrete blink via _Blink.
        _BlinkMode ("Blink Mode", Range(0, 1)) = 0
        _Blink ("Blink", Range(0, 1)) = 1
        _ColorTint ("Color Tint", Color) = (1, 1, 1, 1)
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
                half _BlinkMode;
                half _Blink;
                half4 _ColorTint;
            CBUFFER_END

            // SectorFogSystem globals (same contract as DoomEnhancedWorld/Sprite).
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
                half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
                albedo *= _ColorTint.rgb;
                half mask = SAMPLE_TEXTURE2D(
                    _EmissionMask, sampler_EmissionMask, input.uv).r;
                // Continuous sine (medikit cross) or discrete A/B (armor gem).
                half pulse = _BlinkMode > 0.5h
                    ? _Blink
                    : (0.5h + 0.5h * sin(_Time.y * _PulseSpeed));
                half3 color = albedo * (_Exposure + _EmissionStrength);
                // ARM1B dims the red gem vs ARM1A — darken masked texels off-phase.
                if (_BlinkMode > 0.5h)
                    color = lerp(color, color * 0.62h, mask * (1.0h - pulse));
                color += mask * _PulseStrength * pulse;
                color = ApplyDoomFog(color, input.positionWS);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
