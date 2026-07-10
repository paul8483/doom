using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Wad;
using Doom.Map;
using Doom.Specials;
using Doom.MapBuild;
using Doom.Game;

namespace Doom.Stage3.PlayTests
{
    public class LevelTransitionPlayTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            LevelTransitionController.ResetForTests();
        }

        [UnityTest]
        public IEnumerator E1M1_exit_switch_loads_E1M2_and_carries_inventory()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;
            LevelTransitionController.ResetForTests();
            LevelTransitionController.ImmediateConfirmForTests = true;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForPlayer("E1M1");

            var player = GameObject.Find("Player");
            var health = player.GetComponent<PlayerHealth>();
            var weapons = player.GetComponent<PlayerWeapons>();
            health.Model.Restore(77, 100, ArmorKind.Green);
            weapons.Ammo.Add(AmmoType.Shells, 8);
            weapons.Loadout.Give(WeaponId.Shotgun);
            player.GetComponent<PlayerInventory>().Keys.Give(PlayerKey.RedCard);

            int exitLine = FindLinedef("E1M1", special: 11);
            Assert.That(exitLine, Is.GreaterThanOrEqualTo(0));

            var activator = player.GetComponent<LineActivator>();
            activator.ActivateLineForTest(exitLine);

            // Wait for transition coroutine + scene reload + rebuild.
            MapLoader loader = null;
            for (int i = 0; i < 300; i++)
            {
                loader = Object.FindAnyObjectByType<MapLoader>();
                if (loader != null && loader.LoadedMapName == "E1M2" &&
                    GameObject.Find("Player") != null)
                    break;
                yield return null;
            }

            Assert.That(loader, Is.Not.Null);
            Assert.That(loader.LoadedMapName, Is.EqualTo("E1M2"));

            var host = GameSessionHost.Instance;
            Assert.That(host, Is.Not.Null);
            Assert.That(host.Session.CurrentMap, Is.EqualTo("E1M2"));

            player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null);
            health = player.GetComponent<PlayerHealth>();
            weapons = player.GetComponent<PlayerWeapons>();
            var inv = player.GetComponent<PlayerInventory>();

            Assert.That(health.Model.Health, Is.EqualTo(77));
            Assert.That(health.Model.ArmorType, Is.EqualTo(ArmorKind.Green));
            Assert.That(weapons.Loadout.Has(WeaponId.Shotgun), Is.True);
            Assert.That(weapons.Ammo.Get(AmmoType.Shells), Is.EqualTo(8));
            Assert.That(inv.Keys.HasAny(), Is.False, "keys must reset on level advance");
        }

        [UnityTest]
        public IEnumerator E1M3_secret_exit_loads_E1M9()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;
            LevelTransitionController.ResetForTests();
            LevelTransitionController.ImmediateConfirmForTests = true;

            var host = GameSessionHost.Ensure();
            host.Session.BeginNewGame("E1M3", new[]
            {
                "E1M1", "E1M2", "E1M3", "E1M4", "E1M5", "E1M6", "E1M7", "E1M8", "E1M9"
            });

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForPlayer("E1M3");

            int secretLine = FindLinedef("E1M3", special: 51);
            Assert.That(secretLine, Is.GreaterThanOrEqualTo(0));

            LevelExitRequest? accepted = null;
            var ctrl = LevelTransitionController.Ensure();
            void OnExit(LevelExitRequest r) => accepted = r;
            ctrl.ExitAccepted += OnExit;

            GameObject.Find("Player").GetComponent<LineActivator>()
                .ActivateLineForTest(secretLine);

            for (int i = 0; i < 300; i++)
            {
                var loader = Object.FindAnyObjectByType<MapLoader>();
                if (loader != null && loader.LoadedMapName == "E1M9")
                    break;
                yield return null;
            }

            ctrl.ExitAccepted -= OnExit;
            Assert.That(accepted.HasValue, Is.True);
            Assert.That(accepted.Value.Kind, Is.EqualTo(ExitKind.Secret));

            var mapLoader = Object.FindAnyObjectByType<MapLoader>();
            Assert.That(mapLoader.LoadedMapName, Is.EqualTo("E1M9"));
            Assert.That(GameSessionHost.Instance.Session.CurrentMap, Is.EqualTo("E1M9"));
        }

        [UnityTest]
        public IEnumerator Duplicate_exit_during_transition_is_ignored()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;
            LevelTransitionController.ResetForTests();
            // Force a confirm wait so we can fire a second request mid-transition.
            LevelTransitionController.ImmediateConfirmForTests = false;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForPlayer("E1M1");

            var ctrl = LevelTransitionController.Ensure();
            int accepted = 0;
            ctrl.ExitAccepted += _ => accepted++;

            int exitLine = FindLinedef("E1M1", special: 11);
            var activator = GameObject.Find("Player").GetComponent<LineActivator>();
            activator.ActivateLineForTest(exitLine);
            yield return null;
            Assert.That(ctrl.IsTransitioning, Is.True);
            Assert.That(accepted, Is.EqualTo(1));

            // Second request while transitioning must be rejected.
            bool second = ctrl.TryRequestExit(new LevelExitRequest(ExitKind.Normal, exitLine));
            Assert.That(second, Is.False);
            Assert.That(accepted, Is.EqualTo(1));

            ctrl.ConfirmIntermission();
            for (int i = 0; i < 300; i++)
            {
                var loader = Object.FindAnyObjectByType<MapLoader>();
                if (loader != null && loader.LoadedMapName == "E1M2")
                    break;
                yield return null;
            }
            Assert.That(Object.FindAnyObjectByType<MapLoader>().LoadedMapName, Is.EqualTo("E1M2"));
        }

        [UnityTest]
        public IEnumerator Exit_shows_intermission_stats_then_advances_on_confirm()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;
            LevelTransitionController.ResetForTests();
            LevelTransitionController.ImmediateConfirmForTests = false;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForPlayer("E1M1");

            var tracker = Object.FindAnyObjectByType<LevelStatsTracker>();
            Assert.That(tracker, Is.Not.Null);
            Assert.That(tracker.Stats.KillTotal, Is.GreaterThan(0));

            LevelStatsSnapshot? shown = null;
            var ctrl = LevelTransitionController.Ensure();
            void OnShown(LevelStatsSnapshot s) => shown = s;
            ctrl.IntermissionShown += OnShown;

            int exitLine = FindLinedef("E1M1", special: 11);
            GameObject.Find("Player").GetComponent<LineActivator>()
                .ActivateLineForTest(exitLine);

            for (int i = 0; i < 60; i++)
            {
                if (shown.HasValue && ctrl.Intermission != null && ctrl.Intermission.IsVisible)
                    break;
                yield return null;
            }

            ctrl.IntermissionShown -= OnShown;
            Assert.That(shown.HasValue, Is.True);
            Assert.That(ctrl.Intermission.IsVisible, Is.True);
            Assert.That(ctrl.LastStats.HasValue, Is.True);
            Assert.That(ctrl.IsTransitioning, Is.True);

            ctrl.ConfirmIntermission();
            for (int i = 0; i < 300; i++)
            {
                var loader = Object.FindAnyObjectByType<MapLoader>();
                if (loader != null && loader.LoadedMapName == "E1M2")
                    break;
                yield return null;
            }

            Assert.That(Object.FindAnyObjectByType<MapLoader>().LoadedMapName, Is.EqualTo("E1M2"));
        }

        static IEnumerator WaitForPlayer(string expectedMap)
        {
            for (int i = 0; i < 180; i++)
            {
                var loader = Object.FindAnyObjectByType<MapLoader>();
                if (loader != null && loader.LoadedMapName == expectedMap &&
                    GameObject.Find("Player") != null)
                    yield break;
                yield return null;
            }
            Assert.Fail($"Timed out waiting for player on {expectedMap}");
        }

        static int FindLinedef(string mapName, int special)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var map = MapData.Load(wad, mapName);
            for (int i = 0; i < map.LineDefs.Length; i++)
                if (map.LineDefs[i].Special == special)
                    return i;
            return -1;
        }
    }
}
