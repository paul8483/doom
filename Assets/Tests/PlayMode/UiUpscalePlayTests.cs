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
    public class UiUpscalePlayTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
            HudTextureCache.ForceEnhancedFailureForTests = false;
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

        static HudTextureCache OpenHudCache(GraphicsProfile profile, out WadFile wad)
        {
            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            if (!File.Exists(wadPath))
                Assert.Ignore("freedoom1.wad missing");

            wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var catalog = UiPatchCatalog.LoadStandard(wad, palette);
            return new HudTextureCache(catalog, profile: profile);
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
        public IEnumerator Hud_enhanced_is_4x_with_native_placement_dims()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            var cache = OpenHudCache(GraphicsProfile.Enhanced, out var wad);
            using (wad)
            {
                Assert.IsTrue(cache.TryGet("STBAR", WorldTextureVariant.Native, out var native));
                Assert.IsTrue(native.IsValid);
                int headerW = native.Width;
                int headerH = native.Height;
                int left = native.LeftOffset;
                int top = native.TopOffset;
                int nativeTexW = native.Texture.width;
                int nativeTexH = native.Texture.height;

                Assert.IsTrue(cache.TryGet("STBAR", WorldTextureVariant.Enhanced4X, out var enhanced));
                Assert.IsTrue(enhanced.IsValid);
                Assert.AreEqual(nativeTexW * 4, enhanced.Texture.width);
                Assert.AreEqual(nativeTexH * 4, enhanced.Texture.height);
                // Placement from PatchHeader — not texture dims.
                Assert.AreEqual(headerW, enhanced.Width);
                Assert.AreEqual(headerH, enhanced.Height);
                Assert.AreEqual(left, enhanced.LeftOffset);
                Assert.AreEqual(top, enhanced.TopOffset);

                var t = VirtualScreenRenderer.Compute(1280, 800);
                var rNative = VirtualScreenRenderer.ToScreenSnapped(
                    t, 0, 168, native.Width, native.Height);
                var rEnhanced = VirtualScreenRenderer.ToScreenSnapped(
                    t, 0, 168, enhanced.Width, enhanced.Height);
                Assert.AreEqual(rNative, rEnhanced);
            }
        }

        [UnityTest]
        public IEnumerator Face_patch_offsets_shift_placement_like_v_drawpatch()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            var cache = OpenHudCache(GraphicsProfile.Enhanced, out var wad);
            using (wad)
            {
                // Freedoom face patches carry (-5,-2); ignoring them drew the
                // face left/up of the STBAR frame center (regression 2026-07-22).
                Assert.IsTrue(cache.TryGet("STFST01", WorldTextureVariant.Native, out var face));
                Assert.IsTrue(face.IsValid);
                Assert.AreEqual(-5, face.LeftOffset, "Freedoom face left offset");
                Assert.AreEqual(-2, face.TopOffset, "Freedoom face top offset");

                var t = VirtualScreenRenderer.Compute(1280, 800);
                var expected = VirtualScreenRenderer.ToScreenSnapped(
                    t, 143 + 5, 168 + 2, face.Width, face.Height);
                var actual = DoomHud.PlacementRect(t, face, 143, 168);
                Assert.AreEqual(expected, actual);

                // Zero-offset patches are unaffected by the fix.
                Assert.IsTrue(cache.TryGet("STTNUM0", WorldTextureVariant.Native, out var digit));
                Assert.AreEqual(0, digit.LeftOffset);
                Assert.AreEqual(0, digit.TopOffset);
                var digitExpected = VirtualScreenRenderer.ToScreenSnapped(
                    t, 48, 171, digit.Width, digit.Height);
                Assert.AreEqual(digitExpected, DoomHud.PlacementRect(t, digit, 48, 171));
            }
        }

        [UnityTest]
        public IEnumerator Weapon_placement_rect_identical_native_vs_enhanced()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            var cache = OpenSpriteCache(GraphicsProfile.Enhanced, out var wad);
            using (wad)
            {
                var native = cache.WarmNativeWeapon("PISG", 0, 0);
                var enhanced = cache.GetWeapon("PISG", 0, 0);
                Assert.IsTrue(native.IsValid);
                Assert.IsTrue(enhanced.IsValid);
                // Enhanced serves the 4x display redraw; the placement rect
                // must not move, because it is built from header dims only.
                Assert.AreNotSame(native.Material.mainTexture,
                    enhanced.Material.mainTexture);
                Assert.AreEqual(
                    native.Width * WeaponRedrawAllowlist.Scale,
                    enhanced.Material.mainTexture.width);
                Assert.AreEqual(native.Width, enhanced.Width);
                Assert.AreEqual(native.Height, enhanced.Height);
                Assert.AreEqual(native.LeftOffset, enhanced.LeftOffset);
                Assert.AreEqual(native.TopOffset, enhanced.TopOffset);

                var t = VirtualScreenRenderer.Compute(1280, 800);
                const float sx = 1f, sy = 32f;
                var rNative = WeaponView.PlacementRect(t, sx, sy, native);
                var rEnhanced = WeaponView.PlacementRect(t, sx, sy, enhanced);
                Assert.AreEqual(rNative, rEnhanced);
                Assert.AreEqual(Mathf.Round(rEnhanced.x), rEnhanced.x);
                Assert.AreEqual(Mathf.Round(rEnhanced.y), rEnhanced.y);
                Assert.AreEqual(Mathf.Round(rEnhanced.width), rEnhanced.width);
                Assert.AreEqual(Mathf.Round(rEnhanced.height), rEnhanced.height);
            }
        }

        [UnityTest]
        public IEnumerator Active_profile_serves_enhanced_hud_and_native_menu()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            var cache = OpenHudCache(GraphicsProfile.Enhanced, out var wad);
            using (wad)
            {
                Assert.IsTrue(cache.TryGet("STBAR", out var hud));
                Assert.IsTrue(cache.TryGet("STBAR", WorldTextureVariant.Native, out var hudNative));
                Assert.AreEqual(hudNative.Texture.width * 4, hud.Texture.width);
                Assert.AreEqual(hudNative.Width, hud.Width);

                // Menus / intermission / title stay native even under Enhanced.
                Assert.IsTrue(cache.TryGet("TITLEPIC", out var title));
                Assert.IsTrue(cache.TryGet("TITLEPIC", WorldTextureVariant.Native, out var titleNative));
                Assert.AreSame(titleNative.Texture, title.Texture);
                Assert.AreEqual(titleNative.Texture.width, title.Texture.width);

                if (cache.TryGet("M_PAUSE", out var pause))
                {
                    Assert.IsTrue(cache.TryGet(
                        "M_PAUSE", WorldTextureVariant.Native, out var pauseNative));
                    Assert.AreSame(pauseNative.Texture, pause.Texture);
                }
            }
        }

        [UnityTest]
        public IEnumerator Classic_hud_stays_native_object()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            var cache = OpenHudCache(GraphicsProfile.Classic, out var wad);
            using (wad)
            {
                Assert.IsTrue(cache.TryGet("STBAR", out var a));
                Assert.IsTrue(cache.TryGet("STBAR", out var b));
                Assert.AreSame(a.Texture, b.Texture);
                Assert.AreEqual(0, cache.EnhancedVariantCount);
            }
        }

        [UnityTest]
        public IEnumerator Hud_enhanced_failure_falls_back_to_native()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;

            var cache = OpenHudCache(GraphicsProfile.Enhanced, out var wad);
            using (wad)
            {
                Assert.IsTrue(cache.TryGet("STTNUM0", WorldTextureVariant.Native, out var native));
                HudTextureCache.ForceEnhancedFailureForTests = true;
                Assert.IsTrue(cache.TryGet(
                    "STTNUM0", WorldTextureVariant.Enhanced4X, out var enhanced));
                Assert.AreSame(native.Texture, enhanced.Texture);
                Assert.AreEqual(0, cache.EnhancedVariantCount);

                var again = cache.TryGet(
                    "STTNUM0", WorldTextureVariant.Enhanced4X, out var enhanced2);
                Assert.IsTrue(again);
                Assert.AreSame(native.Texture, enhanced2.Texture);
            }
        }

        [UnityTest]
        public IEnumerator Hot_switch_restores_native_hud_and_weapon()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForMapBuild();

            var loader = Object.FindFirstObjectByType<MapLoader>();
            Assert.IsNotNull(loader);
            Assert.IsNotNull(loader.HudTextures);
            Assert.IsNotNull(loader.Sprites);

            var gfx = GraphicsModeController.Ensure();
            gfx.Apply(GraphicsMode.Classic);
            yield return null;

            Assert.IsTrue(loader.HudTextures.TryGet("STBAR", out var classicHud));
            var classicHudTex = classicHud.Texture;
            int hudW = classicHud.Width;
            int hudH = classicHud.Height;

            var classicWeapon = loader.Sprites.GetWeapon("PISG", 0, 0);
            Assert.IsTrue(classicWeapon.IsValid);
            var classicWeaponTex = classicWeapon.Material.mainTexture as Texture2D;
            Assert.IsNotNull(classicWeaponTex);

            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            Assert.IsNull(gfx.LastError, gfx.LastError);

            Assert.IsTrue(loader.HudTextures.TryGet("STBAR", out var enhancedHud));
            Assert.AreEqual(classicHudTex.width * 4, enhancedHud.Texture.width);
            Assert.AreEqual(hudW, enhancedHud.Width);
            Assert.AreEqual(hudH, enhancedHud.Height);

            // Enhanced routes the 4x weapon display redraw; header dims stay.
            var enhancedWeapon = loader.Sprites.GetWeapon("PISG", 0, 0);
            Assert.IsTrue(enhancedWeapon.IsValid);
            Assert.AreNotSame(classicWeaponTex, enhancedWeapon.Material.mainTexture);
            Assert.AreEqual(
                classicWeaponTex.width * WeaponRedrawAllowlist.Scale,
                enhancedWeapon.Material.mainTexture.width);
            Assert.AreEqual(classicWeapon.Width, enhancedWeapon.Width);

            int texCountAfterEnhanced = gfx.Context != null ? gfx.Context.TextureCount : 0;

            gfx.Apply(GraphicsMode.Classic);
            yield return null;

            Assert.IsTrue(loader.HudTextures.TryGet("STBAR", out var restoredHud));
            Assert.AreSame(classicHudTex, restoredHud.Texture);
            Assert.AreEqual(hudW, restoredHud.Width);

            var restoredWeapon = loader.Sprites.GetWeapon("PISG", 0, 0);
            Assert.AreSame(classicWeaponTex, restoredWeapon.Material.mainTexture);

            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            Assert.IsTrue(loader.HudTextures.TryGet("STBAR", out var enhancedAgain));
            Assert.AreSame(enhancedHud.Texture, enhancedAgain.Texture);
            if (gfx.Context != null)
                Assert.AreEqual(texCountAfterEnhanced, gfx.Context.TextureCount);
        }
    }
}
