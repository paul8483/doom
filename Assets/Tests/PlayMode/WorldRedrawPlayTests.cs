using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Doom.Graphics;
using Doom.MapBuild;
using Doom.MapBuild.Rendering;
using Doom.Wad;

namespace Doom.Stage3.PlayTests
{
    /// World redraw seam end-to-end: an allowlisted (here: injected) redraw is
    /// carried into the Enhanced4X albedo texture, an invalid one falls back to
    /// Super-xBR, and Classic/native is never touched.
    public class WorldRedrawPlayTests
    {
        [SetUp]
        public void SetUp()
        {
            WorldRedrawCatalog.ClearForTests();
            EnhancedVariantStore.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            WorldRedrawCatalog.ClearForTests();
            LogAssert.ignoreFailingMessages = false;
        }

        static string WadPath => Path.Combine(
            Application.streamingAssetsPath, "wads", "freedoom1.wad");

        static DecodedImage Solid(int w, int h, byte r, byte g, byte b)
        {
            var rgba = new byte[w * h * 4];
            for (int i = 0; i < rgba.Length; i += 4)
            {
                rgba[i] = r;
                rgba[i + 1] = g;
                rgba[i + 2] = b;
                rgba[i + 3] = 255;
            }
            return new DecodedImage(w, h, rgba);
        }

        [UnityTest]
        public IEnumerator Redraw_is_carried_into_enhanced_albedo()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;
            if (!File.Exists(WadPath)) Assert.Ignore("freedoom1.wad missing");

            using var wad = WadFile.Open(WadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);
            var factory = new DoomMaterialFactory();
            factory.SetActiveProfile(GraphicsProfile.Enhanced);
            var cache = new TextureCache(wad, textures, palette, factory);

            var native = cache.GetTexture("COMPTALL", WorldTextureVariant.Native);
            var marker = Solid(native.width * 4, native.height * 4, 37, 190, 66);
            WorldRedrawCatalog.SetOverrideForTests("COMPTALL", marker);

            var job = cache.TryCreateAlbedoJob("COMPTALL");
            Assert.IsNotNull(job);
            Assert.IsNotNull(job.Redraw, "albedo job must carry the redraw");

            // CPU content: level zero is the redraw verbatim, not Super-xBR.
            var result = EnhancedJobRunner.Run(job);
            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(marker.Rgba, result.AlbedoMips[0].Rgba);

            var enhanced = cache.GetTexture("COMPTALL", WorldTextureVariant.Enhanced4X);
            Assert.AreEqual(native.width * 4, enhanced.width);
            Assert.AreEqual(native.height * 4, enhanced.height);
            Assert.AreEqual(1, cache.EnhancedVariantCount,
                "redraw builds a real Enhanced variant, not a native alias");
        }

        [UnityTest]
        public IEnumerator Invalid_redraw_falls_back_to_superxbr()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;
            if (!File.Exists(WadPath)) Assert.Ignore("freedoom1.wad missing");

            using var wad = WadFile.Open(WadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);
            var factory = new DoomMaterialFactory();
            factory.SetActiveProfile(GraphicsProfile.Enhanced);
            var cache = new TextureCache(wad, textures, palette, factory);

            var native = cache.GetTexture("COMPTALL", WorldTextureVariant.Native);
            // 2x instead of the 4x contract — catalog must reject it.
            WorldRedrawCatalog.SetOverrideForTests(
                "COMPTALL", Solid(native.width * 2, native.height * 2, 37, 190, 66));

            var job = cache.TryCreateAlbedoJob("COMPTALL");
            Assert.IsNotNull(job);
            Assert.IsNull(job.Redraw, "invalid redraw must not reach the job");

            var enhanced = cache.GetTexture("COMPTALL", WorldTextureVariant.Enhanced4X);
            Assert.AreEqual(native.width * 4, enhanced.width, "Super-xBR fallback still 4x");
            Assert.AreEqual(1, cache.EnhancedVariantCount);
        }

        [UnityTest]
        public IEnumerator Native_variant_ignores_redraws()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;
            if (!File.Exists(WadPath)) Assert.Ignore("freedoom1.wad missing");

            using var wad = WadFile.Open(WadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);
            var factory = new DoomMaterialFactory();
            factory.SetActiveProfile(GraphicsProfile.Classic);
            var cache = new TextureCache(wad, textures, palette, factory);

            var nativeSize = textures.Build("COMPTALL", palette);
            WorldRedrawCatalog.SetOverrideForTests(
                "COMPTALL",
                Solid(nativeSize.Width * 4, nativeSize.Height * 4, 37, 190, 66));

            var native = cache.GetTexture("COMPTALL", WorldTextureVariant.Native);
            Assert.AreEqual(nativeSize.Width, native.width,
                "Classic/native path must stay bit-true regardless of redraws");
        }
    }
}
