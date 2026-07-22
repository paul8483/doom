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
    public class EnhancedAtmospherePlayTests
    {
        [SetUp]
        public void SetUp()
        {
            MapLoader.MapNameOverride = null;
            GameSessionHost.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
            RenderSettings.fog = false;
            GameSessionHost.ResetForTests();
        }

        [UnityTest]
        public IEnumerator Enhanced_enables_sky_fluids_and_fog()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            var gfx = GraphicsModeController.Ensure();
            gfx.Apply(GraphicsMode.Enhanced);
            yield return null;

            var sky = gfx.Context?.Sky ?? Object.FindFirstObjectByType<WadSkyRenderer>();
            Assert.IsNotNull(sky);
            Assert.IsTrue(sky.IsActive, "Enhanced Sky should enable WadSkyRenderer");
            Assert.IsNotNull(sky.SkyMaterial);

            var anim = Object.FindFirstObjectByType<AnimatedSurfaceSystem>();
            Assert.IsNotNull(anim);
            Assert.IsTrue(anim.IsProfileEnabled);
            Assert.IsNotNull(anim.Catalog);
            Assert.That(anim.Catalog.SequenceCount, Is.GreaterThan(0));

            var fog = Object.FindFirstObjectByType<SectorFogSystem>();
            Assert.IsNotNull(fog);
            Assert.IsTrue(fog.IsProfileEnabled);
            Assert.IsTrue(fog.FogGlobalsActive);
            Assert.IsFalse(RenderSettings.fog, "Custom fog uses shader globals, not RenderSettings");
        }

        [UnityTest]
        public IEnumerator Boot_with_enhanced_settings_enables_fog_without_mode_toggle()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            // Simulate persisted Enhanced before map build (standalone New Game path).
            // MapLoader.Start calls ApplyLoadedSettings after Build, so only settings
            // persistence keeps Enhanced across boot — gfx.Apply alone is overwritten.
            var memory = new MemorySettingsStorage();
            var store = new SettingsStore(memory);
            store.Save(GameSettingsData.Defaults.WithGraphicsMode(GraphicsMode.Enhanced));
            SettingsController.Ensure().ConfigureForTests(store, new FakeDisplayAdapter());

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            // Enhanced boot warms 4x textures/sprites/HUD one per frame under the
            // load plaque, so a fixed 90-frame wait is not enough — wait for the
            // build to actually finish (same pattern as the upscale suites).
            for (int i = 0; i < 3600; i++)
            {
                yield return null;
                var loader = Object.FindFirstObjectByType<MapLoader>();
                if (loader != null && loader.LastBuildSeconds > 0f)
                    break;
            }
            yield return null;

            Assert.AreEqual(GraphicsMode.Enhanced, GraphicsModeController.Ensure().Current);
            var fog = Object.FindFirstObjectByType<SectorFogSystem>();
            Assert.IsNotNull(fog);
            Assert.IsTrue(fog.IsProfileEnabled);
            Assert.IsTrue(fog.FogGlobalsActive,
                "Fog must activate on first Enhanced load without Classic↔Enhanced toggle");
        }

        [UnityTest]
        public IEnumerator Classic_keeps_wad_sky_but_disables_fluids_and_fog()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            var gfx = GraphicsModeController.Ensure();
            gfx.Apply(GraphicsMode.Enhanced);
            yield return null;
            gfx.Apply(GraphicsMode.Classic);
            yield return null;

            var sky = Object.FindFirstObjectByType<WadSkyRenderer>();
            Assert.IsNotNull(sky);
            Assert.IsTrue(sky.IsActive, "F_SKY1 openings use WAD SKY1 in Classic too");

            var anim = Object.FindFirstObjectByType<AnimatedSurfaceSystem>();
            Assert.IsNotNull(anim);
            Assert.IsFalse(anim.IsProfileEnabled);

            var fog = Object.FindFirstObjectByType<SectorFogSystem>();
            Assert.IsNotNull(fog);
            Assert.IsFalse(fog.IsProfileEnabled);
            Assert.IsFalse(fog.FogGlobalsActive);
            Assert.IsFalse(RenderSettings.fog);
        }

        [UnityTest]
        public IEnumerator No_pink_sky_or_world_materials_in_either_mode()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            var gfx = GraphicsModeController.Ensure();
            foreach (var mode in new[] { GraphicsMode.Enhanced, GraphicsMode.Classic })
            {
                gfx.Apply(mode);
                yield return null;

                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if (r == null || r.sharedMaterial == null) continue;
                    var sh = r.sharedMaterial.shader;
                    Assert.IsNotNull(sh, $"Missing shader on {r.name} in {mode}");
                    Assert.IsFalse(sh.name.Contains("InternalError") || sh.name == "Hidden/InternalErrorShader",
                        $"Pink/error shader on {r.name}: {sh.name} ({mode})");
                }
            }
        }
    }
}
