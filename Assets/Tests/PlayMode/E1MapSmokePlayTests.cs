using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.MapBuild;
using Doom.MapBuild.Rendering;
using Doom.Game;

namespace Doom.Stage3.PlayTests
{
    public class E1MapSmokePlayTests
    {
        static readonly string[] E1Maps =
        {
            "E1M1", "E1M2", "E1M3", "E1M4", "E1M5", "E1M6", "E1M7", "E1M8", "E1M9"
        };

        static readonly GraphicsMode[] Modes =
        {
            GraphicsMode.Classic,
            GraphicsMode.Enhanced,
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
                foreach (var mode in Modes)
                {
                    GameSessionHost.ResetForTests();
                    MapLoader.MapNameOverride = map;
                    SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
                    yield return null; yield return null;

                    // LineActivator spawns mid-build; LastBuildSeconds is only
                    // set when BuildRoutine finishes — wait for the latter so
                    // the asserts below never race the tail of the build.
                    MapLoader loader = null;
                    for (int i = 0; i < 30000; i++)
                    {
                        loader = Object.FindAnyObjectByType<MapLoader>();
                        if (loader != null && loader.LoadedMapName == map &&
                            loader.LastBuildSeconds > 0f &&
                            Object.FindAnyObjectByType<LineActivator>() != null)
                            break;
                        yield return null;
                    }

                    Assert.That(loader, Is.Not.Null, $"{map}/{mode}: MapLoader missing");
                    Assert.That(loader.LoadedMapName, Is.EqualTo(map),
                        $"{map}/{mode}: wrong loaded map (override={MapLoader.MapNameOverride})");
                    Assert.That(Object.FindAnyObjectByType<PlayerController>(), Is.Not.Null,
                        $"{map}/{mode}: player missing");
                    Assert.That(Object.FindAnyObjectByType<DoomHud>(), Is.Not.Null,
                        $"{map}/{mode}: HUD missing");
                    Assert.That(loader.LastBuildSeconds, Is.GreaterThan(0f).And.LessThan(120f),
                        $"{map}/{mode}: build time out of range ({loader.LastBuildSeconds})");
                    Assert.That(loader.LastMeshCount, Is.GreaterThan(0), $"{map}/{mode}: no meshes");
                    Assert.That(float.IsNaN(loader.LastBuildSeconds), Is.False);

                    var gfx = GraphicsModeController.Ensure();
                    yield return GraphicsApplyWait.Apply(gfx, mode);
                    for (int i = 0; i < 5; i++) yield return null;

                    Assert.AreEqual(mode, gfx.Current, $"{map}/{mode}: mode not applied");
                    Assert.IsNull(gfx.LastError, $"{map}/{mode}: graphics error {gfx.LastError}");
                    AssertNoPinkOrBrokenMeshes($"{map}/{mode}");

                    // No duplicate persistent hosts after sequential loads.
                    Assert.That(Object.FindObjectsByType<GameSessionHost>(FindObjectsSortMode.None).Length,
                        Is.LessThanOrEqualTo(1), $"{map}/{mode}: duplicate GameSessionHost");

                    int tex = gfx.Context != null ? gfx.Context.TextureCount : 0;
                    int mats = gfx.Context != null ? gfx.Context.MaterialCount : 0;
                    baselines.Add(
                        $"{map}/{mode}: build={loader.LastBuildSeconds:F3}s meshes={loader.LastMeshCount} " +
                        $"renderers={loader.LastMaterialCount} colliders={loader.LastColliderCount} " +
                        $"transforms={loader.LastGameObjectCount} tex={tex} mats={mats}");
                }
            }

            foreach (string line in baselines)
                Debug.Log("[8e smoke] " + line);
            TestContext.WriteLine(string.Join("\n", baselines));
        }

        static void AssertNoPinkOrBrokenMeshes(string label)
        {
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                // Skip HUD/UI overlays that intentionally omit world materials.
                if (r.GetComponentInParent<DoomHud>() != null) continue;
                var mat = r.sharedMaterial;
                Assert.IsNotNull(mat, $"{label}: null sharedMaterial on {r.name} (magenta)");
                var sh = mat.shader;
                Assert.IsNotNull(sh, $"{label}: missing shader on {r.name}");
                Assert.IsFalse(
                    sh.name.Contains("InternalError") || sh.name == "Hidden/InternalErrorShader",
                    $"{label}: pink/error shader on {r.name}: {sh.name}");
                if (r.name.StartsWith("Wall_") || r.name == "Floor" || r.name == "Ceiling")
                {
                    Assert.IsNotNull(mat.mainTexture,
                        $"{label}: null mainTexture on {r.name} (magenta checker)");
                }
            }

            foreach (var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (filter == null || filter.sharedMesh == null) continue;
                var verts = filter.sharedMesh.vertices;
                for (int i = 0; i < verts.Length; i++)
                {
                    Assert.IsFalse(float.IsNaN(verts[i].x) || float.IsNaN(verts[i].y) ||
                                   float.IsNaN(verts[i].z),
                        $"{label}: NaN vertex on {filter.name}");
                    break; // one sample per mesh is enough for smoke
                }
            }
        }
    }
}
