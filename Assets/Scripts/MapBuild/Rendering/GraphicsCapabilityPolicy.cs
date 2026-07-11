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
