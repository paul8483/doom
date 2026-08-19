Shader "Doom/ExperimentalFireball"
{
    // Enhanced 3D presentation for the imp's fireball. The mesh is a voxel
    // ball; the sprite's radial gradient (white core -> yellow body -> dark
    // rim) cannot be baked onto a solid sphere's surface, because the core is
    // inside it. So the albedo is a lookup table instead of a texture map:
    //   U = how far this fragment sits from the centre of the PROJECTED disc,
    //   V = the voxel's own colour variant (baked into the mesh UVs).
    // The result reproduces BAL1's own texels from every angle while the ball
    // stays a real object that occludes, spins and takes sector fog.
    Properties
    {
        [MainTexture] _MainTex ("Radial Profile", 2D) = "white" {}
        _Exposure ("Exposure", Range(0.25, 8)) = 1
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
                float3 positionOS : TEXCOORD2;
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
                output.positionOS = input.positionOS.xyz;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // The ball is centred on the object origin with radius 0.5, so
                // the distance from the eye ray through the centre — measured
                // in object space, where the ball is always a unit sphere —
                // is exactly the sprite's radial coordinate.
                float3 camOS = TransformWorldToObject(GetCameraPositionWS());
                float3 viewOS = input.positionOS - camOS;
                float len = max(length(viewOS), 1e-5);
                viewOS /= len;
                float3 perp = input.positionOS
                            - dot(input.positionOS, viewOS) * viewOS;
                float t = min(saturate(length(perp) * 2.0), 0.999);

                half3 albedo = SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex, float2(t, input.uv.y)).rgb;
                half3 color = albedo * _Exposure;
                color = ApplyDoomFog(color, input.positionWS);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
