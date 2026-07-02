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
        };

        static readonly WeaponDef Pistol = new WeaponDef
        {
            Id = WeaponId.Pistol, Slot = 2, Ammo = AmmoType.Bullets, AmmoPerShot = 1,
            Pellets = 1, Melee = false, FirstShotAccurate = true,
            Sprite = "PISG", IdleFrame = 0,
            FireFrames = new[] { 0, 1, 2, 1 }, FireTics = new[] { 4, 6, 4, 5 },
            FlashSprite = "PISF", FlashFrames = new[] { 0 }, FlashTics = new[] { 7 },
        };

        static readonly WeaponDef Shotgun = new WeaponDef
        {
            Id = WeaponId.Shotgun, Slot = 3, Ammo = AmmoType.Shells, AmmoPerShot = 1,
            Pellets = 7, Melee = false, FirstShotAccurate = false,
            Sprite = "SHTG", IdleFrame = 0,
            FireFrames = new[] { 0, 0, 1, 2, 3, 2, 1, 0, 0 },
            FireTics = new[] { 3, 7, 5, 5, 4, 5, 5, 3, 7 },
            FlashSprite = "SHTF", FlashFrames = new[] { 0, 1 }, FlashTics = new[] { 4, 3 },
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
        };

        public static WeaponDef Get(WeaponId id) => id switch
        {
            WeaponId.Fist => Fist,
            WeaponId.Pistol => Pistol,
            WeaponId.Shotgun => Shotgun,
            _ => Chaingun,
        };
    }
}
