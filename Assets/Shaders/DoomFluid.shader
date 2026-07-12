Shader "Doom/Fluid"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Bump Scale", Float) = 1
        _Roughness ("Roughness", Range(0,1)) = 0.95
        _EmissionStrength ("Emission", Range(0,2)) = 0.2
        _SectorAmbient ("Sector Ambient", Color) = (1,1,1,1)
        _SectorAmbientWeight ("Sector Ambient Weight", Range(0,1)) = 0
        _ScrollSpeed ("Scroll Speed", Vector) = (0.03, 0.01, 0, 0)
        _DistortStrength ("Distort", Range(0,0.1)) = 0.02
        _MainTexB ("Next Frame", 2D) = "white" {}
        _FrameBlend ("Frame Blend", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
        }

        Cull Back
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_MainTexB); SAMPLER(sampler_MainTexB);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BumpMap_ST;
                half _BumpScale;
                half _Roughness;
                half _EmissionStrength;
                half4 _SectorAmbient;
                half _SectorAmbientWeight;
                float4 _ScrollSpeed;
                half _DistortStrength;
                half _FrameBlend;
            CBUFFER_END

            // Soft distance fog (SectorFogSystem).
            float4 _DoomFogColor;
            float4 _DoomFogParams; // x=density, y=start, z=end, w=enabled

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
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
                float4 tangentWS : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                float2 scroll = _ScrollSpeed.xy * _Time.y;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex) + scroll;
                output.color = input.color;
                output.normalWS = nrm.normalWS;
                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = float4(nrm.tangentWS, sign);
                return output;
            }

            half3 DoomShade(half3 albedo, half3 sectorAmbient, float3 normalWS,
                            float3 positionWS, half roughness, half emissionStrength)
            {
                half3 lit = albedo * sectorAmbient;
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
                half ndl = saturate(dot(normalWS, mainLight.direction));
                lit += albedo * mainLight.color * (ndl * mainLight.shadowAttenuation *
                       mainLight.distanceAttenuation) * (1.0h - roughness * 0.35h);

                #ifdef _ADDITIONAL_LIGHTS
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; i++)
                {
                    Light l = GetAdditionalLight(i, positionWS);
                    half n = saturate(dot(normalWS, l.direction) * 0.5h + 0.5h);
                    half atten = l.distanceAttenuation * l.shadowAttenuation;
                    lit += albedo * l.color * (atten * lerp(0.35h, 1.0h, n));
                }
                #endif

                lit += albedo * emissionStrength;
                return lit;
            }

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
                float2 uv = input.uv;
                // Mild UV ripple from luminance of a shifted sample.
                half4 shift = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0.07, 0.03));
                uv += (shift.rg - 0.5h) * _DistortStrength;

                half4 texA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half4 texB = SAMPLE_TEXTURE2D(_MainTexB, sampler_MainTexB, uv);
                half4 tex = lerp(texA, texB, saturate(_FrameBlend));
                half3 albedo = tex.rgb;
                half3 sectorAmbient = lerp(input.color.rgb, _SectorAmbient.rgb, _SectorAmbientWeight);

                float3 nTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                float3 T = normalize(input.tangentWS.xyz);
                float3 N = normalize(input.normalWS);
                float3 B = cross(N, T) * input.tangentWS.w;
                float3 normalWS = normalize(T * nTS.x + B * nTS.y + N * nTS.z);

                half3 lit = DoomShade(albedo, sectorAmbient, normalWS,
                                      input.positionWS, _Roughness, _EmissionStrength);
                lit = ApplyDoomFog(lit, input.positionWS);
                return half4(lit, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
