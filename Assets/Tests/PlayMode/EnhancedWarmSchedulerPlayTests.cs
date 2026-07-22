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
    /// Warm-performance Task 2 — parallel EnhancedWarmScheduler.
    public class EnhancedWarmSchedulerPlayTests
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
        [Timeout(600000)]
        public IEnumerator Parallel_warm_completes_with_enhanced_variants()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForMapBuild();

            var loader = Object.FindFirstObjectByType<MapLoader>();
            var gfx = GraphicsModeController.Ensure();
            Assert.IsNotNull(loader);
            Assert.IsNotNull(gfx.Context);

            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Classic);

            float t0 = Time.realtimeSinceStartup;
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            float warmSeconds = Time.realtimeSinceStartup - t0;

            Assert.IsTrue(gfx.EnhancedWarmComplete);
            Assert.That(loader.WorldTextures.EnhancedVariantCount, Is.GreaterThan(0));
            Assert.That(loader.WorldTextures.NormalMapCount, Is.GreaterThan(0));
            Assert.That(loader.WorldTextures.EnhancedTextureBytes, Is.GreaterThan(0L));
            if (loader.Sprites != null)
                Assert.That(loader.Sprites.EnhancedVariantCount, Is.GreaterThan(0));
            if (loader.HudTextures != null)
                Assert.That(loader.HudTextures.EnhancedVariantCount, Is.GreaterThan(0));

            // Idempotent: second Apply must not grow variant counts.
            int world = loader.WorldTextures.EnhancedVariantCount;
            int normals = loader.WorldTextures.NormalMapCount;
            int sprites = loader.Sprites != null ? loader.Sprites.EnhancedVariantCount : 0;
            int hud = loader.HudTextures != null ? loader.HudTextures.EnhancedVariantCount : 0;
            long worldBytes = loader.WorldTextures.EnhancedTextureBytes;

            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Classic);
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);

            Assert.AreEqual(world, loader.WorldTextures.EnhancedVariantCount);
            Assert.AreEqual(normals, loader.WorldTextures.NormalMapCount);
            Assert.AreEqual(worldBytes, loader.WorldTextures.EnhancedTextureBytes);
            if (loader.Sprites != null)
                Assert.AreEqual(sprites, loader.Sprites.EnhancedVariantCount);
            if (loader.HudTextures != null)
                Assert.AreEqual(hud, loader.HudTextures.EnhancedVariantCount);

            Debug.Log($"EnhancedWarmSchedulerPlayTests: first Enhanced warm {warmSeconds:F2}s " +
                      $"(world={world} normals={normals} sprites={sprites} hud={hud})");
            Assert.That(warmSeconds, Is.LessThan(120f), "warm should finish well under old 85s×margin");
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Progress_is_monotonic_across_phases()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            if (!File.Exists(wadPath))
                Assert.Ignore("freedoom1.wad missing");

            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);
            var factory = new DoomMaterialFactory();
            factory.SetActiveProfile(GraphicsProfile.Enhanced);
            var cache = new TextureCache(wad, textures, palette, factory);

            // Native decode first (warm path assumes sources exist).
            var names = new List<string> { "STARTAN2", "FLOOR0_1", "SKY1", "STEP1" };
            for (int i = 0; i < names.Count; i++)
                cache.GetTexture(names[i], WorldTextureVariant.Native);

            var sprites = SpriteSet.Load(wad);
            var spriteCache = new SpriteCache(wad, sprites, palette, factory);
            spriteCache.WarmNative("POSS", 0, 0);
            spriteCache.WarmNative("TROO", 0, 0);

            var ui = UiPatchCatalog.LoadStandard(wad, palette);
            var hud = new HudTextureCache(ui, profile: GraphicsProfile.Enhanced);

            var progress = new List<(float value, string label)>();
            using var scheduler = new EnhancedWarmScheduler();
            yield return scheduler.Warm(
                cache, spriteCache, hud, names,
                warmWorld: true, warmSprites: true, warmHud: true,
                reportProgress: (p, label) => progress.Add((p, label)),
                progressMin: 0.05f, progressMax: 0.95f);

            Assert.That(scheduler.LastJobsStarted, Is.GreaterThan(0));
            Assert.That(scheduler.LastJobsIntegrated, Is.EqualTo(scheduler.LastJobsStarted));
            Assert.That(progress.Count, Is.GreaterThan(0));

            float prev = -1f;
            bool sawTextures = false, sawSprites = false, sawHud = false;
            for (int i = 0; i < progress.Count; i++)
            {
                Assert.That(progress[i].value, Is.GreaterThanOrEqualTo(prev));
                prev = progress[i].value;
                if (progress[i].label == "ENHANCED TEXTURES") sawTextures = true;
                if (progress[i].label == "ENHANCED SPRITES") sawSprites = true;
                if (progress[i].label == "ENHANCED HUD") sawHud = true;
            }

            Assert.IsTrue(sawTextures);
            Assert.IsTrue(sawSprites);
            Assert.IsTrue(sawHud);
            Assert.That(cache.EnhancedVariantCount, Is.GreaterThan(0));
            Assert.That(cache.NormalMapCount, Is.GreaterThan(0));
            Assert.That(spriteCache.EnhancedVariantCount, Is.GreaterThan(0));
            Assert.That(hud.EnhancedVariantCount, Is.GreaterThan(0));
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Cancel_mid_warm_allows_clean_reload()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForMapBuild();

            var gfx = GraphicsModeController.Ensure();
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Classic);

            // Kick Classic→Enhanced warm, then tear the scene down mid-flight.
            gfx.Apply(GraphicsMode.Enhanced);
            bool sawApplying = false;
            for (int i = 0; i < 30; i++)
            {
                if (gfx.IsApplying) { sawApplying = true; break; }
                yield return null;
            }

            // If warm finished instantly (cached), still exercise reload.
            if (!sawApplying)
                Debug.Log("Cancel_mid_warm: warm finished before cancel window — reload still checked");

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForMapBuild();

            gfx = GraphicsModeController.Ensure();
            var loader = Object.FindFirstObjectByType<MapLoader>();
            Assert.IsNotNull(loader);
            Assert.That(loader.LastBuildSeconds, Is.GreaterThan(0f));

            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            Assert.IsTrue(gfx.EnhancedWarmComplete);
            Assert.That(loader.WorldTextures.EnhancedVariantCount, Is.GreaterThan(0));
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Single_job_failure_falls_back_native_only()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            if (!File.Exists(wadPath))
                Assert.Ignore("freedoom1.wad missing");

            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);
            var factory = new DoomMaterialFactory();
            factory.SetActiveProfile(GraphicsProfile.Enhanced);
            var cache = new TextureCache(wad, textures, palette, factory);

            var names = new List<string> { "STARTAN2", "FLOOR0_1", "STEP1" };
            for (int i = 0; i < names.Count; i++)
                cache.GetTexture(names[i], WorldTextureVariant.Native);

            var nativeStartan = cache.GetTexture("STARTAN2", WorldTextureVariant.Native);

            // Fail only the first Integrate by toggling the seam around one name:
            // warm all, but force-fail during job creation for STARTAN2 via seam
            // checked in Integrate — set for whole warm then clear after first item
            // is not available. Instead: warm with seam on, expect all fallback;
            // then clear seam and warm a fresh cache for mixed success.
            TextureCache.ForceEnhancedFailureForTests = true;
            using (var failScheduler = new EnhancedWarmScheduler())
            {
                yield return failScheduler.Warm(
                    cache, null, null, new List<string> { "STARTAN2" },
                    warmWorld: true, warmSprites: false, warmHud: false,
                    reportProgress: null);
            }

            var failed = cache.GetTexture("STARTAN2", WorldTextureVariant.Enhanced4X);
            Assert.AreSame(nativeStartan, failed);
            Assert.AreEqual(0, cache.EnhancedVariantCount);
            TextureCache.ForceEnhancedFailureForTests = false;

            // Sibling textures on a fresh cache still succeed when seam is off.
            var cache2 = new TextureCache(wad, textures, palette, factory);
            cache2.GetTexture("FLOOR0_1", WorldTextureVariant.Native);
            cache2.GetTexture("STEP1", WorldTextureVariant.Native);
            using (var okScheduler = new EnhancedWarmScheduler())
            {
                yield return okScheduler.Warm(
                    cache2, null, null, new List<string> { "FLOOR0_1", "STEP1" },
                    warmWorld: true, warmSprites: false, warmHud: false,
                    reportProgress: null);
            }

            Assert.That(cache2.EnhancedVariantCount, Is.EqualTo(2));
            Assert.That(cache2.NormalMapCount, Is.EqualTo(2));
            var floorEnh = cache2.GetTexture("FLOOR0_1", WorldTextureVariant.Enhanced4X);
            var floorNat = cache2.GetTexture("FLOOR0_1", WorldTextureVariant.Native);
            Assert.AreNotSame(floorNat, floorEnh);
            Assert.AreEqual(floorNat.width * 4, floorEnh.width);
        }
    }
}
