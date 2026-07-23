using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.Graphics;
using Doom.MapBuild;
using Doom.MapBuild.Rendering;
using Doom.Wad;

namespace Doom.Stage3.PlayTests
{
    /// Warm-performance Task 4 — EnhancedDiskCache pack file.
    public class EnhancedDiskCachePlayTests
    {
        string tempRoot;

        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(
                Path.GetTempPath(), "doom-exch-play-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            EnhancedDiskCache.ResetForTests();
            EnhancedVariantStore.ResetForTests();
            EnhancedWarmScheduler.ResetCompletedStats();
            EnhancedDiskCache.EnableForTests(tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            TextureCache.ForceEnhancedFailureForTests = false;
            SpriteCache.ForceEnhancedFailureForTests = false;
            HudTextureCache.ForceEnhancedFailureForTests = false;
            EnhancedVariantStore.ResetForTests();
            EnhancedDiskCache.ResetForTests();
            GameSessionHost.ResetForTests();
            try { Directory.Delete(tempRoot, recursive: true); }
            catch { /* ignore */ }
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Cold_process_with_pack_is_upload_only()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            if (!File.Exists(wadPath))
                Assert.Ignore("freedoom1.wad missing");

            string wadId = GameSessionHost.ComputeWadIdentity(wadPath);
            EnhancedVariantStore.Instance.BindWadIdentity(wadId);

            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);
            var sprites = SpriteSet.Load(wad);
            var ui = UiPatchCatalog.LoadStandard(wad, palette);
            var names = new List<string> { "STARTAN2", "FLOOR0_1", "SKY1", "STEP1" };

            var factory1 = new DoomMaterialFactory();
            factory1.SetActiveProfile(GraphicsProfile.Enhanced);
            var cache1 = new TextureCache(wad, textures, palette, factory1);
            for (int i = 0; i < names.Count; i++)
                cache1.GetTexture(names[i], WorldTextureVariant.Native);

            var spriteCache1 = new SpriteCache(wad, sprites, palette, factory1);
            spriteCache1.WarmNative("POSS", 0, 0);
            spriteCache1.WarmNative("TROO", 0, 0);
            var hud1 = new HudTextureCache(ui, profile: GraphicsProfile.Enhanced);

            using (var first = new EnhancedWarmScheduler())
            {
                yield return first.Warm(
                    cache1, spriteCache1, hud1, names,
                    warmWorld: true, warmSprites: true, warmHud: true,
                    reportProgress: null,
                    wadIdentity: wadId,
                    wadPath: wadPath);
                Assert.That(first.LastJobsStarted, Is.GreaterThan(0));
                Assert.That(first.LastDiskHits, Is.EqualTo(0));
            }

            EnhancedDiskCache.Instance.FlushBlocking();
            Assert.IsTrue(File.Exists(EnhancedDiskCache.Instance.PackPath));
            long packBytes = EnhancedDiskCache.Instance.PackFileBytes;
            Assert.That(packBytes, Is.GreaterThan(0L));
            int world1 = cache1.EnhancedVariantCount;

            // Cold process: wipe store + disk memory; keep pack on disk.
            EnhancedVariantStore.ResetForTests();
            EnhancedVariantStore.Instance.BindWadIdentity(wadId);
            EnhancedDiskCache.ResetForTests();
            EnhancedDiskCache.EnableForTests(tempRoot);

            var factory2 = new DoomMaterialFactory();
            factory2.SetActiveProfile(GraphicsProfile.Enhanced);
            var cache2 = new TextureCache(wad, textures, palette, factory2);
            for (int i = 0; i < names.Count; i++)
                cache2.GetTexture(names[i], WorldTextureVariant.Native);

            var spriteCache2 = new SpriteCache(wad, sprites, palette, factory2);
            spriteCache2.WarmNative("POSS", 0, 0);
            spriteCache2.WarmNative("TROO", 0, 0);
            var hud2 = new HudTextureCache(ui, profile: GraphicsProfile.Enhanced);

            using (var second = new EnhancedWarmScheduler())
            {
                yield return second.Warm(
                    cache2, spriteCache2, hud2, names,
                    warmWorld: true, warmSprites: true, warmHud: true,
                    reportProgress: null,
                    wadIdentity: wadId,
                    wadPath: wadPath);

                Assert.That(second.LastJobsStarted, Is.EqualTo(0),
                    "cold process with pack must not recompute");
                Assert.That(second.LastDiskHits, Is.GreaterThan(0));
                Assert.That(second.LastStoreHits, Is.EqualTo(0));
            }

            Assert.AreEqual(world1, cache2.EnhancedVariantCount);
            Assert.That(EnhancedVariantStore.Instance.Count, Is.GreaterThan(0),
                "disk hits must publish into the session store");

            Debug.Log(
                $"EnhancedDiskCachePlayTests: cold pack upload-only; " +
                $"packMB={packBytes / (1024f * 1024f):F2} " +
                $"diskHits={EnhancedWarmScheduler.LastCompletedDiskHits}");
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Stale_pipeline_pack_triggers_recompute_and_rewrite()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            if (!File.Exists(wadPath))
                Assert.Ignore("freedoom1.wad missing");

            string wadId = GameSessionHost.ComputeWadIdentity(wadPath);
            byte[] hash = EnhancedDiskCache.ComputeWadSha256(wadPath);
            string packPath = Path.Combine(
                tempRoot,
                EnhancedDiskCache.BuildPackFileName(hash, EnhancedPipelineVersion.Value));

            // Pack filename matches current version; header claims a newer one.
            var rgba = new byte[4 * 4 * 4];
            var stale = new List<EnhancedCacheCodec.PackEntry>
            {
                new EnhancedCacheCodec.PackEntry
                {
                    Kind = EnhancedJobKind.Sprite,
                    ItemId = "999",
                    LayerFlags = 0x0f,
                    Result = EnhancedJobResult.OkRgba(
                        EnhancedJobKind.Sprite, new DecodedImage(4, 4, rgba)),
                },
            };
            File.WriteAllBytes(
                packPath,
                EnhancedCacheCodec.Encode(hash, EnhancedPipelineVersion.Value + 1, stale));
            long staleBytes = new FileInfo(packPath).Length;

            EnhancedVariantStore.Instance.BindWadIdentity(wadId);

            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);
            var names = new List<string> { "FLOOR0_1", "STEP1" };

