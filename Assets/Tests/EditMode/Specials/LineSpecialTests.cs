using NUnit.Framework;

namespace Doom.Specials.Tests
{
    public class LineSpecialTests
    {
        [Test]
        public void Executable_only_for_movement_categories()
        {
            var door = new LineSpecial(1, TriggerKind.Push, true, true,
                SpecialCategory.Door, MoveDirection.Up, MoveSpeed.Normal,
                TargetSpec.LowestNeighborCeilingMinus4, KeyKind.None);
            var light = new LineSpecial(35, TriggerKind.Walk, false, false,
                SpecialCategory.Light, MoveDirection.Up, MoveSpeed.Slow,
                TargetSpec.None, KeyKind.None);

            Assert.That(door.IsExecutable, Is.True);
            Assert.That(light.IsExecutable, Is.False);
            Assert.That(door.Repeatable, Is.True);
            Assert.That(door.Trigger, Is.EqualTo(TriggerKind.Push));
        }
    }
}
