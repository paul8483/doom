using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.MapBuild;
using Doom.MapBuild.Rendering;

namespace Doom.Stage3.PlayTests
{
    public class EnhancedLightPlayTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
        }

        [UnityTest]
        public IEnumerator Classic_disables_all_pooled_lights()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            var sys = Object.FindFirstObjectByType<EnhancedLightSystem>();
            Assert.IsNotNull(sys);

            var gfx = GraphicsModeController.Ensure();
            gfx.Apply(GraphicsMode.Enhanced);
            yield return null;

            sys.PulseExplosion(Vector3.zero, 1f / 32f);
            yield return null;
            Assert.That(sys.ActiveLightCount, Is.GreaterThan(0));

            gfx.Apply(GraphicsMode.Classic);
            yield return null;
            Assert.IsFalse(sys.IsProfileEnabled);
            Assert.AreEqual(0, sys.ActiveLightCount);
            Assert.AreEqual(0, sys.ShadowCasterCount);
        }

        [UnityTest]
        public IEnumerator Enhanced_respects_pool_and_shadow_capacity_under_stress()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            var sys = Object.FindFirstObjectByType<EnhancedLightSystem>();
            Assert.IsNotNull(sys);
            var gfx = GraphicsModeController.Ensure();
            gfx.Apply(GraphicsMode.Enhanced);
            yield return null;

            int before = sys.RequestCount;
            for (int i = 0; i < 40; i++)
                sys.PulseExplosion(new Vector3(i * 0.5f, 1f, 0f), 1f / 32f);

            yield return null;

            Assert.That(sys.ActiveLightCount, Is.LessThanOrEqualTo(sys.PoolCapacity));
            Assert.That(sys.ShadowCasterCount, Is.LessThanOrEqualTo(sys.ShadowCapacity));
            Assert.That(sys.RequestCount, Is.GreaterThan(before));

            // After expiry, lights drop; pool slots stay fixed.
            for (int i = 0; i < 90; i++) yield return null;
            Assert.That(sys.ActiveLightCount, Is.LessThanOrEqualTo(sys.PoolCapacity));
        }

        [UnityTest]
        public IEnumerator Muzzle_pulse_does_not_change_ammo_or_hp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            var player = GameObject.Find("Player");
            Assert.IsNotNull(player);
            var weapons = player.GetComponent<PlayerWeapons>();
            var health = player.GetComponent<PlayerHealth>();
            Assert.IsNotNull(weapons);
            Assert.IsNotNull(health);

            var gfx = GraphicsModeController.Ensure();
            gfx.Apply(GraphicsMode.Enhanced);
            yield return null;

            int ammo = weapons.Ammo.Get(AmmoType.Bullets);
            int hp = health.Health;
            weapons.FireOnceForTest();
            yield return null;

            Assert.AreEqual(ammo - 1, weapons.Ammo.Get(AmmoType.Bullets));
            Assert.AreEqual(hp, health.Health);

            var sys = Object.FindFirstObjectByType<EnhancedLightSystem>();
            Assert.IsNotNull(sys);
            Assert.That(sys.RequestCount, Is.GreaterThan(0));
        }
    }
}