            var factory = new DoomMaterialFactory();
            factory.SetActiveProfile(GraphicsProfile.Enhanced);
            var cache = new TextureCache(wad, textures, palette, factory);
            for (int i = 0; i < names.Count; i++)
                cache.GetTexture(names[i], WorldTextureVariant.Native);

            using (var scheduler = new EnhancedWarmScheduler())
            {
                yield return scheduler.Warm(
                    cache, sprites: null, hud: null, names,
                    warmWorld: true, warmSprites: false, warmHud: false,
                    reportProgress: null,
                    wadIdentity: wadId,
                    wadPath: wadPath);
                Assert.That(scheduler.LastJobsStarted, Is.GreaterThan(0),
                    "stale pack must miss and recompute");
                Assert.That(scheduler.LastDiskHits, Is.EqualTo(0));
            }

            EnhancedDiskCache.Instance.FlushBlocking();
            Assert.IsTrue(File.Exists(packPath));
            Assert.That(new FileInfo(packPath).Length, Is.Not.EqualTo(staleBytes));

            Assert.IsTrue(
                EnhancedCacheCodec.TryDecode(
                    File.ReadAllBytes(packPath), hash, EnhancedPipelineVersion.Value,
                    out var decoded, out string error),
                error);
            Assert.That(decoded.Count, Is.GreaterThan(0));
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator E1M1_cold_disk_warm_meets_gate()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;
            yield return null;

            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            if (!File.Exists(wadPath))
                Assert.Ignore("freedoom1.wad missing");

            var prefs = new SettingsStore();
            var previous = prefs.Load();
            prefs.Save(previous.WithGraphicsMode(GraphicsMode.Classic));

            try
            {
                EnhancedVariantStore.ResetForTests();
                EnhancedWarmScheduler.ResetCompletedStats();
                EnhancedDiskCache.ResetForTests();
                EnhancedDiskCache.EnableForTests(tempRoot);

                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    "Stage2_MapPreview", UnityEngine.SceneManagement.LoadSceneMode.Single);

                float tBuild0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - tBuild0 < 180f)
                {
                    yield return null;
                    var loader = Object.FindFirstObjectByType<MapLoader>();
                    if (loader != null && loader.LastBuildSeconds > 0f)
                        break;
                }

                var gfx = GraphicsModeController.Ensure();
                GameSessionHost.Ensure().EnsureWadIdentity(wadPath);
                // Host Reset / scene bootstrap may clear disk enable — re-assert.
                EnhancedDiskCache.EnableForTests(tempRoot);

                yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Classic);
                EnhancedWarmScheduler.ResetCompletedStats();
                float tWarm0 = Time.realtimeSinceStartup;
                yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
                float seedSeconds = Time.realtimeSinceStartup - tWarm0;
                Assert.IsTrue(gfx.EnhancedWarmComplete);
                Assert.That(EnhancedWarmScheduler.LastCompletedComputeJobs, Is.GreaterThan(0));

                EnhancedDiskCache.Instance.FlushBlocking();
                Assert.IsTrue(File.Exists(EnhancedDiskCache.Instance.PackPath));
                long packBytes = EnhancedDiskCache.Instance.PackFileBytes;
                Assert.That(packBytes, Is.GreaterThan(1L * 1024L * 1024L));

                // Keep Classic prefs so the second Build does not warm without disk.
                prefs.Save(previous.WithGraphicsMode(GraphicsMode.Classic));

                // Cold process: wipe store + disk memory; keep pack file.
                GameSessionHost.ResetForTests();
                EnhancedVariantStore.ResetForTests();
                EnhancedDiskCache.ResetForTests();
                EnhancedDiskCache.EnableForTests(tempRoot);
                EnhancedWarmScheduler.ResetCompletedStats();

                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    "Stage2_MapPreview", UnityEngine.SceneManagement.LoadSceneMode.Single);
                float tBuild1 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - tBuild1 < 180f)
                {
                    yield return null;
                    var loader = Object.FindFirstObjectByType<MapLoader>();
                    if (loader != null && loader.LastBuildSeconds > 0f)
                        break;
                }

                gfx = GraphicsModeController.Ensure();
                Assert.AreEqual(GraphicsMode.Classic, gfx.Current);
                GameSessionHost.Ensure().EnsureWadIdentity(wadPath);
                EnhancedDiskCache.EnableForTests(tempRoot);
                EnhancedDiskCache.Instance.BindWad(wadPath);
                yield return EnhancedDiskCache.Instance.WaitUntilLoaded();
                Assert.That(EnhancedDiskCache.Instance.Count, Is.GreaterThan(0),
                    "pack must load into disk cache before cold warm");

                EnhancedWarmScheduler.ResetCompletedStats();
                float tCold0 = Time.realtimeSinceStartup;
                yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
                float coldSeconds = Time.realtimeSinceStartup - tCold0;

                int compute = EnhancedWarmScheduler.LastCompletedComputeJobs;
                int diskHits = EnhancedWarmScheduler.LastCompletedDiskHits;
                int storeHits = EnhancedWarmScheduler.LastCompletedStoreHits;

                Debug.Log(
                    $"EnhancedDiskCachePlayTests: E1M1 cold disk warm; " +
                    $"seedComputeWarm={seedSeconds:F2}s coldWarm={coldSeconds:F2}s " +
                    $"compute={compute} diskHits={diskHits} storeHits={storeHits} " +
                    $"packMB={packBytes / (1024f * 1024f):F1}");

                Assert.That(diskHits, Is.GreaterThan(0), "cold warm must hit the pack");
                Assert.That(diskHits, Is.GreaterThan(compute * 10),
                    "disk hits must dominate residual compute on a cold pack warm");
                // Gate ≤ ~5 s on desktop SSD; generous CI margin.
                Assert.That(coldSeconds, Is.LessThan(30f),
                    $"cold disk warm {coldSeconds:F2}s exceeds 30s CI margin (gate ~5s)");

                // Isolation: leave the persistent controller in Classic so later
                // fixtures don't inherit an Enhanced mode and warm on scene load.
                yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Classic);
            }
            finally
            {
                prefs.Save(previous);
            }
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Corrupt_pack_recomputes_without_player_errors()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            if (!File.Exists(wadPath))
                Assert.Ignore("freedoom1.wad missing");

            string wadId = GameSessionHost.ComputeWadIdentity(wadPath);
            byte[] hash = EnhancedDiskCache.ComputeWadSha256(wadPath);
            string packPath = Path.Combine(
                tempRoot,
                EnhancedDiskCache.BuildPackFileName(hash, EnhancedPipelineVersion.Value));
            File.WriteAllBytes(packPath, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

            EnhancedVariantStore.Instance.BindWadIdentity(wadId);

            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);
            var names = new List<string> { "FLOOR0_1" };

            var factory = new DoomMaterialFactory();
            factory.SetActiveProfile(GraphicsProfile.Enhanced);
            var cache = new TextureCache(wad, textures, palette, factory);
            cache.GetTexture(names[0], WorldTextureVariant.Native);

            using (var scheduler = new EnhancedWarmScheduler())
            {
                yield return scheduler.Warm(
                    cache, sprites: null, hud: null, names,
                    warmWorld: true, warmSprites: false, warmHud: false,
                    reportProgress: null,
                    wadIdentity: wadId,
                    wadPath: wadPath);
                Assert.That(scheduler.LastJobsStarted, Is.GreaterThan(0));
                Assert.That(scheduler.LastDiskHits, Is.EqualTo(0));
            }

            Assert.That(cache.EnhancedVariantCount, Is.GreaterThan(0));
        }
    }
}
