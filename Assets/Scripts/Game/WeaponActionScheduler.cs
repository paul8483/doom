namespace Doom.Game
{
    /// Pure attack timeline for one weapon press/hold. Unity advances tics;
    /// this class decides when the view starts and when ammo/projectile commit.
    public sealed class WeaponActionScheduler
    {
        WeaponDef active;
        int tic;
        bool committed;
        bool running;

        public bool IsRunning => running;
        public bool IsCommitted => running && committed;
        public WeaponDef Active => active;
        public int TicsElapsed => tic;

        /// True when a new attack may begin (idle, or past EffectiveRefireTics of
        /// the current committed attack — plasma held-fire window).
        public bool CanBegin(WeaponDef def)
        {
            if (def == null) return false;
            if (!running) return true;
            if (!committed) return false;
            return tic >= def.EffectiveRefireTics;
        }

        /// Starts an attack if ammo is available and the scheduler allows it.
        /// When ActionTic == 0, marks committed immediately (ammo not spent here).
        public bool TryBegin(WeaponDef def, AmmoModel ammo)
        {
            if (def == null || ammo == null) return false;
            if (!CanBegin(def)) return false;
            if (def.Ammo != AmmoType.None && ammo.Get(def.Ammo) < def.AmmoPerShot)
                return false;

            active = def;
            tic = 0;
            running = true;
            committed = def.ActionTic <= 0;
            return true;
        }

        /// Advance one DOOM tic. Sets <paramref name="justCommitted"/> when this
        /// tic crosses ActionTic. Sets <paramref name="justFinished"/> when the
        /// full CycleTics view sequence ends.
        public void Advance(out bool justCommitted, out bool justFinished)
        {
            justCommitted = false;
            justFinished = false;
            if (!running || active == null) return;

            tic++;
            if (!committed && tic >= active.ActionTic)
            {
                committed = true;
                justCommitted = true;
            }

            if (tic >= active.CycleTics)
            {
                running = false;
                justFinished = true;
            }
        }

        /// Cancel an in-flight attack (death / reset). Uncommitted BFG charge
        /// spends no ammo because consume happens only on commit in the glue.
        public void Cancel()
        {
            active = null;
            tic = 0;
            committed = false;
            running = false;
        }

        /// Snapshot fields for save schema v4.
        public void Capture(out bool isRunning, out WeaponId weapon, out int ticsElapsed,
                            out bool isCommitted)
        {
            isRunning = running;
            weapon = active?.Id ?? WeaponId.Fist;
            ticsElapsed = tic;
            isCommitted = committed;
        }

        public bool TryRestore(WeaponDef def, int ticsElapsed, bool isCommitted)
        {
            if (def == null) return false;
            if (ticsElapsed < 0 || ticsElapsed > def.CycleTics) return false;
            if (isCommitted && ticsElapsed < def.ActionTic) return false;
            if (!isCommitted && ticsElapsed >= def.ActionTic && def.ActionTic > 0)
                return false;

            active = def;
            tic = ticsElapsed;
            committed = isCommitted || def.ActionTic <= 0;
            running = ticsElapsed < def.CycleTics;
            return true;
        }
    }
}
