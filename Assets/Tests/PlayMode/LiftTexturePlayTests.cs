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
    /// Regression for E1M3 mover magenta walls (save slot 0 repros):
    /// after a lift/door rebuild, newly exposed Wall_* renderers must keep a live
    /// Doom shader and non-null albedo — not Unity's missing-texture checker /
    /// Placeholder.Magenta. Closed doors start with floor==ceiling so DOORTRAK
    /// tracks are absent from the initial mesh set and first-touched on open.
    public class LiftTexturePlayTests
    {
        const string MapName = "E1M3";
        const int LiftSector = 91;
        const int PitSector = 90;
        const int DoorSector = 86;
        const float DoorOpenCeiling = 228f;

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
                AssertWallAlbedos(mode, "restored", PitSector, LiftSector, 92);
            }
        }

        [UnityTest]
        public IEnumerator E1M3_door_open_keeps_doortrak_albedos_in_classic_and_enhanced()
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

                // Closed door: no track mesh yet (degenerate floor==ceiling).
                Assert.That(CountActiveWallsNamed(DoorSector, "DOORTRAK"), Is.EqualTo(0),
                    $"{mode}: closed door should not emit DOORTRAK walls");

                var heights = loader.RuntimeHeights;
                float floor = heights.FloorRaw(DoorSector);
                Assert.That(heights.CeilRaw(DoorSector), Is.EqualTo(floor).Within(0.01f),
                    $"{mode}: E1M3 sector {DoorSector} should start closed");

                // Open like the slot-0 save (ceil 228) and rebuild neighbors.
                heights.SetCeil(DoorSector, DoorOpenCeiling);
                loader.Geometry.RebuildSectorAndNeighbors(DoorSector);
                yield return null;

                int tracks = CountActiveWallsNamed(DoorSector, "DOORTRAK");
                Assert.That(tracks, Is.GreaterThan(0),
                    $"{mode}: open door must emit DOORTRAK track walls");
                AssertWallAlbedos(mode, "door-open", DoorSector);
                AssertDoortrakIsRealTexture(mode);
            }
        }

        [UnityTest]
        public IEnumerator E1M3_save_restore_open_door_keeps_doortrak_albedos()
        {
            // Matches slot-0 path: capture with door open, reload via PendingRestore.
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
                        loader.RuntimeHeights != null &&
                        GameFlowController.Instance != null &&
                        GameFlowController.Instance.State == GameFlowState.Playing)
                        break;
                    yield return null;
                }
                Assert.That(loader, Is.Not.Null, $"{mode}: MapLoader missing");

                var gfx = GraphicsModeController.Ensure();
                yield return GraphicsApplyWait.Apply(gfx, mode);
                for (int i = 0; i < 3; i++) yield return null;

                loader.RuntimeHeights.SetCeil(DoorSector, DoorOpenCeiling);
                loader.Geometry.RebuildSectorAndNeighbors(DoorSector);
                yield return null;

                var registry = Object.FindAnyObjectByType<WorldStateRegistry>();
                Assert.That(registry, Is.Not.Null, $"{mode}: registry missing");
                Assert.That(WorldSnapshotCapture.TryCapture(registry, out var world, out string wErr),
                    Is.True, wErr);
                var player = GameObject.Find("Player");
                Assert.That(player, Is.Not.Null);
                var health = player.GetComponent<PlayerHealth>();
                var weapons = player.GetComponent<PlayerWeapons>();
                var inventory = player.GetComponent<PlayerInventory>();
                var pc = player.GetComponent<PlayerController>();
                var pos = player.transform.position;
                var playerSnap = PlayerSnapshot.Capture(
                    pos.x, pos.y, pos.z,
                    player.transform.eulerAngles.y,
                    pc != null ? pc.PitchDegrees : 0f,
                    health.Model, weapons.Ammo, weapons.Loadout,
                    inventory.Keys, inventory.Powers, weapons.Rng);
                var host = GameSessionHost.Ensure();
                host.EnsureWadIdentity(System.IO.Path.Combine(
                    Application.streamingAssetsPath, "wads", "freedoom1.wad"));
                Assert.That(SaveGame.TryCreate(
                        MapName, host.WadIdentity, playerSnap, world,
                        out SaveGame save, out string sErr),
                    Is.True, sErr);

                host.SetPendingRestore(save);
                host.Session.BeginNewGame(MapName, new[] { MapName });
                MapLoader.MapNameOverride = MapName;
                GameFlowController.Ensure().EnterLoading();
                SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
                yield return null;
                yield return null;

                loader = null;
                for (int i = 0; i < 30000; i++)
                {
                    loader = Object.FindAnyObjectByType<MapLoader>();
                    if (loader != null && loader.LoadedMapName == MapName &&
                        loader.LastBuildSeconds > 0f &&
                        loader.Geometry != null &&
                        loader.RuntimeHeights != null &&
                        GameFlowController.Instance != null &&
                        GameFlowController.Instance.State == GameFlowState.Playing)
                        break;
                    yield return null;
                }

                Assert.That(loader, Is.Not.Null, $"{mode}: MapLoader missing after restore");
                yield return GraphicsApplyWait.Apply(GraphicsModeController.Ensure(), mode);
                for (int i = 0; i < 5; i++) yield return null;

                Assert.That(loader.RuntimeHeights.CeilRaw(DoorSector),
                    Is.EqualTo(DoorOpenCeiling).Within(0.01f),
                    $"{mode}: restore must open door sector {DoorSector}");
                Assert.That(CountActiveWallsNamed(DoorSector, "DOORTRAK"), Is.GreaterThan(0),
                    $"{mode}: restored open door must emit DOORTRAK");
                AssertWallAlbedos(mode, "restore-open", DoorSector);
                AssertDoortrakIsRealTexture(mode);
            }
        }

        static void AssertDoortrakIsRealTexture(GraphicsMode mode)
        {
            var root = GameObject.Find($"Sector_{DoorSector}");
            Assert.That(root, Is.Not.Null, $"{mode}: Sector_{DoorSector} missing");
            bool found = false;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                if (child.name.IndexOf("DOORTRAK", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                found = true;
                var tex = child.GetComponent<MeshRenderer>()?.sharedMaterial?.mainTexture as Texture2D;
                Assert.That(tex, Is.Not.Null, $"{mode}: DOORTRAK mainTexture null");
                // Freedoom DOORTRAK is 8×128; placeholder / empty-build is not.
                Assert.That(tex.width, Is.EqualTo(8).Or.EqualTo(32),
                    $"{mode}: DOORTRAK width {tex.width} (native 8 or Enhanced 4× 32)");
                Assert.That(tex.height, Is.EqualTo(128).Or.EqualTo(512),
                    $"{mode}: DOORTRAK height {tex.height}");
            }
            Assert.That(found, Is.True, $"{mode}: no DOORTRAK wall child");
        }

        static int CountActiveWallsNamed(int sector, string textureFragment)
        {
            var root = GameObject.Find($"Sector_{sector}");
            if (root == null) return 0;
            int count = 0;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                if (child.name.StartsWith("Wall_") &&
                    child.name.IndexOf(textureFragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    count++;
            }
            return count;
        }

        static void AssertWallAlbedos(GraphicsMode mode, string phase, params int[] sectors)
        {
            foreach (int sector in sectors)
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
                        // Project placeholder policy: Clamp/Clamp (any size — empty
                        // TextureSet.Build used to return transparent 8×128 DOORTRAK).
                        bool placeholderWrap =
                            tex.wrapModeU == TextureWrapMode.Clamp &&
                            tex.wrapModeV == TextureWrapMode.Clamp;
                        Assert.That(placeholderWrap, Is.False,
                            $"{mode}/{phase}: {child.name} uses placeholder albedo '{tex.name}' " +
                            $"{tex.width}x{tex.height}");
                    }

                    // Effective sample must not be overridden by a null/missing MPB _MainTex.
                    var mpb = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(mpb);
                    int mainTexId = Shader.PropertyToID("_MainTex");
                    if (mpb.HasTexture(mainTexId))
                    {
                        Assert.That(mpb.GetTexture(mainTexId), Is.Not.Null,
                            $"{mode}/{phase}: {child.name} MPB _MainTex is null (magenta checker)");
                    }
                }
            }
        }
    }
}
