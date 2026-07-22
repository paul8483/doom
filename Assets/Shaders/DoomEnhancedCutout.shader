Shader "Doom/EnhancedCutout"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Bump Scale", Float) = 1
        _Roughness ("Roughness", Range(0,1)) = 0.75
        _EmissionStrength ("Emission", Range(0,2)) = 0
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _SectorAmbient ("Sector Ambient", Color) = (1,1,1,1)
        _SectorAmbientWeight ("Sector Ambient Weight", Range(0,1)) = 0
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

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ DOOM_TEXEL_AA
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Shaders/Includes/DoomControlledSampling.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            float4 _MainTex_TexelSize;
            float4 _BumpMap_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BumpMap_ST;
                half _BumpScale;
                half _Roughness;
                half _EmissionStrength;
                half _Cutoff;
                half4 _SectorAmbient;
                half _SectorAmbientWeight;
            CBUFFER_END

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
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                output.normalWS = nrm.normalWS;
                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = float4(nrm.tangentWS, sign);
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
                    half n = saturate(dot(normalWS, light.direction) * 0.5h + 0.5h);
                    half atten = light.distanceAttenuation * light.shadowAttenuation;
                    dynamic += albedo * light.color * (atten * lerp(0.35h, 1.0h, n));
                }
                #endif

                half3 emission = albedo * emissionStrength;
                return shaded + dynamic + emission;
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
                // Alpha cutoff after texel-AA sample so grate edges stay crisp.
                half4 albedoSample = DoomSampleAlbedo(
                    TEXTURE2D_ARGS(_MainTex, sampler_MainTex), input.uv, _MainTex_TexelSize);
                clip(albedoSample.a - _Cutoff);

                half3 albedo = albedoSample.rgb;
                // RGB unpack: height lives in _BumpMap.a (see NormalMapGenerator);
                // UnpackNormalScale would multiply X by alpha on the DXT5nm path.
                half3 normalTS = UnpackNormalRGB(
                    DoomSampleControlled(
                        TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap),
                        input.uv, _BumpMap_TexelSize), _BumpScale);
                float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                float3x3 tbn = float3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                float3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tbn));

                half3 sectorAmbient = lerp(input.color.rgb, _SectorAmbient.rgb, _SectorAmbientWeight);
                half3 color = DoomShade(
                    albedo, sectorAmbient, normalWS, input.positionWS,
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
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ DOOM_TEXEL_AA

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Shaders/Includes/DoomControlledSampling.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BumpMap_ST;
                half _BumpScale;
                half _Roughness;
                half _EmissionStrength;
                half _Cutoff;
                half4 _SectorAmbient;
                half _SectorAmbientWeight;
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
                half alpha = DoomSampleAlbedo(
                    TEXTURE2D_ARGS(_MainTex, sampler_MainTex),
                    input.uv, _MainTex_TexelSize).a;
                clip(alpha - _Cutoff);
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
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ DOOM_TEXEL_AA

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Assets/Shaders/Includes/DoomControlledSampling.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BumpMap_ST;
                half _BumpScale;
                half _Roughness;
                half _EmissionStrength;
                half _Cutoff;
                half4 _SectorAmbient;
                half _SectorAmbientWeight;
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
                half alpha = DoomSampleAlbedo(
                    TEXTURE2D_ARGS(_MainTex, sampler_MainTex),
                    input.uv, _MainTex_TexelSize).a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
