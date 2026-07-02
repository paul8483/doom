namespace Doom.Game
{
    /// Арсенал игрока: чем владеет, что в руках. Старт DOOM — кулак+пистолет.
    public sealed class WeaponLoadout
    {
        readonly bool[] owned = new bool[4];
        public WeaponId Current { get; private set; }

        public WeaponLoadout() { Reset(); }

        public bool Has(WeaponId id) => owned[(int)id];

        /// true если оружие новое (и тогда авто-переключение на него, как в DOOM).
        public bool Give(WeaponId id)
        {
            if (owned[(int)id]) return false;
            owned[(int)id] = true;
            Current = id;
            return true;
        }

        public bool TrySelect(WeaponId id)
        {
            if (!owned[(int)id]) return false;
            Current = id;
            return true;
        }

        /// Лучшее оружие, на которое хватает патронов (порядок P_CheckAmmo).
        public WeaponId BestAvailable(AmmoModel ammo)
        {
            foreach (var id in new[] { WeaponId.Chaingun, WeaponId.Shotgun, WeaponId.Pistol })
            {
                var def = WeaponTable.Get(id);
                if (owned[(int)id] && ammo.Get(def.Ammo) >= def.AmmoPerShot) return id;
            }
            return WeaponId.Fist;
        }

        public void Reset()
        {
            for (int i = 0; i < owned.Length; i++) owned[i] = false;
            owned[(int)WeaponId.Fist] = true;
            owned[(int)WeaponId.Pistol] = true;
            Current = WeaponId.Pistol;
        }
    }
}
