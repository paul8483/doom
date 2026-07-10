namespace Doom.Game
{
    /// Deterministic status-bar face state machine. Advanced in gameplay tics.
    /// Priority: dead &gt; rapid damage (ouch) &gt; directional pain &gt; evil grin &gt; idle look.
    public sealed class FaceState
    {
        enum Mode : byte { Idle, Dead, Ouch, Turn, EvilGrin, Rampage }

        Mode mode;
        int ticsLeft;
        FaceLook look;
        int lookPhase;
        int straightLeft;
        int lastHealth = HealthModel.MaxHealth;

        public string PatchName { get; private set; } = FaceRules.IdlePatch(HealthModel.MaxHealth, FaceLook.Center);

        public void Reset(int health = HealthModel.MaxHealth)
        {
            mode = Mode.Idle;
            ticsLeft = 0;
            look = FaceLook.Center;
            lookPhase = 0;
            straightLeft = FaceRules.StraightTics;
            lastHealth = health;
            PatchName = FaceRules.IdlePatch(health, look);
        }

        public void OnDeath()
        {
            mode = Mode.Dead;
            ticsLeft = int.MaxValue;
            PatchName = FaceRules.DeadPatch;
        }

        /// <paramref name="hpLost"/> is HP removed after armor. Side None → neutral ouch.
        public void OnDamage(int healthAfter, int hpLost, FaceAttackerSide side)
        {
            if (mode == Mode.Dead) return;
            lastHealth = healthAfter;

            if (hpLost >= FaceRules.MuchPain)
            {
                mode = Mode.Ouch;
                ticsLeft = FaceRules.OuchTics;
                PatchName = FaceRules.OuchPatch(healthAfter);
                return;
            }

            if (side != FaceAttackerSide.None)
            {
                // Directional cannot interrupt ouch (higher priority).
                if (mode == Mode.Ouch && ticsLeft > 0) return;
                mode = Mode.Turn;
                ticsLeft = FaceRules.TurnTics;
                PatchName = FaceRules.TurnPatch(healthAfter, side);
                return;
            }

            // Neutral small hit: brief ouch, but do not interrupt an active ouch.
            if (mode == Mode.Ouch && ticsLeft > 0) return;
            mode = Mode.Ouch;
            ticsLeft = FaceRules.OuchTics;
            PatchName = FaceRules.OuchPatch(healthAfter);
        }

        public void OnWeaponPickup(int health)
        {
            if (mode == Mode.Dead) return;
            lastHealth = health;
            // Lower than ouch/turn/rampage.
            if (mode == Mode.Ouch || mode == Mode.Turn || mode == Mode.Rampage)
                return;

            mode = Mode.EvilGrin;
            ticsLeft = FaceRules.EvilGrinTics;
            PatchName = FaceRules.EvilGrinPatch(health);
        }

        public void OnRampage(int health)
        {
            if (mode == Mode.Dead) return;
            lastHealth = health;
            if (mode == Mode.Ouch || mode == Mode.Turn) return;

            mode = Mode.Rampage;
            ticsLeft = FaceRules.RampageTics;
            PatchName = FaceRules.RampagePatch(health);
        }

        public void Advance(int tics, int health)
        {
            if (tics <= 0) return;
            lastHealth = health;

            if (mode == Mode.Dead)
            {
                PatchName = FaceRules.DeadPatch;
                return;
            }

            if (health <= 0)
            {
                OnDeath();
                return;
            }

            if (mode != Mode.Idle)
            {
                ticsLeft -= tics;
                if (ticsLeft > 0)
                {
                    RefreshActivePatch(health);
                    return;
                }

                mode = Mode.Idle;
                look = FaceLook.Center;
                lookPhase = 0;
                straightLeft = FaceRules.StraightTics;
            }

            straightLeft -= tics;
            while (straightLeft <= 0)
            {
                lookPhase = (lookPhase + 1) % 3;
                look = (FaceLook)lookPhase;
                straightLeft += FaceRules.StraightTics;
            }

            PatchName = FaceRules.IdlePatch(health, look);
        }

        void RefreshActivePatch(int health)
        {
            PatchName = mode switch
            {
                Mode.Ouch => FaceRules.OuchPatch(health),
                Mode.Turn => PatchName, // keep chosen L/R; health band may change
                Mode.EvilGrin => FaceRules.EvilGrinPatch(health),
                Mode.Rampage => FaceRules.RampagePatch(health),
                _ => FaceRules.IdlePatch(health, look),
            };

            // Re-resolve turn side from current patch prefix if health band changed.
            if (mode == Mode.Turn)
            {
                bool left = PatchName != null && PatchName.StartsWith("STFTL");
                PatchName = FaceRules.TurnPatch(
                    health, left ? FaceAttackerSide.Left : FaceAttackerSide.Right);
            }
        }
    }
}
