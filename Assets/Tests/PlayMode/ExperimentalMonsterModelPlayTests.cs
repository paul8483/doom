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
            settings.SetEnhanced3DObjects(true);
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
        public IEnumerator Live_frames_swap_and_death_reverts_forever()
        {
            var go = NewMonsterRoot(out var bb);
            var mr = go.GetComponent<MeshRenderer>();
            var model = ExperimentalMonsterModel.TryAttach(go, "POSS", 1f / 32f, bb);
            Assert.That(model, Is.Not.Null);

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display,
                new NoOpGraphicsModeAdapter());
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            settings.SetEnhanced3DObjects(true);
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
                "POSS death meshes (H0-L0) are not authored yet");
            model.NotifyDeathStarted(extremeDeath: false);
            Assert.That(model.RevertedForTest, Is.True,
                "uncovered death tail reverts before the first fall frame");

            // First death frame (H = 7) → billboard, permanently.
            model.NotifyFrame(7);
            Assert.That(model.RevertedForTest, Is.True);
            Assert.That(model.ModelVisible, Is.False);
            Assert.That(mr.enabled, Is.True, "death shows billboard frames");
            Assert.That(bb.enabled, Is.True);

            // Toggle churn must not resurrect the mesh on a corpse.
            settings.SetEnhanced3DObjects(false);
            yield return null;
            settings.SetEnhanced3DObjects(true);
            yield return null;
            Assert.That(model.ModelVisible, Is.False, "corpse never returns to mesh");
            Assert.That(mr.enabled, Is.True);

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Spos_death_chain_stays_on_the_mesh_through_the_corpse()
        {
            var go = NewMonsterRoot(out var bb);
            var mr = go.GetComponent<MeshRenderer>();
            var model = ExperimentalMonsterModel.TryAttach(go, "SPOS", 1f / 32f, bb);
            Assert.That(model, Is.Not.Null);
            Assert.That(model.CoveredDeathFramesForTest, Is.EqualTo(5),
                "SPOS covers its whole death chain H0-L0, corpse included");

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display,
                new NoOpGraphicsModeAdapter());
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            settings.SetEnhanced3DObjects(true);
            yield return null;

            model.NotifyDeathStarted(extremeDeath: false);
            Assert.That(model.RevertedForTest, Is.False,
                "a covered fall keeps the mesh");

            // Death frames 7-11 (H0-L0) are covered and swap like live frames.
            foreach (int frame in new[] { 7, 8, 9, 10, 11 })
            {
                model.NotifyFrame(frame);
                Assert.That(model.CurrentFrameForTest, Is.EqualTo(frame));
                Assert.That(model.ModelVisible, Is.True, $"frame {frame} on mesh");
            }

            // Frame 12 opens the xdeath gib sequence, which the table never
            // covers — presentation hands over to the native sprite for good.
            model.NotifyFrame(12);
            Assert.That(model.RevertedForTest, Is.True);
            Assert.That(model.ModelVisible, Is.False);
            Assert.That(mr.enabled, Is.True);

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Gibs_always_revert_even_with_covered_death()
        {
            var go = NewMonsterRoot(out var bb);
            var mr = go.GetComponent<MeshRenderer>();
            var model = ExperimentalMonsterModel.TryAttach(go, "POSS", 1f / 32f, bb);
            Assert.That(model, Is.Not.Null);

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display,
                new NoOpGraphicsModeAdapter());
            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            settings.SetEnhanced3DObjects(true);
            yield return null;
            Assert.That(model.ModelVisible, Is.True);

            // XDEATH is a different anatomy (flying gibs) and is never covered
            // by the stop-motion set, whatever the death tail holds.
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
            settings.SetEnhanced3DObjects(true);
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
