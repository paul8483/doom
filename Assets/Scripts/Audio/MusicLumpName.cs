using System;

namespace Doom.Audio
{
    /// Resolves a map marker name to its WAD music lump (e.g. E1M1 → D_E1M1).
    /// Doom II MAPxx track names are not invented here — use <see cref="TryForMap"/>.
    public static class MusicLumpName
    {
        public static string ForMap(string mapName)
        {
            if (!TryForMap(mapName, out string lump))
                throw new ArgumentException(
                    $"No music lump mapping for map '{mapName}'", nameof(mapName));
            return lump;
        }

        public static bool TryForMap(string mapName, out string musicLump)
        {
            musicLump = null;
            if (string.IsNullOrWhiteSpace(mapName))
                return false;

            string name = mapName.Trim().ToUpperInvariant();

            // ExMy — DOOM / Freedoom Phase 1
            if (name.Length >= 4
                && name[0] == 'E'
                && char.IsDigit(name[1])
                && name[2] == 'M'
                && char.IsDigit(name[3]))
            {
                // Optional trailing junk rejected: only E#M# or E#M## (map 10+)
                int i = 4;
                while (i < name.Length && char.IsDigit(name[i])) i++;
                if (i != name.Length)
                    return false;

                musicLump = "D_" + name;
                return true;
            }

            // MAPxx — Doom II table deferred until MAPxx content is in the project.
            return false;
        }
    }
}
