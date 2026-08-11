using System.Collections;
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
    public class EnhancedLampGlowPlayTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
        }

        [UnityTest]
        public IEnumerator Enhanced_TLITE_ceiling_flicker_Classic_static_hot_switch()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 35f;

            MapLoader.MapNameOverride = "E1M5";
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);

            MapLoader loader = null;
            for (int i = 0; i < 30000; i++)
            {
                loader = Object.FindAnyObjectByType<MapLoader>();
                if (loader != null && loader.LoadedMapName == "E1M5" &&
                    loader.LastBuildSeconds > 0f &&
                    loader.SectorLights != null)
                    break;
                yield return null;
            }

            Assert.IsNotNull(loader, "E1M5 MapLoader missing");
            Assert.AreEqual("E1M5", loader.LoadedMapName);

            var registry = Object.FindAnyObjectByType<WorldStateRegistry>();
            Assert.IsNotNull(registry);
            Assert.IsNotNull(registry.Map);

            int eligible = -1;
            int wadLight = 0;
            for (int s = 0; s < registry.Map.Sectors.Length; s++)
            {
                var sec = registry.Map.Sectors[s];
                if (!EnhancedLampGlowRules.IsEligible(sec.CeilingFlat, sec.Special))
                    continue;
                eligible = s;
                wadLight = sec.LightLevel;
                break;
            }
            Assert.That(eligible, Is.GreaterThanOrEqualTo(0),
                "E1M5 should have at least one static TLITE ceiling");

            var lights = loader.SectorLights;
            var gfx = GraphicsModeController.Ensure();

            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            lights.NotifyProfileChanged();
            yield return null;

            Assert.AreEqual(SectorLightKind.None, lights.GetState(eligible).Kind,
                "eligible TLITE must not use sector Glow thinker");
            Assert.AreEqual(SectorLightState.ClampLight(wadLight), lights.GetLight(eligible));
            Assert.That(lights.GetCeilingLampFlicker(eligible), Is.GreaterThan(0.5f),
                "Enhanced should enable lamp flicker MPB on Ceiling");

            int start = lights.GetLight(eligible);
            for (int i = 0; i < 45; i++)
            {
                yield return null;
                Assert.AreEqual(start, lights.GetLight(eligible),
                    "sector light must stay static while bulbs flicker in shader");
            }

            gfx.Apply(GraphicsMode.Classic);
            lights.NotifyProfileChanged();
            yield return null;

            Assert.AreEqual(SectorLightKind.None, lights.GetState(eligible).Kind);
            Assert.AreEqual(SectorLightState.ClampLight(wadLight), lights.GetLight(eligible));
            Assert.That(lights.GetCeilingLampFlicker(eligible), Is.LessThan(0.5f),
                "Classic must clear ceiling lamp flicker MPB");

            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            lights.NotifyProfileChanged();
            yield return null;

            Assert.AreEqual(SectorLightKind.None, lights.GetState(eligible).Kind);
            Assert.That(lights.GetCeilingLampFlicker(eligible), Is.GreaterThan(0.5f),
                "hot-switch back to Enhanced must re-enable ceiling flicker");
        }
    }
}
