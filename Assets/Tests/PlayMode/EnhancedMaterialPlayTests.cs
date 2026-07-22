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

        static IEnumerator WaitForMapBuild(int maxFrames = 3600)
        {
            MapLoader loader = null;
            for (int i = 0; i < maxFrames; i++)
            {
                yield return null;
                loader = Object.FindFirstObjectByType<MapLoader>();
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
            gfx.Apply(GraphicsMode.Enhanced);
            Assert.AreEqual(GraphicsMode.Enhanced, gfx.Current);
            Assert.IsNull(gfx.LastError, gfx.LastError);

            int enhanced = 0;
            int withNormal = 0;
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
                Assert.IsTrue(mat.HasProperty(DoomMaterialFactory.BumpMapProperty));
                Assert.IsTrue(mat.HasProperty(DoomMaterialFactory.RoughnessProperty));
                Assert.IsTrue(mat.HasProperty(DoomMaterialFactory.EmissionProperty));

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
            Assert.That(withNormal, Is.GreaterThan(0), "expected procedural normals on world mats");
            Assert.That(loader.WorldTextures.NormalMapCount, Is.GreaterThan(0));

            // Classic must drop Enhanced shaders and clear bump maps.
            gfx.Apply(GraphicsMode.Classic);
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
                if (mat.HasProperty(DoomMaterialFactory.BumpMapProperty))
                    Assert.IsNull(mat.GetTexture(DoomMaterialFactory.BumpMapProperty));
            }
            Assert.That(classic, Is.GreaterThan(0));
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

            gfx.Apply(GraphicsMode.Enhanced);
            yield return null;
            int normalsAfterWarmup = loader.WorldTextures.NormalMapCount;
            int texAfterWarmup = gfx.Context.TextureCount;
            Assert.That(normalsAfterWarmup, Is.GreaterThan(0));

            for (int i = 0; i < 20; i++)
            {
                gfx.Apply(i % 2 == 0 ? GraphicsMode.Classic : GraphicsMode.Enhanced);
                yield return null;
            }

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
