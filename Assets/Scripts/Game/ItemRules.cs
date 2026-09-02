namespace Doom.Game
{
    /// Mutable bag of player models passed into ItemRules.TryPickup.
    public sealed class PickupContext
    {
        public HealthModel Health;
        public AmmoModel Ammo;
        public WeaponLoadout Loadout;
        public KeyInventory Keys;
        public PlayerPowers Powers;

        /// Set by berserk pickup: caller should select Fist.
        public bool PreferFist;
    }

    /// Pure doomednum → pickup router (p_inter.c). Unity destroys the GO only when
    /// TryPickup returns true.
    public static class ItemRules
    {
        public const int IronFeetDurationTics = 60 * 35; // 2100

        public static bool TryPickup(int doomedNum, PickupContext ctx) =>
            TryPickup(doomedNum, ctx, dropped: false);

        /// <paramref name="dropped"/> = MF_DROPPED (P_TouchSpecialThing): a
        /// dropped clip gives clipammo/2, a dropped weapon one clip instead of two.
        public static bool TryPickup(int doomedNum, PickupContext ctx, bool dropped)
        {
            switch (doomedNum)
            {
                case 2011: return ctx.Health.GiveHealth(10, 100);
                case 2012: return ctx.Health.GiveHealth(25, 100);
                // P_TouchSpecialThing: bonuses and the soulsphere are always
                // consumed (only stim/medikit/armor refuse at the cap).
                case 2014: ctx.Health.GiveHealth(1, 200); return true;
                case 2013: ctx.Health.GiveHealth(100, 200); return true;
                case 2018: return ctx.Health.GiveArmor(ArmorKind.Green);
                case 2019: return ctx.Health.GiveArmor(ArmorKind.Blue);
                case 2015: ctx.Health.GiveArmorBonus(1); return true;

                case 5:  ctx.Keys.Give(PlayerKey.BlueCard); return true;
                case 40: ctx.Keys.Give(PlayerKey.BlueSkull); return true;
                case 13: ctx.Keys.Give(PlayerKey.RedCard); return true;
                case 38: ctx.Keys.Give(PlayerKey.RedSkull); return true;
                case 6:  ctx.Keys.Give(PlayerKey.YellowCard); return true;
                case 39: ctx.Keys.Give(PlayerKey.YellowSkull); return true;

                case 8: return ctx.Ammo.GiveBackpack();

                case 2023:
                    if (ctx.Health.Health < 100)
                        ctx.Health.GiveHealth(100 - ctx.Health.Health, 100);
                    ctx.Powers.GiveBerserk();
                    ctx.PreferFist = true;
                    return true;

                case 2025:
                    ctx.Powers.GiveIronFeet(IronFeetDurationTics);
                    return true;

                case 2001: return PickWeapon(ctx, WeaponId.Shotgun, AmmoType.Shells, Clips(8, dropped));
                case 2002: return PickWeapon(ctx, WeaponId.Chaingun, AmmoType.Bullets, Clips(20, dropped));
                case 2003: return PickWeapon(ctx, WeaponId.RocketLauncher, AmmoType.Rockets, Clips(2, dropped));
                case 2004: return PickWeapon(ctx, WeaponId.PlasmaRifle, AmmoType.Cells, Clips(40, dropped));
                case 2005: return ctx.Loadout.Give(WeaponId.Chainsaw);
                case 2006: return PickWeapon(ctx, WeaponId.Bfg9000, AmmoType.Cells, Clips(40, dropped));
                case 2007: return ctx.Ammo.Add(AmmoType.Bullets, Clips(10, dropped));
                case 2048: return ctx.Ammo.Add(AmmoType.Bullets, 50);
                case 2008: return ctx.Ammo.Add(AmmoType.Shells, 4);
                case 2049: return ctx.Ammo.Add(AmmoType.Shells, 20);
                case 2010: return ctx.Ammo.Add(AmmoType.Rockets, 1);
                case 2046: return ctx.Ammo.Add(AmmoType.Rockets, 5);
                case 2047: return ctx.Ammo.Add(AmmoType.Cells, 20);
                case 17: return ctx.Ammo.Add(AmmoType.Cells, 100);

                default: return false;
            }
        }

        /// Placed items carry two clips (or one full clip of ammo); MF_DROPPED
        /// halves that (P_GiveWeapon dropped → P_GiveAmmo(…, 1); dropped clip → clipammo/2).
        static int Clips(int placed, bool dropped) => dropped ? placed / 2 : placed;

        static bool PickWeapon(PickupContext ctx, WeaponId id, AmmoType ammo, int give)
        {
            bool gotWeapon = ctx.Loadout.Give(id);
            bool gotAmmo = ctx.Ammo.Add(ammo, give);
            return gotWeapon || gotAmmo;
        }

        /// True when this doomednum is a Stage 6e pickup (ThingSpawner attaches ThingPickup).
        public static bool IsPickup(int doomedNum)
        {
            switch (doomedNum)
            {
                case 2011: case 2012: case 2014: case 2013:
                case 2018: case 2019: case 2015:
                case 5: case 40: case 13: case 38: case 6: case 39:
                case 8: case 2023: case 2025:
                case 2001: case 2002: case 2003: case 2004: case 2005: case 2006:
                case 2007: case 2048: case 2008: case 2049: case 2010: case 2046:
                case 2047: case 17:
                    return true;
                default: return false;
            }
        }
    }
}
