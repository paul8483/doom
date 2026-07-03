namespace Doom.Things
{
    /// One animation sequence: sprite frame indices (0='A') + DOOM tics per entry.
    public sealed class MonsterSeq
    {
        public readonly int[] Frames;
        public readonly int[] Tics;
        public MonsterSeq(int[] frames, int[] tics) { Frames = frames; Tics = tics; }
    }

    /// Static combat/AI data for one monster (info.c + p_enemy.c, linuxdoom-1.10).
    /// Damage formulas are ((P_Random() % Mod) + 1) * Mult.
    public sealed class MonsterDef
    {
        public int Speed;            // DOOM units per A_Chase move turn
        public int PainChance;       // 0..255, roll on every damage
        public int ReactionMoves;    // move turns of delay after waking (reactiontime 8)

        public int MeleeMod;         // 0 = no melee attack
        public int MeleeMult;
        public int HitscanCount;     // bullets per volley (0 = none)
        public bool HasMissile;

        public int MissileSpeed;         // units/tic (imp fireball 10)
        public int MissileImpactMod;     // damage ((r%8)+1)*3
        public int MissileImpactMult;
        public int MissileRadius;        // units (6)
        public int MissileSpawnHeight;   // units above feet (32)
        public string MissileSprite;     // "BAL1"
        public int[] MissileFlyFrames;   // {0,1} loop @ MissileFlyTics
        public int[] MissileFlyTics;
        public int[] MissileExplodeFrames; // {2,3,4}
        public int[] MissileExplodeTics;

        public MonsterSeq Stand;     // loop, A_Look on each entry
        public MonsterSeq Run;       // loop, one move turn per entry
        public MonsterSeq Attack;    // one-shot; FaceTarget on entries before FireIndex
        public int FireIndex;        // damage/projectile happens entering this entry
        public MonsterSeq Pain;      // one-shot
        public MonsterSeq Death;     // one-shot; then ThingDef.CorpseFrame
    }
}
