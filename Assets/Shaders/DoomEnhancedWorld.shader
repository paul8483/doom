Shader "Doom/EnhancedWorld"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Bump Scale", Float) = 1
        _ParallaxAmplitude ("Parallax Amplitude", Float) = 0
        _Roughness ("Roughness", Range(0,1)) = 0.75
        _EmissionStrength ("Emission", Range(0,2)) = 0
        _SectorAmbient ("Sector Ambient", Color) = (1,1,1,1)
        _SectorAmbientWeight ("Sector Ambient Weight", Range(0,1)) = 0
        // Packed: r=enable, g=grid/8, b=amp, a=speed/8 — Color MPB is reliable in URP.
        _LampFlickerParams ("Lamp Flicker Params", Color) = (0,0.75,0.30,0.35)
        _LampFlickerLuma ("Lamp Flicker Luma", Float) = 0.32
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
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ DOOM_TEXEL_AA
            #pragma multi_compile_local _ DOOM_PARALLAX
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
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
                half _ParallaxAmplitude;
                half _Roughness;
                half _EmissionStrength;
                half4 _SectorAmbient;
                half _SectorAmbientWeight;
                half4 _LampFlickerParams;
                half _LampFlickerLuma;
            CBUFFER_END

            float LampFlickerHash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            void ApplyLampFlicker(
                float2 uv, inout half3 albedo,
                inout half3 sectorAmbient, inout half emissionStrength)
            {
                // r=enable; g=grid/8; b=amp; a=speed/8
                half enable = _LampFlickerParams.r;
                if (enable < 0.5h) return;

                float grid = max((float)_LampFlickerParams.g * 8.0, 1.0);
                half amp = _LampFlickerParams.b;
                float speed = max((float)_LampFlickerParams.a * 8.0, 0.1);

                float2 tileUv = frac(uv);
                float2 cell = floor(tileUv * grid);
                float2 tileId = floor(uv);
                float phase = LampFlickerHash(cell + tileId * 17.0) * 6.2831853;

                // URP time (survives better than raw _Time in some batch modes).
                float t = _TimeParameters.x * speed;
                float pulse = 0.5 + 0.5 * sin(t + phase);
                float pulse2 = 0.5 + 0.5 * sin(t * 1.73 + phase * 1.31);
                pulse = lerp(pulse, pulse2, 0.35);

                half luma = dot(albedo, half3(0.30h, 0.59h, 0.11h));
                half thr = _LampFlickerLuma;
                half gate = smoothstep(thr - 0.06h, thr + 0.10h, luma);

                half dim = 1.0h - amp;          // amp 0.30 → ~30% darker when "off"
                half bright = 1.0h;             // peak stays at baseline (rhythm unchanged)
                half flicker = lerp(dim, bright, (half)pulse);
                albedo = lerp(albedo, albedo * flicker, gate);
                sectorAmbient *= lerp(1.0h, flicker, gate);
                // Dim phase must nearly lose emission or the "off" read stays washed out.
                emissionStrength += gate * lerp(0.0h, 1.15h, (half)pulse);
            }

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

            float4 _DoomFogColor;
            float4 _DoomFogParams; // x=density y=start z=end w=enabled

            half3 DoomShade(half3 albedo, half3 sectorAmbient, float3 normalWS,
                            float3 positionWS, half roughness, half emissionStrength)
            {
                // Sector light is authoritative ambient — never crush it to black.
                half3 ambient = albedo * sectorAmbient;

                // Soft fixed key for normal relief without a scene Directional Light.
                half3 fakeL = normalize(half3(0.35h, 0.85h, 0.25h));
                half ndotl = saturate(dot(normalWS, fakeL));
                half relief = lerp(0.70h, 1.0h, ndotl);
                half reliefAmt = saturate(1.0h - roughness);
                half3 shaded = ambient * lerp(1.0h, relief, reliefAmt);

                // Optional Unity lights (Task 9 pool) add on top of sector ambient.
                // Half-Lambert + fill so floor under a lamp reads even when the
                // geometric N·L is grazing (alcoves / dark sectors).
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
                float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                float3x3 tbn = float3x3(input.tangentWS.xyz, bitangent, input.normalWS);

                float2 uv = input.uv;
                #if defined(DOOM_PARALLAX)
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 viewDirTS = mul(tbn, viewDirWS);
                uv = DoomParallaxOcclusionUV(
                    TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap),
                    uv, viewDirTS, _ParallaxAmplitude);
                #endif

                // Albedo: texel-AA when DOOM_TEXEL_AA; normals stay controlled.
                half4 albedoSample = DoomSampleAlbedo(
                    TEXTURE2D_ARGS(_MainTex, sampler_MainTex), uv, _MainTex_TexelSize);
                half3 albedo = albedoSample.rgb;

                // RGB unpack: our runtime normal maps store height in alpha, and
                // UnpackNormalScale's RGorAG path multiplies X by alpha (DXT5nm).
                half3 normalTS = UnpackNormalRGB(
                    DoomSampleControlled(
                        TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap),
                        uv, _BumpMap_TexelSize), _BumpScale);
                float3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tbn));

                half3 sectorAmbient = lerp(input.color.rgb, _SectorAmbient.rgb, _SectorAmbientWeight);
                half emissionStrength = _EmissionStrength;
                ApplyLampFlicker(uv, albedo, sectorAmbient, emissionStrength);
                half3 color = DoomShade(
                    albedo, sectorAmbient, normalWS, input.positionWS,
                    _Roughness, emissionStrength);
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
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
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
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
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
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
