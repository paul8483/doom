namespace Doom.Game
{
    /// Статические данные одного оружия. Кадры — индексы в спрайте viewmodel
    /// (0='A'), тики — DOOM-тики (35/с). CycleTics = сумма FireTics.
    /// ActionTic — момент расхода ammo / spawn projectile от начала атаки;
    /// RefireTics — интервал удерживаемого огня (0 = CycleTics).
    public sealed class WeaponDef
    {
        public WeaponId Id;
        public int Slot;                 // клавиши 1..7
        public AmmoType Ammo;
        public int AmmoPerShot;          // 0 для кулака
        public int Pellets;              // лучей за выстрел (дробовик 7); 0 — projectile
        public bool Melee;               // кулак: дальность 64 юнита
        public bool FirstShotAccurate;   // пистолет: без разброса вне очереди
        public string Sprite;            // viewmodel, напр. "PISG"
        public int IdleFrame;            // кадр покоя (0)
        public int[] FireFrames;         // последовательность кадров выстрела
        public int[] FireTics;           // тики каждого кадра
        public string FlashSprite;       // null — без вспышки (кулак)
        public int[] FlashFrames;
        public int[] FlashTics;

        /// Tic offset from attack start when ammo is spent and the shot commits.
        /// Immediate weapons use 0; BFG uses 30 (after sound+flash charge).
        public int ActionTic;

        /// Held-fire interval in tics. 0 means use CycleTics (legacy weapons).
        public int RefireTics;

        /// Delay before muzzle flash starts (BFG flash begins with A_GunFlash at tic 20).
        public int FlashDelayTic;

        /// When true, WeaponView picks one FlashFrames entry via DoomRandom (&1).
        public bool RandomFlash;

        /// DMX lump name for the fire sound (e.g. "DSPISTOL"). Never null for E1 weapons.
        public string FireSound;

        public int CycleTics
        {
            get { int s = 0; foreach (int t in FireTics) s += t; return s; }
        }

        /// Effective held-fire cooldown in tics.
        public int EffectiveRefireTics => RefireTics > 0 ? RefireTics : CycleTics;
    }
}
