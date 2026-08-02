using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Doom.Graphics;

namespace Doom.Graphics.Tests
{
    public class EnhancedCacheCodecTests
    {
        static readonly byte[] TestHash = MakeHash(0x11);

        static byte[] MakeHash(byte fill)
        {
            var hash = new byte[EnhancedCacheCodec.Sha256Length];
            for (int i = 0; i < hash.Length; i++) hash[i] = fill;
            return hash;
        }

        static EnhancedJobResult OkSprite()
        {
            var rgba = new byte[4 * 4 * 4];
            for (int i = 0; i < rgba.Length; i++) rgba[i] = (byte)(i & 0xff);
            return EnhancedJobResult.OkRgba(
                EnhancedJobKind.Sprite, new DecodedImage(4, 4, rgba));
        }

        static EnhancedJobResult OkHud()
        {
            var rgba = new byte[4 * 4 * 4];
            for (int i = 0; i < rgba.Length; i++) rgba[i] = (byte)(255 - (i & 0xff));
            return EnhancedJobResult.OkRgba(
                EnhancedJobKind.Hud, new DecodedImage(4, 4, rgba));
        }

        static EnhancedJobResult OkPickup()
        {
            var rgba = new byte[8 * 8 * 4];
            for (int i = 0; i < rgba.Length; i++) rgba[i] = (byte)(i * 7);
            return EnhancedJobResult.OkRgba(
                EnhancedJobKind.PickupSprite, new DecodedImage(8, 8, rgba));
        }

        static EnhancedJobResult OkEnemy()
        {
            var rgba = new byte[8 * 8 * 4];
            for (int i = 0; i < rgba.Length; i++) rgba[i] = (byte)(255 - i * 5);
            return EnhancedJobResult.OkRgba(
                EnhancedJobKind.EnemySprite, new DecodedImage(8, 8, rgba));
        }

        static EnhancedJobResult OkWeapon()
        {
            var rgba = new byte[8 * 8 * 4];
            for (int i = 0; i < rgba.Length; i++) rgba[i] = (byte)(i * 11);
            return EnhancedJobResult.OkRgba(
                EnhancedJobKind.WeaponSprite, new DecodedImage(8, 8, rgba));
        }

        static EnhancedJobResult OkAlbedo()
        {
            var l0 = new byte[8 * 8 * 4];
            for (int i = 0; i < l0.Length; i++) l0[i] = (byte)(200 - (i & 0x7f));
            var l1 = new byte[4 * 4 * 4];
            for (int i = 0; i < l1.Length; i++) l1[i] = (byte)(i & 0xff);
            var chain = new PaletteMipChain(new[]
            {
                new DecodedImage(8, 8, l0),
                new DecodedImage(4, 4, l1),
            });
            return EnhancedJobResult.OkWorldAlbedo(chain);
        }

        static EnhancedJobResult OkNormal()
        {
            var rgba = new byte[8 * 8 * 4];
            for (int i = 0; i < rgba.Length; i++) rgba[i] = (byte)(i * 3);
            var chain = new PaletteMipChain(new[] { new DecodedImage(8, 8, rgba) });
            return EnhancedJobResult.OkWorldNormal(chain);
        }

        static EnhancedCacheCodec.PackEntry Entry(
            EnhancedJobKind kind, string id, byte flags, EnhancedJobResult result) =>
            new EnhancedCacheCodec.PackEntry
            {
                Kind = kind,
                ItemId = id,
                LayerFlags = flags,
                Result = result,
            };

        static void AssertImagesEqual(DecodedImage a, DecodedImage b)
        {
            Assert.AreEqual(a.Width, b.Width);
            Assert.AreEqual(a.Height, b.Height);
            Assert.AreEqual(a.Rgba.Length, b.Rgba.Length);
            Assert.AreEqual(a.Rgba, b.Rgba);
        }

