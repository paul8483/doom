using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class AmmoModelTests
    {
        [Test]
        public void Starts_with_doom_loadout_and_clamps_to_max()
        {
            var a = new AmmoModel();
            Assert.That(a.Get(AmmoType.Bullets), Is.EqualTo(50));
            Assert.That(a.Get(AmmoType.Shells), Is.EqualTo(0));
            Assert.That(a.Get(AmmoType.Rockets), Is.EqualTo(0));

            Assert.That(a.Add(AmmoType.Bullets, 500), Is.True);
            Assert.That(a.Get(AmmoType.Bullets), Is.EqualTo(200), "кламп к maxammo");
            Assert.That(a.Add(AmmoType.Bullets, 10), Is.False, "уже полно — не подобрано");
        }

        [Test]
        public void Rockets_clamp_and_consume()
        {
            var a = new AmmoModel();
            Assert.That(a.Add(AmmoType.Rockets, 500), Is.True);
            Assert.That(a.Get(AmmoType.Rockets), Is.EqualTo(AmmoModel.MaxRockets));
            Assert.That(a.TryConsume(AmmoType.Rockets, 1), Is.True);
            Assert.That(a.Get(AmmoType.Rockets), Is.EqualTo(AmmoModel.MaxRockets - 1));
        }

        [Test]
        public void Cells_clamp_consume_and_capture_restore()
        {
            var a = new AmmoModel();
            Assert.That(a.Get(AmmoType.Cells), Is.EqualTo(0));
            Assert.That(a.GetMax(AmmoType.Cells), Is.EqualTo(AmmoModel.MaxCells));
            Assert.That(a.Add(AmmoType.Cells, 500), Is.True);
            Assert.That(a.Get(AmmoType.Cells), Is.EqualTo(AmmoModel.MaxCells));
            Assert.That(a.TryConsume(AmmoType.Cells, 40), Is.True);
            Assert.That(a.Get(AmmoType.Cells), Is.EqualTo(260));
            Assert.That(a.TryConsume(AmmoType.Cells, 261), Is.False);

            a.Capture(out int b, out int s, out int r, out int c, out bool bp);
            Assert.That(c, Is.EqualTo(260));
            a.Restore(b, s, r, -5, bp);
            Assert.That(a.Get(AmmoType.Cells), Is.EqualTo(0));
            a.Restore(b, s, r, 9999, false);
            Assert.That(a.Get(AmmoType.Cells), Is.EqualTo(AmmoModel.MaxCells));
        }

        [Test]
        public void TryConsume_spends_or_refuses()
        {
            var a = new AmmoModel();
            Assert.That(a.TryConsume(AmmoType.Shells, 1), Is.False, "дроби нет");
            a.Add(AmmoType.Shells, 4);
            Assert.That(a.TryConsume(AmmoType.Shells, 1), Is.True);
            Assert.That(a.Get(AmmoType.Shells), Is.EqualTo(3));
        }

        [Test]
        public void None_ammo_is_always_available_and_reset_restores_start()
        {
            var a = new AmmoModel();
            Assert.That(a.TryConsume(AmmoType.None, 1), Is.True, "кулак патронов не требует");
            while (a.TryConsume(AmmoType.Bullets, 1)) { }
            a.Reset();
            Assert.That(a.Get(AmmoType.Bullets), Is.EqualTo(50));
            Assert.That(a.Get(AmmoType.Shells), Is.EqualTo(0));
        }
    }
}
