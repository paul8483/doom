namespace Doom.Specials
{
    /// Ported DOOM damaging-sector classification (P_PlayerInSpecialSector). Maps a
    /// Sector.Special to the damage applied per ~0.9s tic while the player stands on
    /// that sector's floor. 0 = not a damaging sector.
    public static class SectorDamageTable
    {
        public static int DamagePerTick(int special) => special switch
        {
            7 => 5,    // nukage
            5 => 10,   // hellslime
            4 => 20,   // strobe + hurt
            16 => 20,  // super hellslime
            11 => 20,  // exit super damage (the level-exit on low HP is deferred to Stage 7)
            _ => 0,
        };
    }
}
