using NUnit.Framework;
using Doom.Specials;

namespace Doom.Specials.Tests
{
    public class ExitSectorRulesTests
    {
        [Test]
        public void Exits_when_special_11_and_hp_at_or_below_10()
        {
            Assert.That(ExitSectorRules.ShouldExitAfterDamage(11, 10), Is.True);
            Assert.That(ExitSectorRules.ShouldExitAfterDamage(11, 0), Is.True);
            Assert.That(ExitSectorRules.ShouldExitAfterDamage(11, 5), Is.True);
        }

        [Test]
        public void Does_not_exit_when_hp_above_10()
        {
            Assert.That(ExitSectorRules.ShouldExitAfterDamage(11, 11), Is.False);
            Assert.That(ExitSectorRules.ShouldExitAfterDamage(11, 100), Is.False);
        }

        [Test]
        public void Other_damage_specials_do_not_exit()
        {
            Assert.That(ExitSectorRules.ShouldExitAfterDamage(5, 5), Is.False);
            Assert.That(ExitSectorRules.ShouldExitAfterDamage(7, 0), Is.False);
            Assert.That(ExitSectorRules.ShouldExitAfterDamage(16, 10), Is.False);
        }
    }
}
