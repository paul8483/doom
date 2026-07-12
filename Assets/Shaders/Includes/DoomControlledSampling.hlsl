#ifndef DOOM_CONTROLLED_SAMPLING_INCLUDED
#define DOOM_CONTROLLED_SAMPLING_INCLUDED

// LOD0 is sampled at an exact texel center. Once the projected footprint
// exceeds one texel, the texture's trilinear/anisotropic sampler takes over.
half4 DoomSampleControlled(
    TEXTURE2D_PARAM(textureName, samplerName),
    float2 uv,
    float4 texelSize)
{
    float2 texelPosition = uv * texelSize.zw;
    float2 dx = ddx(texelPosition);
    float2 dy = ddy(texelPosition);
    float footprint = max(length(dx), length(dy));
    float distantWeight = smoothstep(0.9, 1.2, footprint);

    float2 nearestUv = (floor(texelPosition) + 0.5) * texelSize.xy;
    half4 closeSample = SAMPLE_TEXTURE2D_LOD(
        textureName, samplerName, nearestUv, 0.0);
    half4 distantSample = SAMPLE_TEXTURE2D(
        textureName, samplerName, uv);
    return lerp(closeSample, distantSample, distantWeight);
}

#endif
