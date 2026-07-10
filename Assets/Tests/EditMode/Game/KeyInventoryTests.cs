using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class KeyInventoryTests
    {
        [Test]
        public void Give_and_Has_distinguish_card_and_skull()
        {
            var k = new KeyInventory();
            k.Give(PlayerKey.RedCard);
            Assert.That(k.Has(PlayerKey.RedCard), Is.True);
            Assert.That(k.Has(PlayerKey.RedSkull), Is.False);
        }

        [Test]
        public void HasAny_true_if_any_key()
        {
            var k = new KeyInventory();
            Assert.That(k.HasAny(), Is.False);
            k.Give(PlayerKey.BlueSkull);
            Assert.That(k.HasAny(), Is.True);
        }

        [Test]
        public void Give_idempotent_returns_false_if_owned()
        {
            var k = new KeyInventory();
            Assert.That(k.Give(PlayerKey.YellowCard), Is.True);
            Assert.That(k.Give(PlayerKey.YellowCard), Is.False);
        }
    }
}
