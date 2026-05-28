using NUnit.Framework;

namespace Doom.Map.Tests
{
    public class MapTestsSmoke
    {
        [Test]
        public void Map_test_assembly_is_wired_up()
        {
            Assert.That(2 + 2, Is.EqualTo(4));
        }

        [Test]
        public void Can_see_SyntheticWadBuilder_from_Wad_tests()
        {
            // Если эта ассерт-цепочка компилируется, значит ссылка на Doom.Wad.Tests работает.
            var bytes = Doom.Wad.Tests.SyntheticWadBuilder.Build("IWAD",
                new[] { new Doom.Wad.Tests.SyntheticWadBuilder.Lump("X", new byte[0]) });
            Assert.That(bytes.Length, Is.GreaterThan(12));
        }
    }
}
