using System;
using System.Collections.Generic;

namespace Doom.Graphics
{
    /// One resolved animation sequence (flat or wall). Frames are upper-case WAD names.
    public readonly struct TextureAnimationSequence : IEquatable<TextureAnimationSequence>
    {
        public readonly string BaseName;
        public readonly string[] Frames;
        public readonly int TicDuration;
        public readonly bool IsWall;

        public TextureAnimationSequence(
            string baseName, string[] frames, int ticDuration, bool isWall)
        {
            BaseName = baseName ?? throw new ArgumentNullException(nameof(baseName));
            Frames = frames ?? throw new ArgumentNullException(nameof(frames));
            TicDuration = ticDuration;
            IsWall = isWall;
        }

        public bool IsValid => Frames != null && Frames.Length >= 2;

        public bool Equals(TextureAnimationSequence other) =>
            BaseName == other.BaseName &&
            TicDuration == other.TicDuration &&
            IsWall == other.IsWall;

        public override bool Equals(object obj) =>
            obj is TextureAnimationSequence other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(BaseName, TicDuration, IsWall);
    }

    /// Builds DOOM-style texture/flat animation ranges from existing lump/texture names.
    /// Missing frames truncate or disable a sequence; they never throw.
    public sealed class TextureAnimationCatalog
    {
        /// Classic ANIMATED-style ranges (isWall, tics, ordered frame names).
        static readonly (bool isWall, int tics, string[] frames)[] KnownRanges =
        {
            // Flats
            (false, 8, new[] { "NUKAGE1", "NUKAGE2", "NUKAGE3" }),
            (false, 8, new[] { "FWATER1", "FWATER2", "FWATER3", "FWATER4" }),
            (false, 8, new[] { "SWATER1", "SWATER2", "SWATER3", "SWATER4" }),
            (false, 8, new[] { "LAVA1", "LAVA2", "LAVA3", "LAVA4" }),
            (false, 8, new[] { "BLOOD1", "BLOOD2", "BLOOD3" }),
            (false, 8, new[] { "RROCK05", "RROCK06", "RROCK07", "RROCK08" }),
            (false, 8, new[] { "SLIME01", "SLIME02", "SLIME03", "SLIME04" }),
            (false, 8, new[] { "SLIME05", "SLIME06", "SLIME07", "SLIME08" }),
            (false, 8, new[] { "SLIME09", "SLIME10", "SLIME11", "SLIME12" }),
            // Walls
            (true, 8, new[] { "BLODGR1", "BLODGR2", "BLODGR3", "BLODGR4" }),
            (true, 8, new[] { "SLADRIP1", "SLADRIP2", "SLADRIP3" }),
            (true, 8, new[] { "BLODRIP1", "BLODRIP2", "BLODRIP3", "BLODRIP4" }),
            (true, 8, new[] { "FIREBLU1", "FIREBLU2" }),
            (true, 8, new[] { "FIRELAV3", "FIRELAVA" }),
            (true, 8, new[] { "FIREMAG1", "FIREMAG2", "FIREMAG3" }),
            (true, 8, new[] { "FIREWALA", "FIREWALB", "FIREWALL" }),
            (true, 8, new[] { "GSTFONT1", "GSTFONT2", "GSTFONT3" }),
            (true, 8, new[] { "ROCKRED1", "ROCKRED2", "ROCKRED3" }),
            (true, 8, new[] { "BFALL1", "BFALL2", "BFALL3", "BFALL4" }),
            (true, 8, new[] { "SFALL1", "SFALL2", "SFALL3", "SFALL4" }),
            (true, 8, new[] { "WFALL1", "WFALL2", "WFALL3", "WFALL4" }),
            (true, 8, new[] { "DBRAIN1", "DBRAIN2", "DBRAIN3", "DBRAIN4" }),
        };

        readonly Dictionary<string, TextureAnimationSequence> byAnyFrame =
            new Dictionary<string, TextureAnimationSequence>(StringComparer.Ordinal);

        readonly List<TextureAnimationSequence> unique = new List<TextureAnimationSequence>();

        public IReadOnlyList<TextureAnimationSequence> Sequences => unique;
        public int SequenceCount => unique.Count;

        /// Resolve known ranges against <paramref name="exists"/>. A gap truncates
        /// the sequence at the first missing frame after at least one hit. Fewer
        /// than two present frames disables the sequence.
        public static TextureAnimationCatalog Build(Func<string, bool> exists)
        {
            if (exists == null) throw new ArgumentNullException(nameof(exists));
            var catalog = new TextureAnimationCatalog();

            foreach (var (isWall, tics, frames) in KnownRanges)
            {
                var present = ResolveFrames(frames, exists);
                if (present.Count < 2) continue;

                var arr = present.ToArray();
                var seq = new TextureAnimationSequence(arr[0], arr, tics, isWall);
                catalog.unique.Add(seq);
                foreach (var f in arr)
                    catalog.byAnyFrame[f] = seq;
            }

            return catalog;
        }

        public bool TryGet(string textureOrFlatName, out TextureAnimationSequence sequence)
        {
            sequence = default;
            if (string.IsNullOrEmpty(textureOrFlatName)) return false;
            return byAnyFrame.TryGetValue(textureOrFlatName.ToUpperInvariant(), out sequence);
        }

        /// Keep frames in order until the first gap after a present frame.
        public static List<string> ResolveFrames(string[] declared, Func<string, bool> exists)
        {
            if (declared == null) throw new ArgumentNullException(nameof(declared));
            if (exists == null) throw new ArgumentNullException(nameof(exists));

            var result = new List<string>(declared.Length);
            bool started = false;
            for (int i = 0; i < declared.Length; i++)
            {
                string name = declared[i].ToUpperInvariant();
                if (exists(name))
                {
                    result.Add(name);
                    started = true;
                }
                else if (started)
                {
                    break; // truncate at first hole
                }
            }
            return result;
        }

        /// Increment the trailing numeric/alpha suffix of a DOOM name (test helper).
        public static string IncrementName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            char[] chars = name.ToCharArray();
            for (int i = chars.Length - 1; i >= 0; i--)
            {
                char c = chars[i];
                if (c >= '0' && c <= '8')
                {
                    chars[i] = (char)(c + 1);
                    return new string(chars);
                }
                if (c == '9')
                {
                    chars[i] = '0';
                    continue;
                }
                if (c >= 'A' && c <= 'Y')
                {
                    chars[i] = (char)(c + 1);
                    return new string(chars);
                }
                if (c == 'Z')
                {
                    chars[i] = 'A';
                    continue;
                }
                return null;
            }
            return null;
        }
    }
}
