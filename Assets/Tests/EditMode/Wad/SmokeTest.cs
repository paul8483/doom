using NUnit.Framework;

namespace Doom.Wad.Tests
{
    public class SmokeTest
    {
        [Test]
        public void Test_framework_is_wired_up()
        {
            Assert.That(2 + 2, Is.EqualTo(4));
        }
    }
}
