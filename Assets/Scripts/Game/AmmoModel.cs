namespace Doom.Game
{
    /// Счётчики патронов игрока. Значения DOOM: старт 50 пуль,
    /// максимумы maxammo = 200 пуль / 50 дроби (p_inter.c, без рюкзака).
    public sealed class AmmoModel
    {
        public const int MaxBullets = 200;
        public const int MaxShells = 50;
        public const int StartBullets = 50;

        int bullets = StartBullets;
        int shells;

        public int Get(AmmoType t) => t switch
        {
            AmmoType.Bullets => bullets,
            AmmoType.Shells => shells,
            _ => 0,
        };

        public int GetMax(AmmoType t) => t switch
        {
            AmmoType.Bullets => MaxBullets,
            AmmoType.Shells => MaxShells,
            _ => 0,
        };

        /// true — что-то добавилось; false — уже полно (вещь не подбирается).
        public bool Add(AmmoType t, int n)
        {
            int cur = Get(t), max = GetMax(t);
            if (t == AmmoType.None || cur >= max) return false;
            Set(t, System.Math.Min(cur + n, max));
            return true;
        }

        public bool TryConsume(AmmoType t, int n)
        {
            if (t == AmmoType.None) return true;
            if (Get(t) < n) return false;
            Set(t, Get(t) - n);
            return true;
        }

        public void Reset() { bullets = StartBullets; shells = 0; }

        void Set(AmmoType t, int v)
        {
            if (t == AmmoType.Bullets) bullets = v;
            else if (t == AmmoType.Shells) shells = v;
        }
    }
}