        [Test]
        public void Encode_Decode_round_trips_mixed_kinds()
        {
            byte flags = EnhancedCacheCodec.PackLayerFlags(true, true, true, false);
            var entries = new List<EnhancedCacheCodec.PackEntry>
            {
                Entry(EnhancedJobKind.WorldAlbedo, "FLOOR0_1", flags, OkAlbedo()),
                Entry(EnhancedJobKind.WorldNormal, "FLOOR0_1", flags, OkNormal()),
                Entry(EnhancedJobKind.Sprite, "42", flags, OkSprite()),
                Entry(EnhancedJobKind.Hud, "STBAR", flags, OkHud()),
                Entry(EnhancedJobKind.PickupSprite, "84", flags, OkPickup()),
                Entry(EnhancedJobKind.EnemySprite, "96", flags, OkEnemy()),
                Entry(EnhancedJobKind.WeaponSprite, "108", flags, OkWeapon()),
            };

            byte[] bytes = EnhancedCacheCodec.Encode(
                TestHash, EnhancedPipelineVersion.Value, entries);

            Assert.IsTrue(
                EnhancedCacheCodec.TryDecode(
                    bytes, TestHash, EnhancedPipelineVersion.Value,
                    out var decoded, out string error),
                error);
            Assert.AreEqual(7, decoded.Count);

            Assert.AreEqual(EnhancedJobKind.WorldAlbedo, decoded[0].Kind);
            Assert.AreEqual("FLOOR0_1", decoded[0].ItemId);
            Assert.AreEqual(flags, decoded[0].LayerFlags);
            Assert.AreEqual(2, decoded[0].Result.AlbedoMips.Count);
            AssertImagesEqual(
                entries[0].Result.AlbedoMips[0], decoded[0].Result.AlbedoMips[0]);
            AssertImagesEqual(
                entries[0].Result.AlbedoMips[1], decoded[0].Result.AlbedoMips[1]);

            Assert.AreEqual(EnhancedJobKind.WorldNormal, decoded[1].Kind);
            AssertImagesEqual(
                entries[1].Result.NormalMips[0], decoded[1].Result.NormalMips[0]);

            Assert.AreEqual(EnhancedJobKind.Sprite, decoded[2].Kind);
            AssertImagesEqual(entries[2].Result.Rgba, decoded[2].Result.Rgba);

            Assert.AreEqual(EnhancedJobKind.Hud, decoded[3].Kind);
            AssertImagesEqual(entries[3].Result.Rgba, decoded[3].Result.Rgba);

            Assert.AreEqual(EnhancedJobKind.PickupSprite, decoded[4].Kind);
            AssertImagesEqual(entries[4].Result.Rgba, decoded[4].Result.Rgba);

            Assert.AreEqual(EnhancedJobKind.EnemySprite, decoded[5].Kind);
            AssertImagesEqual(entries[5].Result.Rgba, decoded[5].Result.Rgba);

            Assert.AreEqual(EnhancedJobKind.WeaponSprite, decoded[6].Kind);
            AssertImagesEqual(entries[6].Result.Rgba, decoded[6].Result.Rgba);
        }

        [Test]
        public void Kind_mismatch_between_entry_and_result_throws()
        {
            var entries = new List<EnhancedCacheCodec.PackEntry>
            {
                // Header claims WorldAlbedo, payload result is a Sprite.
                Entry(EnhancedJobKind.WorldAlbedo, "STBAR", 0x0f, OkSprite()),
            };

            Assert.Throws<ArgumentException>(() =>
                EnhancedCacheCodec.Encode(
                    TestHash, EnhancedPipelineVersion.Value, entries));
        }

        [Test]
        public void EncodeTo_stream_matches_byte_array_encode()
        {
            byte flags = EnhancedCacheCodec.PackLayerFlags(true, false, true, false);
            var entries = new List<EnhancedCacheCodec.PackEntry>
            {
                Entry(EnhancedJobKind.Sprite, "42", flags, OkSprite()),
                Entry(EnhancedJobKind.WorldAlbedo, "FLOOR0_1", flags, OkAlbedo()),
            };

            byte[] viaArray = EnhancedCacheCodec.Encode(
                TestHash, EnhancedPipelineVersion.Value, entries);

            using var ms = new MemoryStream();
            EnhancedCacheCodec.EncodeTo(
                ms, TestHash, EnhancedPipelineVersion.Value, entries);

            Assert.AreEqual(viaArray, ms.ToArray());
        }

        [Test]
        public void Truncated_pack_is_a_miss()
        {
            var entries = new List<EnhancedCacheCodec.PackEntry>
            {
                Entry(EnhancedJobKind.Sprite, "10", 0x0f, OkSprite()),
            };
            byte[] full = EnhancedCacheCodec.Encode(
                TestHash, EnhancedPipelineVersion.Value, entries);
            var truncated = new byte[full.Length / 2];
            Array.Copy(full, truncated, truncated.Length);

            Assert.IsFalse(
                EnhancedCacheCodec.TryDecode(
                    truncated, TestHash, EnhancedPipelineVersion.Value,
                    out _, out string error));
            Assert.That(error, Does.Contain("truncat").IgnoreCase);
        }

