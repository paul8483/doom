using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Doom.Graphics
{
    /// Pure pack-file codec for Enhanced CPU results. One pack per
    /// (wadHash, pipelineVersion). Corrupt / mismatched input is a miss —
    /// never throws to the caller.
    public static class EnhancedCacheCodec
    {
        /// ASCII "EXCH" little-endian.
        public const uint Magic = 0x48435845u;
        public const int FormatVersion = 1;
        public const int Sha256Length = 32;

        public const byte LayerWorldDedither = 1 << 0;
        public const byte LayerWorldUpscale4X = 1 << 1;
        public const byte LayerSpritesUpscale4X = 1 << 2;
        public const byte LayerUiUpscale4X = 1 << 3;

        public sealed class PackEntry
        {
            public EnhancedJobKind Kind;
            public string ItemId;
            public byte LayerFlags;
            public EnhancedJobResult Result;
        }

        public static byte PackLayerFlags(
            bool worldDedither,
            bool worldUpscale4X,
            bool spritesUpscale4X,
            bool uiUpscale4X)
        {
            byte flags = 0;
            if (worldDedither) flags |= LayerWorldDedither;
            if (worldUpscale4X) flags |= LayerWorldUpscale4X;
            if (spritesUpscale4X) flags |= LayerSpritesUpscale4X;
            if (uiUpscale4X) flags |= LayerUiUpscale4X;
            return flags;
        }

        public static void UnpackLayerFlags(
            byte flags,
            out bool worldDedither,
            out bool worldUpscale4X,
            out bool spritesUpscale4X,
            out bool uiUpscale4X)
        {
            worldDedither = (flags & LayerWorldDedither) != 0;
            worldUpscale4X = (flags & LayerWorldUpscale4X) != 0;
            spritesUpscale4X = (flags & LayerSpritesUpscale4X) != 0;
            uiUpscale4X = (flags & LayerUiUpscale4X) != 0;
        }

        public static byte[] Encode(
            byte[] wadHash,
            int pipelineVersion,
            IReadOnlyList<PackEntry> entries)
        {
            using var ms = new MemoryStream();
            EncodeTo(ms, wadHash, pipelineVersion, entries);
            return ms.ToArray();
        }

        /// Streaming encode — writes straight to <paramref name="stream"/> so a
        /// multi-hundred-MB pack never needs a second in-memory copy.
        public static void EncodeTo(
            Stream stream,
            byte[] wadHash,
            int pipelineVersion,
            IReadOnlyList<PackEntry> entries)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (wadHash == null || wadHash.Length != Sha256Length)
                throw new ArgumentException("WAD hash must be 32 bytes.", nameof(wadHash));
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            using var w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            w.Write(Magic);
            w.Write(FormatVersion);
            w.Write(wadHash);
            w.Write(pipelineVersion);
            w.Write(entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i] ?? throw new ArgumentException(
                    $"Pack entry {i} is null.", nameof(entries));
                if (entry.Result == null || !entry.Result.Success)
                    throw new ArgumentException(
                        $"Pack entry {i} requires a successful result.", nameof(entries));
                if (entry.Kind != entry.Result.Kind)
                    throw new ArgumentException(
                        $"Pack entry {i} kind {entry.Kind} does not match its " +
                        $"result kind {entry.Result.Kind}.", nameof(entries));
                if (string.IsNullOrEmpty(entry.ItemId))
                    throw new ArgumentException(
                        $"Pack entry {i} requires an item id.", nameof(entries));

                w.Write((int)entry.Kind);
                w.Write(entry.LayerFlags);
                WriteString(w, entry.ItemId);
                WriteResultPayload(w, entry.Result);
            }
        }

        public static bool TryDecode(
            byte[] data,
            byte[] expectedWadHash,
            int expectedPipelineVersion,
            out List<PackEntry> entries,
            out string error)
        {
            entries = null;
            error = null;

            if (data == null || data.Length < 4 + 4 + Sha256Length + 4 + 4)
            {
                error = "Pack truncated.";
                return false;
            }

            if (expectedWadHash == null || expectedWadHash.Length != Sha256Length)
            {
                error = "Expected WAD hash must be 32 bytes.";
                return false;
            }

            try
            {
                using var ms = new MemoryStream(data, writable: false);
                using var r = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

                uint magic = r.ReadUInt32();
                if (magic != Magic)
                {
                    error = "Bad pack magic.";
                    return false;
                }

                int formatVersion = r.ReadInt32();
                if (formatVersion != FormatVersion)
                {
                    error = "Unsupported pack format version.";
                    return false;
                }

                byte[] wadHash = r.ReadBytes(Sha256Length);
                if (wadHash.Length != Sha256Length)
                {
                    error = "Pack truncated in WAD hash.";
                    return false;
                }

                if (!BytesEqual(wadHash, expectedWadHash))
                {
                    error = "WAD hash mismatch.";
                    return false;
                }

                int pipelineVersion = r.ReadInt32();
                if (pipelineVersion != expectedPipelineVersion)
                {
                    error = "Pipeline version mismatch.";
                    return false;
                }

                int count = r.ReadInt32();
                if (count < 0 || count > 1_000_000)
                {
                    error = "Invalid entry count.";
                    return false;
                }

                var list = new List<PackEntry>(count);
                for (int i = 0; i < count; i++)
                {
                    if (ms.Position >= ms.Length)
                    {
                        error = "Pack truncated in index.";
                        return false;
                    }

                    int kindRaw = r.ReadInt32();
                    if (kindRaw < 0 || kindRaw > (int)EnhancedJobKind.Hud)
                    {
                        error = "Invalid job kind.";
                        return false;
                    }

                    var kind = (EnhancedJobKind)kindRaw;
                    byte layerFlags = r.ReadByte();
                    if (!TryReadString(r, out string itemId, out error))
                        return false;

                    if (!TryReadResultPayload(r, kind, out var result, out error))
                        return false;

                    list.Add(new PackEntry
                    {
                        Kind = kind,
                        ItemId = itemId,
                        LayerFlags = layerFlags,
                        Result = result,
                    });
                }

                entries = list;
                return true;
            }
            catch (EndOfStreamException)
            {
                error = "Pack truncated.";
                entries = null;
                return false;
            }
            catch (Exception ex)
            {
                error = "Pack decode failed: " + ex.Message;
                entries = null;
                return false;
            }
        }

        static void WriteResultPayload(BinaryWriter w, EnhancedJobResult result)
        {
            using var payload = new MemoryStream();
            using (var pw = new BinaryWriter(payload, Encoding.UTF8, leaveOpen: true))
            {
                switch (result.Kind)
                {
                    case EnhancedJobKind.WorldAlbedo:
                        WriteMipChain(pw, result.AlbedoMips);
                        break;
                    case EnhancedJobKind.WorldNormal:
                        WriteMipChain(pw, result.NormalMips);
                        break;
                    case EnhancedJobKind.Sprite:
                    case EnhancedJobKind.Hud:
                        WriteImage(pw, result.Rgba);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(result));
                }
            }

            byte[] bytes = payload.ToArray();
            w.Write(bytes.Length);
            w.Write(bytes);
        }

        static bool TryReadResultPayload(
            BinaryReader r,
            EnhancedJobKind kind,
            out EnhancedJobResult result,
            out string error)
        {
            result = null;
            error = null;

            int payloadLen = r.ReadInt32();
            if (payloadLen < 0 || payloadLen > 512 * 1024 * 1024)
            {
                error = "Invalid payload length.";
                return false;
            }

            byte[] payload = r.ReadBytes(payloadLen);
            if (payload.Length != payloadLen)
            {
                error = "Pack truncated in payload.";
                return false;
            }

            try
            {
                using var ms = new MemoryStream(payload, writable: false);
                using var pr = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

                switch (kind)
                {
                    case EnhancedJobKind.WorldAlbedo:
                        if (!TryReadMipChain(pr, out var albedo, out error))
                            return false;
                        result = EnhancedJobResult.OkWorldAlbedo(albedo);
                        return true;
                    case EnhancedJobKind.WorldNormal:
                        if (!TryReadMipChain(pr, out var normals, out error))
                            return false;
                        result = EnhancedJobResult.OkWorldNormal(normals);
                        return true;
                    case EnhancedJobKind.Sprite:
                    case EnhancedJobKind.Hud:
                        if (!TryReadImage(pr, out var rgba, out error))
                            return false;
                        result = EnhancedJobResult.OkRgba(kind, rgba);
                        return true;
                    default:
                        error = "Invalid job kind.";
                        return false;
                }
            }
            catch (EndOfStreamException)
            {
                error = "Pack truncated in payload.";
                return false;
            }
        }

        static void WriteMipChain(BinaryWriter w, PaletteMipChain chain)
        {
            if (chain == null || chain.Count == 0)
                throw new ArgumentException("Mip chain is required.");
            w.Write(chain.Count);
            for (int i = 0; i < chain.Count; i++)
                WriteImage(w, chain[i]);
        }

        static bool TryReadMipChain(BinaryReader r, out PaletteMipChain chain, out string error)
        {
            chain = null;
            error = null;
            int count = r.ReadInt32();
            if (count <= 0 || count > 32)
            {
                error = "Invalid mip count.";
                return false;
            }

            var levels = new DecodedImage[count];
            for (int i = 0; i < count; i++)
            {
                if (!TryReadImage(r, out levels[i], out error))
                    return false;
            }

            chain = new PaletteMipChain(levels);
            return true;
        }

        static void WriteImage(BinaryWriter w, DecodedImage image)
        {
            if (image == null || image.Rgba == null)
                throw new ArgumentException("Decoded image is required.");
            w.Write(image.Width);
            w.Write(image.Height);
            w.Write(image.Rgba.Length);
            w.Write(image.Rgba);
        }

        static bool TryReadImage(BinaryReader r, out DecodedImage image, out string error)
        {
            image = null;
            error = null;
            int width = r.ReadInt32();
            int height = r.ReadInt32();
            int rgbaLen = r.ReadInt32();
            if (width <= 0 || height <= 0 || width > 16384 || height > 16384)
            {
                error = "Invalid image dimensions.";
                return false;
            }

            long expected = (long)width * height * 4;
            if (rgbaLen != expected || rgbaLen < 0 || rgbaLen > 256 * 1024 * 1024)
            {
                error = "Invalid RGBA length.";
                return false;
            }

            byte[] rgba = r.ReadBytes(rgbaLen);
            if (rgba.Length != rgbaLen)
            {
                error = "Pack truncated in RGBA.";
                return false;
            }

            image = new DecodedImage(width, height, rgba);
            return true;
        }

        static void WriteString(BinaryWriter w, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            w.Write(bytes.Length);
            w.Write(bytes);
        }

        static bool TryReadString(BinaryReader r, out string value, out string error)
        {
            value = null;
            error = null;
            int len = r.ReadInt32();
            if (len < 0 || len > 4096)
            {
                error = "Invalid item id length.";
                return false;
            }

            byte[] bytes = r.ReadBytes(len);
            if (bytes.Length != len)
            {
                error = "Pack truncated in item id.";
                return false;
            }

            value = Encoding.UTF8.GetString(bytes);
            if (string.IsNullOrEmpty(value))
            {
                error = "Empty item id.";
                return false;
            }

            return true;
        }

        static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }

            return true;
        }
    }
}
