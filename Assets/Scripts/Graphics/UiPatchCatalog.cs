using System;
using System.Collections.Generic;
using Doom.Wad;

namespace Doom.Graphics
{
    /// Decoded UI patch (status bar, face, menu, intermission) with header offsets.
    /// Image is null for a recorded miss — never throws from the render loop.
    public readonly struct UiPatchInfo
    {
        public readonly string Name;
        public readonly int Width;
        public readonly int Height;
        public readonly int LeftOffset;
        public readonly int TopOffset;
        public readonly DecodedImage Image;

        public bool IsPresent => Image != null;

        public UiPatchInfo(string name, int width, int height, int left, int top, DecodedImage image)
        {
            Name = name;
            Width = width;
            Height = height;
            LeftOffset = left;
            TopOffset = top;
            Image = image;
        }

        public static UiPatchInfo Miss(string name) =>
            new UiPatchInfo(name, 0, 0, 0, 0, null);
    }

    /// Pure-C# catalog of named WAD patches for HUD/menu/intermission.
    /// All reads happen at <see cref="Load"/>; afterwards lookups never touch the WAD.
    public sealed class UiPatchCatalog
    {
        readonly Dictionary<string, UiPatchInfo> byName =
            new Dictionary<string, UiPatchInfo>(StringComparer.OrdinalIgnoreCase);

        /// Status-bar / face patches expected for a playable HUD.
        public static IReadOnlyList<string> StatusBarNames { get; } = BuildStatusBarNames();

        /// Optional menu / intermission / title lumps — miss is allowed.
        public static IReadOnlyList<string> OptionalUiNames { get; } = BuildOptionalUiNames();

        /// Load every name in <paramref name="names"/>. Missing lumps become recorded misses.
        public static UiPatchCatalog Load(WadFile wad, Palette palette, IEnumerable<string> names)
        {
            if (wad == null) throw new ArgumentNullException(nameof(wad));
            if (palette == null) throw new ArgumentNullException(nameof(palette));
            if (names == null) throw new ArgumentNullException(nameof(names));

            var catalog = new UiPatchCatalog();
            foreach (string raw in names)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                string key = raw.ToUpperInvariant();
                if (catalog.byName.ContainsKey(key)) continue;

                int idx = FindLumpIgnoreCase(wad, key);
                if (idx < 0)
                {
                    catalog.byName[key] = UiPatchInfo.Miss(key);
                    continue;
                }

                byte[] lump = wad.ReadLump(idx);
                var header = Patch.ReadHeader(lump);
                var image = Patch.Decode(lump, palette);
                catalog.byName[key] = new UiPatchInfo(
                    key, header.Width, header.Height,
                    header.LeftOffset, header.TopOffset, image);
            }

            return catalog;
        }

        /// Convenience: status bar + optional UI names in one pass.
        public static UiPatchCatalog LoadStandard(WadFile wad, Palette palette)
        {
            var names = new List<string>(StatusBarNames.Count + OptionalUiNames.Count);
            names.AddRange(StatusBarNames);
            names.AddRange(OptionalUiNames);
            return Load(wad, palette, names);
        }

        public bool TryGet(string name, out UiPatchInfo info)
        {
            if (string.IsNullOrEmpty(name))
            {
                info = default;
                return false;
            }

            if (!byName.TryGetValue(name, out info))
                return false;
            return info.IsPresent;
        }

        /// True if <paramref name="name"/> was requested at load and the lump was absent.
        public bool IsMiss(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return byName.TryGetValue(name, out var info) && !info.IsPresent;
        }

        public bool ContainsKey(string name) =>
            !string.IsNullOrEmpty(name) && byName.ContainsKey(name);

        public int Count => byName.Count;

        /// All load-time entries (present and misses).
        public IEnumerable<UiPatchInfo> Entries => byName.Values;

        static int FindLumpIgnoreCase(WadFile wad, string name)
        {
            int idx = wad.FindLump(name);
            if (idx >= 0) return idx;

            var dir = wad.Directory;
            for (int i = 0; i < dir.Count; i++)
            {
                if (string.Equals(dir[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        static List<string> BuildStatusBarNames()
        {
            var list = new List<string>(128)
            {
                "STBAR", "STARMS",
                "STTMINUS", "STTPRCNT",
                "STFGOD0", "STFDEAD0",
            };

            for (int i = 0; i <= 9; i++)
            {
                list.Add("STTNUM" + i);
                list.Add("STYSNUM" + i);
                list.Add("STGNUM" + i);
            }

            for (int i = 0; i <= 8; i++)
                list.Add("STKEYS" + i);

            // Idle faces: 5 health bands × 3 look directions.
            for (int pain = 0; pain <= 4; pain++)
                for (int look = 0; look <= 2; look++)
                    list.Add($"STFST{pain}{look}");

            for (int pain = 0; pain <= 4; pain++)
            {
                list.Add($"STFTL{pain}0");
                list.Add($"STFTR{pain}0");
                list.Add($"STFOUCH{pain}");
                list.Add($"STFEVL{pain}");
                list.Add($"STFKILL{pain}");
            }

            return list;
        }

        static List<string> BuildOptionalUiNames()
        {
            var list = new List<string>(64)
            {
                "TITLEPIC", "INTERPIC",
                "WIMAP0", "WIMAP1", "WIMAP2",
                "WIENTER", "WIF", "WITIME", "WIPAR", "WIPCNT",
                "WIOSTK", "WIOSTI", "WIOSTS", "WIOSTF",
                "WISUCKS", "WISCRT2",
                "WIMINUS", "WIMSTAR", "WIMSTT", "WICOLON",
                "M_DOOM", "M_NGAME", "M_OPTION", "M_LOADG", "M_SAVEG", "M_QUITG",
                "M_ENDGAM", "M_SKULL1", "M_SKULL2", "M_PAUSE",
                // Intentionally absent sentinel — verifies controlled miss path.
                "ZZNOUIXX",
            };

            for (int i = 0; i <= 9; i++)
                list.Add("WINUM" + i);

            for (int map = 0; map <= 8; map++)
                list.Add($"WILV0{map}");

            return list;
        }
    }
}
