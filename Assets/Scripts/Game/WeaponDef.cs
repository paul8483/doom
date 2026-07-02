namespace Doom.Game
{
    /// Статические данные одного оружия. Кадры — индексы в спрайте viewmodel
    /// (0='A'), тики — DOOM-тики (35/с). CycleTics = сумма FireTics = кулдаун
    /// между выстрелами. Урон наносится в момент нажатия (упрощение против
    /// DOOM, где A_Fire* срабатывает на 3–10-м тике последовательности).
    public sealed class WeaponDef
    {
        public WeaponId Id;
        public int Slot;                 // клавиши 1..4
        public AmmoType Ammo;
        public int AmmoPerShot;          // 0 для кулака
        public int Pellets;              // лучей за выстрел (дробовик 7)
        public bool Melee;               // кулак: дальность 64 юнита
        public bool FirstShotAccurate;   // пистолет: без разброса вне очереди
        public string Sprite;            // viewmodel, напр. "PISG"
        public int IdleFrame;            // кадр покоя (0)
        public int[] FireFrames;         // последовательность кадров выстрела
        public int[] FireTics;           // тики каждого кадра
        public string FlashSprite;       // null — без вспышки (кулак)
        public int[] FlashFrames;
        public int[] FlashTics;

        public int CycleTics
        {
            get { int s = 0; foreach (int t in FireTics) s += t; return s; }
        }
    }
}
