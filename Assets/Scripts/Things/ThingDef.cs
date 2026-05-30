using System;

namespace Doom.Things
{
    /// Subset of DOOM's MF_* mobj flags relevant to Stage 5 (static rendering +
    /// blocking). Room is left for the rest; Stage 6 (AI/gameplay) extends this.
    [Flags]
    public enum ThingFlags
    {
        None         = 0,
        Solid        = 1 << 0, // MF_SOLID — blocks the player (collider)
        SpawnCeiling = 1 << 1, // MF_SPAWNCEILING — hangs from the ceiling
        Shootable    = 1 << 2, // MF_SHOOTABLE — monsters/barrels (Stage 6 uses it)
        CountKill    = 1 << 3, // MF_COUNTKILL — counts toward kill % (Stage 6)
    }

    /// One ported mobjinfo row, addressed by its map "doomednum" (THINGS.Type).
    /// Sprite is the 4-char sprite prefix; Frame is the spawn-state frame index
    /// (0 = 'A'). Radius/Height are DOOM units.
    public readonly struct ThingDef
    {
        public readonly int DoomEdNum;
        public readonly string Sprite;
        public readonly int Frame;
        public readonly int Radius;
        public readonly int Height;
        public readonly ThingFlags Flags;

        public ThingDef(int doomEdNum, string sprite, int frame,
                        int radius, int height, ThingFlags flags)
        {
            DoomEdNum = doomEdNum;
            Sprite = sprite;
            Frame = frame;
            Radius = radius;
            Height = height;
            Flags = flags;
        }

        public bool Has(ThingFlags f) => (Flags & f) != 0;
    }
}
