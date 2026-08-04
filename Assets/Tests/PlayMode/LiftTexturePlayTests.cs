using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.MapBuild;
using Doom.MapBuild.Rendering;
using Doom.Game;

namespace Doom.Stage3.PlayTests
{
    /// Regression for E1M3 nukage-lift magenta walls (save slot 0 repro):
    /// after a lift rebuild, Wall_* renderers under the pit/lift must keep a live
    /// Doom shader and non-null albedo — not Unity's missing-texture checker.
    public class LiftTexturePlayTests
    {
        const string MapName = "E1M3";
        const int LiftSector = 91;
        const int PitSector = 90;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            MapLoader.MapNameOverride = MapName;
            GameSessionHost.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            MapLoader.MapNameOverride = null;
            GameSessionHost.ResetForTests();
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator E1M3_lift_rebuild_keeps_wall_albedos_in_classic_and_enhanced()
        {
            foreach (var mode in new[] { GraphicsMode.Classic, GraphicsMode.Enhanced })
            {
                GameSessionHost.ResetForTests();
                MapLoader.MapNameOverride = MapName;
                SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
                yield return null;
                yield return null;

                MapLoader loader = null;
                for (int i = 0; i < 30000; i++)
                {
                    loader = Object.FindAnyObjectByType<MapLoader>();
                    if (loader != null && loader.LoadedMapName == MapName &&
                        loader.LastBuildSeconds > 0f &&
                        loader.Geometry != null &&
                        loader.RuntimeHeights != null)
                        break;
                    yield return null;
                }

                Assert.That(loader, Is.Not.Null, $"{mode}: MapLoader missing");
                Assert.That(loader.Geometry, Is.Not.Null, $"{mode}: Geometry missing");

                var gfx = GraphicsModeController.Ensure();
                yield return GraphicsApplyWait.Apply(gfx, mode);
                for (int i = 0; i < 5; i++) yield return null;

                AssertWallAlbedos(mode, "initial");

                var heights = loader.RuntimeHeights;
                var geom = loader.Geometry;
                float start = heights.FloorRaw(LiftSector);
                float pit = heights.FloorRaw(PitSector);

                // Ride the lift down into the pit and back up (rebuild every step).
                Time.captureDeltaTime = 1f / 35f;
                const int steps = 16;
                for (int i = 1; i <= steps; i++)
                {
                    heights.SetFloor(LiftSector, Mathf.Lerp(start, pit, i / (float)steps));
                    geom.RebuildSectorAndNeighbors(LiftSector);
                    yield return null;
                }
                AssertWallAlbedos(mode, "lowered");

                for (int i = 1; i <= steps; i++)
                {
                    heights.SetFloor(LiftSector, Mathf.Lerp(pit, start, i / (float)steps));
                    geom.RebuildSectorAndNeighbors(LiftSector);
                    yield return null;
                }
                Time.captureDeltaTime = 0f;
                AssertWallAlbedos(mode, "restored");
            }
        }

        static void AssertWallAlbedos(GraphicsMode mode, string phase)
        {
            foreach (int sector in new[] { PitSector, LiftSector, 92 })
            {
                var root = GameObject.Find($"Sector_{sector}");
                if (root == null) continue;

                for (int i = 0; i < root.transform.childCount; i++)
                {
                    var child = root.transform.GetChild(i);
                    if (!child.name.StartsWith("Wall_") || !child.gameObject.activeInHierarchy)
                        continue;

                    var renderer = child.GetComponent<MeshRenderer>();
                    Assert.That(renderer, Is.Not.Null,
                        $"{mode}/{phase}: {child.name} missing MeshRenderer");
                    var mat = renderer.sharedMaterial;
                    Assert.That(mat, Is.Not.Null,
                        $"{mode}/{phase}: {child.name} sharedMaterial is null (magenta)");
                    Assert.That(mat.shader, Is.Not.Null,
                        $"{mode}/{phase}: {child.name} shader is null");
                    Assert.That(
                        mat.shader.name.Contains("InternalError") ||
                        mat.shader.name == "Hidden/InternalErrorShader",
                        Is.False,
                        $"{mode}/{phase}: {child.name} error shader {mat.shader.name}");
                    Assert.That(mat.shader.name.StartsWith("Doom/"), Is.True,
                        $"{mode}/{phase}: {child.name} unexpected shader {mat.shader.name}");
                    Assert.That(mat.mainTexture, Is.Not.Null,
                        $"{mode}/{phase}: {child.name} mainTexture is null (magenta checker)");

                    var tex = mat.mainTexture as Texture2D;
                    if (tex != null)
                    {
                        // Project placeholder policy: Clamp/Clamp 64×64 magenta.
                        bool placeholderWrap =
                            tex.wrapModeU == TextureWrapMode.Clamp &&
                            tex.wrapModeV == TextureWrapMode.Clamp &&
                            tex.width == 64 && tex.height == 64;
                        Assert.That(placeholderWrap, Is.False,
                            $"{mode}/{phase}: {child.name} uses placeholder albedo '{tex.name}'");
                    }
                }
            }
        }
    }
}
