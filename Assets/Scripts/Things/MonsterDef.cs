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
    /// Immutable: MonsterTable hands out shared singleton instances.
    public sealed class MonsterDef
    {
        public int Speed { get; init; }            // DOOM units per A_Chase move turn
        public int PainChance { get; init; }       // 0..255, roll on every damage
        public int ReactionMoves { get; init; }    // move turns of delay after waking (reactiontime 8)

        public int MeleeMod { get; init; }         // 0 = no melee attack
        public int MeleeMult { get; init; }
        public int HitscanCount { get; init; }     // bullets per volley (0 = none)
        public bool HasMissile { get; init; }

        public int MissileSpeed { get; init; }         // units/tic (imp fireball 10)
        public int MissileImpactMod { get; init; }     // damage ((r%8)+1)*3
        public int MissileImpactMult { get; init; }
        public int MissileRadius { get; init; }        // units (6)
        public int MissileSpawnHeight { get; init; }   // units above feet (32)
        public string MissileSprite { get; init; }     // "BAL1"
        public int[] MissileFlyFrames { get; init; }   // {0,1} loop @ MissileFlyTics
        public int[] MissileFlyTics { get; init; }
        public int[] MissileExplodeFrames { get; init; } // {2,3,4}
        public int[] MissileExplodeTics { get; init; }

        public MonsterSeq Stand { get; init; }     // loop, A_Look on each entry
        public MonsterSeq Run { get; init; }       // loop, one move turn per entry
        public MonsterSeq Attack { get; init; }    // one-shot; FaceTarget on entries before FireIndex
        public int FireIndex { get; init; }        // damage/projectile happens entering this entry
        public MonsterSeq Pain { get; init; }      // one-shot
        public MonsterSeq Death { get; init; }     // one-shot; then ThingDef.CorpseFrame

        /// DMX lump names for this monster (Stage 6f). Never null for E1 roster.
        public MonsterSoundSet Sounds { get; init; }
    }

    /// Sound lump names for one monster. Arrays may have multiple variants.
    public sealed class MonsterSoundSet
    {
        public string[] Sight { get; init; }
        public string Active { get; init; }
        public string RangedAttack { get; init; }
        public string MeleeAttack { get; init; }
        public string Pain { get; init; }
        public string[] Death { get; init; }
    }
}
