using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.MapBuild;
using Doom.Things;

namespace Doom.Stage3.PlayTests
{
    /// The firesticks in Enhanced+3D: a lathe stand plus one plume per flame
    /// frame, both computed from the sprite by Tools/make_torch_model.py. The
    /// runtime never measures the meshes — it scales them by row counts read
    /// off the colour tables and the WAD patch — so the placement assertions
    /// here are what keeps a torch standing on the floor.
    ///
    /// Everything runs on E1M5, the one E1 map that places all six torches:
    /// a synthetic torch on another map would ask SpriteCache for a lump that
    /// was never pre-warmed, and the WAD is closed by the time a test runs.
    public class ExperimentalTorchModelPlayTests
    {
        const float WorldScale = 1f / 32f;
        const int TallBlueTorch = 44;
        const int Candelabra = 35;

        MemorySettingsStorage memory;
        FakeDisplayAdapter display;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            GameFlowController.ResetForTests();
            GameFlowController.AutoStartPlaying = true;
            // Order matters: ResetForTests goes through GameSessionHost, which
            // clears MapNameOverride — set the map after it, not before, or the
            // scene quietly builds E1M1, where no firestick stands at all.
            MapLoader.MapNameOverride = "E1M5";
            memory = new MemorySettingsStorage();
            display = new FakeDisplayAdapter();
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            MapLoader.MapNameOverride = null;
            GameSessionHost.ResetForTests();
            GameFlowController.ResetForTests();
            LogAssert.ignoreFailingMessages = false;
        }

