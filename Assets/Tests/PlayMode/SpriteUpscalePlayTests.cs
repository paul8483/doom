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
    public class SpriteUpscalePlayTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
            SpriteCache.ForceEnhancedFailureForTests = false;
        }

        static IEnumerator WaitForMapBuild(int maxFrames = 3600)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                yield return null;
                var loader = Object.FindFirstObjectByType<MapLoader>();
                if (loader != null && loader.LastBuildSeconds > 0f)
                    yield break;
            }
            Assert.Fail("MapLoader build did not finish in time");
        }

        static SpriteCache OpenSpriteCache(GraphicsProfile profile, out WadFile wad)
        {
            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            if (!File.Exists(wadPath))
                Assert.Ignore("freedoom1.wad missing");

            wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var sprites = SpriteSet.Load(wad);
            var factory = new DoomMaterialFactory();
            factory.SetActiveProfile(profile);
            return new SpriteCache(wad, sprites, palette, factory);
        }

        [UnityTest]
        public IEnumerator Enhanced_sprite_is_4x_with_native_header_dims()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            var cache = OpenSpriteCache(GraphicsProfile.Enhanced, out var wad);
            using (wad)
            {
                var native = cache.Get("POSS", 0, 0, spectre: false, WorldTextureVariant.Native);
                Assert.IsTrue(native.IsValid);
                Assert.IsNotNull(native.Material.mainTexture);
                int nativeW = native.Material.mainTexture.width;
                int nativeH = native.Material.mainTexture.height;
                int headerW = native.Width;
                int headerH = native.Height;
                int left = native.LeftOffset;
                int top = native.TopOffset;

                var enhanced = cache.Get("POSS", 0, 0, spectre: false, WorldTextureVariant.Enhanced4X);
                Assert.IsTrue(enhanced.IsValid);
                Assert.AreEqual(nativeW * 4, enhanced.Material.mainTexture.width);
                Assert.AreEqual(nativeH * 4, enhanced.Material.mainTexture.height);
                // Placement rects come from PatchHeader — not texture dims.
                Assert.AreEqual(headerW, enhanced.Width);
                Assert.AreEqual(headerH, enhanced.Height);
                Assert.AreEqual(left, enhanced.LeftOffset);
                Assert.AreEqual(top, enhanced.TopOffset);
                Assert.AreEqual(native.Mirrored, enhanced.Mirrored);
                Assert.AreSame(
                    enhanced.Material,
                    cache.Get("POSS", 0, 0, spectre: false, WorldTextureVariant.Enhanced4X).Material);
            }
        }

        [UnityTest]
        public IEnumerator Classic_sprite_stays_native_object()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            var cache = OpenSpriteCache(GraphicsProfile.Classic, out var wad);
            using (wad)
            {
                var a = cache.Get("POSS", 0, 0);
                var b = cache.Get("POSS", 0, 0);
                Assert.IsTrue(a.IsValid);
                Assert.AreSame(a.Material, b.Material);
                Assert.AreSame(a.Material.mainTexture, b.Material.mainTexture);
                Assert.AreEqual(0, cache.EnhancedVariantCount);
            }
        }

        [UnityTest]
        public IEnumerator Spectre_variant_shares_enhanced_4x_source()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            var cache = OpenSpriteCache(GraphicsProfile.Enhanced, out var wad);
            using (wad)
            {
                var normal = cache.Get("POSS", 0, 0, spectre: false, WorldTextureVariant.Enhanced4X);
                var spectre = cache.Get("POSS", 0, 0, spectre: true, WorldTextureVariant.Enhanced4X);
                Assert.IsTrue(normal.IsValid);
                Assert.IsTrue(spectre.IsValid);
                Assert.AreNotSame(normal.Material, spectre.Material);
                Assert.AreSame(normal.Material.mainTexture, spectre.Material.mainTexture);
                Assert.AreEqual(
                    normal.Material.mainTexture.width,
                    spectre.Material.mainTexture.width);
                Assert.That(spectre.Material.mainTexture.width, Is.GreaterThan(0));
                Assert.AreEqual(
                    cache.Get("POSS", 0, 0, spectre: false, WorldTextureVariant.Native)
                        .Material.mainTexture.width * 4,
                    spectre.Material.mainTexture.width);
            }
        }

        [UnityTest]
        public IEnumerator Masked_sprite_pipeline_has_no_dark_visible_rgb()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            // Synthetic cutout: opaque light cells, transparent cells with black RGB.
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
        public IEnumerator Enhanced_transform_failure_falls_back_to_native()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            var cache = OpenSpriteCache(GraphicsProfile.Enhanced, out var wad);
            using (wad)
            {
                var native = cache.Get("POSS", 0, 0, spectre: false, WorldTextureVariant.Native);
                Assert.IsTrue(native.IsValid);

                SpriteCache.ForceEnhancedFailureForTests = true;
                var enhanced = cache.Get("POSS", 0, 0, spectre: false, WorldTextureVariant.Enhanced4X);
                Assert.IsTrue(enhanced.IsValid);
                Assert.AreSame(native.Material.mainTexture, enhanced.Material.mainTexture);
                Assert.AreEqual(0, cache.EnhancedVariantCount);

                var again = cache.Get("POSS", 0, 0, spectre: false, WorldTextureVariant.Enhanced4X);
                Assert.AreSame(native.Material.mainTexture, again.Material.mainTexture);
                Assert.AreEqual(0, cache.EnhancedVariantCount);
            }
        }

        [UnityTest]
        public IEnumerator Hot_switch_restores_native_sprite_textures()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForMapBuild();

            var loader = Object.FindFirstObjectByType<MapLoader>();
            Assert.IsNotNull(loader);
            Assert.IsNotNull(loader.Sprites);

            var gfx = GraphicsModeController.Ensure();
            gfx.Apply(GraphicsMode.Classic);
            yield return null;

            // Sample a live billboard material under Classic.
            Texture2D classicTex = null;
            int headerW = 0, headerH = 0;
            foreach (var bb in Object.FindObjectsByType<SpriteBillboard>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var r = bb.GetComponent<MeshRenderer>();
                if (r == null || r.sharedMaterial == null) continue;
                if (r.sharedMaterial.mainTexture is Texture2D t)
                {
                    classicTex = t;
                    // Force a cache resolve for header dims via a known monster.
                    break;
                }
            }
            Assert.IsNotNull(classicTex, "expected a Classic sprite billboard texture");

            var nativeSm = loader.Sprites.Get(
                "POSS", 0, 0, spectre: false, WorldTextureVariant.Native);
            Assert.IsTrue(nativeSm.IsValid);
            headerW = nativeSm.Width;
            headerH = nativeSm.Height;
            var nativeTex = nativeSm.Material.mainTexture as Texture2D;
            Assert.IsNotNull(nativeTex);

            gfx.Apply(GraphicsMode.Enhanced);
            yield return null;
            Assert.IsNull(gfx.LastError, gfx.LastError);

            var enhancedSm = loader.Sprites.Get(
                "POSS", 0, 0, spectre: false, WorldTextureVariant.Enhanced4X);
            Assert.IsTrue(enhancedSm.IsValid);
            Assert.AreEqual(nativeTex.width * 4, enhancedSm.Material.mainTexture.width);
            Assert.AreEqual(nativeTex.height * 4, enhancedSm.Material.mainTexture.height);
            Assert.AreEqual(headerW, enhancedSm.Width);
            Assert.AreEqual(headerH, enhancedSm.Height);

            int texCountAfterEnhanced = gfx.Context != null ? gfx.Context.TextureCount : 0;

            gfx.Apply(GraphicsMode.Classic);
            yield return null;

            var restored = loader.Sprites.Get(
                "POSS", 0, 0, spectre: false, WorldTextureVariant.Native);
            Assert.AreSame(nativeTex, restored.Material.mainTexture);
            Assert.AreEqual(headerW, restored.Width);
            Assert.AreEqual(headerH, restored.Height);

            // Re-enter Enhanced: same Enhanced objects, no count growth from rebuild.
            gfx.Apply(GraphicsMode.Enhanced);
            yield return null;
            var enhancedAgain = loader.Sprites.Get(
                "POSS", 0, 0, spectre: false, WorldTextureVariant.Enhanced4X);
            Assert.AreSame(enhancedSm.Material.mainTexture, enhancedAgain.Material.mainTexture);
            if (gfx.Context != null)
                Assert.AreEqual(texCountAfterEnhanced, gfx.Context.TextureCount);
        }
    }
}
