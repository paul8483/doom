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
        /// Palette-aware dedither/deband before upscale (Enhanced texture quality).
        public readonly bool WorldDedither;
        /// Runtime Super-xBR 4× world albedo/sky variants.
        public readonly bool WorldUpscale4X;
        /// Fat-pixel texel-AA sampling in Enhanced world shaders.
        public readonly bool WorldTexelAA;
        /// Multi-scale normals + parallax occlusion on solid world surfaces.
        public readonly bool WorldParallax;
        /// Super-xBR 4× for projectile/effect sprites; pickups/enemies/weapons use EdgeMix 8×.
        public readonly bool SpritesUpscale4X;
        /// Super-xBR 4× for HUD patches (menus stay native; weapon view uses EdgeMix 8×).
        public readonly bool UiUpscale4X;
        /// Keeps LOD0 point-sharp while enabling controlled mip/aniso minification.
        public readonly bool ControlledWorldMipmaps;

        public WorldTextureVariant WorldTextureVariant =>
            WorldUpscale4X ? WorldTextureVariant.Enhanced4X : WorldTextureVariant.Native;

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
            bool bilinearWorldFiltering,
            bool worldDedither = false,
            bool worldUpscale4X = false,
            bool worldTexelAA = false,
            bool worldParallax = false,
            bool spritesUpscale4X = false,
            bool uiUpscale4X = false,
            bool controlledWorldMipmaps = false)
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
            WorldDedither = worldDedither;
            WorldUpscale4X = worldUpscale4X;
            WorldTexelAA = worldTexelAA;
            WorldParallax = worldParallax;
            SpritesUpscale4X = spritesUpscale4X;
            UiUpscale4X = uiUpscale4X;
            ControlledWorldMipmaps = controlledWorldMipmaps;
        }

        public static GraphicsProfile ForMode(GraphicsMode mode) =>
            mode == GraphicsMode.Enhanced ? Enhanced : Classic;

        /// Enhanced base with selective texture-quality layers for editor/test
        /// layered captures. Not exposed in Options UI.
        public static GraphicsProfile EnhancedWithLayers(
            bool worldDedither = true,
            bool worldUpscale4X = true,
            bool worldTexelAA = true,
            bool worldParallax = true,
            bool spritesUpscale4X = true,
            bool uiUpscale4X = true)
        {
            var e = Enhanced;
            return new GraphicsProfile(
                e.Mode,
                e.UseLitMaterials,
                e.ProceduralNormals,
                e.SectorAmbientBinding,
                e.DynamicLights,
                e.Shadows,
                e.PostProcessing,
                e.Hdr,
                e.Ssao,
                e.Bloom,
                e.ColorGrading,
                e.Fog,
                e.Msaa,
                e.RenderScaleOrFsr,
                e.Sky,
                e.AnimatedFluids,
                e.LitSprites,
                e.SpectreMaterial,
                e.SoftFloorIntersection,
                e.Particles,
                e.Decals,
                e.BilinearWorldFiltering,
                worldDedither,
                worldUpscale4X,
                worldTexelAA,
                worldParallax,
                spritesUpscale4X,
                uiUpscale4X,
                e.ControlledWorldMipmaps);
        }

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
            bilinearWorldFiltering: false,
            worldDedither: false,
            worldUpscale4X: false,
            worldTexelAA: false,
            worldParallax: false,
            spritesUpscale4X: false,
            uiUpscale4X: false,
            controlledWorldMipmaps: false);

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
            // Enhanced4X albedo is Trilinear via ControlledWorldMipmaps + aniso.
            // Texel-AA (DOOM_TEXEL_AA) sharpens close-ups in the shader; keep
            // BilinearWorldFiltering false so shared native Point textures are
            // not mutated during Enhanced (Classic hot-switch exact restore).
            bilinearWorldFiltering: false,
            worldDedither: true,
            worldUpscale4X: true,
            worldTexelAA: true,
            worldParallax: true,
            spritesUpscale4X: true,
            uiUpscale4X: true,
            controlledWorldMipmaps: true);
    }
}
