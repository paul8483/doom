using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.Graphics;
using Doom.MapBuild;
using Doom.MapBuild.Rendering;
using Doom.Wad;

namespace Doom.Stage3.PlayTests
{
    /// Warm-performance Task 3 — session EnhancedVariantStore.
    public class EnhancedVariantStorePlayTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
            TextureCache.ForceEnhancedFailureForTests = false;
            SpriteCache.ForceEnhancedFailureForTests = false;
            HudTextureCache.ForceEnhancedFailureForTests = false;
            EnhancedVariantStore.ResetForTests();
            GameSessionHost.ResetForTests();
        }

        static IEnumerator WaitForMapBuild(float timeoutSeconds = 180f)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < timeoutSeconds)
            {
                yield return null;
                var loader = Object.FindFirstObjectByType<MapLoader>();
                if (loader != null && loader.LastBuildSeconds > 0f)
                    yield break;
            }
            Assert.Fail("MapLoader build did not finish in time");
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Second_warm_on_fresh_caches_is_upload_only()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            if (!File.Exists(wadPath))
                Assert.Ignore("freedoom1.wad missing");

            string wadId = GameSessionHost.ComputeWadIdentity(wadPath);
            EnhancedVariantStore.ResetForTests();
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
                    wadIdentity: wadId);
                Assert.That(first.LastJobsStarted, Is.GreaterThan(0));
                Assert.That(first.LastStoreHits, Is.EqualTo(0));
            }

            Assert.That(EnhancedVariantStore.Instance.Count, Is.GreaterThan(0));
            int world1 = cache1.EnhancedVariantCount;
            int normals1 = cache1.NormalMapCount;
            int sprites1 = spriteCache1.EnhancedVariantCount;
            int hudCount1 = hud1.EnhancedVariantCount;
            Assert.That(world1, Is.GreaterThan(0));
            Assert.That(normals1, Is.EqualTo(world1));

            // Fresh GPU caches (scene-reload style); session store keeps CPU results.
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
                    wadIdentity: wadId);

                Assert.That(second.LastJobsStarted, Is.EqualTo(0),
                    "second warm must not recompute — store upload only");
                Assert.That(second.LastStoreHits, Is.GreaterThan(0));
                Assert.That(second.LastJobsIntegrated, Is.EqualTo(0));
            }

            Assert.AreEqual(world1, cache2.EnhancedVariantCount);
            Assert.AreEqual(normals1, cache2.NormalMapCount);
            Assert.AreEqual(sprites1, spriteCache2.EnhancedVariantCount);
            Assert.AreEqual(hudCount1, hud2.EnhancedVariantCount);

            var floorEnh = cache2.GetTexture("FLOOR0_1", WorldTextureVariant.Enhanced4X);
            var floorNat = cache2.GetTexture("FLOOR0_1", WorldTextureVariant.Native);
            Assert.AreNotSame(floorNat, floorEnh);
            Assert.AreEqual(floorNat.width * 4, floorEnh.width);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Scene_reload_same_map_uses_store_zero_compute()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;
            EnhancedVariantStore.ResetForTests();
            EnhancedWarmScheduler.ResetCompletedStats();

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForMapBuild();

            var gfx = GraphicsModeController.Ensure();
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Classic);
            EnhancedWarmScheduler.ResetCompletedStats();
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);

            Assert.That(EnhancedWarmScheduler.LastCompletedComputeJobs, Is.GreaterThan(0));
            Assert.That(EnhancedVariantStore.Instance.Count, Is.GreaterThan(0));
            long storeBytes = EnhancedVariantStore.Instance.ApproximateCpuBytes;
            Assert.That(storeBytes, Is.GreaterThan(0L));

            var loader = Object.FindFirstObjectByType<MapLoader>();
            int world = loader.WorldTextures.EnhancedVariantCount;
            int normals = loader.WorldTextures.NormalMapCount;

            // Isolate second-load stats; keep GameSessionHost + store.
            EnhancedWarmScheduler.ResetCompletedStats();
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForMapBuild();

            gfx = GraphicsModeController.Ensure();
            loader = Object.FindFirstObjectByType<MapLoader>();

            // Persisted Enhanced mode warms during MapLoader; else Apply is upload-only.
            if (!gfx.EnhancedWarmComplete)
                yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);

            Assert.IsTrue(gfx.EnhancedWarmComplete);
            Assert.That(EnhancedWarmScheduler.LastCompletedComputeJobs, Is.EqualTo(0),
                "reload of same map must build 0 Enhanced jobs from store");
            Assert.That(EnhancedWarmScheduler.LastCompletedStoreHits, Is.GreaterThan(0));
            Assert.That(loader.WorldTextures.EnhancedVariantCount, Is.EqualTo(world));
            Assert.That(loader.WorldTextures.NormalMapCount, Is.EqualTo(normals));

            Debug.Log(
                $"EnhancedVariantStorePlayTests: reload upload-only; " +
                $"storeEntries={EnhancedVariantStore.Instance.Count} " +
                $"storeCpuMB={storeBytes / (1024f * 1024f):F1} " +
                $"storeHits={EnhancedWarmScheduler.LastCompletedStoreHits}");
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator E1M1_to_E1M2_reuses_store_for_shared_items()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;
            EnhancedVariantStore.ResetForTests();
            EnhancedWarmScheduler.ResetCompletedStats();

            MapLoader.MapNameOverride = "E1M1";
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForMapBuild();

            var gfx = GraphicsModeController.Ensure();
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Classic);
            EnhancedWarmScheduler.ResetCompletedStats();
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);

            int firstCompute = EnhancedWarmScheduler.LastCompletedComputeJobs;
            int storeAfterE1M1 = EnhancedVariantStore.Instance.Count;
            long storeBytes = EnhancedVariantStore.Instance.ApproximateCpuBytes;
            Assert.That(firstCompute, Is.GreaterThan(0));
            Assert.That(storeAfterE1M1, Is.GreaterThan(0));

            EnhancedWarmScheduler.ResetCompletedStats();
            MapLoader.MapNameOverride = "E1M2";
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForMapBuild();

            gfx = GraphicsModeController.Ensure();
            if (!gfx.EnhancedWarmComplete)
                yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);

            int e1m2Compute = EnhancedWarmScheduler.LastCompletedComputeJobs;
            int e1m2Hits = EnhancedWarmScheduler.LastCompletedStoreHits;
            Assert.That(e1m2Hits, Is.GreaterThan(0), "E1M2 must reuse session store hits");
            Assert.That(e1m2Compute, Is.LessThan(firstCompute / 2),
                "E1M2 should recompute far fewer items than a cold E1M1 warm");

            long managedMb = System.GC.GetTotalMemory(false) / (1024L * 1024L);
            long storeBytesNow = EnhancedVariantStore.Instance.ApproximateCpuBytes;
            Debug.Log(
                $"EnhancedVariantStorePlayTests: E1M1→E1M2; " +
                $"e1m1Compute={firstCompute} e1m2Compute={e1m2Compute} e1m2Hits={e1m2Hits} " +
                $"storeEntries={EnhancedVariantStore.Instance.Count} " +
                $"storeCpuMB={storeBytesNow / (1024f * 1024f):F1} " +
                $"(afterE1M1={storeBytes / (1024f * 1024f):F1}) managedMB={managedMb}");
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Classic_load_does_not_publish_to_store()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;
            EnhancedVariantStore.ResetForTests();

            // Pin Classic in PlayerPrefs so ApplyLoadedSettings cannot warm Enhanced.
            var prefs = new SettingsStore();
            var previous = prefs.Load();
            prefs.Save(previous.WithGraphicsMode(GraphicsMode.Classic));

            try
            {
                SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
                yield return WaitForMapBuild();

                // Allow ApplyLoadedSettings to finish after Build.
                for (int i = 0; i < 10; i++)
                    yield return null;

                var gfx = GraphicsModeController.Ensure();
                Assert.AreEqual(GraphicsMode.Classic, gfx.Current);
                Assert.AreEqual(0, EnhancedVariantStore.Instance.Count,
                    "Classic load must not publish Enhanced CPU results");
            }
            finally
            {
                prefs.Save(previous);
            }
        }
    }
}
