Shader "Doom/EnhancedSprite"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _CrossTex ("Crossfade", 2D) = "white" {}
        _CrossFade ("Crossfade Amount", Range(0,1)) = 0
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _SoftFloorFade ("Soft Floor Fade", Float) = 0
        _SectorAmbient ("Sector Ambient", Color) = (1,1,1,1)
        _SectorAmbientWeight ("Sector Ambient Weight", Range(0,1)) = 0
        _Roughness ("Roughness", Range(0,1)) = 0.85
        _EmissionStrength ("Emission", Range(0,2)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
        }

        Cull Off
        ZWrite On
        ZTest LEqual
        // Converts the cutout alpha edge to MSAA coverage in Enhanced mode.
        AlphaToMask On

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ DOOM_SPRITE_TEXEL_AA
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Includes/DoomControlledSampling.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_CrossTex); SAMPLER(sampler_CrossTex);
            float4 _MainTex_TexelSize;
            float4 _CrossTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _CrossTex_ST;
                half _CrossFade;
                half _Cutoff;
                half _SoftFloorFade;
                half4 _SectorAmbient;
                half _SectorAmbientWeight;
                half _Roughness;
                half _EmissionStrength;
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
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                // Camera-facing billboard normal in view space → world.
                float3 nVS = float3(0.0, 0.0, 1.0);
                output.normalWS = normalize(mul((float3x3)UNITY_MATRIX_I_V, nVS));
                return output;
            }

            half3 DoomShade(half3 albedo, half3 sectorAmbient, float3 normalWS,
                            float3 positionWS, half roughness, half emissionStrength)
            {
                half3 ambient = albedo * sectorAmbient;
                half3 fakeL = normalize(half3(0.35h, 0.85h, 0.25h));
                half ndotl = saturate(dot(normalWS, fakeL));
                half relief = lerp(0.70h, 1.0h, ndotl);
                half reliefAmt = saturate(1.0h - roughness);
                half3 shaded = ambient * lerp(1.0h, relief, reliefAmt);

                half3 dynamic = 0;
                #ifdef _ADDITIONAL_LIGHTS
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; i++)
                {
                    Light light = GetAdditionalLight(i, positionWS);
                    half n = saturate(dot(normalWS, light.direction));
                    dynamic += albedo * light.color * (light.distanceAttenuation * light.shadowAttenuation * n);
                }
                #endif

                half3 emission = albedo * emissionStrength;
                return shaded + dynamic * 0.85h + emission;
            }

            float4 _DoomFogColor;
            float4 _DoomFogParams;

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

            half4 Frag(Varyings input) : SV_Target
            {
                #if defined(DOOM_SPRITE_TEXEL_AA)
                half4 mainSample = DoomSamplePointTexelAA(
                    TEXTURE2D_ARGS(_MainTex, sampler_MainTex), input.uv, _MainTex_TexelSize);
                half4 crossSample = DoomSamplePointTexelAA(
                    TEXTURE2D_ARGS(_CrossTex, sampler_CrossTex), input.uv, _CrossTex_TexelSize);
                #else
                half4 mainSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 crossSample = SAMPLE_TEXTURE2D(_CrossTex, sampler_CrossTex, input.uv);
                #endif
                half4 albedoSample = lerp(mainSample, crossSample, saturate(_CrossFade));

                half softFade = 1.0h;
                if (_SoftFloorFade > 1e-4h)
                    softFade = saturate(input.uv.y / max(_SoftFloorFade, 1e-4h));

                clip(albedoSample.a * softFade - _Cutoff);

                half3 albedo = albedoSample.rgb;
                half3 sectorAmbient = lerp(input.color.rgb, _SectorAmbient.rgb, _SectorAmbientWeight);
                half3 color = DoomShade(
                    albedo, sectorAmbient, input.normalWS, input.positionWS,
                    _Roughness, _EmissionStrength);
                color = ApplyDoomFog(color, input.positionWS);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ DOOM_SPRITE_TEXEL_AA

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Includes/DoomControlledSampling.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_CrossTex); SAMPLER(sampler_CrossTex);
            float4 _MainTex_TexelSize;
            float4 _CrossTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _CrossTex_ST;
                half _CrossFade;
                half _Cutoff;
                half _SoftFloorFade;
                half4 _SectorAmbient;
                half _SectorAmbientWeight;
                half _Roughness;
                half _EmissionStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                #if defined(DOOM_SPRITE_TEXEL_AA)
                half4 mainSample = DoomSamplePointTexelAA(
                    TEXTURE2D_ARGS(_MainTex, sampler_MainTex), input.uv, _MainTex_TexelSize);
                half4 crossSample = DoomSamplePointTexelAA(
                    TEXTURE2D_ARGS(_CrossTex, sampler_CrossTex), input.uv, _CrossTex_TexelSize);
                #else
                half4 mainSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 crossSample = SAMPLE_TEXTURE2D(_CrossTex, sampler_CrossTex, input.uv);
                #endif
                half alpha = lerp(mainSample.a, crossSample.a, saturate(_CrossFade));
                half softFade = 1.0h;
                if (_SoftFloorFade > 1e-4h)
                    softFade = saturate(input.uv.y / max(_SoftFloorFade, 1e-4h));
                clip(alpha * softFade - _Cutoff);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ DOOM_SPRITE_TEXEL_AA

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Includes/DoomControlledSampling.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_CrossTex); SAMPLER(sampler_CrossTex);
            float4 _MainTex_TexelSize;
            float4 _CrossTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _CrossTex_ST;
                half _CrossFade;
                half _Cutoff;
                half _SoftFloorFade;
                half4 _SectorAmbient;
                half _SectorAmbientWeight;
                half _Roughness;
                half _EmissionStrength;
            CBUFFER_END

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, _LightDirection));
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                #if defined(DOOM_SPRITE_TEXEL_AA)
                half4 mainSample = DoomSamplePointTexelAA(
                    TEXTURE2D_ARGS(_MainTex, sampler_MainTex), input.uv, _MainTex_TexelSize);
                half4 crossSample = DoomSamplePointTexelAA(
                    TEXTURE2D_ARGS(_CrossTex, sampler_CrossTex), input.uv, _CrossTex_TexelSize);
                #else
                half4 mainSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 crossSample = SAMPLE_TEXTURE2D(_CrossTex, sampler_CrossTex, input.uv);
                #endif
                half alpha = lerp(mainSample.a, crossSample.a, saturate(_CrossFade));
                half softFade = 1.0h;
                if (_SoftFloorFade > 1e-4h)
                    softFade = saturate(input.uv.y / max(_SoftFloorFade, 1e-4h));
                clip(alpha * softFade - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
