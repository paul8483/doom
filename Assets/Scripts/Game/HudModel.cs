using System;

namespace Doom.Game
{
    /// Immutable HUD projection of player models. Renderer only reads this.
    public readonly struct HudModel
    {
        public readonly int Health;
        public readonly int Armor;
        public readonly ArmorKind ArmorType;

        /// Ammo for the ready weapon, or 0 when the weapon uses none (fist).
        public readonly int ReadyAmmo;
        public readonly bool ReadyAmmoVisible;

        public readonly int Bullets;
        public readonly int Shells;
        public readonly int Rockets;
        public readonly int Cells;
        public readonly int MaxBullets;
        public readonly int MaxShells;
        public readonly int MaxRockets;
        public readonly int MaxCells;

        public readonly bool OwnsPistol;
        public readonly bool OwnsShotgun;
        public readonly bool OwnsChaingun;

        public readonly bool BlueCard;
        public readonly bool YellowCard;
        public readonly bool RedCard;
        public readonly bool BlueSkull;
        public readonly bool YellowSkull;
        public readonly bool RedSkull;

        public readonly bool Berserk;
        public readonly bool IronFeet;

        public readonly string FacePatch;

        public HudModel(
            int health, int armor, ArmorKind armorType,
            int readyAmmo, bool readyAmmoVisible,
            int bullets, int shells, int rockets, int cells,
            int maxBullets, int maxShells, int maxRockets, int maxCells,
            bool ownsPistol, bool ownsShotgun, bool ownsChaingun,
            bool blueCard, bool yellowCard, bool redCard,
            bool blueSkull, bool yellowSkull, bool redSkull,
            bool berserk, bool ironFeet,
            string facePatch)
        {
            Health = health;
            Armor = armor;
            ArmorType = armorType;
            ReadyAmmo = readyAmmo;
            ReadyAmmoVisible = readyAmmoVisible;
            Bullets = bullets;
            Shells = shells;
            Rockets = rockets;
            Cells = cells;
            MaxBullets = maxBullets;
            MaxShells = maxShells;
            MaxRockets = maxRockets;
            MaxCells = maxCells;
            OwnsPistol = ownsPistol;
            OwnsShotgun = ownsShotgun;
            OwnsChaingun = ownsChaingun;
            BlueCard = blueCard;
            YellowCard = yellowCard;
            RedCard = redCard;
            BlueSkull = blueSkull;
            YellowSkull = yellowSkull;
            RedSkull = redSkull;
            Berserk = berserk;
            IronFeet = ironFeet;
            FacePatch = facePatch ?? FaceRules.DeadPatch;
        }

        public static HudModel From(
            HealthModel health,
            AmmoModel ammo,
            WeaponLoadout loadout,
            KeyInventory keys,
            PlayerPowers powers,
            FaceState face)
        {
            if (health == null) throw new ArgumentNullException(nameof(health));
            if (ammo == null) throw new ArgumentNullException(nameof(ammo));
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (powers == null) throw new ArgumentNullException(nameof(powers));
            if (face == null) throw new ArgumentNullException(nameof(face));

            var def = WeaponTable.Get(loadout.Current);
            bool ammoVisible = def.Ammo != AmmoType.None;
            int ready = ammoVisible ? ammo.Get(def.Ammo) : 0;

            return new HudModel(
                health.Health, health.Armor, health.ArmorType,
                ready, ammoVisible,
                ammo.Get(AmmoType.Bullets), ammo.Get(AmmoType.Shells), 0, 0,
                ammo.GetMax(AmmoType.Bullets), ammo.GetMax(AmmoType.Shells), 0, 0,
                loadout.Has(WeaponId.Pistol),
                loadout.Has(WeaponId.Shotgun),
                loadout.Has(WeaponId.Chaingun),
                keys.Has(PlayerKey.BlueCard),
                keys.Has(PlayerKey.YellowCard),
                keys.Has(PlayerKey.RedCard),
                keys.Has(PlayerKey.BlueSkull),
                keys.Has(PlayerKey.YellowSkull),
                keys.Has(PlayerKey.RedSkull),
                powers.Berserk,
                powers.IronFeetTics > 0,
                face.PatchName);
        }
    }
}
