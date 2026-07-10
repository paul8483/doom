using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.MapBuild;
using Doom.Game;

namespace Doom.Stage3.PlayTests
{
    public class E1MapSmokePlayTests
    {
        static readonly string[] E1Maps =
        {
            "E1M1", "E1M2", "E1M3", "E1M4", "E1M5", "E1M6", "E1M7", "E1M8", "E1M9"
        };

        [SetUp]
        public void SetUp()
        {
            MapLoader.MapNameOverride = null;
            GameSessionHost.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
            GameSessionHost.ResetForTests();
        }

        [UnityTest]
        public IEnumerator E1M1_through_E1M9_build_without_blockers()
        {
            LogAssert.ignoreFailingMessages = true;
            var baselines = new List<string>();

            foreach (string map in E1Maps)
            {
                GameSessionHost.ResetForTests();
                MapLoader.MapNameOverride = map;
                SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
                yield return null; yield return null;

                MapLoader loader = null;
                for (int i = 0; i < 180; i++)
                {
                    loader = Object.FindAnyObjectByType<MapLoader>();
                    if (loader != null && loader.LoadedMapName == map &&
                        Object.FindAnyObjectByType<LineActivator>() != null)
                        break;
                    yield return null;
                }

                Assert.That(loader, Is.Not.Null, $"{map}: MapLoader missing");
                Assert.That(loader.LoadedMapName, Is.EqualTo(map),
                    $"{map}: wrong loaded map (override={MapLoader.MapNameOverride})");
                Assert.That(Object.FindAnyObjectByType<PlayerController>(), Is.Not.Null,
                    $"{map}: player missing");
                Assert.That(Object.FindAnyObjectByType<DoomHud>(), Is.Not.Null,
                    $"{map}: HUD missing");
                Assert.That(loader.LastBuildSeconds, Is.GreaterThan(0f).And.LessThan(120f),
                    $"{map}: build time out of range ({loader.LastBuildSeconds})");
                Assert.That(loader.LastMeshCount, Is.GreaterThan(0), $"{map}: no meshes");
                Assert.That(float.IsNaN(loader.LastBuildSeconds), Is.False);

                // No duplicate persistent hosts after sequential loads.
                Assert.That(Object.FindObjectsByType<GameSessionHost>(FindObjectsSortMode.None).Length,
                    Is.LessThanOrEqualTo(1), $"{map}: duplicate GameSessionHost");

                baselines.Add(
                    $"{map}: build={loader.LastBuildSeconds:F3}s meshes={loader.LastMeshCount} " +
                    $"renderers={loader.LastMaterialCount} colliders={loader.LastColliderCount} " +
                    $"transforms={loader.LastGameObjectCount}");
            }

            foreach (string line in baselines)
                Debug.Log("[7e baseline] " + line);
            TestContext.WriteLine(string.Join("\n", baselines));
        }
    }
}
