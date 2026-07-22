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
    // Offset must ramp only in the LAST boxSize fraction of each texel
    // (reference fat-pixel filtering). Ramping from 0 instead keeps the
    // offset at 1 across the whole texel interior, which samples the next
    // texel's center — a constant one-texel shift of every world texture.
    float2 txOffset = smoothstep(1.0 - boxSize, float2(1.0, 1.0), frac(tx));
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

// Conservative POM from height in _BumpMap.a (white = raised). Fixed step
// count; amplitude is small (relief, not stereogram). Used only when
// DOOM_PARALLAX is enabled on solid opaque Enhanced materials.
float2 DoomParallaxOcclusionUV(
    TEXTURE2D_PARAM(bumpMap, bumpSampler),
    float2 uv,
    float3 viewDirTS,
    float amplitude)
{
    if (amplitude < 1e-5)
        return uv;

    viewDirTS = normalize(viewDirTS);
    // Soften grazing angles so offsets stay bounded.
    float z = max(abs(viewDirTS.z), 0.35);
    float2 maxOffset = -viewDirTS.xy / z * amplitude;

    const int steps = 8;
    float2 deltaUv = maxOffset / steps;
    float layerDepth = 1.0 / steps;

    float2 currentUv = uv;
    float currentLayer = 0.0;
    // Depth = 1 - height (white height = surface top).
    float currentDepth = 1.0 - SAMPLE_TEXTURE2D_LOD(
        bumpMap, bumpSampler, currentUv, 0.0).a;

    [unroll]
    for (int i = 0; i < steps; i++)
    {
        if (currentLayer >= currentDepth)
            break;
        currentUv += deltaUv;
        currentDepth = 1.0 - SAMPLE_TEXTURE2D_LOD(
            bumpMap, bumpSampler, currentUv, 0.0).a;
        currentLayer += layerDepth;
    }

    return currentUv;
}

#endif
