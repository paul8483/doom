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
    public class EnhancedMaterialPlayTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
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
        public IEnumerator Enhanced_assigns_lit_shader_normal_and_surface_props()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForMapBuild();

            var loader = Object.FindFirstObjectByType<MapLoader>();
            Assert.IsNotNull(loader);
            Assert.IsNotNull(loader.WorldTextures);

            var gfx = GraphicsModeController.Ensure();
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            Assert.AreEqual(GraphicsMode.Enhanced, gfx.Current);
            Assert.IsNull(gfx.LastError, gfx.LastError);
            Assert.IsTrue(gfx.ActiveProfile.WorldTexelAA);

            int enhanced = 0;
            int withNormal = 0;
            int withAlbedo = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var mat = r.sharedMaterial;
                if (mat == null || mat.shader == null) continue;
                string name = mat.shader.name;
                if (name != DoomMaterialFactory.EnhancedOpaqueName &&
                    name != DoomMaterialFactory.EnhancedCutoutName)
                    continue;

                enhanced++;
                Assert.That(name, Does.Not.Contain("Hidden/InternalErrorShader"));
                Assert.IsTrue(mat.IsKeywordEnabled(DoomMaterialFactory.TexelAaKeyword),
                    "Enhanced world materials must enable DOOM_TEXEL_AA");
                if (name == DoomMaterialFactory.EnhancedOpaqueName)
                {
                    // Most solid walls/flats enable POM; fluid-category amplitude is 0.
                    // At least one opaque with parallax is asserted after the loop.
                }
                else
                {
                    Assert.IsFalse(mat.IsKeywordEnabled(DoomMaterialFactory.ParallaxKeyword),
                        "Cutout must not enable DOOM_PARALLAX");
                }
                Assert.IsTrue(mat.HasProperty(DoomMaterialFactory.BumpMapProperty));
                Assert.IsTrue(mat.HasProperty(DoomMaterialFactory.RoughnessProperty));
                Assert.IsTrue(mat.HasProperty(DoomMaterialFactory.EmissionProperty));

                if (mat.mainTexture is Texture2D albedo)
                {
                    withAlbedo++;
                    Assert.AreEqual(FilterMode.Trilinear, albedo.filterMode,
                        "Enhanced albedo uses controlled mips (Trilinear) with texel-AA");
                    Assert.That(albedo.mipmapCount, Is.GreaterThan(1));
                    Assert.That(albedo.anisoLevel, Is.GreaterThan(1));
                }

                var bump = mat.GetTexture(DoomMaterialFactory.BumpMapProperty);
                if (bump != null && bump != Texture2D.normalTexture)
                {
                    withNormal++;
                    Assert.That(bump.name, Does.EndWith("/Normal"));
                    if (bump is Texture2D bumpTex)
                    {
                        Assert.AreEqual(FilterMode.Trilinear, bumpTex.filterMode);
                        Assert.That(bumpTex.mipmapCount, Is.GreaterThan(1));
                    }
                }

                float roughness = mat.GetFloat(DoomMaterialFactory.RoughnessProperty);
                Assert.That(roughness, Is.InRange(0f, 1f));
            }

            Assert.That(enhanced, Is.GreaterThan(0), "expected Enhanced world materials");
            Assert.That(withAlbedo, Is.GreaterThan(0), "expected Enhanced albedo textures");
            Assert.That(withNormal, Is.GreaterThan(0), "expected procedural normals on world mats");
            Assert.That(loader.WorldTextures.NormalMapCount, Is.GreaterThan(0));

            int enhancedSprites = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var mat = r.sharedMaterial;
                if (mat == null || mat.shader == null ||
                    mat.shader.name != DoomMaterialFactory.EnhancedSpriteName)
                    continue;
                enhancedSprites++;
                Assert.IsTrue(
                    mat.IsKeywordEnabled(DoomMaterialFactory.SpriteTexelAaKeyword),
                    "Enhanced sprite materials must enable point-compatible texel AA");
                if (mat.mainTexture is Texture2D spriteTexture)
                {
                    Assert.AreEqual(FilterMode.Point, spriteTexture.filterMode,
                        "sprite texel AA must not blur the shared IMGUI weapon texture");
                    Assert.AreEqual(1, spriteTexture.mipmapCount);
                }
            }
            Assert.That(enhancedSprites, Is.GreaterThan(0), "expected Enhanced sprite materials");

            // Classic must drop Enhanced shaders, texel-AA keyword, and clear bump maps.
            gfx.Apply(GraphicsMode.Classic);
            Assert.IsFalse(gfx.ActiveProfile.WorldTexelAA);
            int classic = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var mat = r.sharedMaterial;
                if (mat == null || mat.shader == null) continue;
                string name = mat.shader.name;
                if (name != DoomMaterialFactory.ClassicOpaqueName &&
                    name != DoomMaterialFactory.ClassicCutoutName)
                    continue;
                classic++;
                Assert.IsFalse(mat.IsKeywordEnabled(DoomMaterialFactory.TexelAaKeyword),
                    "Classic materials must not keep DOOM_TEXEL_AA");
                Assert.IsFalse(mat.IsKeywordEnabled(DoomMaterialFactory.ParallaxKeyword),
                    "Classic materials must not keep DOOM_PARALLAX");
                if (mat.mainTexture is Texture2D classicAlbedo)
                    Assert.AreEqual(FilterMode.Point, classicAlbedo.filterMode);
                if (mat.HasProperty(DoomMaterialFactory.BumpMapProperty))
                    Assert.IsNull(mat.GetTexture(DoomMaterialFactory.BumpMapProperty));
            }
            Assert.That(classic, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator Parallax_keyword_only_on_solid_opaque_when_WorldParallax()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForMapBuild();

            var gfx = GraphicsModeController.Ensure();
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            Assert.IsNull(gfx.LastError, gfx.LastError);
            Assert.IsTrue(gfx.ActiveProfile.WorldParallax);

            int opaqueWithPom = 0;
            int cutoutChecked = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var mat = r.sharedMaterial;
                if (mat == null || mat.shader == null) continue;
                string name = mat.shader.name;
                if (name == DoomMaterialFactory.EnhancedOpaqueName)
                {
                    if (mat.IsKeywordEnabled(DoomMaterialFactory.ParallaxKeyword))
                    {
                        opaqueWithPom++;
                        Assert.That(
                            mat.GetFloat(DoomMaterialFactory.ParallaxAmplitudeProperty),
                            Is.GreaterThan(0f));
                    }
                }
                else if (name == DoomMaterialFactory.EnhancedCutoutName)
                {
                    cutoutChecked++;
                    Assert.IsFalse(mat.IsKeywordEnabled(DoomMaterialFactory.ParallaxKeyword),
                        "Cutout/masked must not enable DOOM_PARALLAX");
                }
            }

            Assert.That(opaqueWithPom, Is.GreaterThan(0),
                "expected solid opaque materials with POM");
            Assert.That(cutoutChecked, Is.GreaterThan(0),
                "expected cutout materials to assert POM-off");

            // Layered profile without parallax: keyword off.
            var noPom = GraphicsProfile.EnhancedWithLayers(worldParallax: false);
            gfx.Context.ApplyProfile(noPom, gfx.Factory);
            yield return null;
            int stillOn = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var mat = r.sharedMaterial;
                if (mat == null || mat.shader == null) continue;
                if (mat.shader.name != DoomMaterialFactory.EnhancedOpaqueName) continue;
                if (mat.IsKeywordEnabled(DoomMaterialFactory.ParallaxKeyword))
                    stillOn++;
            }
            Assert.AreEqual(0, stillOn, "WorldParallax=false must clear DOOM_PARALLAX");

            // Restore full Enhanced.
            gfx.Context.ApplyProfile(GraphicsProfile.Enhanced, gfx.Factory);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Texel_AA_keyword_follows_WorldTexelAA_layer_flag()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForMapBuild();

            var gfx = GraphicsModeController.Ensure();
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            Assert.IsNull(gfx.LastError, gfx.LastError);

            // Layered profile without texel-AA: keyword off (editor/test capture path).
            // Apply via context — GraphicsModeController.Apply early-outs on same mode.
            var noAa = GraphicsProfile.EnhancedWithLayers(worldTexelAA: false);
            gfx.Context.ApplyProfile(noAa, gfx.Factory);
            int checkedMats = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var mat = r.sharedMaterial;
                if (mat == null || mat.shader == null) continue;
                string name = mat.shader.name;
                if (name != DoomMaterialFactory.EnhancedOpaqueName &&
                    name != DoomMaterialFactory.EnhancedCutoutName)
                    continue;
                checkedMats++;
                Assert.IsFalse(mat.IsKeywordEnabled(DoomMaterialFactory.TexelAaKeyword));
                Assert.That(name, Does.Not.Contain("Hidden/InternalErrorShader"));
            }
            Assert.That(checkedMats, Is.GreaterThan(0));

            // Restore full Enhanced layers (texel-AA on).
            gfx.Context.ApplyProfile(GraphicsProfile.Enhanced, gfx.Factory);
            yield return null;
            int restored = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var mat = r.sharedMaterial;
                if (mat == null || mat.shader == null) continue;
                if (mat.shader.name != DoomMaterialFactory.EnhancedOpaqueName &&
                    mat.shader.name != DoomMaterialFactory.EnhancedCutoutName)
                    continue;
                restored++;
                Assert.IsTrue(mat.IsKeywordEnabled(DoomMaterialFactory.TexelAaKeyword));
                Assert.That(mat.shader.name, Does.Not.Contain("Hidden/InternalErrorShader"));
            }
            Assert.That(restored, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator Normal_maps_are_cached_across_hot_switch_and_destroyed_on_teardown()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForMapBuild();

            var loader = Object.FindFirstObjectByType<MapLoader>();
            Assert.IsNotNull(loader);
            var gfx = GraphicsModeController.Ensure();

            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            int normalsAfterWarmup = loader.WorldTextures.NormalMapCount;
            int texAfterWarmup = gfx.Context.TextureCount;
            Assert.That(normalsAfterWarmup, Is.GreaterThan(0));

            // No frame yields: billboards would otherwise lazy-create Enhanced
            // rotation frames and inflate TextureCount unrelated to hot-switch.
            Time.timeScale = 0f;
            for (int i = 0; i < 20; i++)
                gfx.Apply(i % 2 == 0 ? GraphicsMode.Classic : GraphicsMode.Enhanced);
            Time.timeScale = 1f;

            Assert.AreEqual(normalsAfterWarmup, loader.WorldTextures.NormalMapCount,
                "hot-switch must not create new normal maps after warm-up");
            Assert.AreEqual(texAfterWarmup, gfx.Context.TextureCount,
                "registered texture count must stay stable after warm-up");

            // Teardown via context dispose destroys owned normal maps.
            var ownedNormals = CollectLiveNormals();
            Assert.That(ownedNormals, Is.GreaterThan(0));
            gfx.ClearContext();
            yield return null;
            Assert.AreEqual(0, CollectLiveNormals(),
                "ClearContext must destroy owned runtime normal textures");
        }

        static int CollectLiveNormals()
        {
            int n = 0;
            foreach (var tex in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                if (tex != null && tex.name != null && tex.name.EndsWith("/Normal"))
                    n++;
            }
            return n;
        }
    }
}
