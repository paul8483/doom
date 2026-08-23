using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    public class HudPlayTests
    {
        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            MapLoader.MapNameOverride = null;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
        }

        static IEnumerator LoadLevel()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null;
            yield return null;
            for (int i = 0; i < 90; i++) yield return null;
            Time.captureDeltaTime = 1f / 60f;
        }

        [UnityTest]
        public IEnumerator Spawn_has_exactly_one_DoomHud_and_no_PlayerHud()
        {
            yield return LoadLevel();
            var huds = Object.FindObjectsByType<DoomHud>(FindObjectsSortMode.None);
            Assert.That(huds.Length, Is.EqualTo(1));
            Assert.That(huds[0].IsReady, Is.True);
            Assert.That(huds[0].Model.Health, Is.EqualTo(100));
            // The idle look direction (STFST00/01/02) randomizes every ~17 tics,
            // so which frame the assert catches depends on scene-load wall time —
            // pin the healthy idle family, not one random member (recurring
            // full-suite flake; went deterministic-red 2026-08-23 when the world
            // redraw Resources shifted load timing past a look boundary).
            Assert.That(huds[0].Model.FacePatch, Does.Match("^STFST0[0-2]$"));

            // PlayerHud was removed in Stage 7b.
            var legacy = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mb in legacy)
                Assert.That(mb.GetType().Name, Is.Not.EqualTo("PlayerHud"));
        }

        [UnityTest]
        public IEnumerator Damage_updates_HudModel_and_ouch_face()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var health = player.GetComponent<PlayerHealth>();
            var hud = player.GetComponent<DoomHud>();

            health.TakeDamage(30); // enough for MuchPain after armor? no armor → 30 HP lost
            yield return null;

            Assert.That(hud.Model.Health, Is.EqualTo(70));
            Assert.That(hud.Model.FacePatch, Is.EqualTo("STFOUCH1"));
        }

        [UnityTest]
        public IEnumerator Death_selects_dead_face()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var health = player.GetComponent<PlayerHealth>();
            var hud = player.GetComponent<DoomHud>();

            health.TakeDamage(1000);
            yield return null;

            Assert.That(health.IsDead, Is.True);
            Assert.That(hud.Model.FacePatch, Is.EqualTo(FaceRules.DeadPatch));
            Assert.That(hud.Face.PatchName, Is.EqualTo(FaceRules.DeadPatch));
        }

        [UnityTest]
        public IEnumerator Weapon_pickup_sets_evil_grin()
        {
            yield return LoadLevel();
            var player = GameObject.Find("Player");
            var inv = player.GetComponent<PlayerInventory>();
            var hud = player.GetComponent<DoomHud>();

            Assert.That(inv.TryPickup(2001), Is.True); // shotgun
            yield return null;

            Assert.That(hud.Model.OwnsShotgun, Is.True);
            Assert.That(hud.Model.FacePatch, Is.EqualTo("STFEVL0"));
        }
    }
}
