using NUnit.Framework;

namespace Doom.Game.Tests
{
    /// The Enhanced 2D mode (3D Objects toggle) was removed 2026-08-28:
    /// the cascade is Classic -> native, Enhanced -> mesh -> redraw -> native.
    public class ObjectPresentationResolverTests
    {
        [Test]
        public void Classic_always_native_regardless_of_assets()
        {
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Classic, hasMesh: true,
                hasDisplayRedraw: true, isAnimated: false),
                Is.EqualTo(ObjectPresentation.NativeBillboard));
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Classic, hasMesh: false,
                hasDisplayRedraw: true, isAnimated: true),
                Is.EqualTo(ObjectPresentation.NativeBillboard));
        }

        [Test]
        public void Enhanced_prefers_mesh_then_redraw_then_native()
        {
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Enhanced, hasMesh: true,
                hasDisplayRedraw: true, isAnimated: false),
                Is.EqualTo(ObjectPresentation.Mesh));
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Enhanced, hasMesh: false,
                hasDisplayRedraw: true, isAnimated: false),
                Is.EqualTo(ObjectPresentation.RedrawBillboard));
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Enhanced, hasMesh: false,
                hasDisplayRedraw: false, isAnimated: false),
                Is.EqualTo(ObjectPresentation.NativeBillboard));
        }

        [Test]
        public void Animated_lump_without_mesh_falls_to_native_even_with_redraw()
        {
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Enhanced, hasMesh: false,
                hasDisplayRedraw: true, isAnimated: true),
                Is.EqualTo(ObjectPresentation.NativeBillboard));
            // A mesh still wins over the animated-redraw skip.
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Enhanced, hasMesh: true,
                hasDisplayRedraw: true, isAnimated: true),
                Is.EqualTo(ObjectPresentation.Mesh));
        }

        [Test]
        public void Lump_without_assets_is_native_in_enhanced()
        {
            Assert.That(ObjectPresentationResolver.Resolve(
                GraphicsMode.Enhanced, hasMesh: false,
                hasDisplayRedraw: false, isAnimated: true),
                Is.EqualTo(ObjectPresentation.NativeBillboard));
        }
    }
}
