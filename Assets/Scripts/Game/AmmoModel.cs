namespace Doom.Game
{
    /// Счётчики патронов игрока. Значения DOOM: старт 50 пуль,
    /// максимумы maxammo = 200 пуль / 50 дроби / 50 ракет (p_inter.c);
    /// с рюкзаком — 400 / 100 / 100.
    public sealed class AmmoModel
    {
        public const int MaxBullets = 200;
        public const int MaxShells = 50;
        public const int MaxRockets = 50;
        public const int MaxBulletsBackpack = 400;
        public const int MaxShellsBackpack = 100;
        public const int MaxRocketsBackpack = 100;
        public const int StartBullets = 50;
        public const int BackpackClipBullets = 10;
        public const int BackpackClipShells = 4;
        public const int BackpackClipRockets = 1;

        int bullets = StartBullets;
        int shells;
        int rockets;
        public bool HasBackpack { get; private set; }

        public int Get(AmmoType t) => t switch
        {
            AmmoType.Bullets => bullets,
            AmmoType.Shells => shells,
            AmmoType.Rockets => rockets,
            _ => 0,
        };

        public int GetMax(AmmoType t) => t switch
        {
            AmmoType.Bullets => HasBackpack ? MaxBulletsBackpack : MaxBullets,
            AmmoType.Shells => HasBackpack ? MaxShellsBackpack : MaxShells,
            AmmoType.Rockets => HasBackpack ? MaxRocketsBackpack : MaxRockets,
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

        /// Doubles max ammo (idempotent) and always grants a clip of each type.
        /// Always returns true so ItemRules accepts every backpack pickup.
        public bool GiveBackpack()
        {
            HasBackpack = true;
            Add(AmmoType.Bullets, BackpackClipBullets);
            Add(AmmoType.Shells, BackpackClipShells);
            Add(AmmoType.Rockets, BackpackClipRockets);
            return true;
        }

        public void Reset()
        {
            bullets = StartBullets;
            shells = 0;
            rockets = 0;
            HasBackpack = false;
        }

        /// Authoritative restore for carry-over / save. Counts are clamped to the
        /// max implied by <paramref name="hasBackpack"/>.
        public void Restore(int bulletCount, int shellCount, bool hasBackpack)
            => Restore(bulletCount, shellCount, 0, hasBackpack);

        public void Restore(int bulletCount, int shellCount, int rocketCount, bool hasBackpack)
        {
            HasBackpack = hasBackpack;
            bullets = Clamp(bulletCount, GetMax(AmmoType.Bullets));
            shells = Clamp(shellCount, GetMax(AmmoType.Shells));
            rockets = Clamp(rocketCount, GetMax(AmmoType.Rockets));
        }

        public void Capture(out int bulletCount, out int shellCount, out bool hasBackpack)
        {
            bulletCount = bullets;
            shellCount = shells;
            hasBackpack = HasBackpack;
        }

        public void Capture(
            out int bulletCount, out int shellCount, out int rocketCount, out bool hasBackpack)
        {
            bulletCount = bullets;
            shellCount = shells;
            rocketCount = rockets;
            hasBackpack = HasBackpack;
        }

        static int Clamp(int v, int max)
        {
            if (v < 0) return 0;
            return v > max ? max : v;
        }

        void Set(AmmoType t, int v)
        {
            if (t == AmmoType.Bullets) bullets = v;
            else if (t == AmmoType.Shells) shells = v;
            else if (t == AmmoType.Rockets) rockets = v;
        }
    }
}
