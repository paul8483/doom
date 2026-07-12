using Doom.Game;

namespace Doom.MapBuild.Rendering
{
    /// Declared presentation flags for a graphics mode. Gameplay-neutral.
    public readonly struct GraphicsProfile
    {
        public readonly GraphicsMode Mode;
        public readonly bool UseLitMaterials;
        public readonly bool ProceduralNormals;
        public readonly bool SectorAmbientBinding;
        public readonly bool DynamicLights;
        public readonly bool Shadows;
        public readonly bool PostProcessing;
        public readonly bool Hdr;
        public readonly bool Ssao;
        public readonly bool Bloom;
        public readonly bool ColorGrading;
        public readonly bool Fog;
        public readonly bool Msaa;
        public readonly bool RenderScaleOrFsr;
        public readonly bool Sky;
        public readonly bool AnimatedFluids;
        public readonly bool LitSprites;
        public readonly bool SpectreMaterial;
        public readonly bool SoftFloorIntersection;
        public readonly bool Particles;
        public readonly bool Decals;
        public readonly bool BilinearWorldFiltering;

        public GraphicsProfile(
            GraphicsMode mode,
            bool useLitMaterials,
            bool proceduralNormals,
            bool sectorAmbientBinding,
            bool dynamicLights,
            bool shadows,
            bool postProcessing,
            bool hdr,
            bool ssao,
            bool bloom,
            bool colorGrading,
            bool fog,
            bool msaa,
            bool renderScaleOrFsr,
            bool sky,
            bool animatedFluids,
            bool litSprites,
            bool spectreMaterial,
            bool softFloorIntersection,
            bool particles,
            bool decals,
            bool bilinearWorldFiltering)
        {
            Mode = mode;
            UseLitMaterials = useLitMaterials;
            ProceduralNormals = proceduralNormals;
            SectorAmbientBinding = sectorAmbientBinding;
            DynamicLights = dynamicLights;
            Shadows = shadows;
            PostProcessing = postProcessing;
            Hdr = hdr;
            Ssao = ssao;
            Bloom = bloom;
            ColorGrading = colorGrading;
            Fog = fog;
            Msaa = msaa;
            RenderScaleOrFsr = renderScaleOrFsr;
            Sky = sky;
            AnimatedFluids = animatedFluids;
            LitSprites = litSprites;
            SpectreMaterial = spectreMaterial;
            SoftFloorIntersection = softFloorIntersection;
            Particles = particles;
            Decals = decals;
            BilinearWorldFiltering = bilinearWorldFiltering;
        }

        public static GraphicsProfile ForMode(GraphicsMode mode) =>
            mode == GraphicsMode.Enhanced ? Enhanced : Classic;

        public static GraphicsProfile Classic { get; } = new GraphicsProfile(
            GraphicsMode.Classic,
            useLitMaterials: false,
            proceduralNormals: false,
            sectorAmbientBinding: false,
            dynamicLights: false,
            shadows: false,
            postProcessing: false,
            hdr: false,
            ssao: false,
            bloom: false,
            colorGrading: false,
            fog: false,
            msaa: false,
            renderScaleOrFsr: false,
            sky: true,
            animatedFluids: false,
            litSprites: false,
            spectreMaterial: false,
            softFloorIntersection: false,
            particles: false,
            decals: false,
            bilinearWorldFiltering: false);

        public static GraphicsProfile Enhanced { get; } = new GraphicsProfile(
            GraphicsMode.Enhanced,
            useLitMaterials: true,
            proceduralNormals: true,
            sectorAmbientBinding: true,
            dynamicLights: true,
            shadows: true,
            postProcessing: true,
            hdr: true,
            ssao: true,
            bloom: true,
            colorGrading: true,
            fog: true,
            msaa: true,
            renderScaleOrFsr: true,
            sky: true,
            animatedFluids: true,
            litSprites: true,
            spectreMaterial: true,
            softFloorIntersection: true,
            particles: true,
            decals: true,
            // Point keeps WAD albedo crisp. Bilinear on 64×N patches reads as
            // soft mush, not "higher detail", once lighting/post are on.
            bilinearWorldFiltering: false);
    }
}
