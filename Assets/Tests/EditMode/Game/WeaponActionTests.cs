using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class WeaponActionTests
    {
        [Test]
        public void Immediate_weapon_commits_on_begin()
        {
            var s = new WeaponActionScheduler();
            var ammo = new AmmoModel();
            var pistol = WeaponTable.Get(WeaponId.Pistol);

            Assert.That(s.TryBegin(pistol, ammo), Is.True);
            Assert.That(s.IsCommitted, Is.True);

            bool committed = false, finished = false;
            for (int i = 0; i < pistol.CycleTics; i++)
                s.Advance(out committed, out finished);

            Assert.That(finished, Is.True);
            Assert.That(s.IsRunning, Is.False);
        }

        [Test]
        public void BFG_commits_exactly_on_tic_30()
        {
            var s = new WeaponActionScheduler();
            var ammo = new AmmoModel();
            ammo.Add(AmmoType.Cells, 40);
            var bfg = WeaponTable.Get(WeaponId.Bfg9000);

            Assert.That(s.TryBegin(bfg, ammo), Is.True);
            Assert.That(s.IsCommitted, Is.False);

            bool sawCommit = false;
            for (int i = 0; i < 30; i++)
            {
                s.Advance(out bool justCommitted, out _);
                if (justCommitted)
                {
                    Assert.That(i, Is.EqualTo(29), "commit on the 30th Advance");
                    sawCommit = true;
                }
            }
            Assert.That(sawCommit, Is.True);
            Assert.That(s.IsCommitted, Is.True);
            Assert.That(s.IsRunning, Is.True);
        }

        [Test]
        public void BFG_rejects_begin_with_39_cells()
        {
            var s = new WeaponActionScheduler();
            var ammo = new AmmoModel();
            ammo.Add(AmmoType.Cells, 39);
            Assert.That(s.TryBegin(WeaponTable.Get(WeaponId.Bfg9000), ammo), Is.False);
        }

        [Test]
        public void Plasma_allows_held_refire_after_3_tics()
        {
            var s = new WeaponActionScheduler();
            var ammo = new AmmoModel();
            ammo.Add(AmmoType.Cells, 10);
            var plasma = WeaponTable.Get(WeaponId.PlasmaRifle);

            Assert.That(s.TryBegin(plasma, ammo), Is.True);
            Assert.That(s.IsCommitted, Is.True);
            Assert.That(s.CanBegin(plasma), Is.False);

            s.Advance(out _, out _);
            s.Advance(out _, out _);
            Assert.That(s.CanBegin(plasma), Is.False);
            s.Advance(out _, out _);
            Assert.That(s.CanBegin(plasma), Is.True);

            Assert.That(s.TryBegin(plasma, ammo), Is.True);
            Assert.That(s.TicsElapsed, Is.EqualTo(0));
        }

        [Test]
        public void Cancel_clears_uncommitted_BFG_without_requiring_ammo_spend()
        {
            var s = new WeaponActionScheduler();
            var ammo = new AmmoModel();
            ammo.Add(AmmoType.Cells, 40);
            Assert.That(s.TryBegin(WeaponTable.Get(WeaponId.Bfg9000), ammo), Is.True);
            for (int i = 0; i < 10; i++) s.Advance(out _, out _);
            Assert.That(s.IsCommitted, Is.False);
            s.Cancel();
            Assert.That(s.IsRunning, Is.False);
            Assert.That(ammo.Get(AmmoType.Cells), Is.EqualTo(40));
        }

        [Test]
        public void Restore_rejects_impossible_BFG_commit_state()
        {
            var s = new WeaponActionScheduler();
            var bfg = WeaponTable.Get(WeaponId.Bfg9000);
            Assert.That(s.TryRestore(bfg, ticsElapsed: 10, isCommitted: true), Is.False);
            Assert.That(s.TryRestore(bfg, ticsElapsed: 30, isCommitted: false), Is.False);
            Assert.That(s.TryRestore(bfg, ticsElapsed: 30, isCommitted: true), Is.True);
            Assert.That(s.IsCommitted, Is.True);
        }
    }
}
