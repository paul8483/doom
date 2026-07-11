using NUnit.Framework;

namespace Doom.Game.Tests
{
    public class HudModelTests
    {
        [Test]
        public void Fist_hides_ready_ammo()
        {
            var health = new HealthModel();
            var ammo = new AmmoModel();
            var loadout = new WeaponLoadout();
            loadout.TrySelect(WeaponId.Fist);
            var keys = new KeyInventory();
            var powers = new PlayerPowers();
            var face = new FaceState();
            face.Reset(health.Health);

            var hud = HudModel.From(health, ammo, loadout, keys, powers, face);

            Assert.That(hud.ReadyAmmoVisible, Is.False);
            Assert.That(hud.ReadyAmmo, Is.EqualTo(0));
            Assert.That(hud.Health, Is.EqualTo(100));
            Assert.That(hud.FacePatch, Is.EqualTo("STFST00"));
        }

        [Test]
        public void Pistol_shows_bullet_count_as_ready_ammo()
        {
            var health = new HealthModel();
            var ammo = new AmmoModel();
            ammo.Add(AmmoType.Bullets, 10); // 50 start + 10
            var loadout = new WeaponLoadout();
            var keys = new KeyInventory();
            var powers = new PlayerPowers();
            var face = new FaceState();
            face.Reset();

            var hud = HudModel.From(health, ammo, loadout, keys, powers, face);

            Assert.That(hud.ReadyAmmoVisible, Is.True);
            Assert.That(hud.ReadyAmmo, Is.EqualTo(60));
            Assert.That(hud.Bullets, Is.EqualTo(60));
            Assert.That(hud.Shells, Is.EqualTo(0));
            Assert.That(hud.OwnsPistol, Is.True);
            Assert.That(hud.OwnsShotgun, Is.False);
        }

        [Test]
        public void Keys_and_powers_project()
        {
            var health = new HealthModel();
            health.GiveArmor(ArmorKind.Blue);
            var ammo = new AmmoModel();
            var loadout = new WeaponLoadout();
            loadout.Give(WeaponId.Shotgun);
            loadout.Give(WeaponId.Chaingun);
            var keys = new KeyInventory();
            keys.Give(PlayerKey.BlueCard);
            keys.Give(PlayerKey.RedSkull);
            var powers = new PlayerPowers();
            powers.GiveBerserk();
            powers.GiveIronFeet(100);
            var face = new FaceState();
            face.Reset();

            var hud = HudModel.From(health, ammo, loadout, keys, powers, face);

            Assert.That(hud.Armor, Is.EqualTo(200));
            Assert.That(hud.ArmorType, Is.EqualTo(ArmorKind.Blue));
            Assert.That(hud.OwnsShotgun, Is.True);
            Assert.That(hud.OwnsChaingun, Is.True);
            Assert.That(hud.BlueCard, Is.True);
            Assert.That(hud.RedSkull, Is.True);
            Assert.That(hud.YellowCard, Is.False);
            Assert.That(hud.Berserk, Is.True);
            Assert.That(hud.IronFeet, Is.True);
        }

        [Test]
        public void Rockets_are_projected_while_cells_remain_unsupported()
        {
            var ammo = new AmmoModel();
            ammo.Add(AmmoType.Rockets, 7);
            var loadout = new WeaponLoadout();
            loadout.Give(WeaponId.RocketLauncher);
            var hud = HudModel.From(
                new HealthModel(), ammo, loadout,
                new KeyInventory(), new PlayerPowers(), new FaceState());

            Assert.That(hud.Rockets, Is.EqualTo(7));
            Assert.That(hud.ReadyAmmo, Is.EqualTo(7));
            Assert.That(hud.OwnsRocketLauncher, Is.True);
            Assert.That(hud.Cells, Is.EqualTo(0));
            Assert.That(hud.MaxRockets, Is.EqualTo(AmmoModel.MaxRockets));
            Assert.That(hud.MaxCells, Is.EqualTo(0));
        }
    }
}
