using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.MapBuild;
using Doom.MapBuild.Rendering;

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
        }

        [UnityTest]
        public IEnumerator Enhanced_world_textures_are_2x_mipped_and_hot_switch_restores_native()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

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

            gfx.Apply(GraphicsMode.Enhanced);
            yield return null;
            Assert.IsNull(gfx.LastError, gfx.LastError);

            var enhanced = loader.WorldTextures.GetTexture(sampleName, WorldTextureVariant.Enhanced2X);
            Assert.IsNotNull(enhanced);
            Assert.AreNotSame(nativeTex, enhanced);
            Assert.AreEqual(nativeW * 2, enhanced.width);
            Assert.AreEqual(nativeH * 2, enhanced.height);
            Assert.AreEqual(FilterMode.Trilinear, enhanced.filterMode);
            Assert.That(enhanced.mipmapCount, Is.GreaterThan(1));
            Assert.That(enhanced.anisoLevel, Is.GreaterThan(1));
            Assert.AreSame(enhanced,
                loader.WorldTextures.GetTexture(sampleName, WorldTextureVariant.Enhanced2X));

            // Material on a matching renderer should now reference the 2× object.
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
            Assert.IsTrue(foundEnhancedMat, "Enhanced materials should use 2× albedo");

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
            for (int i = 0; i < 20; i++)
            {
                gfx.Apply(i % 2 == 0 ? GraphicsMode.Enhanced : GraphicsMode.Classic);
                yield return null;
            }
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
            for (int i = 0; i < 90; i++) yield return null;

            var loader = Object.FindFirstObjectByType<MapLoader>();
            var gfx = GraphicsModeController.Ensure();

            var nativeSky = loader.WorldTextures.GetTexture(
                WadSkyRenderer.SkyTextureName, WorldTextureVariant.Native);
            Assert.IsNotNull(nativeSky);

            gfx.Apply(GraphicsMode.Enhanced);
            yield return null;

            var enhancedSky = loader.WorldTextures.GetTexture(
                WadSkyRenderer.SkyTextureName, WorldTextureVariant.Enhanced2X);
            Assert.AreEqual(nativeSky.width * 2, enhancedSky.width);
            Assert.AreEqual(nativeSky.height * 2, enhancedSky.height);
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
    }
}