        IEnumerator LoadLevel()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            // LoadScene lands at the end of the frame; the previous test's
            // MapLoader stays findable until then and its SpriteCache is about
            // to lose its WAD.
            yield return null;
            for (int i = 0; i < 900; i++)
            {
                var flow = GameFlowController.Instance;
                var loader = Object.FindAnyObjectByType<MapLoader>();
                if (flow != null && flow.State == GameFlowState.Playing &&
                    loader != null && loader.LoadedMapName == "E1M5" &&
                    loader.LastBuildSeconds > 0f && loader.Sprites != null &&
                    GameObject.Find("Player") != null)
                    yield break;
                yield return null;
            }
            var last = Object.FindAnyObjectByType<MapLoader>();
            Assert.Fail("Stage2_MapPreview did not finish loading E1M5 (loaded=" +
                (last != null ? last.LoadedMapName : "none") + ")");
        }

        static SpriteCache Sprites() =>
            Object.FindAnyObjectByType<MapLoader>().Sprites;

        static Dictionary<int, ExperimentalTorchModel> Torches()
        {
            var found = new Dictionary<int, ExperimentalTorchModel>();
            foreach (var model in Object.FindObjectsByType<ExperimentalTorchModel>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var identity = model.GetComponent<MapThingIdentity>();
                if (identity != null)
                    found[identity.DoomEdNum] = model;
            }
            return found;
        }

        static Bounds Bounds(Transform part)
        {
            var renderers = part.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.GreaterThan(0), $"{part.name} has no renderers");
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        static SettingsController Enhanced3D(MemorySettingsStorage memory,
                                             FakeDisplayAdapter display)
        {
            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display,
                new NoOpGraphicsModeAdapter());
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            settings.SetEnhanced3DObjects(true);
            return settings;
        }

        [UnityTest]
        public IEnumerator Every_firestick_on_the_map_routes_the_3d_torch()
        {
            yield return LoadLevel();
            var torches = Torches();
            foreach (var pair in ExperimentalTorchModel.RoutedForTest)
                Assert.That(torches.ContainsKey(pair.Key), Is.True,
                    $"{pair.Value} ({pair.Key}) stands on E1M5 but took no 3D " +
                    "presentation — coverage is all-or-nothing");
            Assert.That(torches.ContainsKey(Candelabra), Is.False,
                "the candelabra is a light too, but it is not in this wave");
        }

        [UnityTest]
        public IEnumerator Torch_follows_the_toggle_cascade()
        {
            yield return LoadLevel();
            var model = Torches()[TallBlueTorch];
            var mr = model.GetComponent<MeshRenderer>();
            var bb = model.GetComponent<SpriteBillboard>();

            var settings = Enhanced3D(memory, display);
            yield return null;
            Assert.That(model.ModelVisible, Is.True, "Enhanced+3D On → mesh");
            Assert.That(mr.enabled, Is.False);
            Assert.That(bb.enabled, Is.False);

            settings.SetEnhanced3DObjects(false);
            yield return null;
            Assert.That(model.ModelVisible, Is.False, "3D Off → billboard");
            Assert.That(mr.enabled, Is.True);
            Assert.That(bb.enabled, Is.True);

            settings.SetGraphicsMode(GraphicsMode.Classic);
            yield return null;
            Assert.That(model.ModelVisible, Is.False, "Classic → billboard");

            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            settings.SetEnhanced3DObjects(true);
            yield return null;
            Assert.That(model.ModelVisible, Is.True, "hot-switch back to mesh");
        }

        [UnityTest]
        public IEnumerator Parts_occupy_exactly_the_sprite_the_billboard_drew()
        {
            yield return LoadLevel();
            Enhanced3D(memory, display);
            var model = Torches()[TallBlueTorch];
            model.SetEnhancedForTest(true);
            yield return null;

            var patch = Sprites().Get("TBLU", 0, 0);
            Assert.That(patch.IsValid, Is.True);

            var stand = model.ModelRootForTest.Find("Stand");
            var flame = model.ModelRootForTest.Find("FlameA");
            Assert.That(stand, Is.Not.Null);
            Assert.That(flame, Is.Not.Null);

            // Feet on the floor: the patch hangs from its own top offset.
            // Measured on the rendered bounds, not on the pivot's scale, so
            // the assertion holds for the computed stand (unit-height OBJ) and
            // for a generated one (scaled to fit after measuring) alike.
            float feet = model.transform.position.y +
                         (patch.TopOffset - patch.Height) * WorldScale;
            Bounds standBounds = Bounds(stand);
            Assert.That(standBounds.min.y, Is.EqualTo(feet).Within(0.01f),
                "the stand must start at the sprite's bottom row");
            Assert.That(flame.position.y,
                Is.EqualTo(standBounds.max.y).Within(0.01f),
                "the flame must start where the stand ends — no gap, no overlap");
            Assert.That(standBounds.size.y + flame.localScale.y,
                Is.EqualTo(patch.Height * WorldScale).Within(0.01f),
                "together the parts must be exactly as tall as the sprite");

            // The white-corpse trap of 2026-08-16: meshes render fine with a
            // null texture, and every other assertion still passes.
            foreach (var renderer in model.ModelRootForTest
                         .GetComponentsInChildren<Renderer>(true))
                Assert.That(renderer.sharedMaterial.mainTexture, Is.Not.Null,
                    $"{renderer.name} lost its colour table");
        }

        [UnityTest]
        public IEnumerator Exactly_one_flame_frame_shows_and_it_follows_the_tic()
        {
            yield return LoadLevel();
            Enhanced3D(memory, display);
            var model = Torches()[TallBlueTorch];
            model.SetEnhancedForTest(true);
            yield return null;

            for (int tic = 0; tic < 20; tic++)
            {
                model.AdvanceToTicForTest(tic);
                int expected = (tic / ExperimentalTorchModel.FrameTics)
                    % ExperimentalTorchModel.FrameCount;
                Assert.That(model.CurrentFrameForTest, Is.EqualTo(expected),
                    $"tic {tic} must show flame frame {expected}");

                int active = 0;
                for (int i = 0; i < ExperimentalTorchModel.FrameCount; i++)
                {
                    var frame = model.ModelRootForTest.Find(
                        "Flame" + (char)('A' + i));
                    Assert.That(frame, Is.Not.Null);
                    if (frame.gameObject.activeSelf) active++;
                }
                Assert.That(active, Is.EqualTo(1),
                    "two plumes at once would read as a double flame");
            }
        }

        [UnityTest]
        public IEnumerator Generated_stand_wins_over_the_computed_one_when_present()
        {
            yield return LoadLevel();
            Enhanced3D(memory, display);
            foreach (var pair in ExperimentalTorchModel.RoutedForTest)
            {
                var model = Torches()[pair.Key];
                // Pins the rule, not today's assets: while no TRELLIS stand is
                // on disk the lathe carries the torch, and the day one is
                // dropped in it takes over without another code change.
                Assert.That(model.UsesGeneratedStandForTest,
                    Is.EqualTo(ExperimentalTorchModel.HasGeneratedStand(pair.Value)),
                    $"{pair.Value} routes the wrong stand");
                var stand = model.ModelRootForTest.Find("Stand");
                Assert.That(stand, Is.Not.Null, $"{pair.Value} has no stand at all");
                foreach (var renderer in stand.GetComponentsInChildren<Renderer>(true))
                    Assert.That(renderer.sharedMaterial.mainTexture, Is.Not.Null,
                        $"{pair.Value} stand lost its texture");
            }
        }

        [UnityTest]
        public IEnumerator Candelabra_routes_its_metal_and_three_caged_fires()
        {
            yield return LoadLevel();
            Enhanced3D(memory, display);
            yield return null;

            ExperimentalCandelabraModel model = null;
            foreach (var candidate in Object.FindObjectsByType<ExperimentalCandelabraModel>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                model = candidate;

            // Pins the rule, not today's assets: CBRA's metal is generated —
            // a candelabra is not a solid of revolution, so there is no
            // computed fallback and it stays a billboard until a mesh lands.
            bool expected = ExperimentalCandelabraModel.HasGeneratedStand();
            Assert.That(model != null, Is.EqualTo(expected),
                expected
                    ? "the candelabra has a generated mesh but took no 3D"
                    : "the candelabra must stay a billboard until its metal exists");
            if (model == null) yield break;

            model.SetEnhancedForTest(true);
            yield return null;
            Assert.That(model.FireCountForTest, Is.EqualTo(3));
            Assert.That(model.ModelRootForTest.Find("Metal"), Is.Not.Null);
            foreach (var renderer in model.ModelRootForTest
                         .GetComponentsInChildren<Renderer>(true))
                Assert.That(renderer.sharedMaterial.mainTexture, Is.Not.Null,
                    $"{renderer.name} lost its texture");
        }

        [UnityTest]
        public IEnumerator Billboard_flame_flickers_in_classic_too()
        {
            yield return LoadLevel();
            var model = Torches()[TallBlueTorch];
            var animator = model.GetComponent<PickupAnimator>();
            Assert.That(animator, Is.Not.Null,
                "vanilla runs the torch through four frames; the port used to " +
                "leave every firestick frozen on frame A");

            Assert.That(DecorationAnimationTable.TryGet(TallBlueTorch, out var animation),
                Is.True);
            var seen = new HashSet<int>();
            for (int i = 0; i < animation.Frames.Length; i++)
            {
                seen.Add(animator.FrameForTest);
                animator.AdvanceTicsForTest(ExperimentalTorchModel.FrameTics);
            }
            Assert.That(seen.Count, Is.EqualTo(ExperimentalTorchModel.FrameCount),
                "all four sprite frames must appear over one cycle");
        }
    }
}
