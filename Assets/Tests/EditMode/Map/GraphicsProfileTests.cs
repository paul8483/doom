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
