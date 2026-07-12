using NUnit.Framework;

namespace Doom.Specials.Tests
{
    public class WallScrollRulesTests
    {
        [Test]
        public void Classic_and_boom_scrollers_move_in_opposite_directions()
        {
            Assert.IsTrue(WallScrollRules.TryGetUnitsPerTic(48, out float left));
            Assert.IsTrue(WallScrollRules.TryGetUnitsPerTic(85, out float right));
            Assert.AreEqual(1f, left);
            Assert.AreEqual(-1f, right);
        }

        [Test]
        public void Normalized_speed_uses_texture_width_and_35_hz_tics()
        {
            Assert.That(WallScrollRules.NormalizedSpeed(48, 70),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.AreEqual(0f, WallScrollRules.NormalizedSpeed(0, 64));
            Assert.AreEqual(0f, WallScrollRules.NormalizedSpeed(48, 0));
        }

        [Test]
        public void Offset_is_tic_derived_and_wraps_in_both_directions()
        {
            Assert.That(WallScrollRules.NormalizedOffset(48, 64, 32),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(WallScrollRules.NormalizedOffset(48, 64, 96),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(WallScrollRules.NormalizedOffset(85, 64, 32),
                Is.EqualTo(0.5f).Within(0.0001f));
        }
    }
}
