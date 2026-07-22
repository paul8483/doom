using NUnit.Framework;
using Doom.Game;
using Doom.MapBuild.Rendering;

namespace Doom.Map.Tests
{
    public class GraphicsProfileTests
    {
        [Test]
        public void Classic_disables_all_presentation_effects()
        {
            var p = GraphicsProfile.Classic;
            Assert.AreEqual(GraphicsMode.Classic, p.Mode);
            Assert.IsFalse(p.UseLitMaterials);
            Assert.IsFalse(p.ProceduralNormals);
            Assert.IsFalse(p.DynamicLights);
            Assert.IsFalse(p.Shadows);
            Assert.IsFalse(p.PostProcessing);
            Assert.IsFalse(p.Hdr);
            Assert.IsFalse(p.Ssao);
            Assert.IsFalse(p.Bloom);
            Assert.IsFalse(p.Fog);
            Assert.IsFalse(p.Particles);
            Assert.IsFalse(p.Decals);
            Assert.IsFalse(p.BilinearWorldFiltering);
            Assert.IsFalse(p.WorldDedither);
            Assert.IsFalse(p.WorldUpscale4X);
            Assert.IsFalse(p.WorldTexelAA);
            Assert.IsFalse(p.WorldParallax);
            Assert.IsFalse(p.SpritesUpscale4X);
            Assert.IsFalse(p.UiUpscale4X);
            Assert.IsFalse(p.ControlledWorldMipmaps);
            Assert.AreEqual(WorldTextureVariant.Native, p.WorldTextureVariant);
            Assert.IsTrue(p.Sky);
        }

        [Test]
        public void Enhanced_requests_full_feature_set()
        {
            var p = GraphicsProfile.Enhanced;
            Assert.AreEqual(GraphicsMode.Enhanced, p.Mode);
            Assert.IsTrue(p.UseLitMaterials);
            Assert.IsTrue(p.ProceduralNormals);
            Assert.IsTrue(p.DynamicLights);
            Assert.IsTrue(p.Shadows);
            Assert.IsTrue(p.PostProcessing);
            Assert.IsTrue(p.Hdr);
            Assert.IsTrue(p.Ssao);
            Assert.IsTrue(p.Bloom);
            Assert.IsTrue(p.Fog);
            Assert.IsTrue(p.Particles);
            Assert.IsTrue(p.Decals);
            Assert.IsFalse(p.BilinearWorldFiltering);
            Assert.IsTrue(p.WorldDedither);
            Assert.IsTrue(p.WorldUpscale4X);
            Assert.IsTrue(p.WorldTexelAA);
            Assert.IsTrue(p.WorldParallax);
            Assert.IsTrue(p.SpritesUpscale4X);
            Assert.IsTrue(p.UiUpscale4X);
            Assert.IsTrue(p.ControlledWorldMipmaps);
            Assert.AreEqual(WorldTextureVariant.Enhanced4X, p.WorldTextureVariant);
        }

        [Test]
        public void EnhancedWithLayers_builds_intermediate_profiles_for_captures()
        {
            var deditherOnly = GraphicsProfile.EnhancedWithLayers(
                worldDedither: true,
                worldUpscale4X: false,
                worldTexelAA: false,
                worldParallax: false,
                spritesUpscale4X: false,
                uiUpscale4X: false);

            Assert.AreEqual(GraphicsMode.Enhanced, deditherOnly.Mode);
            Assert.IsTrue(deditherOnly.WorldDedither);
            Assert.IsFalse(deditherOnly.WorldUpscale4X);
            Assert.IsFalse(deditherOnly.WorldTexelAA);
            Assert.IsFalse(deditherOnly.WorldParallax);
            Assert.IsFalse(deditherOnly.SpritesUpscale4X);
            Assert.IsFalse(deditherOnly.UiUpscale4X);
            Assert.AreEqual(WorldTextureVariant.Native, deditherOnly.WorldTextureVariant);
            Assert.IsTrue(deditherOnly.UseLitMaterials);
            Assert.IsTrue(deditherOnly.ControlledWorldMipmaps);

            var full = GraphicsProfile.EnhancedWithLayers();
            Assert.AreEqual(WorldTextureVariant.Enhanced4X, full.WorldTextureVariant);
            Assert.IsTrue(full.WorldDedither);
            Assert.IsTrue(full.WorldUpscale4X);
            Assert.IsTrue(full.WorldTexelAA);
            Assert.IsTrue(full.WorldParallax);
            Assert.IsTrue(full.SpritesUpscale4X);
            Assert.IsTrue(full.UiUpscale4X);
        }

        [Test]
        public void Capability_policy_clears_unsupported_flags_without_changing_mode()
        {
            var caps = new GraphicsCapabilityReport(
                hdr: false, depthTexture: false, ssao: false, decals: false,
                msaa: false, renderScale: false, fsr: false);
            var effective = GraphicsCapabilityPolicy.Apply(GraphicsProfile.Enhanced, caps);

            Assert.AreEqual(GraphicsMode.Enhanced, effective.Mode);
            Assert.IsFalse(effective.Hdr);
            Assert.IsFalse(effective.Ssao);
            Assert.IsFalse(effective.Bloom);
            Assert.IsTrue(effective.Fog);
            Assert.IsFalse(effective.Decals);
            Assert.IsFalse(effective.Msaa);
            Assert.IsFalse(effective.RenderScaleOrFsr);
            Assert.IsTrue(effective.UseLitMaterials);
            Assert.IsTrue(effective.PostProcessing);
            Assert.IsTrue(effective.WorldDedither);
            Assert.IsTrue(effective.WorldUpscale4X);
            Assert.IsTrue(effective.WorldTexelAA);
            Assert.IsTrue(effective.WorldParallax);
            Assert.IsTrue(effective.SpritesUpscale4X);
            Assert.IsTrue(effective.UiUpscale4X);
            Assert.IsTrue(effective.ControlledWorldMipmaps);
        }

        [Test]
        public void Capability_policy_leaves_classic_unchanged()
        {
            var caps = new GraphicsCapabilityReport(hdr: false);
            var effective = GraphicsCapabilityPolicy.Apply(GraphicsProfile.Classic, caps);
            Assert.AreEqual(GraphicsMode.Classic, effective.Mode);
            Assert.IsFalse(effective.Hdr);
        }
    }
}
