using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Doom.Graphics.Tests
{
    /// Byte-blob builders for the graphics lumps, mirroring SyntheticMapBuilder.
    public static class SyntheticGfxBuilder
    {
        /// PLAYPAL with `paletteCount` palettes of 256 RGB triples.
        /// Palette p, color c = (c, p, (c+p) & 0xFF) so tests can assert a formula.
        public static byte[] BuildPlaypal(int paletteCount = 14)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            for (int p = 0; p < paletteCount; p++)
                for (int c = 0; c < 256; c++)
                {
                    w.Write((byte)c);
                    w.Write((byte)p);
                    w.Write((byte)((c + p) & 0xFF));
                }
            return ms.ToArray();
        }

        /// Flat: size*size raw palette indices. `fill` writes one index everywhere;
        /// pass an explicit indices array for per-pixel control.
        public static byte[] BuildFlat(byte fill, int size = 64)
        {
            var data = new byte[size * size];
            for (int i = 0; i < data.Length; i++) data[i] = fill;
            return data;
        }

        public static byte[] BuildFlat(byte[] indices) => (byte[])indices.Clone();

        /// A column post: topdelta + length + padding + pixels + padding.
        public struct Post
        {
            public byte TopDelta;
            public byte[] Pixels; // palette indices
        }

        /// DOOM picture (patch) format. `columns[x]` is the list of posts in column x.
        public static byte[] BuildPatch(short width, short height,
                                        short leftOffset, short topOffset,
                                        IReadOnlyList<IReadOnlyList<Post>> columns)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);

            w.Write(width);
            w.Write(height);
            w.Write(leftOffset);
            w.Write(topOffset);

            // Reserve column offset table (one int32 per column).
            long offsetTablePos = ms.Position;
            for (int x = 0; x < width; x++) w.Write(0);

            var colOffsets = new int[width];
            for (int x = 0; x < width; x++)
            {
                colOffsets[x] = (int)ms.Position;
                var posts = x < columns.Count ? columns[x] : null;
                if (posts != null)
                {
                    foreach (var post in posts)
                    {
                        w.Write(post.TopDelta);
                        w.Write((byte)post.Pixels.Length);
                        w.Write((byte)0);               // unused padding pre-pixel
                        w.Write(post.Pixels);
                        w.Write((byte)0);               // unused padding post-pixel
                    }
                }
                w.Write((byte)0xFF);                    // column terminator
            }

            // Back-patch the offset table.
            ms.Position = offsetTablePos;
            for (int x = 0; x < width; x++) w.Write(colOffsets[x]);

            return ms.ToArray();
        }

        /// PNAMES: int32 count, then count * 8-byte upper-case names.
        public static byte[] BuildPnames(params string[] names)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(names.Length);
            foreach (var n in names) w.Write(Name8(n));
            return ms.ToArray();
        }

        public struct PatchRef
        {
            public short OriginX;
            public short OriginY;
            public short PatchIndex; // index into PNAMES
        }

        public struct TexDef
        {
            public string Name;
            public short Width;
            public short Height;
            public PatchRef[] Patches;
        }

        /// TEXTURE1/TEXTURE2 lump: int32 numTextures, numTextures int32 offsets,
        /// then each texture record (maptexture_t + mappatch_t entries).
        public static byte[] BuildTextureLump(params TexDef[] textures)
        {
            // First serialize each texture record into its own buffer.
            var records = new List<byte[]>();
            foreach (var t in textures)
            {
                using var rms = new MemoryStream();
                using var rw = new BinaryWriter(rms);
                rw.Write(Name8(t.Name));
                rw.Write(0);                 // masked (unused)
                rw.Write(t.Width);
                rw.Write(t.Height);
                rw.Write(0);                 // columndirectory (unused)
                rw.Write((short)t.Patches.Length);
                foreach (var p in t.Patches)
                {
                    rw.Write(p.OriginX);
                    rw.Write(p.OriginY);
                    rw.Write(p.PatchIndex);
                    rw.Write((short)0);      // stepdir (unused)
                    rw.Write((short)0);      // colormap (unused)
                }
                records.Add(rms.ToArray());
            }

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(textures.Length);
            int headerSize = 4 + textures.Length * 4;
            int offset = headerSize;
            foreach (var rec in records) { w.Write(offset); offset += rec.Length; }
            foreach (var rec in records) w.Write(rec);
            return ms.ToArray();
        }

        private static byte[] Name8(string name)
        {
            var buf = new byte[8];
            if (string.IsNullOrEmpty(name)) return buf;
            var ascii = Encoding.ASCII.GetBytes(name.ToUpperInvariant());
            System.Array.Copy(ascii, buf, System.Math.Min(ascii.Length, 8));
            return buf;
        }
    }
}
