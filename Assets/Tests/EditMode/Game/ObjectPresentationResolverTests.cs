using NUnit.Framework;

namespace Doom.Game.Tests
{
    public class ObjectPresentationResolverTests
    {
        [Test]
        public void Classic_always_native_regardless_of_assets_and_toggle()
        {
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Classic, true, true, true, false),
                Is.EqualTo(ObjectPresentation.NativeBillboard));
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Classic, false, true, true, true),
                Is.EqualTo(ObjectPresentation.NativeBillboard));
        }

        [Test]
        public void Enhanced_3d_on_prefers_mesh_then_redraw_then_edgemix()
        {
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Enhanced, true, true, true, false),
                Is.EqualTo(ObjectPresentation.Mesh));
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Enhanced, true, false, true, false),
                Is.EqualTo(ObjectPresentation.RedrawBillboard));
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Enhanced, true, false, false, false),
                Is.EqualTo(ObjectPresentation.EdgeMixBillboard));
        }

        [Test]
        public void Enhanced_3d_off_prefers_redraw_then_edgemix_never_mesh()
        {
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Enhanced, false, true, true, false),
                Is.EqualTo(ObjectPresentation.RedrawBillboard));
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Enhanced, false, true, false, false),
                Is.EqualTo(ObjectPresentation.EdgeMixBillboard));
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Enhanced, false, false, false, false),
                Is.EqualTo(ObjectPresentation.EdgeMixBillboard));
        }

        [Test]
        public void Animated_lump_in_2d_falls_to_edgemix_even_with_redraw()
        {
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Enhanced, false, false, true, true),
                Is.EqualTo(ObjectPresentation.EdgeMixBillboard));
            // 3D On still prefers mesh over animated redraw skip.
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Enhanced, true, true, true, true),
                Is.EqualTo(ObjectPresentation.Mesh));
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Enhanced, true, false, true, true),
                Is.EqualTo(ObjectPresentation.EdgeMixBillboard));
        }

        [Test]
        public void Lump_without_assets_is_edgemix_in_enhanced()
        {
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Enhanced, true, false, false, false),
                Is.EqualTo(ObjectPresentation.EdgeMixBillboard));
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Enhanced, false, false, false, false),
                Is.EqualTo(ObjectPresentation.EdgeMixBillboard));
        }
    }
}
