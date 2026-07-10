using System.Collections.Generic;

namespace Doom.Things
{
    /// Ported DOOM mobjinfo, keyed by doomednum (THINGS.Type). Spawn points
    /// (player 1–4, deathmatch 11) are excluded — the spawner filters them.
    public static class ThingTable
    {
        private static readonly Dictionary<int, ThingDef> Defs = Build();

        public static bool TryGet(int doomEdNum, out ThingDef def)
            => Defs.TryGetValue(doomEdNum, out def);

        public static IEnumerable<ThingDef> All => Defs.Values;

        private static Dictionary<int, ThingDef> Build()
        {
            const ThingFlags Mon = ThingFlags.Solid | ThingFlags.Shootable | ThingFlags.CountKill;
            const ThingFlags Sol = ThingFlags.Solid;
            const ThingFlags Hang = ThingFlags.SpawnCeiling;
            const ThingFlags HangSol = ThingFlags.Solid | ThingFlags.SpawnCeiling;
            const ThingFlags None = ThingFlags.None;

            var d = new Dictionary<int, ThingDef>();
            void Add(int n, string s, int f, int r, int h, ThingFlags fl,
                     int health = 0, int corpseFrame = -1)
                => d[n] = new ThingDef(n, s, f, r, h, fl, health, corpseFrame);

            // ── Monsters ──────────────────────────────────────────────────────
            Add(3004, "POSS", 0, 20, 56, Mon, health: 20, corpseFrame: 11);   // zombieman
            Add(9,    "SPOS", 0, 20, 56, Mon, health: 30, corpseFrame: 11);   // shotgun guy
            Add(65,   "CPOS", 0, 20, 56, Mon);   // chaingunner
            Add(3001, "TROO", 0, 20, 56, Mon, health: 60, corpseFrame: 12);   // imp
            Add(3002, "SARG", 0, 30, 56, Mon, health: 150, corpseFrame: 13);   // demon
            Add(58,   "SARG", 0, 30, 56, Mon, health: 150, corpseFrame: 13);   // spectre
            Add(3006, "SKUL", 0, 16, 56, ThingFlags.Solid | ThingFlags.Shootable); // lost soul
            Add(3005, "HEAD", 0, 31, 56, Mon);   // cacodemon
            Add(3003, "BOSS", 0, 24, 64, Mon, health: 1000, corpseFrame: 14);  // baron of hell
            Add(69,   "BOS2", 0, 24, 64, Mon);   // hell knight
            Add(68,   "BSPI", 0, 64, 64, Mon);   // arachnotron
            Add(71,   "PAIN", 0, 31, 56, Mon);   // pain elemental
            Add(66,   "SKEL", 0, 20, 56, Mon);   // revenant
            Add(67,   "FATT", 0, 48, 64, Mon);   // mancubus
            Add(64,   "VILE", 0, 20, 56, Mon);   // arch-vile
            Add(7,    "SPID", 0, 128, 100, Mon); // spider mastermind
            Add(16,   "CYBR", 0, 40, 110, Mon);  // cyberdemon
            Add(88,   "BBRN", 0, 16, 16, ThingFlags.Solid | ThingFlags.Shootable); // boss brain
            Add(84,   "SSWV", 0, 20, 56, Mon);   // wolfenstein SS
            Add(72,   "KEEN", 0, 16, 72, HangSol); // commander keen (hangs)

            // ── Weapons ───────────────────────────────────────────────────────
            Add(2005, "CSAW", 0, 20, 16, None);  // chainsaw
            Add(2001, "SHOT", 0, 20, 16, None);  // shotgun
            Add(82,   "SGN2", 0, 20, 16, None);  // super shotgun
            Add(2002, "MGUN", 0, 20, 16, None);  // chaingun
            Add(2003, "LAUN", 0, 20, 16, None);  // rocket launcher
            Add(2004, "PLAS", 0, 20, 16, None);  // plasma rifle
            Add(2006, "BFUG", 0, 20, 16, None);  // BFG9000

            // ── Ammo ──────────────────────────────────────────────────────────
            Add(2007, "CLIP", 0, 20, 16, None);  // ammo clip
            Add(2048, "AMMO", 0, 20, 16, None);  // box of bullets
            Add(2010, "ROCK", 0, 20, 16, None);  // rocket
            Add(2046, "BROK", 0, 20, 16, None);  // box of rockets
            Add(2047, "CELL", 0, 20, 16, None);  // cell charge
            Add(17,   "CELP", 0, 20, 16, None);  // cell pack
            Add(2008, "SHEL", 0, 20, 16, None);  // shotgun shells
            Add(2049, "SBOX", 0, 20, 16, None);  // box of shells
            Add(8,    "BPAK", 0, 20, 16, None);  // backpack

            // ── Powerups & health/armor ───────────────────────────────────────
            Add(2011, "STIM", 0, 20, 16, None);  // stimpack
            Add(2012, "MEDI", 0, 20, 16, None);  // medikit
            Add(2014, "BON1", 0, 20, 16, None);  // health bonus
            Add(2015, "BON2", 0, 20, 16, None);  // armor bonus
            Add(2018, "ARM1", 0, 20, 16, None);  // green armor
            Add(2019, "ARM2", 0, 20, 16, None);  // blue armor
            Add(2013, "SOUL", 0, 20, 16, None);  // soulsphere
            Add(2022, "PINV", 0, 20, 16, None);  // invulnerability
            Add(2023, "PSTR", 0, 20, 16, None);  // berserk
            Add(2024, "PINS", 0, 20, 16, None);  // blur (partial invis)
            Add(2025, "SUIT", 0, 20, 16, None);  // radiation suit
            Add(2026, "PMAP", 0, 20, 16, None);  // computer area map
            Add(2045, "PVIS", 0, 20, 16, None);  // light amp visor
            Add(83,   "MEGA", 0, 20, 16, None);  // megasphere

            // ── Keys ──────────────────────────────────────────────────────────
            Add(5,  "BKEY", 0, 20, 16, None);    // blue keycard
            Add(40, "BSKU", 0, 20, 16, None);    // blue skull key
            Add(13, "RKEY", 0, 20, 16, None);    // red keycard
            Add(38, "RSKU", 0, 20, 16, None);    // red skull key
            Add(6,  "YKEY", 0, 20, 16, None);    // yellow keycard
            Add(39, "YSKU", 0, 20, 16, None);    // yellow skull key

            // ── Solid obstacles (block the player) ────────────────────────────
            Add(2035, "BAR1", 0, 10, 42, ThingFlags.Solid | ThingFlags.Shootable); // barrel
            Add(70,   "FCAN", 0, 10, 42, Sol);   // burning barrel
            Add(43,   "TRE1", 0, 16, 64, Sol);   // burnt tree
            Add(54,   "TRE2", 0, 32, 64, Sol);   // large brown tree
            Add(47,   "SMIT", 0, 16, 64, Sol);   // stalagmite
            Add(48,   "ELEC", 0, 16, 128, Sol);  // tall techno pillar
            Add(35,   "CBRA", 0, 16, 64, Sol);   // candelabra
            Add(2028, "COLU", 0, 16, 48, Sol);   // floor lamp
            Add(30,   "COL1", 0, 16, 48, Sol);   // tall green pillar
            Add(31,   "COL2", 0, 16, 36, Sol);   // short green pillar
            Add(32,   "COL3", 0, 16, 48, Sol);   // tall red pillar
            Add(33,   "COL4", 0, 16, 36, Sol);   // short red pillar
            Add(36,   "COL5", 0, 16, 36, Sol);   // short green pillar (beating heart)
            Add(37,   "COL6", 0, 16, 40, Sol);   // short red pillar (skull)
            Add(41,   "CEYE", 0, 16, 54, Sol);   // evil eye
            Add(42,   "FSKU", 0, 16, 26, Sol);   // floating skull rock
            Add(44,   "TBLU", 0, 16, 32, Sol);   // tall blue firestick
            Add(45,   "TGRN", 0, 16, 32, Sol);   // tall green firestick
            Add(46,   "TRED", 0, 16, 32, Sol);   // tall red firestick
            Add(55,   "SMBT", 0, 16, 16, Sol);   // short blue firestick
            Add(56,   "SMGT", 0, 16, 16, Sol);   // short green firestick
            Add(57,   "SMRT", 0, 16, 16, Sol);   // short red firestick
            Add(25,   "POL1", 0, 16, 80, Sol);   // impaled human
            Add(26,   "POL6", 0, 16, 80, Sol);   // twitching impaled human
            Add(27,   "POL4", 0, 16, 80, Sol);   // skull on a pole
            Add(28,   "POL2", 0, 16, 80, Sol);   // 5 skulls shish kebab
            Add(29,   "POL3", 0, 16, 80, Sol);   // pile of skulls and candles

            // ── Hanging decorations (from ceiling) ────────────────────────────
            Add(49, "GOR1", 0, 16, 68, HangSol); // hanging victim, twitching
            Add(50, "GOR2", 0, 16, 84, HangSol); // hanging victim, arms out
            Add(51, "GOR3", 0, 16, 84, HangSol); // hanging victim, one-legged
            Add(52, "GOR4", 0, 16, 68, HangSol); // hanging pair of legs
            Add(53, "GOR5", 0, 16, 52, HangSol); // hanging leg
            Add(59, "GOR2", 0, 16, 84, Hang);    // hanging victim, arms out (non-solid)
            Add(60, "GOR4", 0, 16, 68, Hang);    // hanging pair of legs (non-solid)
            Add(61, "GOR3", 0, 16, 52, Hang);    // hanging victim, one-legged (non-solid)
            Add(62, "GOR5", 0, 16, 52, Hang);    // hanging leg (non-solid)
            Add(63, "GOR1", 0, 16, 68, Hang);    // hanging victim, twitching (non-solid)

            // ── Non-solid floor decorations ───────────────────────────────────
            Add(34, "CAND", 0, 20, 16, None);    // candle (floor lamp 2028 is in the obstacles group above)

            // ── Corpses / gibs (decorations; final death frame) ───────────────
            // Frame letters are the spawnstate frame from info.c death-end states.
            Add(15, "PLAY", 13, 16, 16, None);   // dead player (PLAY frame N)
            Add(18, "POSS", 11, 20, 16, None);   // dead former human (frame L)
            Add(19, "SPOS", 11, 20, 16, None);   // dead former sergeant (frame L)
            Add(20, "TROO", 12, 20, 16, None);   // dead imp (frame M)
            Add(21, "SARG", 13, 30, 16, None);   // dead demon (frame N)
            Add(22, "HEAD", 11, 31, 16, None);   // dead cacodemon (frame L)
            Add(23, "SKUL", 10, 16, 16, None);   // dead lost soul (frame K) — invisible in DOOM
            Add(10, "PLAY", 22, 16, 16, None);   // bloody mess (PLAY frame W)
            Add(12, "PLAY", 22, 16, 16, None);   // bloody mess 2
            Add(24, "POL5", 0, 16, 16, None);    // pool of blood and flesh
            Add(79, "POB1", 0, 16, 16, None);    // pool of blood
            Add(80, "POB2", 0, 16, 16, None);    // pool of blood 2
            Add(81, "BRS1", 0, 16, 16, None);    // pool of brains

            return d;
        }
    }
}
