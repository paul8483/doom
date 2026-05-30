using System.Collections.Generic;
using Doom.Wad;

namespace Doom.Graphics
{
    /// One sprite lump reference for a given (sprite, frame, rotation).
    public readonly struct SpriteFrameRef
    {
        public readonly int LumpIndex;
        public readonly bool Mirrored;
        public SpriteFrameRef(int lumpIndex, bool mirrored)
        {
            LumpIndex = lumpIndex; Mirrored = mirrored;
        }
    }

    /// Catalog of the sprite lumps between S_START/S_END (and SS_START/SS_END).
    /// Parses the DOOM sprite naming (frame letter + rotation digit, optional
    /// mirrored second pair) into a (sprite, frame) → 8-rotation lookup.
    public sealed class SpriteSet
    {
        // Per (sprite, frame): 8 rotation slots. Each slot: lump index (-1 = none)
        // and a mirror flag.
        private sealed class FrameRots
        {
            public readonly int[] Lump = { -1, -1, -1, -1, -1, -1, -1, -1 };
            public readonly bool[] Flip = new bool[8];
        }

        private readonly Dictionary<(string sprite, int frame), FrameRots> frames = new();

        public static SpriteSet Load(WadFile wad)
        {
            var set = new SpriteSet();
            var dir = wad.Directory;
            bool inside = false;
            for (int i = 0; i < dir.Count; i++)
            {
                string name = dir[i].Name;
                if (name == "S_START" || name == "SS_START") { inside = true; continue; }
                if (name == "S_END" || name == "SS_END") { inside = false; continue; }
                if (!inside) continue;
                if (name.Length < 6) continue; // not a sprite name

                set.Register(name.Substring(0, 4), name[4], name[5], i, mirrored: false);
                if (name.Length >= 8)
                    set.Register(name.Substring(0, 4), name[6], name[7], i, mirrored: true);
            }
            return set;
        }

        private void Register(string sprite, char frameChar, char rotChar,
                              int lumpIndex, bool mirrored)
        {
            int frame = frameChar - 'A';
            if (frame < 0) return;
            var key = (sprite, frame);
            if (!frames.TryGetValue(key, out var fr))
            {
                fr = new FrameRots();
                frames[key] = fr;
            }

            if (rotChar == '0')
            {
                // All-angle frame: every rotation slot uses this lump.
                for (int r = 0; r < 8; r++) { fr.Lump[r] = lumpIndex; fr.Flip[r] = false; }
                return;
            }

            int rot = rotChar - '1'; // '1'..'8' → 0..7
            if (rot < 0 || rot > 7) return;
            fr.Lump[rot] = lumpIndex;
            fr.Flip[rot] = mirrored;
        }

        /// rotationIndex is 0..7 (0 = DOOM rotation '1'). Returns false if the
        /// (sprite, frame) is unknown or that rotation slot is empty.
        public bool TryGet(string sprite, int frame, int rotationIndex, out SpriteFrameRef result)
        {
            result = default;
            if (sprite == null) return false;
            if (rotationIndex < 0 || rotationIndex > 7) return false;
            if (!frames.TryGetValue((sprite.ToUpperInvariant(), frame), out var fr)) return false;
            int lump = fr.Lump[rotationIndex];
            if (lump < 0) return false;
            result = new SpriteFrameRef(lump, fr.Flip[rotationIndex]);
            return true;
        }
    }
}