        [Test]
        public void Bad_magic_is_a_miss()
        {
            var entries = new List<EnhancedCacheCodec.PackEntry>
            {
                Entry(EnhancedJobKind.Hud, "STBAR", 0x0f, OkHud()),
            };
            byte[] bytes = EnhancedCacheCodec.Encode(
                TestHash, EnhancedPipelineVersion.Value, entries);
            bytes[0] ^= 0xff;

            Assert.IsFalse(
                EnhancedCacheCodec.TryDecode(
                    bytes, TestHash, EnhancedPipelineVersion.Value,
                    out _, out string error));
            Assert.That(error, Does.Contain("magic").IgnoreCase);
        }

        [Test]
        public void Wrong_wadHash_is_a_miss()
        {
            var entries = new List<EnhancedCacheCodec.PackEntry>
            {
                Entry(EnhancedJobKind.Sprite, "10", 0x0f, OkSprite()),
            };
            byte[] bytes = EnhancedCacheCodec.Encode(
                TestHash, EnhancedPipelineVersion.Value, entries);

            Assert.IsFalse(
                EnhancedCacheCodec.TryDecode(
                    bytes, MakeHash(0x22), EnhancedPipelineVersion.Value,
                    out _, out string error));
            Assert.That(error, Does.Contain("hash").IgnoreCase);
        }

        [Test]
        public void Wrong_pipelineVersion_is_a_miss()
        {
            var entries = new List<EnhancedCacheCodec.PackEntry>
            {
                Entry(EnhancedJobKind.Sprite, "10", 0x0f, OkSprite()),
            };
            byte[] bytes = EnhancedCacheCodec.Encode(
                TestHash, EnhancedPipelineVersion.Value, entries);

            Assert.IsFalse(
                EnhancedCacheCodec.TryDecode(
                    bytes, TestHash, EnhancedPipelineVersion.Value + 1,
                    out _, out string error));
            Assert.That(error, Does.Contain("Pipeline").IgnoreCase);
        }

        [Test]
        public void Interrupted_temp_write_leaves_old_pack_intact()
        {
            string root = Path.Combine(
                Path.GetTempPath(), "doom-exch-codec-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                byte[] hash = MakeHash(0x33);
                string packPath = Path.Combine(root, "pack.bin");
                var v1 = new List<EnhancedCacheCodec.PackEntry>
                {
                    Entry(EnhancedJobKind.Sprite, "old", 0x0f, OkSprite()),
                };
                byte[] v1Bytes = EnhancedCacheCodec.Encode(
                    hash, EnhancedPipelineVersion.Value, v1);
                File.WriteAllBytes(packPath, v1Bytes);

                // Simulate crash after writing temp but before replace.
                var v2 = new List<EnhancedCacheCodec.PackEntry>
                {
                    Entry(EnhancedJobKind.Hud, "new", 0x0f, OkHud()),
                };
                byte[] v2Bytes = EnhancedCacheCodec.Encode(
                    hash, EnhancedPipelineVersion.Value, v2);
                File.WriteAllBytes(packPath + ".tmp", v2Bytes);

                byte[] stillThere = File.ReadAllBytes(packPath);
                Assert.IsTrue(
                    EnhancedCacheCodec.TryDecode(
                        stillThere, hash, EnhancedPipelineVersion.Value,
                        out var decoded, out string error),
                    error);
                Assert.AreEqual(1, decoded.Count);
                Assert.AreEqual("old", decoded[0].ItemId);
                Assert.IsTrue(File.Exists(packPath + ".tmp"));
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); }
                catch { /* ignore */ }
            }
        }

        [Test]
        public void Layer_flag_pack_unpack_round_trips()
        {
            byte flags = EnhancedCacheCodec.PackLayerFlags(true, false, true, false);
            EnhancedCacheCodec.UnpackLayerFlags(
                flags, out bool d, out bool w, out bool s, out bool u);
            Assert.IsTrue(d);
            Assert.IsFalse(w);
            Assert.IsTrue(s);
            Assert.IsFalse(u);
        }
    }
}
