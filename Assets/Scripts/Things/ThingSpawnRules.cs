namespace Doom.Things
{
    /// P_SpawnMapThing filters on the THINGS options word: a thing spawns only
    /// when its skill bit is set, and never in single player when it carries
    /// MTF_NOTSINGLE (multiplayer-only). The port spawned every map thing
    /// regardless, so all three skill layers and the deathmatch extras stood
    /// on the map at once (and counted toward the kill / item totals).
    public static class ThingSpawnRules
    {
        public const int SkillEasyBit = 0x1;    // baby / easy
        public const int SkillMediumBit = 0x2;  // hurt me plenty
        public const int SkillHardBit = 0x4;    // ultra-violence / nightmare
        public const int Ambush = 0x8;
        public const int NotSingle = 0x10;

        /// The port has no difficulty menu; it plays Ultra-Violence (the hard
        /// skill bit), which keeps the largest roster the map author placed.
        public const int DefaultSkillBit = SkillHardBit;

        public static bool ShouldSpawnSinglePlayer(int flags, int skillBit = DefaultSkillBit)
        {
            if ((flags & NotSingle) != 0) return false;
            return (flags & skillBit) != 0;
        }
    }
}
