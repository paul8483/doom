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

// Fat-pixel texel-AA: UV snaps toward texel centers with an fwidth-wide
// smoothstep edge, then bilinear samples. Close range stays crisp with
// antialiased texel borders; distance falls back to ordinary filtered
// sampling so mip/aniso derivatives stay valid (floor discontinuities
// would otherwise break automatic LOD).
half4 DoomSampleTexelAA(
    TEXTURE2D_PARAM(textureName, samplerName),
    float2 uv,
    float4 texelSize)
{
    float2 texelPosition = uv * texelSize.zw;
    float2 dx = ddx(texelPosition);
    float2 dy = ddy(texelPosition);
    float footprint = max(length(dx), length(dy));
    float distantWeight = smoothstep(0.9, 1.2, footprint);

    float2 boxSize = clamp(fwidth(texelPosition), 1e-5, 1.0);
    float2 tx = texelPosition - 0.5 * boxSize;
    float2 txOffset = smoothstep(float2(0.0, 0.0), boxSize, frac(tx));
    float2 aaUv = (floor(tx) + 0.5 + txOffset) * texelSize.xy;

    half4 closeSample = SAMPLE_TEXTURE2D_LOD(
        textureName, samplerName, aaUv, 0.0);
    half4 distantSample = SAMPLE_TEXTURE2D(
        textureName, samplerName, uv);
    return lerp(closeSample, distantSample, distantWeight);
}

// Albedo path for Enhanced world shaders. Keyword is toggled from
// DoomMaterialFactory from GraphicsProfile.WorldTexelAA.
half4 DoomSampleAlbedo(
    TEXTURE2D_PARAM(textureName, samplerName),
    float2 uv,
    float4 texelSize)
{
#if defined(DOOM_TEXEL_AA)
    return DoomSampleTexelAA(
        TEXTURE2D_ARGS(textureName, samplerName), uv, texelSize);
#else
    return DoomSampleControlled(
        TEXTURE2D_ARGS(textureName, samplerName), uv, texelSize);
#endif
}

#endif
