using NUnit.Framework;
using Doom.MapBuild;

namespace Doom.Map.Tests
{
    /// P_UseSpecialLine whitelist for non-player actors (p_spec.c): a monster
    /// may "use" 1 / 32 / 33 / 34, and EV_VerticalDoor then refuses 32–34 with
    /// `if (!player) return 0`. Net effect pinned here: only special 1 opens
    /// for a monster; every keyed door is a wall to it (no open, no key
    /// grunt). Regression for the slot-0 E1M4 report: a monster pack parked
    /// at the blue door ran the player's key check through PlayKeyDenied and
    /// played the 2D oof four times a second, map-wide, forever.
    public class MonsterDoorUseTests
    {
        [Test]
        public void Manual_raise_door_is_the_only_monster_usable_special()
        {
            Assert.That(LineActivator.IsMonsterUsableDoorSpecial(1), Is.True);
        }

        [TestCase(26, TestName = "blue DR")]
        [TestCase(27, TestName = "yellow DR")]
        [TestCase(28, TestName = "red DR")]
        [TestCase(32, TestName = "blue D1")]
        [TestCase(33, TestName = "red D1")]
        [TestCase(34, TestName = "yellow D1")]
        [TestCase(31, TestName = "D1 open stay")]
        [TestCase(117, TestName = "blaze DR")]
        [TestCase(118, TestName = "blaze D1")]
        public void Keyed_and_non_whitelisted_doors_are_walls_to_a_monster(int special)
        {
            Assert.That(LineActivator.IsMonsterUsableDoorSpecial(special), Is.False);
        }
    }
}
