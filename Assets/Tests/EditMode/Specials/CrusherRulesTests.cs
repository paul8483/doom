using NUnit.Framework;

namespace Doom.Specials.Tests
{
    public class CrusherRulesTests
    {
        [TestCase(6, 2f, true, false)]
        [TestCase(25, 1f, true, true)]
        [TestCase(49, 1f, true, true)]
        [TestCase(73, 1f, true, true)]
        [TestCase(77, 2f, true, false)]
        [TestCase(141, 1f, true, true)]
        public void Cycling_crushers_have_classic_speed_and_slowdown(
            int special, float speed, bool cycles, bool slows)
        {
            Assert.That(CrusherRules.TryGet(special, out var rule), Is.True);
            Assert.That(rule.Behavior, Is.EqualTo(CrusherBehavior.CrushAndRaise));
            Assert.That(rule.SpeedUnitsPerTic, Is.EqualTo(speed));
            Assert.That(rule.Cycles, Is.EqualTo(cycles));
            Assert.That(rule.SlowsWhenCrushing, Is.EqualTo(slows));
        }

        [TestCase(44)]
        [TestCase(72)]
        public void Lower_and_crush_specials_stop_at_floor_plus_eight(int special)
        {
            Assert.That(CrusherRules.TryGet(special, out var rule), Is.True);
            Assert.That(rule.Behavior, Is.EqualTo(CrusherBehavior.LowerAndCrush));
            Assert.That(rule.Cycles, Is.False);
            Assert.That(CrusherRules.TargetHeight(-24f), Is.EqualTo(-16f));
            Assert.That(LineSpecialTable.TryGet(special, out var line), Is.True);
            Assert.That(line.IsExecutable, Is.True);
        }

        [TestCase(57)]
        [TestCase(74)]
        public void Stop_specials_are_explicit(int special)
        {
            Assert.That(CrusherRules.TryGet(special, out var rule), Is.True);
            Assert.That(rule.Behavior, Is.EqualTo(CrusherBehavior.Stop));
        }
    }
}
