namespace Doom.Game
{
    public static class WeaponTable
    {
        static readonly WeaponDef Fist = new WeaponDef
        {
            Id = WeaponId.Fist, Slot = 1, Ammo = AmmoType.None, AmmoPerShot = 0,
            Pellets = 1, Melee = true, FirstShotAccurate = false,
            Sprite = "PUNG", IdleFrame = 0,
            FireFrames = new[] { 1, 2, 3, 2, 1 }, FireTics = new[] { 4, 4, 5, 4, 5 },
            FlashSprite = null, FlashFrames = System.Array.Empty<int>(),
            FlashTics = System.Array.Empty<int>(),
            ActionTic = 0, RefireTics = 0, FlashDelayTic = 0, RandomFlash = false,
            FireSound = "DSPUNCH",
        };

        static readonly WeaponDef Pistol = new WeaponDef
        {
            Id = WeaponId.Pistol, Slot = 2, Ammo = AmmoType.Bullets, AmmoPerShot = 1,
            Pellets = 1, Melee = false, FirstShotAccurate = true,
            Sprite = "PISG", IdleFrame = 0,
            FireFrames = new[] { 0, 1, 2, 1 }, FireTics = new[] { 4, 6, 4, 5 },
            FlashSprite = "PISF", FlashFrames = new[] { 0 }, FlashTics = new[] { 7 },
            ActionTic = 0, RefireTics = 0, FlashDelayTic = 0, RandomFlash = false,
            FireSound = "DSPISTOL",
        };

        static readonly WeaponDef Shotgun = new WeaponDef
        {
            Id = WeaponId.Shotgun, Slot = 3, Ammo = AmmoType.Shells, AmmoPerShot = 1,
            Pellets = 7, Melee = false, FirstShotAccurate = false,
            Sprite = "SHTG", IdleFrame = 0,
            FireFrames = new[] { 0, 0, 1, 2, 3, 2, 1, 0, 0 },
            FireTics = new[] { 3, 7, 5, 5, 4, 5, 5, 3, 7 },
            FlashSprite = "SHTF", FlashFrames = new[] { 0, 1 }, FlashTics = new[] { 4, 3 },
            ActionTic = 0, RefireTics = 0, FlashDelayTic = 0, RandomFlash = false,
            FireSound = "DSSHOTGN",
        };

        // DOOM: 2 выстрела за 8 тиков (S_CHAIN1 A 4 + S_CHAIN2 B 4). Моделируем
        // как 1 выстрел / 4 тика с чередованием кадра — та же скорострельность.
        static readonly WeaponDef Chaingun = new WeaponDef
        {
            Id = WeaponId.Chaingun, Slot = 4, Ammo = AmmoType.Bullets, AmmoPerShot = 1,
            Pellets = 1, Melee = false, FirstShotAccurate = false,
            Sprite = "CHGG", IdleFrame = 0,
            FireFrames = new[] { 1 }, FireTics = new[] { 4 },
            FlashSprite = "CHGF", FlashFrames = new[] { 0 }, FlashTics = new[] { 5 },
            ActionTic = 0, RefireTics = 0, FlashDelayTic = 0, RandomFlash = false,
            FireSound = "DSPISTOL",
        };

        static readonly WeaponDef RocketLauncher = new WeaponDef
        {
            Id = WeaponId.RocketLauncher, Slot = 5, Ammo = AmmoType.Rockets, AmmoPerShot = 1,
            Pellets = 0, Melee = false, FirstShotAccurate = true,
            Sprite = "MISG", IdleFrame = 0,
            FireFrames = new[] { 1, 1 }, FireTics = new[] { 8, 12 },
            FlashSprite = "MISF", FlashFrames = new[] { 0, 1, 2, 3 },
            FlashTics = new[] { 3, 4, 4, 4 },
            ActionTic = 0, RefireTics = 0, FlashDelayTic = 0, RandomFlash = false,
            FireSound = "DSRLAUNC",
        };

        // A_Saw every 4 tics (S_SAW1/S_SAW2); idle uses SAWG C like S_SAW.
        // FireFrames A+B over 4 tics keeps chaingun-rate damage with both saw frames.
        static readonly WeaponDef Chainsaw = new WeaponDef
        {
            Id = WeaponId.Chainsaw, Slot = 1, Ammo = AmmoType.None, AmmoPerShot = 0,
            Pellets = 1, Melee = true, FirstShotAccurate = false,
            Sprite = "SAWG", IdleFrame = 2,
            FireFrames = new[] { 0, 1 }, FireTics = new[] { 2, 2 },
            FlashSprite = null, FlashFrames = System.Array.Empty<int>(),
            FlashTics = System.Array.Empty<int>(),
            ActionTic = 0, RefireTics = 0, FlashDelayTic = 0, RandomFlash = false,
            FireSound = "DSSAWFUL",
        };

        // S_PLASMA A 3 A_FirePlasma → S_PLASMA2 B 20 A_ReFire.
        // Held fire restarts at 3 tics; release plays out the 20-tic B frame.
        // Flash is one of PLSF A/B chosen by P_Random()&1 for 4 tics.
        static readonly WeaponDef PlasmaRifle = new WeaponDef
        {
            Id = WeaponId.PlasmaRifle, Slot = 6, Ammo = AmmoType.Cells, AmmoPerShot = 1,
            Pellets = 0, Melee = false, FirstShotAccurate = true,
            Sprite = "PLSG", IdleFrame = 0,
            FireFrames = new[] { 0, 1 }, FireTics = new[] { 3, 20 },
            FlashSprite = "PLSF", FlashFrames = new[] { 0, 1 }, FlashTics = new[] { 4 },
            ActionTic = 0, RefireTics = 3, FlashDelayTic = 0, RandomFlash = true,
            FireSound = PlasmaRules.FireSound,
        };

        // S_BFG A 20 A_BFGsound → B 10 flash → B 10 A_FireBFG → B 20 A_ReFire.
        static readonly WeaponDef Bfg9000 = new WeaponDef
        {
            Id = WeaponId.Bfg9000, Slot = 7, Ammo = AmmoType.Cells, AmmoPerShot = 40,
            Pellets = 0, Melee = false, FirstShotAccurate = true,
            Sprite = "BFGG", IdleFrame = 0,
            FireFrames = new[] { 0, 1, 1, 1 }, FireTics = new[] { 20, 10, 10, 20 },
            FlashSprite = "BFGF", FlashFrames = new[] { 0, 1 }, FlashTics = new[] { 11, 6 },
            ActionTic = 30, RefireTics = 0, FlashDelayTic = 20, RandomFlash = false,
            FireSound = BfgRules.FireSound,
        };

        public static WeaponDef Get(WeaponId id) => id switch
        {
            WeaponId.Fist => Fist,
            WeaponId.Pistol => Pistol,
            WeaponId.Shotgun => Shotgun,
            WeaponId.Chaingun => Chaingun,
            WeaponId.RocketLauncher => RocketLauncher,
            WeaponId.Chainsaw => Chainsaw,
            WeaponId.PlasmaRifle => PlasmaRifle,
            WeaponId.Bfg9000 => Bfg9000,
            _ => Fist,
        };
    }
}
