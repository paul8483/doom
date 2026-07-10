namespace Doom.Game
{
    /// P_KillMobj drop table for E1 zombies (linuxdoom-1.10).
    public static class DeathDropTable
    {
        public static bool TryGet(int monsterDoomEdNum, out int dropDoomEdNum)
        {
            switch (monsterDoomEdNum)
            {
                case 3004: dropDoomEdNum = 2007; return true; // POSS → CLIP
                case 9:    dropDoomEdNum = 2001; return true; // SPOS → SHOTGUN
                default:   dropDoomEdNum = 0; return false;
            }
        }
    }
}
