using System.Collections.Generic;

namespace Doom.Things
{
    /// doomednum -> MonsterDef for the E1 roster. Numbers from linuxdoom-1.10.
    public static class MonsterTable
    {
        static readonly Dictionary<int, MonsterDef> Defs = new Dictionary<int, MonsterDef>
        {
            [3004] = new MonsterDef // POSS, zombieman
            {
                Speed = 8, PainChance = 200, ReactionMoves = 8,
                MeleeMod = 0, MeleeMult = 0, HitscanCount = 1, HasMissile = false,
                Stand = new MonsterSeq(new[] { 0, 1 }, new[] { 10, 10 }),
                Run = new MonsterSeq(new[] { 0, 0, 1, 1, 2, 2, 3, 3 },
                                     new[] { 4, 4, 4, 4, 4, 4, 4, 4 }),
                Attack = new MonsterSeq(new[] { 4, 5, 4 }, new[] { 10, 8, 8 }),
                FireIndex = 1,
                Pain = new MonsterSeq(new[] { 6, 6 }, new[] { 3, 3 }),
                Death = new MonsterSeq(new[] { 7, 8, 9, 10 }, new[] { 5, 5, 5, 5 }),
                Sounds = new MonsterSoundSet
                {
                    Sight = new[] { "DSPOSIT1", "DSPOSIT2", "DSPOSIT3" },
                    Active = "DSPOSACT",
                    RangedAttack = "DSPISTOL",
                    MeleeAttack = null,
                    Pain = "DSPOPAIN",
                    Death = new[] { "DSPODTH1", "DSPODTH2", "DSPODTH3" },
                },
            },
            [9] = new MonsterDef // SPOS, shotgun guy
            {
                Speed = 8, PainChance = 170, ReactionMoves = 8,
                MeleeMod = 0, MeleeMult = 0, HitscanCount = 3, HasMissile = false,
                Stand = new MonsterSeq(new[] { 0, 1 }, new[] { 10, 10 }),
                Run = new MonsterSeq(new[] { 0, 0, 1, 1, 2, 2, 3, 3 },
                                     new[] { 3, 3, 3, 3, 3, 3, 3, 3 }),
                Attack = new MonsterSeq(new[] { 4, 5, 4 }, new[] { 10, 10, 10 }),
                FireIndex = 1,
                Pain = new MonsterSeq(new[] { 6, 6 }, new[] { 3, 3 }),
                Death = new MonsterSeq(new[] { 7, 8, 9, 10 }, new[] { 5, 5, 5, 5 }),
                Sounds = new MonsterSoundSet
                {
                    Sight = new[] { "DSPOSIT1", "DSPOSIT2", "DSPOSIT3" },
                    Active = "DSPOSACT",
                    RangedAttack = "DSSHOTGN",
                    MeleeAttack = null,
                    Pain = "DSPOPAIN",
                    Death = new[] { "DSPODTH1", "DSPODTH2", "DSPODTH3" },
                },
            },
            [3001] = new MonsterDef // TROO, imp
            {
                Speed = 8, PainChance = 200, ReactionMoves = 8,
                MeleeMod = 8, MeleeMult = 3, HitscanCount = 0, HasMissile = true,
                MissileSpeed = 10, MissileImpactMod = 8, MissileImpactMult = 3,
                MissileRadius = 6, MissileSpawnHeight = 32, MissileSprite = "BAL1",
                MissileFlyFrames = new[] { 0, 1 }, MissileFlyTics = new[] { 4, 4 },
                MissileExplodeFrames = new[] { 2, 3, 4 }, MissileExplodeTics = new[] { 6, 6, 6 },
                Stand = new MonsterSeq(new[] { 0, 1 }, new[] { 10, 10 }),
                Run = new MonsterSeq(new[] { 0, 0, 1, 1, 2, 2, 3, 3 },
                                     new[] { 3, 3, 3, 3, 3, 3, 3, 3 }),
                Attack = new MonsterSeq(new[] { 4, 5, 6 }, new[] { 8, 8, 6 }),
                FireIndex = 2,
                Pain = new MonsterSeq(new[] { 7, 7 }, new[] { 2, 2 }),
                Death = new MonsterSeq(new[] { 8, 9, 10, 11 }, new[] { 8, 8, 6, 6 }),
                Sounds = new MonsterSoundSet
                {
                    Sight = new[] { "DSBGSIT1", "DSBGSIT2" },
                    Active = "DSDMACT",
                    RangedAttack = "DSFIRSHT",
                    MeleeAttack = "DSCLAW",
                    Pain = "DSDMPAIN",
                    Death = new[] { "DSBGDTH1", "DSBGDTH2" },
                },
            },
            [3002] = new MonsterDef // SARG, demon
            {
                Speed = 10, PainChance = 180, ReactionMoves = 8,
                MeleeMod = 10, MeleeMult = 4, HitscanCount = 0, HasMissile = false,
                Stand = new MonsterSeq(new[] { 0, 1 }, new[] { 10, 10 }),
                Run = new MonsterSeq(new[] { 0, 0, 1, 1, 2, 2, 3, 3 },
                                     new[] { 2, 2, 2, 2, 2, 2, 2, 2 }),
                Attack = new MonsterSeq(new[] { 4, 5, 6 }, new[] { 8, 8, 8 }),
                FireIndex = 2,
                Pain = new MonsterSeq(new[] { 7, 7 }, new[] { 2, 2 }),
                Death = new MonsterSeq(new[] { 8, 9, 10, 11, 12 }, new[] { 8, 8, 4, 4, 4 }),
                Sounds = new MonsterSoundSet
                {
                    Sight = new[] { "DSSGTSIT" },
                    Active = "DSDMACT",
                    RangedAttack = null,
                    MeleeAttack = "DSSGTATK",
                    Pain = "DSDMPAIN",
                    Death = new[] { "DSSGTDTH" },
                },
            },
            // Spectre: same AI as demon (MF_SHADOW rendering deferred).
            [58] = new MonsterDef
            {
                Speed = 10, PainChance = 180, ReactionMoves = 8,
                MeleeMod = 10, MeleeMult = 4, HitscanCount = 0, HasMissile = false,
                Stand = new MonsterSeq(new[] { 0, 1 }, new[] { 10, 10 }),
                Run = new MonsterSeq(new[] { 0, 0, 1, 1, 2, 2, 3, 3 },
                                     new[] { 2, 2, 2, 2, 2, 2, 2, 2 }),
                Attack = new MonsterSeq(new[] { 4, 5, 6 }, new[] { 8, 8, 8 }),
                FireIndex = 2,
                Pain = new MonsterSeq(new[] { 7, 7 }, new[] { 2, 2 }),
                Death = new MonsterSeq(new[] { 8, 9, 10, 11, 12 }, new[] { 8, 8, 4, 4, 4 }),
                Sounds = new MonsterSoundSet
                {
                    Sight = new[] { "DSSGTSIT" },
                    Active = "DSDMACT",
                    RangedAttack = null,
                    MeleeAttack = "DSSGTATK",
                    Pain = "DSDMPAIN",
                    Death = new[] { "DSSGTDTH" },
                },
            },
            [3003] = new MonsterDef // BOSS, baron of hell
            {
                Speed = 8, PainChance = 50, ReactionMoves = 8,
                MeleeMod = 10, MeleeMult = 10, HitscanCount = 0, HasMissile = true,
                MissileSpeed = 15, MissileImpactMod = 8, MissileImpactMult = 8,
                MissileRadius = 6, MissileSpawnHeight = 32, MissileSprite = "BAL7",
                MissileFlyFrames = new[] { 0, 1 }, MissileFlyTics = new[] { 4, 4 },
                MissileExplodeFrames = new[] { 2, 3, 4 }, MissileExplodeTics = new[] { 6, 6, 6 },
                Stand = new MonsterSeq(new[] { 0, 1 }, new[] { 10, 10 }),
                Run = new MonsterSeq(new[] { 0, 0, 1, 1, 2, 2, 3, 3 },
                                     new[] { 3, 3, 3, 3, 3, 3, 3, 3 }),
                Attack = new MonsterSeq(new[] { 4, 5, 6 }, new[] { 8, 8, 8 }),
                FireIndex = 2,
                Pain = new MonsterSeq(new[] { 7, 7 }, new[] { 2, 2 }),
                Death = new MonsterSeq(new[] { 8, 9, 10, 11, 12, 13, 14 },
                                       new[] { 8, 8, 8, 8, 8, 8, 8 }),
                Sounds = new MonsterSoundSet
                {
                    Sight = new[] { "DSBRSSIT" },
                    Active = "DSDMACT",
                    RangedAttack = "DSFIRSHT",
                    MeleeAttack = "DSCLAW",
                    Pain = "DSDMPAIN",
                    Death = new[] { "DSBRSDTH" },
                },
            },
        };

        public static bool TryGet(int doomEdNum, out MonsterDef def)
            => Defs.TryGetValue(doomEdNum, out def);
    }
}
