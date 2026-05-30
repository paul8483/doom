using NUnit.Framework;

namespace Doom.Specials.Tests
{
    public class LineSpecialTableTests
    {
        [Test]
        public void Manual_door_type1_is_repeatable_push_door()
        {
            Assert.That(LineSpecialTable.TryGet(1, out var s), Is.True);
            Assert.That(s.Category, Is.EqualTo(SpecialCategory.Door));
            Assert.That(s.Trigger, Is.EqualTo(TriggerKind.Push));
            Assert.That(s.Repeatable, Is.True);
            Assert.That(s.Target, Is.EqualTo(TargetSpec.LowestNeighborCeilingMinus4));
        }

        [Test]
        public void Lift_type62_is_repeatable_switch_plat()
        {
            Assert.That(LineSpecialTable.TryGet(62, out var s), Is.True);
            Assert.That(s.Category, Is.EqualTo(SpecialCategory.Plat));
            Assert.That(s.Repeatable, Is.True);
        }

        [Test]
        public void Stairs_type8_is_once_walk_stair()
        {
            Assert.That(LineSpecialTable.TryGet(8, out var s), Is.True);
            Assert.That(s.Category, Is.EqualTo(SpecialCategory.Stair));
            Assert.That(s.Trigger, Is.EqualTo(TriggerKind.Walk));
            Assert.That(s.Repeatable, Is.False);
        }

        [Test]
        public void Locked_door_carries_key()
        {
            Assert.That(LineSpecialTable.TryGet(32, out var s), Is.True); // D1 blue key door
            Assert.That(s.Category, Is.EqualTo(SpecialCategory.LockedDoor));
            Assert.That(s.Key, Is.Not.EqualTo(KeyKind.None));
        }

        [Test]
        public void Unknown_type_absent()
        {
            Assert.That(LineSpecialTable.TryGet(99999, out _), Is.False);
            Assert.That(LineSpecialTable.TryGet(0, out _), Is.False); // 0 = no special
        }
    }
}
