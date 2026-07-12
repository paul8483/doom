using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    public class MenuPlayTests
    {
        [SetUp]
        public void SetUp()
        {
            MapLoader.MapNameOverride = null;
            GameFlowController.ResetForTests();
            GameFlowController.AutoStartPlaying = false;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            MapLoader.MapNameOverride = null;
            LevelTransitionController.ImmediateConfirmForTests = true;
            GameFlowController.ResetForTests();
        }

        [UnityTest]
        public IEnumerator Boot_shows_main_menu_without_player()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForFlow(GameFlowState.MainMenu);

            var flow = GameFlowController.Instance;
            Assert.That(flow, Is.Not.Null);
            Assert.That(flow.State, Is.EqualTo(GameFlowState.MainMenu));
            Assert.That(flow.Menu.IsVisible, Is.True);
            Assert.That(flow.Menu.Kind, Is.EqualTo(MenuKind.Main));
            Assert.That(GameObject.Find("Player"), Is.Null);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator NewGame_enters_playing_on_E1M1()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForFlow(GameFlowState.MainMenu);

            var flow = GameFlowController.Instance;
            flow.Menu.Activate(MenuAction.NewGame);

            yield return WaitForPlayer("E1M1");
            Assert.That(GameFlowController.Instance.State, Is.EqualTo(GameFlowState.Playing));
            Assert.That(GameFlowController.Instance.Menu.IsVisible, Is.False);
            Assert.That(GameFlowController.Instance.Loading, Is.Not.Null);
            Assert.That(GameFlowController.Instance.Loading.IsVisible, Is.False);

            var pc = GameObject.Find("Player").GetComponent<PlayerController>();
            Assert.That(pc.enabled, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator NewGame_shows_loading_plate_before_playing()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForFlow(GameFlowState.MainMenu);

            var flow = GameFlowController.Instance;
            flow.Menu.Activate(MenuAction.NewGame);

            bool sawLoading = false;
            for (int i = 0; i < 300; i++)
            {
                flow = GameFlowController.Instance;
                if (flow != null && flow.Loading != null && flow.Loading.IsVisible)
                {
                    sawLoading = true;
                    Assert.That(flow.State, Is.EqualTo(GameFlowState.Loading));
                    break;
                }

                if (flow != null && flow.State == GameFlowState.Playing)
                    break;
                yield return null;
            }

            Assert.That(sawLoading, Is.True, "Expected LoadingView during New Game map build");
            yield return WaitForPlayer("E1M1");
            Assert.That(GameFlowController.Instance.Loading.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator Pause_and_resume_round_trip()
        {
            // Auto-start path used by most PlayMode tests.
            GameFlowController.AutoStartPlaying = true;
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForPlayer("E1M1");

            var flow = GameFlowController.Ensure();
            Assert.That(flow.State, Is.EqualTo(GameFlowState.Playing));

            flow.RequestPause();
            yield return null;

            Assert.That(flow.State, Is.EqualTo(GameFlowState.Paused));
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(flow.Menu.Kind, Is.EqualTo(MenuKind.Pause));
            Assert.That(GameObject.Find("Player").GetComponent<PlayerController>().enabled, Is.False);
            Assert.That(GameFlowController.ShouldDrawStatusHud(), Is.False);
            Assert.That(GameFlowController.ShouldDrawWeaponView(), Is.False);

            // Idempotent: pause again does nothing harmful.
            flow.RequestPause();
            Assert.That(flow.State, Is.EqualTo(GameFlowState.Paused));
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            flow.Resume();
            yield return null;

            Assert.That(flow.State, Is.EqualTo(GameFlowState.Playing));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(flow.Menu.IsVisible, Is.False);
            Assert.That(GameObject.Find("Player").GetComponent<PlayerController>().enabled, Is.True);
            Assert.That(GameFlowController.ShouldDrawStatusHud(), Is.True);
            Assert.That(GameFlowController.ShouldDrawWeaponView(), Is.True);
        }

        [UnityTest]
        public IEnumerator Pause_forbidden_while_dead()
        {
            GameFlowController.AutoStartPlaying = true;
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForPlayer("E1M1");

            var player = GameObject.Find("Player");
            player.GetComponent<PlayerHealth>().TakeDamage(1000);
            yield return null;

            var flow = GameFlowController.Instance;
            Assert.That(flow.State, Is.EqualTo(GameFlowState.Dead));

            flow.RequestPause();
            Assert.That(flow.State, Is.EqualTo(GameFlowState.Dead));
            Assert.That(flow.Menu.IsVisible, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator Pause_forbidden_during_intermission()
        {
            GameFlowController.AutoStartPlaying = true;
            LevelTransitionController.ImmediateConfirmForTests = false;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForPlayer("E1M1");

            int exitLine = LevelTransitionPlayTests_FindLinedef();
            Assert.That(exitLine, Is.GreaterThanOrEqualTo(0));

            GameObject.Find("Player").GetComponent<LineActivator>().ActivateLineForTest(exitLine);

            var flow = GameFlowController.Instance;
            for (int i = 0; i < 120; i++)
            {
                if (flow.State == GameFlowState.Intermission) break;
                yield return null;
            }

            Assert.That(flow.State, Is.EqualTo(GameFlowState.Intermission));
            flow.RequestPause();
            Assert.That(flow.State, Is.EqualTo(GameFlowState.Intermission));
            Assert.That(flow.Menu.IsVisible, Is.False);

            LevelTransitionController.Instance.ConfirmIntermission();
            yield return WaitForPlayer("E1M2");
            Assert.That(GameFlowController.Instance.State, Is.EqualTo(GameFlowState.Playing));
        }

        [UnityTest]
        public IEnumerator Quit_to_main_clears_player_and_session()
        {
            GameFlowController.AutoStartPlaying = true;
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForPlayer("E1M1");

            var flow = GameFlowController.Ensure();
            flow.RequestPause();
            flow.Menu.Activate(MenuAction.QuitToMain);

            yield return WaitForFlow(GameFlowState.MainMenu);
            Assert.That(GameObject.Find("Player"), Is.Null);
            Assert.That(GameSessionHost.Instance.Session.IsActive, Is.False);
            Assert.That(GameFlowController.Instance.Menu.Kind, Is.EqualTo(MenuKind.Main));
        }

        [UnityTest]
        public IEnumerator Death_and_respawn_restore_playing()
        {
            GameFlowController.AutoStartPlaying = true;
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForPlayer("E1M1");

            var player = GameObject.Find("Player");
            var health = player.GetComponent<PlayerHealth>();
            var death = player.GetComponent<PlayerDeathHandler>();
            var pc = player.GetComponent<PlayerController>();

            health.TakeDamage(1000);
            yield return null;
            Assert.That(GameFlowController.Instance.State, Is.EqualTo(GameFlowState.Dead));
            Assert.That(pc.enabled, Is.False);

            death.Respawn();
            yield return null;
            Assert.That(GameFlowController.Instance.State, Is.EqualTo(GameFlowState.Playing));
            Assert.That(pc.enabled, Is.True);
            Assert.That(health.Health, Is.EqualTo(100));
        }

        static IEnumerator WaitForFlow(GameFlowState expected)
        {
            for (int i = 0; i < 180; i++)
            {
                var flow = GameFlowController.Instance;
                if (flow != null && flow.State == expected)
                    yield break;
                yield return null;
            }

            Assert.Fail($"Timed out waiting for GameFlowState.{expected} " +
                        $"(have {GameFlowController.Instance?.State})");
        }

        static IEnumerator WaitForPlayer(string expectedMap)
        {
            for (int i = 0; i < 300; i++)
            {
                var loader = Object.FindAnyObjectByType<MapLoader>();
                var flow = GameFlowController.Instance;
                if (loader != null && loader.LoadedMapName == expectedMap &&
                    GameObject.Find("Player") != null &&
                    flow != null && flow.State == GameFlowState.Playing)
                    yield break;
                yield return null;
            }

            Assert.Fail($"Timed out waiting for player on {expectedMap} " +
                        $"(map={Object.FindAnyObjectByType<MapLoader>()?.LoadedMapName}, " +
                        $"flow={GameFlowController.Instance?.State})");
        }

        static int LevelTransitionPlayTests_FindLinedef()
        {
            string path = System.IO.Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = Doom.Wad.WadFile.Open(path);
            var map = Doom.Map.MapData.Load(wad, "E1M1");
            for (int i = 0; i < map.LineDefs.Length; i++)
                if (map.LineDefs[i].Special == 11)
                    return i;
            return -1;
        }
    }
}
