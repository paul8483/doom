using NUnit.Framework;

namespace Doom.Map.Tests
{
    public class MeshDataTests
    {
        [Test]
        public void Legacy_constructor_leaves_uv_and_colors_empty()
        {
            var m = new MeshData(
                new[] { new Float3(0, 0, 0) },
                new[] { 0, 0, 0 });
            Assert.That(m.Uv.Length, Is.EqualTo(0));
            Assert.That(m.Colors.Length, Is.EqualTo(0));
        }

        [Test]
        public void Full_constructor_stores_uv_and_colors()
        {
            var m = new MeshData(
                new[] { new Float3(0, 0, 0) },
                new[] { 0, 0, 0 },
                new[] { new Float2(0.25f, 0.5f) },
                new[] { new Float3(0.5f, 0.5f, 0.5f) });
            Assert.That(m.Uv[0].X, Is.EqualTo(0.25f));
            Assert.That(m.Uv[0].Y, Is.EqualTo(0.5f));
            Assert.That(m.Colors[0].Y, Is.EqualTo(0.5f));
        }
    }
}
