using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    public class ExperimentalMonsterModelPlayTests
    {
        MemorySettingsStorage memory;
        FakeDisplayAdapter display;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            MapLoader.MapNameOverride = null;
            GameFlowController.ResetForTests();
            GameFlowController.AutoStartPlaying = true;
            memory = new MemorySettingsStorage();
            display = new FakeDisplayAdapter();
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            MapLoader.MapNameOverride = null;
            GameFlowController.ResetForTests();
            ExperimentalMonsterModel.DeathCoverageCapForTest = int.MaxValue;
            LogAssert.ignoreFailingMessages = false;
        }

        static GameObject NewMonsterRoot(out SpriteBillboard bb)
        {
            var go = new GameObject("MonsterModelTest",
                typeof(MeshFilter), typeof(MeshRenderer));
            bb = go.AddComponent<SpriteBillboard>();
            return go;
        }

        [UnityTest]
        public IEnumerator Poss_attaches_and_follows_toggle_cascade()
        {
            var go = NewMonsterRoot(out var bb);
            var mr = go.GetComponent<MeshRenderer>();
            var model = ExperimentalMonsterModel.TryAttach(go, "POSS", 1f / 32f, bb);
            Assert.That(model, Is.Not.Null,
                "all 7 POSS frame meshes are in Resources — attach must succeed");

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display,
                new NoOpGraphicsModeAdapter());
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            yield return null;

            Assert.That(model.ModelVisible, Is.True, "Enhanced+3D On → mesh");
            Assert.That(mr.enabled, Is.False);
            Assert.That(bb.enabled, Is.False);

            settings.SetGraphicsMode(GraphicsMode.Classic);
            yield return null;
            Assert.That(model.ModelVisible, Is.False, "Classic → billboard");
            Assert.That(mr.enabled, Is.True);
            Assert.That(bb.enabled, Is.True);

            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            yield return null;
            Assert.That(model.ModelVisible, Is.True, "hot-switch back → mesh");

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Unknown_sprite_does_not_attach()
        {
            var go = NewMonsterRoot(out var bb);
            Assert.That(ExperimentalMonsterModel.TryAttach(go, "CPOS", 1f / 32f, bb),
                Is.Null, "CPOS is not in the E1 roster and has no meshes");
            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Live_frames_swap_and_uncovered_death_reverts_forever()
        {
            // Every routed monster now ships its whole death chain, so the
            // "death tail is uncovered" case — the one a monster takes while
            // its death meshes are still being authored — is exercised
            // through the coverage cap instead of a gap in Resources.
            ExperimentalMonsterModel.DeathCoverageCapForTest = 0;
            var go = NewMonsterRoot(out var bb);
            var mr = go.GetComponent<MeshRenderer>();
            var model = ExperimentalMonsterModel.TryAttach(go, "BOSS", 1f / 32f, bb);
            Assert.That(model, Is.Not.Null);

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display,
                new NoOpGraphicsModeAdapter());
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            yield return null;
            Assert.That(model.ModelVisible, Is.True);

            // Walk/attack/pain frames stay on the mesh.
            foreach (int frame in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                model.NotifyFrame(frame);
                Assert.That(model.CurrentFrameForTest, Is.EqualTo(frame));
                Assert.That(model.RevertedForTest, Is.False);
                Assert.That(model.ModelVisible, Is.True);
            }

            // Death meshes are not in Resources yet, so the death tail is
            // uncovered and the kill hands over to the billboard, as before.
            Assert.That(model.CoveredDeathFramesForTest, Is.EqualTo(0),
                "capped: the death tail counts as unauthored");
            model.NotifyDeathStarted(extremeDeath: false);
            Assert.That(model.RevertedForTest, Is.True,
                "uncovered death tail reverts before the first fall frame");

            // First death frame (I = 8) → billboard, permanently.
            model.NotifyFrame(8);
            Assert.That(model.RevertedForTest, Is.True);
            Assert.That(model.ModelVisible, Is.False);
            Assert.That(mr.enabled, Is.True, "death shows billboard frames");
            Assert.That(bb.enabled, Is.True);

            // Mode churn must not resurrect the mesh on a corpse.
            settings.SetGraphicsMode(GraphicsMode.Classic);
            yield return null;
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            yield return null;
            Assert.That(model.ModelVisible, Is.False, "corpse never returns to mesh");
            Assert.That(mr.enabled, Is.True);

            Object.Destroy(go);
            yield return null;
        }

        // Every routed monster: a covered kill must stay on the mesh from the
        // first fall frame to the body on the floor, and only gibs hand back.
        // Frame numbers come from the table itself — the zombies fall from
        // frame 7, the imp, the demon and the baron from frame 8.
        [UnityTest]
        public IEnumerator Death_chain_stays_on_the_mesh_through_the_corpse(
            [Values("POSS", "SPOS", "TROO", "SARG", "BOSS")] string sprite)
        {
            var go = NewMonsterRoot(out var bb);
            var mr = go.GetComponent<MeshRenderer>();
            var model = ExperimentalMonsterModel.TryAttach(go, sprite, 1f / 32f, bb);
            Assert.That(model, Is.Not.Null);
            Assert.That(ExperimentalMonsterModel.TryGetFrameTableForTest(
                sprite, out int live, out var lumps, out _), Is.True);
            Assert.That(model.CoveredDeathFramesForTest,
                Is.EqualTo(lumps.Length - live),
                $"{sprite} covers its whole death chain, corpse included");

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display,
                new NoOpGraphicsModeAdapter());
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            yield return null;

            model.NotifyDeathStarted(extremeDeath: false);
            Assert.That(model.RevertedForTest, Is.False,
                "a covered fall keeps the mesh");

            // The whole death tail is covered and swaps like live frames.
            for (int frame = live; frame < lumps.Length; frame++)
            {
                model.NotifyFrame(frame);
                Assert.That(model.CurrentFrameForTest, Is.EqualTo(frame));
                Assert.That(model.ModelVisible, Is.True, $"frame {frame} on mesh");
            }

            // Every instantiated frame must carry its own albedo: an OBJ that
            // cannot resolve its .mtl imports with Unity's default material
            // and the frame renders as a plain white silhouette in game —
            // which is how the POSS death meshes first shipped (2026-08-16).
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.gameObject == go) continue;   // the sprite billboard
                foreach (var mat in r.sharedMaterials)
                    Assert.That(mat != null && mat.mainTexture != null, Is.True,
                        $"{sprite}: frame mesh '{r.transform.parent?.name}' " +
                        "lost its albedo — it would render white");
            }

            // The frame right after the corpse opens the xdeath gib sequence,
            // which the table never covers — presentation hands over to the
            // native sprite for good.
            model.NotifyFrame(lumps.Length);
            Assert.That(model.RevertedForTest, Is.True);
            Assert.That(model.ModelVisible, Is.False);
            Assert.That(mr.enabled, Is.True);

            Object.Destroy(go);
            yield return null;
        }

        // The fire frame's mesh cannot carry a muzzle flash (a baked fire
        // stop-frame is a lump of geometry), so the runtime attaches a
        // shader-drawn flash quad to that frame instance — without it the
        // shot is invisible and hits come "from nowhere" (SPOS gate,
        // 2026-08-27). Riding the frame's own SetActive gives it the vanilla
        // fullbright-frame cadence for free, which this pins.
        [UnityTest]
        public IEnumerator Spos_fire_frame_carries_the_shader_muzzle_flash()
        {
            var go = NewMonsterRoot(out var bb);
            var model = ExperimentalMonsterModel.TryAttach(go, "SPOS", 1f / 32f, bb);
            Assert.That(model, Is.Not.Null);
            Assert.That(ExperimentalMonsterModel.TryGetMuzzleFlashForTest(
                "SPOS", out int fire, out _), Is.True);

            Transform flash = null;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name == "MuzzleFlash") flash = t;
            Assert.That(flash, Is.Not.Null,
                "the fire frame instance must carry the MuzzleFlash quad");
            Assert.That(flash.parent.name, Is.EqualTo("SPOSF1"),
                "the flash rides the fire frame, not the pivot");
            var renderer = flash.GetComponent<MeshRenderer>();
            Assert.That(renderer.sharedMaterial.shader.name,
                Is.EqualTo("Doom/ExperimentalMuzzleFlash"));
            Assert.That(renderer.sharedMaterial.mainTexture, Is.Not.Null,
                "the flash samples the native-baked radial LUT");

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display,
                new NoOpGraphicsModeAdapter());
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            yield return null;

            Assert.That(flash.gameObject.activeInHierarchy, Is.False,
                "no flash outside the fire frame");
            model.NotifyFrame(fire);
            Assert.That(flash.gameObject.activeInHierarchy, Is.True,
                "the flash shows exactly while the fire frame does");
            model.NotifyFrame(0);
            Assert.That(flash.gameObject.activeInHierarchy, Is.False,
                "leaving the fire frame hides the flash");

            Object.Destroy(go);
            yield return null;
        }

        // A frame that lies flat on the floor is scaled by the native patch
        // WIDTH, not its height: its Y extent is thickness, and matching that
        // to the patch height rears the pile up (the corpses read as meat
        // propped against a wall, 2026-08-17). Death frames are built lazily,
        // so the monster is usually facing somewhere when they appear — the
        // measurement must not ride that yaw.
        [UnityTest]
        public IEnumerator Flat_death_frames_take_the_patch_width_and_lie_down(
            [Values(0f, 90f)] float doomAngleDeg)
        {
            const float WorldScale = 1f / 32f;
            var go = NewMonsterRoot(out var bb);
            var model = ExperimentalMonsterModel.TryAttach(go, "SARG", WorldScale, bb);
            Assert.That(model, Is.Not.Null);

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display,
                new NoOpGraphicsModeAdapter());
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            yield return null;

            // Turn the corpse before its death meshes are instantiated.
            bb.SetDoomAngle(doomAngleDeg);
            // Twice, so the pose interpolation has the same angle on both
            // ends: a half-turned pivot would inflate the world AABB this
            // test measures with.
            model.NotifyGameplayPose(go.transform.position, doomAngleDeg);
            model.NotifyGameplayPose(go.transform.position, doomAngleDeg);
            yield return null;

            Assert.That(ExperimentalMonsterModel.TryGetFlatWidthsForTest(
                "SARG", out var widths), Is.True);
            Assert.That(ExperimentalMonsterModel.TryGetFrameTableForTest(
                "SARG", out _, out var lumps, out var heights), Is.True);

            model.NotifyDeathStarted(extremeDeath: false);
            for (int i = 0; i < lumps.Length; i++)
            {
                if (widths[i] <= 0f) continue;
                model.NotifyFrame(i);
                yield return null;

                var frame = go.transform.Find($"Enhanced3DMonster/SARG{lumps[i]}");
                Assert.That(frame, Is.Not.Null, $"SARG{lumps[i]} instantiated");

                Bounds b = default;
                bool first = true;
                foreach (var r in frame.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (first) { b = r.bounds; first = false; }
                    else b.Encapsulate(r.bounds);
                }
                Assert.That(first, Is.False, $"SARG{lumps[i]} has renderers");

                float widthM = widths[i] * WorldScale;
                float measured = Mathf.Max(b.size.x, b.size.z);
                Assert.That(measured, Is.EqualTo(widthM).Within(widthM * 0.02f),
                    $"SARG{lumps[i]}: a flat frame spans the native patch " +
                    $"width at any facing (yaw {doomAngleDeg})");
                Assert.That(b.size.y,
                    Is.LessThan(heights[i] * WorldScale * 0.85f),
                    $"SARG{lumps[i]}: a pile must stay lower than the patch " +
                    "height — at the patch height it stands on edge");
                Assert.That(b.min.y,
                    Is.EqualTo(go.transform.position.y).Within(0.01f),
                    $"SARG{lumps[i]}: the pile rests on the floor");
            }

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Gibs_ride_the_billboard_then_the_corpse_mesh_lands()
        {
            var go = NewMonsterRoot(out var bb);
            var mr = go.GetComponent<MeshRenderer>();
            var model = ExperimentalMonsterModel.TryAttach(go, "POSS", 1f / 32f, bb);
            Assert.That(model, Is.Not.Null);

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display,
                new NoOpGraphicsModeAdapter());
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            yield return null;
            Assert.That(model.ModelVisible, Is.True);

            // XDEATH: the gib ANIMATION is flying pixels and rides the native
            // sprite (billboard interlude, not a permanent revert)...
            model.NotifyDeathStarted(extremeDeath: true);
            Assert.That(model.RevertedForTest, Is.False,
                "an xdeath with a corpse mesh must not revert for good");
            Assert.That(model.GibInterludeForTest, Is.True);
            Assert.That(model.ModelVisible, Is.False);
            Assert.That(mr.enabled, Is.True);

            // ...the spray frames stay on the billboard...
            model.NotifyFrame(12);
            model.NotifyFrame(15);
            Assert.That(model.ModelVisible, Is.False);

            // ...and the lasting gib-corpse frame (U = 20) swaps in its mesh.
            model.NotifyFrame(20);
            Assert.That(model.XdeathCorpseShownForTest, Is.True);
            Assert.That(model.ModelVisible, Is.True);
            Assert.That(mr.enabled, Is.False);
            var corpse = model.transform.Find("Enhanced3DMonster/POSSU0");
            Assert.That(corpse, Is.Not.Null, "POSSU0 instance must exist");
            Assert.That(corpse.gameObject.activeInHierarchy, Is.True);

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Gibs_revert_for_good_without_an_xdeath_mesh()
        {
            // The demon has no XDeath in info.c and no xdeath corpse mesh —
            // an extreme death (crusher) reverts to the billboard as before.
            var go = NewMonsterRoot(out var bb);
            var mr = go.GetComponent<MeshRenderer>();
            var model = ExperimentalMonsterModel.TryAttach(go, "SARG", 1f / 32f, bb);
            Assert.That(model, Is.Not.Null);

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display,
                new NoOpGraphicsModeAdapter());
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            yield return null;
            Assert.That(model.ModelVisible, Is.True);

            model.NotifyDeathStarted(extremeDeath: true);
            Assert.That(model.RevertedForTest, Is.True);
            Assert.That(model.ModelVisible, Is.False);
            Assert.That(mr.enabled, Is.True);

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator E1m1_spawns_mesh_routed_poss_monster()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 300; i++)
            {
                var flow = GameFlowController.Instance;
                if (flow != null && flow.State == GameFlowState.Playing &&
                    GameObject.Find("Player") != null)
                    break;
                yield return null;
            }

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display);
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            yield return null; yield return null;

            ExperimentalMonsterModel found = null;
            foreach (var m in Object.FindObjectsByType<ExperimentalMonsterModel>(
                         FindObjectsSortMode.None))
            {
                if (m != null && m.HasModel) { found = m; break; }
            }
            Assert.That(found, Is.Not.Null,
                "E1M1 must spawn at least one mesh-routed POSS zombieman");
            Assert.That(found.GetComponent<MonsterController>(), Is.Not.Null,
                "model rides the original monster root");
            Assert.That(found.ModelVisible, Is.True);

            settings.SetGraphicsMode(GraphicsMode.Classic);
            yield return null;
            Assert.That(found.ModelVisible, Is.False, "Classic → native billboard");
        }
    }
}
