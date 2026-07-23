using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Doom.Graphics;
using Doom.MapBuild.Rendering;

namespace Doom.Map.Tests
{
    public class EnhancedDiskCacheTests
    {
        string tempRoot;

        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(
                Path.GetTempPath(), "doom-exch-disk-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            EnhancedDiskCache.ResetForTests();
            EnhancedDiskCache.EnableForTests(tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            EnhancedDiskCache.ResetForTests();
            try { Directory.Delete(tempRoot, recursive: true); }
            catch { /* ignore */ }
        }

        static string FreedoomPath()
        {
            return Path.Combine(
                UnityEngine.Application.dataPath, "StreamingAssets", "wads", "freedoom1.wad");
        }

        static EnhancedJobResult OkSprite()
        {
            var rgba = new byte[4 * 4 * 4];
            for (int i = 0; i < rgba.Length; i++) rgba[i] = (byte)(i & 0xff);
            return EnhancedJobResult.OkRgba(
                EnhancedJobKind.Sprite, new DecodedImage(4, 4, rgba));
        }

        static EnhancedLayerConfig Layers() =>
            new EnhancedLayerConfig(true, true, true, true);

        [Test]
        public void Flush_then_reload_hits()
        {
            string wadPath = FreedoomPath();
            if (!File.Exists(wadPath))
                Assert.Ignore("freedoom1.wad missing");

            var disk = EnhancedDiskCache.Instance;
            disk.BindWad(wadPath);
            // Synchronously wait for empty/miss load.
            while (!disk.IsLoaded)
                System.Threading.Thread.Sleep(1);

            var layers = Layers();
            var result = OkSprite();
            disk.Publish(EnhancedJobKind.Sprite, "10", layers, result);
            disk.FlushBlocking();

            Assert.IsTrue(File.Exists(disk.PackPath));
            Assert.That(disk.PackFileBytes, Is.GreaterThan(0L));

            // Cold session: wipe memory, keep pack file.
            string packPath = disk.PackPath;
            EnhancedDiskCache.ResetForTests();
            EnhancedDiskCache.EnableForTests(tempRoot);
            disk = EnhancedDiskCache.Instance;
            disk.BindWad(wadPath);
            while (!disk.IsLoaded)
                System.Threading.Thread.Sleep(1);

            Assert.AreEqual(packPath, disk.PackPath);
            Assert.IsTrue(disk.TryGet(EnhancedJobKind.Sprite, "10", layers, out var hit));
            Assert.AreEqual(result.Rgba.Width, hit.Rgba.Width);
            Assert.AreEqual(result.Rgba.Rgba, hit.Rgba.Rgba);
        }

        [Test]
        public void Corrupt_pack_is_ignored_without_throw()
        {
            string wadPath = FreedoomPath();
            if (!File.Exists(wadPath))
                Assert.Ignore("freedoom1.wad missing");

            var disk = EnhancedDiskCache.Instance;
            disk.BindWad(wadPath);
            while (!disk.IsLoaded)
                System.Threading.Thread.Sleep(1);

            disk.Publish(EnhancedJobKind.Hud, "STBAR", Layers(), OkSprite());
            disk.FlushBlocking();

            // Truncate the pack on disk.
            using (var fs = new FileStream(disk.PackPath, FileMode.Open, FileAccess.Write))
                fs.SetLength(Math.Max(8, fs.Length / 4));

            EnhancedDiskCache.ResetForTests();
            EnhancedDiskCache.EnableForTests(tempRoot);
            disk = EnhancedDiskCache.Instance;
            disk.BindWad(wadPath);
            while (!disk.IsLoaded)
                System.Threading.Thread.Sleep(1);

            Assert.AreEqual(0, disk.Count);
            Assert.IsFalse(disk.TryGet(EnhancedJobKind.Hud, "STBAR", Layers(), out _));
        }

        [Test]
        public void Stale_pipeline_header_is_a_miss()
        {
            string wadPath = FreedoomPath();
            if (!File.Exists(wadPath))
                Assert.Ignore("freedoom1.wad missing");

            byte[] hash = EnhancedDiskCache.ComputeWadSha256(wadPath);
            string packPath = Path.Combine(
                tempRoot,
                EnhancedDiskCache.BuildPackFileName(hash, EnhancedPipelineVersion.Value));

            var stale = new List<EnhancedCacheCodec.PackEntry>
            {
                new EnhancedCacheCodec.PackEntry
                {
                    Kind = EnhancedJobKind.Sprite,
                    ItemId = "10",
                    LayerFlags = 0x0f,
                    Result = OkSprite(),
                },
            };
            // Filename uses current version; header claims a newer pipeline.
            byte[] bytes = EnhancedCacheCodec.Encode(
                hash, EnhancedPipelineVersion.Value + 1, stale);
            File.WriteAllBytes(packPath, bytes);

            var disk = EnhancedDiskCache.Instance;
            disk.BindWad(wadPath);
            while (!disk.IsLoaded)
                System.Threading.Thread.Sleep(1);

            Assert.AreEqual(0, disk.Count);
            Assert.IsFalse(disk.TryGet(EnhancedJobKind.Sprite, "10", Layers(), out _));
        }
    }
}
