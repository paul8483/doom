Shader "Doom/ExperimentalTorch"
{
    // Enhanced 3D presentation for the firesticks. Both halves of a torch are
    // solids of revolution, so both are drawn the same way: the mesh carries
    // only the shape, and the colour is a lookup table indexed by
    //   U = how far this fragment sits from the part's axis AS SEEN (the
    //       projected radius, which is why the flame's core reads hot and the
    //       pole's highlight stays on the pole's middle from any angle),
    //   V = height up the part.
    // The table is baked from the sprite's own texels by
    // Tools/make_torch_model.py — no colour here is invented.
    //
    // A flame's axis bends (the tongue curls), so the offset per height comes
    // from a second 1 x N table instead of a second vertex stream: OBJ carries
    // one UV channel and it already holds (radius of this row, height).
    // Alpha 0 in the colour table means "this frame has no flame here", which
    // is how the silhouette animates on a static mesh.
    Properties
    {
        [MainTexture] _MainTex ("Radial Profile", 2D) = "white" {}
        _SpineTex ("Axis Offset", 2D) = "grey" {}
        _SpineRange ("Spine Range", Float) = 0.5
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
            TEXTURE2D(_SpineTex);
            SAMPLER(sampler_SpineTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _SpineRange;
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
                float v = saturate(input.uv.y);
                float rowRadius = max(input.uv.x, 1e-5);

                // Where this height's axis sits, in object units.
                float encoded = SAMPLE_TEXTURE2D(
                    _SpineTex, sampler_SpineTex, float2(0.5, v)).r;
                float offset = (encoded * 2.0 - 1.0) * _SpineRange;

                // Distance from the eye ray that passes through the axis,
                // measured in the horizontal plane: exactly the coordinate the
                // sprite's own columns are laid out along.
                float3 camOS = TransformWorldToObject(GetCameraPositionWS());
                float2 dir = input.positionOS.xz - camOS.xz;
                dir /= max(length(dir), 1e-5);
                float2 rel = input.positionOS.xz - float2(offset, 0.0);
                float perp = abs(rel.x * dir.y - rel.y * dir.x);
                float t = min(perp / rowRadius, 0.999);

                half4 table = SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex, float2(t, v));
                clip(table.a - 0.5);

                half3 color = table.rgb * _Exposure;
                color = ApplyDoomFog(color, input.positionWS);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
