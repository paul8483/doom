using System.Collections;
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
    public class TextureUpscalePlayTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
            TextureCache.ForceEnhancedFailureForTests = false;
        }

        /// Parallel Enhanced warm is wall-clock bound; frame-count waits race it.
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
        public IEnumerator Enhanced_world_textures_are_4x_mipped_and_hot_switch_restores_native()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForMapBuild();

            var loader = Object.FindFirstObjectByType<MapLoader>();
            Assert.IsNotNull(loader);
            Assert.IsNotNull(loader.WorldTextures);

            var gfx = GraphicsModeController.Ensure();
            gfx.Apply(GraphicsMode.Classic);
            yield return null;

            string sampleName = null;
            Texture2D nativeTex = null;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var mat = r.sharedMaterial;
                if (mat == null) continue;
                string shader = mat.shader != null ? mat.shader.name : null;
                if (shader != DoomMaterialFactory.ClassicOpaqueName &&
                    shader != DoomMaterialFactory.ClassicCutoutName)
                    continue;
                if (mat.mainTexture is Texture2D t && !string.IsNullOrEmpty(t.name) &&
                    t.name != WadSkyRenderer.SkyTextureName)
                {
                    sampleName = t.name;
                    nativeTex = t;
                    break;
                }
            }

            Assert.IsNotNull(sampleName, "expected a Classic world material texture");
            Assert.IsNotNull(nativeTex);
            Assert.AreEqual(FilterMode.Point, nativeTex.filterMode);
            Assert.AreEqual(1, nativeTex.mipmapCount);

            int nativeW = nativeTex.width;
            int nativeH = nativeTex.height;
            var nativeFromCache = loader.WorldTextures.GetTexture(sampleName, WorldTextureVariant.Native);
            Assert.AreSame(nativeTex, nativeFromCache);

            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            Assert.IsNull(gfx.LastError, gfx.LastError);

            var enhanced = loader.WorldTextures.GetTexture(sampleName, WorldTextureVariant.Enhanced4X);
            Assert.IsNotNull(enhanced);
            Assert.AreNotSame(nativeTex, enhanced);
            Assert.AreEqual(nativeW * 4, enhanced.width);
            Assert.AreEqual(nativeH * 4, enhanced.height);
            Assert.AreEqual(FilterMode.Trilinear, enhanced.filterMode);
            Assert.That(enhanced.mipmapCount, Is.GreaterThan(1));
            Assert.That(enhanced.anisoLevel, Is.GreaterThan(1));
            Assert.AreSame(enhanced,
                loader.WorldTextures.GetTexture(sampleName, WorldTextureVariant.Enhanced4X));

            // Material on a matching renderer should now reference the 4× object.
            bool foundEnhancedMat = false;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var mat = r.sharedMaterial;
                if (mat == null || mat.mainTexture == null) continue;
                if (mat.mainTexture.name != sampleName) continue;
                if (mat.mainTexture == enhanced)
                {
                    foundEnhancedMat = true;
                    var bump = mat.HasProperty(DoomMaterialFactory.BumpMapProperty)
                        ? mat.GetTexture(DoomMaterialFactory.BumpMapProperty) as Texture2D
                        : null;
                    if (bump != null && bump != Texture2D.normalTexture)
                    {
                        Assert.AreEqual(enhanced.width, bump.width);
                        Assert.AreEqual(enhanced.height, bump.height);
                        Assert.AreEqual(enhanced.mipmapCount, bump.mipmapCount);
                    }
                    break;
                }
            }
            Assert.IsTrue(foundEnhancedMat, "Enhanced materials should use 4× albedo");

            var player = GameObject.Find("Player");
            Assert.IsNotNull(player);
            var health = player.GetComponent<PlayerHealth>();
            int hp = health.Health;
            Vector3 pos = player.transform.position;

            gfx.Apply(GraphicsMode.Classic);
            yield return null;
            Assert.AreSame(nativeTex,
                loader.WorldTextures.GetTexture(sampleName, WorldTextureVariant.Native));
            Assert.AreEqual(hp, health.Health);
            Assert.AreEqual(pos.x, player.transform.position.x, 0.001f);

            int texCount = gfx.Context.TextureCount;
            int enhancedCount = loader.WorldTextures.EnhancedVariantCount;
            int normals = loader.WorldTextures.NormalMapCount;
            // No frame yields: sprite billboards lazy-create Enhanced frames
            // while the clock runs, which is not a hot-switch leak.
            Time.timeScale = 0f;
            for (int i = 0; i < 20; i++)
                gfx.Apply(i % 2 == 0 ? GraphicsMode.Enhanced : GraphicsMode.Classic);
            Time.timeScale = 1f;
            Assert.AreEqual(texCount, gfx.Context.TextureCount);
            Assert.AreEqual(enhancedCount, loader.WorldTextures.EnhancedVariantCount);
            Assert.AreEqual(normals, loader.WorldTextures.NormalMapCount);
        }

        [UnityTest]
        public IEnumerator Sky_and_fluids_use_matching_variant_dimensions()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForMapBuild();

            var loader = Object.FindFirstObjectByType<MapLoader>();
            var gfx = GraphicsModeController.Ensure();

            var nativeSky = loader.WorldTextures.GetTexture(
                WadSkyRenderer.SkyTextureName, WorldTextureVariant.Native);
            Assert.IsNotNull(nativeSky);

            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);

            var enhancedSky = loader.WorldTextures.GetTexture(
                WadSkyRenderer.SkyTextureName, WorldTextureVariant.Enhanced4X);
            // SKY1 ships an 8x redraw (WorldRedrawAllowlist.ScaleFor); the
            // Enhanced slot simply carries the larger level zero.
            int skyScale = WorldRedrawAllowlist.ScaleFor(WadSkyRenderer.SkyTextureName);
            Assert.AreEqual(nativeSky.width * skyScale, enhancedSky.width);
            Assert.AreEqual(nativeSky.height * skyScale, enhancedSky.height);
            Assert.That(enhancedSky.mipmapCount, Is.GreaterThan(1));
            Assert.AreEqual(FilterMode.Trilinear, enhancedSky.filterMode);

            var sky = Object.FindFirstObjectByType<WadSkyRenderer>();
            Assert.IsNotNull(sky);
            Assert.IsNotNull(sky.SkyTexture);
            Assert.AreEqual(enhancedSky.width, sky.SkyTexture.width);

            gfx.Apply(GraphicsMode.Classic);
            yield return null;
            Assert.AreEqual(nativeSky.width, sky.SkyTexture.width);
        }

        [UnityTest]
        public IEnumerator Enhanced4X_wrap_modes_match_surface_kind()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            // Own open WAD: MapLoader closes the file after load, so late GetTexture
            // of a never-warmed name would decode as placeholder (Clamp/Clamp).
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

            var wall = cache.GetTexture("STARTAN2", WorldTextureVariant.Enhanced4X);
            Assert.AreEqual(TextureWrapMode.Repeat, wall.wrapModeU);
            // Walls tile vertically in DOOM (tall walls repeat the texture);
            // Clamp here smeared edge rows into streaks on E1M6's tall shaft.
            Assert.AreEqual(TextureWrapMode.Repeat, wall.wrapModeV);
            Assert.AreEqual(cache.GetTexture("STARTAN2", WorldTextureVariant.Native).width * 4,
                wall.width);

            var flat = cache.GetTexture("FLOOR4_8", WorldTextureVariant.Enhanced4X);
            Assert.AreEqual(TextureWrapMode.Repeat, flat.wrapModeU);
            Assert.AreEqual(TextureWrapMode.Repeat, flat.wrapModeV);

            // SKY1 is a patch lump (not TEXTURE1); DecodeWithKind falls through to
            // placeholder today — Clamp wrap is the documented placeholder policy.
            var placeholder = cache.GetTexture(
                "__NO_SUCH_TEXQUALITY__", WorldTextureVariant.Enhanced4X);
            Assert.AreEqual(TextureWrapMode.Clamp, placeholder.wrapModeU);
            Assert.AreEqual(TextureWrapMode.Clamp, placeholder.wrapModeV);
            Assert.AreEqual(64 * 4, placeholder.width);
            Assert.AreEqual(64 * 4, placeholder.height);
        }

        [UnityTest]
        public IEnumerator Masked_Enhanced4X_pipeline_has_no_dark_visible_rgb()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            // Synthetic grate: opaque light cells, transparent cells with hidden black RGB.
            // Real WAD art may contain legitimate black opaque paint — that is not a fringe.
            var rgba = new byte[4 * 4 * 4];
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                int o = (y * 4 + x) * 4;
                bool opaque = ((x + y) & 1) == 0;
                if (opaque)
                {
                    rgba[o] = 220; rgba[o + 1] = 220; rgba[o + 2] = 220; rgba[o + 3] = 255;
                }
                else
                {
                    rgba[o] = 0; rgba[o + 1] = 0; rgba[o + 2] = 0; rgba[o + 3] = 0;
                }
            }

            var native = new DecodedImage(4, 4, rgba);
            var x4 = TextureCache.BuildEnhanced4XDecoded(
                native, PixelWrapMode.Clamp, applyDedither: true, applyAlphaBleed: true);

            Assert.AreEqual(16, x4.Width);
            Assert.AreEqual(16, x4.Height);

            const byte cutoff = 128;
            for (int y = 0; y < x4.Height; y++)
            for (int x = 0; x < x4.Width; x++)
            {
                var p = x4.GetPixel(x, y);
                if (p.a <= cutoff) continue;
                Assert.Greater(p.r, 40, $"dark fringe r at ({x},{y}) a={p.a}");
                Assert.Greater(p.g, 40, $"dark fringe g at ({x},{y}) a={p.a}");
                Assert.Greater(p.b, 40, $"dark fringe b at ({x},{y}) a={p.a}");
            }
        }

        [UnityTest]
        public IEnumerator Enhanced4X_transform_failure_falls_back_to_native()
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

            var native = cache.GetTexture("STARTAN2", WorldTextureVariant.Native);
            Assert.IsNotNull(native);

            TextureCache.ForceEnhancedFailureForTests = true;
            var enhanced = cache.GetTexture("STARTAN2", WorldTextureVariant.Enhanced4X);
            Assert.AreSame(native, enhanced, "failed transform must return native");
            Assert.AreEqual(0, cache.EnhancedVariantCount);

            // Failed state is cached — no second attempt / no Enhanced object.
            var again = cache.GetTexture("STARTAN2", WorldTextureVariant.Enhanced4X);
            Assert.AreSame(native, again);
            Assert.AreEqual(0, cache.EnhancedVariantCount);
        }

    }
}
