using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class PickupSoundTableTests
    {
        [Test]
        public void Weapons_and_powers_and_items_classify()
        {
            Assert.That(PickupSoundTable.Get(2001), Is.EqualTo(PickupSoundKind.Weapon));
            Assert.That(PickupSoundTable.Get(2002), Is.EqualTo(PickupSoundKind.Weapon));
            Assert.That(PickupSoundTable.Get(2003), Is.EqualTo(PickupSoundKind.Weapon));
            Assert.That(PickupSoundTable.Get(2005), Is.EqualTo(PickupSoundKind.Weapon));
            Assert.That(PickupSoundTable.Get(2013), Is.EqualTo(PickupSoundKind.Power));
            Assert.That(PickupSoundTable.Get(2023), Is.EqualTo(PickupSoundKind.Power));
            Assert.That(PickupSoundTable.Get(2025), Is.EqualTo(PickupSoundKind.Power));
            Assert.That(PickupSoundTable.Get(2011), Is.EqualTo(PickupSoundKind.Item));
            Assert.That(PickupSoundTable.Get(2018), Is.EqualTo(PickupSoundKind.Item));
            Assert.That(PickupSoundTable.Get(5), Is.EqualTo(PickupSoundKind.Item));
            Assert.That(PickupSoundTable.Get(8), Is.EqualTo(PickupSoundKind.Item));
            Assert.That(PickupSoundTable.Get(2007), Is.EqualTo(PickupSoundKind.Item));
            Assert.That(PickupSoundTable.Get(2010), Is.EqualTo(PickupSoundKind.Item));
            Assert.That(PickupSoundTable.Get(2046), Is.EqualTo(PickupSoundKind.Item));
            Assert.That(PickupSoundTable.Get(9999), Is.EqualTo(PickupSoundKind.None));
        }

        [Test]
        public void Lump_names()
        {
            Assert.That(PickupSoundTable.LumpName(PickupSoundKind.Item), Is.EqualTo("DSITEMUP"));
            Assert.That(PickupSoundTable.LumpName(PickupSoundKind.Weapon), Is.EqualTo("DSWPNUP"));
            Assert.That(PickupSoundTable.LumpName(PickupSoundKind.Power), Is.EqualTo("DSGETPOW"));
            Assert.That(PickupSoundTable.LumpName(PickupSoundKind.None), Is.Null);
        }
    }
}
