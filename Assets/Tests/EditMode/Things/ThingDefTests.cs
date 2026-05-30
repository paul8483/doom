using NUnit.Framework;

namespace Doom.Things.Tests
{
    public class ThingDefTests
    {
        [Test]
        public void Has_reports_individual_flags()
        {
            var d = new ThingDef(2035, "BAR1", 0, 10, 42,
                                 ThingFlags.Solid | ThingFlags.Shootable);
            Assert.That(d.Has(ThingFlags.Solid), Is.True);
            Assert.That(d.Has(ThingFlags.Shootable), Is.True);
            Assert.That(d.Has(ThingFlags.SpawnCeiling), Is.False);
            Assert.That(d.Sprite, Is.EqualTo("BAR1"));
            Assert.That((d.Radius, d.Height), Is.EqualTo((10, 42)));
            Assert.That(d.DoomEdNum, Is.EqualTo(2035));
            Assert.That(d.Frame, Is.EqualTo(0));
        }
    }
}
