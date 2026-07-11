using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.MapBuild;
using Doom.MapBuild.Rendering;
using Doom.Specials;

namespace Doom.Stage3.PlayTests
{
    public class SectorLightPlayTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
        }

        [UnityTest]
        public IEnumerator Runtime_lights_tick_without_mesh_recreation()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 35f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            var loader = Object.FindFirstObjectByType<MapLoader>();
            Assert.IsNotNull(loader);
            Assert.IsNotNull(loader.SectorLights);

            // Find a sector with an active light thinker (flicker/strobe/glow/fire).
            int lit = -1;
            for (int s = 0; s < loader.SectorLights.SectorCount; s++)
            {
                if (loader.SectorLights.GetState(s).Kind != SectorLightKind.None)
                {
                    lit = s;
                    break;
                }
            }
            Assert.That(lit, Is.GreaterThanOrEqualTo(0), "E1M1 should have at least one light special");

            var root = loader.Geometry.GetSectorRoot(lit);
            Assert.IsNotNull(root);
            var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            var meshIds = meshFilters.Select(mf => mf.sharedMesh != null ? mf.sharedMesh.GetInstanceID() : 0).ToArray();

            int startLight = loader.SectorLights.GetLight(lit);
            bool changed = false;
            for (int i = 0; i < 90; i++)
            {
                yield return null;
                if (loader.SectorLights.GetLight(lit) != startLight)
                {
                    changed = true;
                    break;
                }
            }
            Assert.IsTrue(changed, "light thinker should change light level over time");

            var meshFiltersAfter = root.GetComponentsInChildren<MeshFilter>(true);
            Assert.AreEqual(meshIds.Length, meshFiltersAfter.Length);
            for (int i = 0; i < meshIds.Length; i++)
                Assert.AreEqual(meshIds[i], meshFiltersAfter[i].sharedMesh.GetInstanceID(),
                    "light ticks must not recreate sector meshes");
        }

        [UnityTest]
        public IEnumerator Enhanced_binds_sector_ambient_mpb_classic_clears()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            var loader = Object.FindFirstObjectByType<MapLoader>();
            var gfx = GraphicsModeController.Ensure();
            gfx.Apply(GraphicsMode.Enhanced);
            loader.SectorLights.NotifyProfileChanged();
            yield return null;

            int sector = 0;
            var root = loader.Geometry.GetSectorRoot(sector);
            while (root == null && sector < loader.SectorLights.SectorCount)
            {
                sector++;
                root = loader.Geometry.GetSectorRoot(sector);
            }
            Assert.IsNotNull(root);

            var renderer = root.GetComponentInChildren<MeshRenderer>();
            Assert.IsNotNull(renderer);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetFloat(RuntimeSectorLights.SectorAmbientWeightProperty),
                Is.EqualTo(1f).Within(0.01f));

            float expected = loader.SectorLights.GetLight(sector) / 255f;
            Color ambient = block.GetColor(RuntimeSectorLights.SectorAmbientProperty);
            Assert.That(ambient.r, Is.EqualTo(expected).Within(0.02f));

            gfx.Apply(GraphicsMode.Classic);
            loader.SectorLights.NotifyProfileChanged();
            yield return null;

            renderer.GetPropertyBlock(block);
            // Cleared property block → weight defaults to 0 / empty.
            Assert.That(block.isEmpty ||
                        block.GetFloat(RuntimeSectorLights.SectorAmbientWeightProperty) < 0.5f);
        }

        [UnityTest]
        public IEnumerator Linedef_light_changes_tagged_sector_level()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            var loader = Object.FindFirstObjectByType<MapLoader>();
            var lights = loader.SectorLights;
            Assert.IsNotNull(lights);

            // Force a known absolute change on sector 0 (does not require real linedef).
            int before = lights.GetLight(0);
            lights.ApplyLinedef(138, new[] { 0 }); // to 255
            Assert.AreEqual(255, lights.GetLight(0));
            lights.ApplyLinedef(139, new[] { 0 }); // to 35
            Assert.AreEqual(35, lights.GetLight(0));
            Assert.AreNotEqual(before == 35 ? 255 : before, lights.GetLight(0));
        }
    }
}
