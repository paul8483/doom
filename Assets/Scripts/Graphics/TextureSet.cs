using System.Collections.Generic;
using System.IO;
using System.Text;
using Doom.Wad;

namespace Doom.Graphics
{
    /// Reads TEXTURE1 (+ TEXTURE2 if present) and PNAMES, and assembles composite
    /// wall textures from patches. Names are upper-cased for case-insensitive match.
    public sealed class TextureSet : ITextureSizeSource
    {
        private struct PatchRef { public int OriginX, OriginY, PatchIndex; }
        private sealed class TexDef
        {
            public string Name;
            public int Width, Height;
            public PatchRef[] Patches;
        }

        private readonly Dictionary<string, TexDef> defs;
        private readonly PatchNames pnames;
        private readonly WadFile wad;

        public IEnumerable<string> Names => defs.Keys;

        private TextureSet(Dictionary<string, TexDef> defs, PatchNames pnames, WadFile wad)
        {
            this.defs = defs;
            this.pnames = pnames;
            this.wad = wad;
        }

        public static TextureSet Load(WadFile wad)
        {
            var pnames = new PatchNames(wad.ReadLump("PNAMES"));
            var defs = new Dictionary<string, TexDef>();

            // TEXTURE2 (if any) is read first so TEXTURE1 entries override duplicates.
            foreach (var lumpName in new[] { "TEXTURE2", "TEXTURE1" })
            {
                int idx = wad.FindLump(lumpName);
                if (idx < 0) continue;
                ParseTextureLump(wad.ReadLump(lumpName), defs);
            }

            return new TextureSet(defs, pnames, wad);
        }

        private static void ParseTextureLump(byte[] lump, Dictionary<string, TexDef> defs)
        {
            using var ms = new MemoryStream(lump);
            using var r = new BinaryReader(ms);
            int numTextures = r.ReadInt32();
            var offsets = new int[numTextures];
            for (int i = 0; i < numTextures; i++) offsets[i] = r.ReadInt32();

            for (int i = 0; i < numTextures; i++)
            {
                ms.Position = offsets[i];
                string name = ReadName8(r).ToUpperInvariant();
                r.ReadInt32();                 // masked (unused)
                int width = r.ReadInt16();
                int height = r.ReadInt16();
                r.ReadInt32();                 // columndirectory (unused)
                int patchCount = r.ReadInt16();
                var patches = new PatchRef[patchCount];
                for (int p = 0; p < patchCount; p++)
                {
                    patches[p].OriginX = r.ReadInt16();
                    patches[p].OriginY = r.ReadInt16();
                    patches[p].PatchIndex = r.ReadInt16();
                    r.ReadInt16();             // stepdir (unused)
                    r.ReadInt16();             // colormap (unused)
                }
                defs[name] = new TexDef { Name = name, Width = width, Height = height, Patches = patches };
            }
        }

        public bool Contains(string name)
            => name != null && defs.ContainsKey(name.ToUpperInvariant());

        public bool TryGetSize(string name, out int width, out int height)
        {
            width = height = 0;
            if (name == null || !defs.TryGetValue(name.ToUpperInvariant(), out var d)) return false;
            width = d.Width; height = d.Height;
            return true;
        }

        /// Assemble a composite texture: blank canvas, patches stamped at origins,
        /// opaque source pixels overwrite the canvas (later patch wins).
        public DecodedImage Build(string name, Palette palette)
        {
            if (!defs.TryGetValue(name.ToUpperInvariant(), out var d))
            {
                GraphicsLog.Warning($"TextureSet: unknown texture '{name}'");
                return Placeholder.Magenta(64, 128);
            }

            var rgba = new byte[d.Width * d.Height * 4];
            foreach (var pr in d.Patches)
            {
                if (pr.PatchIndex < 0 || pr.PatchIndex >= pnames.Count) continue;
                string patchName = pnames[pr.PatchIndex];
                int li = wad.FindLump(patchName);
                if (li < 0) { GraphicsLog.Warning($"TextureSet: missing patch '{patchName}'"); continue; }
                var patch = Patch.Decode(wad.ReadLump(li), palette);
                Stamp(rgba, d.Width, d.Height, patch, pr.OriginX, pr.OriginY);
            }
            return new DecodedImage(d.Width, d.Height, rgba);
        }

        private static void Stamp(byte[] dst, int dstW, int dstH,
                                  DecodedImage src, int originX, int originY)
        {
            for (int y = 0; y < src.Height; y++)
            {
                int dy = originY + y;
                if (dy < 0 || dy >= dstH) continue;
                for (int x = 0; x < src.Width; x++)
                {
                    int dx = originX + x;
                    if (dx < 0 || dx >= dstW) continue;
                    int si = (y * src.Width + x) * 4;
                    if (src.Rgba[si + 3] == 0) continue; // transparent source pixel
                    int di = (dy * dstW + dx) * 4;
                    dst[di] = src.Rgba[si];
                    dst[di + 1] = src.Rgba[si + 1];
                    dst[di + 2] = src.Rgba[si + 2];
                    dst[di + 3] = 255;
                }
            }
        }

        private static string ReadName8(BinaryReader r)
        {
            var raw = r.ReadBytes(8);
            int end = raw.Length;
            for (int i = 0; i < raw.Length; i++)
                if (raw[i] == 0) { end = i; break; }
            return Encoding.ASCII.GetString(raw, 0, end);
        }
    }

    /// Visible fallback for missing textures/flats: a magenta checker.
    public static class Placeholder
    {
        public static DecodedImage Magenta(int width, int height)
        {
            var rgba = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    bool on = ((x >> 3) + (y >> 3)) % 2 == 0;
                    int o = (y * width + x) * 4;
                    rgba[o] = (byte)(on ? 255 : 0);
                    rgba[o + 1] = 0;
                    rgba[o + 2] = (byte)(on ? 255 : 0);
                    rgba[o + 3] = 255;
                }
            return new DecodedImage(width, height, rgba);
        }
    }
}
