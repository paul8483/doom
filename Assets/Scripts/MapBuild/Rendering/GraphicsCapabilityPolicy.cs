using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Doom.MapBuild.Rendering
{
    /// Hardware/API capability report. Individual features may be forced off
    /// without rewriting the requested GraphicsMode.
    public readonly struct GraphicsCapabilityReport
    {
        public readonly bool Hdr;
        public readonly bool DepthTexture;
        public readonly bool OpaqueTexture;
        public readonly bool Ssao;
        public readonly bool Decals;
        public readonly bool Msaa;
        public readonly bool RenderScale;
        public readonly bool Fsr;

        public GraphicsCapabilityReport(
            bool hdr = true,
            bool depthTexture = true,
            bool opaqueTexture = true,
            bool ssao = true,
            bool decals = true,
            bool msaa = true,
            bool renderScale = true,
            bool fsr = true)
        {
            Hdr = hdr;
            DepthTexture = depthTexture;
            OpaqueTexture = opaqueTexture;
            Ssao = ssao;
            Decals = decals;
            Msaa = msaa;
            RenderScale = renderScale;
            Fsr = fsr;
        }

        public static GraphicsCapabilityReport Full { get; } = new GraphicsCapabilityReport();
    }

    public static class GraphicsCapabilityPolicy
    {
        /// Probe runtime/device support. Never rewrites the requested GraphicsMode.
        public static GraphicsCapabilityReport Probe()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset
                ?? QualitySettings.renderPipeline as UniversalRenderPipelineAsset
                ?? GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            bool depth = pipeline == null || pipeline.supportsCameraDepthTexture;
            bool opaque = pipeline == null || pipeline.supportsCameraOpaqueTexture;
            bool hdr = pipeline == null || pipeline.supportsHDR;
            bool msaa = SystemInfo.supportsMultisampledTextures > 0;
            // SSAO needs a renderer feature (added by Configure URP); treat depth as proxy.
            // Decals need opaque texture + feature (Task 13); opaque is the runtime gate.
            bool ssao = depth;
            bool decals = opaque;
            // Render scale / FSR are URP asset options — keep enabled unless a later
            // probe proves the device cannot sample (never clear just because Awake
            // ran before the pipeline asset was bound).
            bool fsr = true;
            bool renderScale = true;
            return new GraphicsCapabilityReport(
                hdr, depth, opaque, ssao, decals, msaa, renderScale, fsr);
        }

        /// Returns a profile with unsupported Enhanced flags cleared. Mode is preserved.
        public static GraphicsProfile Apply(GraphicsProfile requested, GraphicsCapabilityReport caps)
        {
            if (requested.Mode != Doom.Game.GraphicsMode.Enhanced)
                return requested;

            bool post = requested.PostProcessing;
            bool hdr = requested.Hdr && caps.Hdr;
            bool ssao = requested.Ssao && caps.Ssao && caps.DepthTexture;
            bool bloom = requested.Bloom && hdr;
            bool grading = requested.ColorGrading && post;
            bool fog = requested.Fog && caps.DepthTexture;
            bool msaa = requested.Msaa && caps.Msaa;
            bool scale = requested.RenderScaleOrFsr && (caps.RenderScale || caps.Fsr);
            bool decals = requested.Decals && caps.Decals;

            return new GraphicsProfile(
                requested.Mode,
                requested.UseLitMaterials,
                requested.ProceduralNormals,
                requested.SectorAmbientBinding,
                requested.DynamicLights,
                requested.Shadows,
                post,
                hdr,
                ssao,
                bloom,
                grading,
                fog,
                msaa,
                scale,
                requested.Sky,
                requested.AnimatedFluids,
                requested.LitSprites,
                requested.SpectreMaterial,
                requested.SoftFloorIntersection,
                requested.Particles,
                decals,
                requested.BilinearWorldFiltering);
        }
    }
}
