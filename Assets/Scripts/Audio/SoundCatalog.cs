using System;
using Doom.Wad;

namespace Doom.Audio
{
    /// Case-insensitive lookup of DMX <c>DS*</c> lumps from an open WAD.
    /// Does not retain the <see cref="WadFile"/> after the call returns.
    public static class SoundCatalog
    {
        public static bool TryRead(WadFile wad, string lumpName, out DecodedSound sound)
        {
            sound = null;
            if (wad == null) throw new ArgumentNullException(nameof(wad));
            if (string.IsNullOrEmpty(lumpName)) return false;

            string name = lumpName.ToUpperInvariant();
            if (!name.StartsWith("DS", StringComparison.Ordinal)) return false;

            int idx = wad.FindLump(name);
            if (idx < 0) return false;

            try
            {
                sound = DmxSound.Decode(wad.ReadLump(idx));
                return true;
            }
            catch (System.IO.InvalidDataException)
            {
                sound = null;
                return false;
            }
        }
    }
}
