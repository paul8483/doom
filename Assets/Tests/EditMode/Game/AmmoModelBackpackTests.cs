using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class AmmoModelBackpackTests
    {
        [Test]
        public void GiveBackpack_doubles_max_and_grants_clips()
        {
            var a = new AmmoModel(); // 50 bullets, 0 shells
            Assert.That(a.GiveBackpack(), Is.True);
            Assert.That(a.HasBackpack, Is.True);
            Assert.That(a.GetMax(AmmoType.Bullets), Is.EqualTo(400));
            Assert.That(a.GetMax(AmmoType.Shells), Is.EqualTo(100));
            Assert.That(a.Get(AmmoType.Bullets), Is.EqualTo(60)); // 50+10
            Assert.That(a.Get(AmmoType.Shells), Is.EqualTo(4));
        }

        [Test]
        public void GiveBackpack_again_grants_more_clips()
        {
            var a = new AmmoModel();
            a.GiveBackpack();
            int b = a.Get(AmmoType.Bullets);
            a.GiveBackpack();
            Assert.That(a.Get(AmmoType.Bullets), Is.EqualTo(b + 10));
            Assert.That(a.GetMax(AmmoType.Bullets), Is.EqualTo(400));
        }

        [Test]
        public void Reset_clears_backpack()
        {
            var a = new AmmoModel();
            a.GiveBackpack();
            a.Reset();
            Assert.That(a.HasBackpack, Is.False);
            Assert.That(a.GetMax(AmmoType.Bullets), Is.EqualTo(200));
        }
    }

    public class PlayerPowersTests
    {
        [Test]
        public void Berserk_does_not_tick_down()
        {
            var p = new PlayerPowers();
            p.GiveBerserk();
            p.Advance(10_000);
            Assert.That(p.Berserk, Is.True);
        }

        [Test]
        public void IronFeet_ticks_to_zero()
        {
            var p = new PlayerPowers();
            p.GiveIronFeet(10);
            p.Advance(4);
            Assert.That(p.IronFeetTics, Is.EqualTo(6));
            p.Advance(100);
            Assert.That(p.IronFeetTics, Is.EqualTo(0));
        }
    }
}
