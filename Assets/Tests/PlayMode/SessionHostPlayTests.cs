using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Doom.MapBuild;
using Doom.Game;

namespace Doom.Stage3.PlayTests
{
    public class SessionHostPlayTests
    {
        static readonly string[] FullE1 =
        {
            "E1M1", "E1M2", "E1M3", "E1M4", "E1M5", "E1M6", "E1M7", "E1M8", "E1M9"
        };

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            GameSessionHost.ResetForTests();
        }

        [UnityTest]
        public IEnumerator Session_host_survives_reload_and_selects_map()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            GameSessionHost.ResetForTests();
            var host = GameSessionHost.Ensure();
            host.Session.BeginNewGame("E1M2", FullE1);

            // Override must NOT win over an active production session.
            MapLoader.MapNameOverride = "E1M1";

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 120; i++) yield return null;

            var hosts = Object.FindObjectsByType<GameSessionHost>(FindObjectsSortMode.None);
            Assert.That(hosts.Length, Is.EqualTo(1), "Exactly one GameSessionHost after reload");

            var loader = Object.FindAnyObjectByType<MapLoader>();
            Assert.That(loader, Is.Not.Null);
            Assert.That(loader.LoadedMapName, Is.EqualTo("E1M2"),
                "Active session map must beat MapNameOverride and inspector default");

            Assert.That(GameObject.Find("Player"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Carry_over_applies_health_and_weapons_on_spawn()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            GameSessionHost.ResetForTests();
            var host = GameSessionHost.Ensure();
            host.Session.BeginNewGame("E1M1", FullE1);

            var health = new HealthModel(42, 100, ArmorKind.Green);
            var ammo = new AmmoModel();
            ammo.Add(AmmoType.Shells, 12);
            var loadout = new WeaponLoadout();
            loadout.Give(WeaponId.Shotgun);
            host.Session.Advance(ExitKind.Normal, PlayerCarryState.Capture(health, ammo, loadout));
            Assert.That(host.Session.CurrentMap, Is.EqualTo("E1M2"));

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);

            MapLoader loader = null;
            GameObject player = null;
            for (int i = 0; i < 180; i++)
            {
                loader = Object.FindAnyObjectByType<MapLoader>();
                player = GameObject.Find("Player");
                if (loader != null && loader.LoadedMapName == "E1M2" && player != null)
                    break;
                yield return null;
            }

            Assert.That(loader, Is.Not.Null);
            Assert.That(loader.LoadedMapName, Is.EqualTo("E1M2"));
            Assert.That(player, Is.Not.Null);

            // Assert before floor-damage tics accumulate (~0.9s).
            var ph = player.GetComponent<PlayerHealth>();
            var pw = player.GetComponent<PlayerWeapons>();
            var inv = player.GetComponent<PlayerInventory>();

            Assert.That(ph.Model.Health, Is.EqualTo(42));
            Assert.That(ph.Model.Armor, Is.EqualTo(100));
            Assert.That(ph.Model.ArmorType, Is.EqualTo(ArmorKind.Green));
            Assert.That(pw.Loadout.Has(WeaponId.Shotgun), Is.True);
            Assert.That(pw.Ammo.Get(AmmoType.Shells), Is.EqualTo(12));
            Assert.That(inv.Keys.HasAny(), Is.False, "keys must not carry between maps");
            Assert.That(inv.Powers.Berserk, Is.False, "powers must not carry between maps");
        }
    }
}
