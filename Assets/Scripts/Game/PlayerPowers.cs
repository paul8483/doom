namespace Doom.Game
{
    /// Timed / level-permanent powerups. Berserk (pw_strength) never ticks down;
    /// iron feet (radiation suit) counts down in DOOM tics.
    public sealed class PlayerPowers
    {
        public bool Berserk { get; private set; }
        public int IronFeetTics { get; private set; }

        public void GiveBerserk() => Berserk = true;

        public void GiveIronFeet(int durationTics)
        {
            if (durationTics > IronFeetTics)
                IronFeetTics = durationTics;
        }

        public void Advance(int tics)
        {
            if (tics <= 0 || IronFeetTics <= 0) return;
            IronFeetTics -= tics;
            if (IronFeetTics < 0) IronFeetTics = 0;
        }

        public void Reset()
        {
            Berserk = false;
            IronFeetTics = 0;
        }

        public void Capture(out bool berserk, out int ironFeetTics)
        {
            berserk = Berserk;
            ironFeetTics = IronFeetTics;
        }

        public void Restore(bool berserk, int ironFeetTics)
        {
            Berserk = berserk;
            IronFeetTics = ironFeetTics < 0 ? 0 : ironFeetTics;
        }
    }
}
