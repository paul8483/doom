using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Doom.MapBuild.Rendering
{
    /// Applies Enhanced post, MSAA, and render-scale/FSR policy to the URP pipeline
    /// and world Volume. Classic restores baseline camera/pipeline state.
    public sealed class EnhancedPostController
    {
        public const float EnhancedRenderScale = 0.85f;
        public const int EnhancedMsaaSamples = 4;
        public const float BloomThreshold = 1.05f;
        public const float BloomIntensity = 0.28f;
        public const float ColorContrast = 0.05f;
        public const float ColorSaturation = 0.08f;

        UniversalRenderPipelineAsset pipeline;
        float classicRenderScale = 1f;
        int classicMsaa = 1;
        UpscalingFilterSelection classicUpscale = UpscalingFilterSelection.Auto;
        bool capturedBaseline;
        VolumeProfile profile;
        bool volumeReady;

        public bool VolumeReady => volumeReady;
        // ActiveRenderScale / ActiveMsaa defined below with ResolvePipeline fallback.

        public void Bind(VolumeProfile enhancedProfile)
        {
            profile = enhancedProfile;
            EnsureVolumeOverrides();
            CapturePipelineBaseline();
        }

        void CapturePipelineBaseline()
        {
            pipeline = ResolvePipeline();
            if (pipeline == null) return;
            if (capturedBaseline) return;
            classicRenderScale = pipeline.renderScale;
            classicMsaa = pipeline.msaaSampleCount;
            classicUpscale = pipeline.upscalingFilter;
            capturedBaseline = true;
        }

        static UniversalRenderPipelineAsset ResolvePipeline() =>
            GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset
            ?? QualitySettings.renderPipeline as UniversalRenderPipelineAsset
            ?? GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;

        public float ActiveRenderScale
        {
            get
            {
                var p = pipeline ?? ResolvePipeline();
                return p != null ? p.renderScale : 1f;
            }
        }

        public int ActiveMsaa
        {
            get
            {
                var p = pipeline ?? ResolvePipeline();
                return p != null ? p.msaaSampleCount : 1;
            }
        }

        public void EnsureVolumeOverrides()
        {
            if (profile == null) return;

            if (!profile.TryGet(out Bloom bloom))
                bloom = profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.threshold.Override(BloomThreshold);
            bloom.intensity.Override(BloomIntensity);
            bloom.scatter.Override(0.7f);
            bloom.clamp.Override(20f);

            if (!profile.TryGet(out ColorAdjustments grading))
                grading = profile.Add<ColorAdjustments>(true);
            grading.active = true;
            grading.contrast.Override(ColorContrast * 100f);
            grading.saturation.Override(ColorSaturation * 100f);
            grading.postExposure.Override(0f);

            if (!profile.TryGet(out Tonemapping tone))
                tone = profile.Add<Tonemapping>(true);
            tone.active = true;
            tone.mode.Override(TonemappingMode.Neutral);

            volumeReady = true;
        }

        public void Apply(GraphicsProfile profileFlags, GraphicsCapabilityReport caps)
        {
            CapturePipelineBaseline();
            EnsureVolumeOverrides();

            bool enhanced = profileFlags.Mode == Doom.Game.GraphicsMode.Enhanced;
            bool post = enhanced && profileFlags.PostProcessing;
            bool hdr = enhanced && profileFlags.Hdr && caps.Hdr;
            bool bloom = post && profileFlags.Bloom && hdr;
            bool grading = post && profileFlags.ColorGrading;
            bool msaa = enhanced && profileFlags.Msaa && caps.Msaa;
            bool scale = enhanced && profileFlags.RenderScaleOrFsr &&
                         (caps.RenderScale || caps.Fsr);

            if (this.profile != null)
            {
                if (this.profile.TryGet(out Bloom b)) b.active = bloom;
                if (this.profile.TryGet(out ColorAdjustments g)) g.active = grading;
                if (this.profile.TryGet(out Tonemapping t)) t.active = post && hdr;
            }

            if (pipeline == null) return;

            if (!enhanced)
            {
                // Classic always restores native resolution / no MSAA — do not keep
                // a previously applied Enhanced scale as the "baseline".
                pipeline.renderScale = 1f;
                pipeline.msaaSampleCount = 1;
                pipeline.upscalingFilter = UpscalingFilterSelection.Auto;
                RenderSettings.fog = false;
                return;
            }

            pipeline.msaaSampleCount = msaa ? EnhancedMsaaSamples : 1;

            if (scale)
            {
                pipeline.renderScale = EnhancedRenderScale;
                if (caps.Fsr)
                    pipeline.upscalingFilter = UpscalingFilterSelection.FSR;
                else if (caps.RenderScale)
                    pipeline.upscalingFilter = UpscalingFilterSelection.Auto;
            }
            else
            {
                pipeline.renderScale = 1f;
                pipeline.upscalingFilter = classicUpscale;
            }

            // Soft distance fog until Task 11 sector fog; Classic clears it.
            if (profileFlags.Fog && caps.DepthTexture)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = 0.012f;
                RenderSettings.fogColor = new Color(0.08f, 0.08f, 0.1f);
            }
            else
            {
                RenderSettings.fog = false;
            }
        }
    }
}
