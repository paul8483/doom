namespace Doom.Specials
{
    /// Pure rules for sector special 11 (damaging exit floor).
    public static class ExitSectorRules
    {
        public const int ExitDamageSpecial = 11;

        /// After floor damage is applied, exit when HP is 10 or below (DOOM
        /// P_PlayerInSpecialSector). Includes 0 HP.
        public static bool ShouldExitAfterDamage(int sectorSpecial, int healthAfterDamage) =>
            sectorSpecial == ExitDamageSpecial && healthAfterDamage <= 10;
    }
}
