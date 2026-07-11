using NUnit.Framework;
using Doom.Game;
using Doom.MapBuild.Rendering;

namespace Doom.Map.Tests
{
    public class GraphicsCapabilityPolicyTests
    {
        [Test]
        public void Unsupported_hdr_clears_bloom_keeps_enhanced_mode()
        {
            var caps = new GraphicsCapabilityReport(hdr: false);
            var effective = GraphicsCapabilityPolicy.Apply(GraphicsProfile.Enhanced, caps);
            Assert.AreEqual(GraphicsMode.Enhanced, effective.Mode);
            Assert.IsFalse(effective.Hdr);
            Assert.IsFalse(effective.Bloom);
            Assert.IsTrue(effective.PostProcessing);
            Assert.IsTrue(effective.UseLitMaterials);
        }

        [Test]
        public void Unsupported_depth_clears_ssao_and_fog()
        {
            var caps = new GraphicsCapabilityReport(depthTexture: false, ssao: false);
            var effective = GraphicsCapabilityPolicy.Apply(GraphicsProfile.Enhanced, caps);
            Assert.AreEqual(GraphicsMode.Enhanced, effective.Mode);
            Assert.IsFalse(effective.Ssao);
            Assert.IsFalse(effective.Fog);
        }

        [Test]
        public void Unsupported_msaa_and_scale_clear_only_those_flags()
        {
            var caps = new GraphicsCapabilityReport(
                msaa: false, renderScale: false, fsr: false);
            var effective = GraphicsCapabilityPolicy.Apply(GraphicsProfile.Enhanced, caps);
            Assert.AreEqual(GraphicsMode.Enhanced, effective.Mode);
            Assert.IsFalse(effective.Msaa);
            Assert.IsFalse(effective.RenderScaleOrFsr);
            Assert.IsTrue(effective.Hdr);
            Assert.IsTrue(effective.DynamicLights);
        }

        [Test]
        public void Unsupported_decals_clears_decals_only()
        {
            var caps = new GraphicsCapabilityReport(decals: false);
            var effective = GraphicsCapabilityPolicy.Apply(GraphicsProfile.Enhanced, caps);
            Assert.IsFalse(effective.Decals);
            Assert.IsTrue(effective.Particles);
            Assert.AreEqual(GraphicsMode.Enhanced, effective.Mode);
        }

        [Test]
        public void Classic_profile_ignores_capability_report()
        {
            var caps = new GraphicsCapabilityReport(hdr: false, msaa: false);
            var effective = GraphicsCapabilityPolicy.Apply(GraphicsProfile.Classic, caps);
            Assert.AreEqual(GraphicsMode.Classic, effective.Mode);
            Assert.IsFalse(effective.PostProcessing);
            Assert.IsFalse(effective.Msaa);
        }
    }
}
