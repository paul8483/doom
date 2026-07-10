namespace Doom.Game
{
    /// Арсенал игрока: чем владеет, что в руках. Старт DOOM — кулак+пистолет.
    public sealed class WeaponLoadout
    {
        readonly bool[] owned = new bool[4];
        public WeaponId Current { get; private set; }
        /// Queued weapon switch (save contract); cleared on Reset / successful select.
        WeaponId? pending;

        public WeaponLoadout() { Reset(); }

        public bool Has(WeaponId id) => owned[(int)id];
        public bool HasPending => pending.HasValue;
        public WeaponId Pending =>
            pending ?? throw new System.InvalidOperationException("No pending weapon.");

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
            pending = null;
            return true;
        }

        /// Queue a switch for later (e.g. mid-cooldown). Must be owned.
        public bool TryQueuePending(WeaponId id)
        {
            if (!owned[(int)id]) return false;
            pending = id;
            return true;
        }

        public void ClearPending() => pending = null;

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
            pending = null;
        }

        /// Authoritative restore for carry-over / save. Fist is always owned.
        /// If <paramref name="current"/> is not owned, falls back to BestAvailable order
        /// using a temporary ammo-agnostic preference (pistol if owned, else fist).
        public void Restore(
            bool fist, bool pistol, bool shotgun, bool chaingun, WeaponId current)
        {
            Restore(fist, pistol, shotgun, chaingun, current, pendingWeapon: null);
        }

        public void Restore(
            bool fist, bool pistol, bool shotgun, bool chaingun,
            WeaponId current, WeaponId? pendingWeapon)
        {
            owned[(int)WeaponId.Fist] = true; // always
            owned[(int)WeaponId.Pistol] = pistol;
            owned[(int)WeaponId.Shotgun] = shotgun;
            owned[(int)WeaponId.Chaingun] = chaingun;
            _ = fist; // fist flag accepted for DTO symmetry; ownership forced true

            if (owned[(int)current])
                Current = current;
            else if (owned[(int)WeaponId.Pistol])
                Current = WeaponId.Pistol;
            else
                Current = WeaponId.Fist;

            if (pendingWeapon.HasValue && owned[(int)pendingWeapon.Value])
                pending = pendingWeapon;
            else
                pending = null;
        }

        public void Capture(
            out bool fist, out bool pistol, out bool shotgun, out bool chaingun,
            out WeaponId current, out WeaponId? pendingWeapon)
        {
            fist = owned[(int)WeaponId.Fist];
            pistol = owned[(int)WeaponId.Pistol];
            shotgun = owned[(int)WeaponId.Shotgun];
            chaingun = owned[(int)WeaponId.Chaingun];
            current = Current;
            pendingWeapon = pending;
        }
    }
}
