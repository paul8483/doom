using System;

namespace Doom.Game
{
    /// Inter-level player snapshot. Position is never carried; keys and temporary
    /// powers are cleared on advance and therefore omitted from this DTO.
    public sealed class PlayerCarryState
    {
        public int Health { get; }
        public int Armor { get; }
        public ArmorKind ArmorType { get; }
        public int Bullets { get; }
        public int Shells { get; }
        public int Rockets { get; }
        public bool HasBackpack { get; }
        public bool OwnsFist { get; }
        public bool OwnsPistol { get; }
        public bool OwnsShotgun { get; }
        public bool OwnsChaingun { get; }
        public bool OwnsRocketLauncher { get; }
        public bool OwnsChainsaw { get; }
        public WeaponId CurrentWeapon { get; }

        public PlayerCarryState(
            int health, int armor, ArmorKind armorType,
            int bullets, int shells, bool hasBackpack,
            bool ownsFist, bool ownsPistol, bool ownsShotgun, bool ownsChaingun,
            WeaponId currentWeapon)
            : this(
                health, armor, armorType,
                bullets, shells, 0, hasBackpack,
                ownsFist, ownsPistol, ownsShotgun, ownsChaingun, false, false,
                currentWeapon)
        {
        }

        public PlayerCarryState(
            int health, int armor, ArmorKind armorType,
            int bullets, int shells, int rockets, bool hasBackpack,
            bool ownsFist, bool ownsPistol, bool ownsShotgun, bool ownsChaingun,
            bool ownsRocketLauncher, WeaponId currentWeapon)
            : this(
                health, armor, armorType,
                bullets, shells, rockets, hasBackpack,
                ownsFist, ownsPistol, ownsShotgun, ownsChaingun, ownsRocketLauncher,
                false, currentWeapon)
        {
        }

        public PlayerCarryState(
            int health, int armor, ArmorKind armorType,
            int bullets, int shells, int rockets, bool hasBackpack,
            bool ownsFist, bool ownsPistol, bool ownsShotgun, bool ownsChaingun,
            bool ownsRocketLauncher, bool ownsChainsaw, WeaponId currentWeapon)
        {
            Health = health;
            Armor = armor;
            ArmorType = armorType;
            Bullets = bullets;
            Shells = shells;
            Rockets = rockets;
            HasBackpack = hasBackpack;
            OwnsFist = ownsFist;
            OwnsPistol = ownsPistol;
            OwnsShotgun = ownsShotgun;
            OwnsChaingun = ownsChaingun;
            OwnsRocketLauncher = ownsRocketLauncher;
            OwnsChainsaw = ownsChainsaw;
            CurrentWeapon = currentWeapon;
        }

        /// Fresh pistol start — New Game / death restart.
        public static PlayerCarryState FreshStart() =>
            new PlayerCarryState(
                HealthModel.MaxHealth, 0, ArmorKind.None,
                AmmoModel.StartBullets, 0, false,
                true, true, false, false,
                WeaponId.Pistol);

        public static PlayerCarryState Capture(
            HealthModel health, AmmoModel ammo, WeaponLoadout loadout)
        {
            if (health == null) throw new ArgumentNullException(nameof(health));
            if (ammo == null) throw new ArgumentNullException(nameof(ammo));
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));

            return new PlayerCarryState(
                health.Health, health.Armor, health.ArmorType,
                ammo.Get(AmmoType.Bullets), ammo.Get(AmmoType.Shells),
                ammo.Get(AmmoType.Rockets), ammo.HasBackpack,
                loadout.Has(WeaponId.Fist), loadout.Has(WeaponId.Pistol),
                loadout.Has(WeaponId.Shotgun), loadout.Has(WeaponId.Chaingun),
                loadout.Has(WeaponId.RocketLauncher),
                loadout.Has(WeaponId.Chainsaw),
                loadout.Current);
        }

        public void ApplyTo(HealthModel health, AmmoModel ammo, WeaponLoadout loadout)
        {
            if (health == null) throw new ArgumentNullException(nameof(health));
            if (ammo == null) throw new ArgumentNullException(nameof(ammo));
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));

            health.Restore(Health, Armor, ArmorType);
            ammo.Restore(Bullets, Shells, Rockets, HasBackpack);
            loadout.Restore(
                OwnsFist, OwnsPistol, OwnsShotgun, OwnsChaingun,
                OwnsRocketLauncher, OwnsChainsaw, CurrentWeapon);
        }
    }
}
