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
    public class EnhancedEffectsPlayTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
        }

        [UnityTest]
        public IEnumerator Pool_capacity_not_exceeded_under_stress()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            var particles = Object.FindFirstObjectByType<ParticleEffectPool>();
            var decals = Object.FindFirstObjectByType<DecalEffectPool>();
            Assert.IsNotNull(particles);
            Assert.IsNotNull(decals);

            var gfx = GraphicsModeController.Ensure();
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);

            for (int i = 0; i < 40; i++)
            {
                particles.Pulse(EffectKind.Explosion, new Vector3(i * 0.2f, 1f, 0f), 1f / 32f);
                decals.Spawn(EffectKind.Puff, new Vector3(i * 0.2f, 1f, 1f), Vector3.forward, null, 1f / 32f);
            }

            yield return null;
            Assert.That(particles.ActiveCount, Is.LessThanOrEqualTo(particles.PoolCapacity));
            Assert.That(decals.ActiveCount, Is.LessThanOrEqualTo(decals.PoolCapacity));
        }

        [UnityTest]
        public IEnumerator Classic_disables_particles_and_decals()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            var particles = Object.FindFirstObjectByType<ParticleEffectPool>();
            var decals = Object.FindFirstObjectByType<DecalEffectPool>();
            Assert.IsNotNull(particles);
            Assert.IsNotNull(decals);

            var gfx = GraphicsModeController.Ensure();
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);
            particles.Pulse(EffectKind.Puff, Vector3.zero, 1f / 32f);
            decals.Spawn(EffectKind.Puff, Vector3.one, Vector3.forward);
            yield return null;
            Assert.That(particles.ActiveCount, Is.GreaterThan(0));
            Assert.That(decals.ActiveCount, Is.GreaterThan(0));

            gfx.Apply(GraphicsMode.Classic);
            yield return null;
            Assert.IsFalse(particles.IsProfileEnabled);
            Assert.IsFalse(decals.IsProfileEnabled);
            Assert.AreEqual(0, particles.ActiveCount);
            Assert.AreEqual(0, decals.ActiveCount);
        }

        [UnityTest]
        public IEnumerator Enhanced_pulse_increases_active_count()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            for (int i = 0; i < 90; i++) yield return null;

            var particles = Object.FindFirstObjectByType<ParticleEffectPool>();
            Assert.IsNotNull(particles);

            var gfx = GraphicsModeController.Ensure();
            yield return GraphicsApplyWait.Apply(gfx, GraphicsMode.Enhanced);

            int before = particles.ActiveCount;
            particles.Pulse(EffectKind.Muzzle, new Vector3(0f, 1f, 0f), 1f / 32f);
            yield return null;
            Assert.That(particles.ActiveCount, Is.GreaterThan(before));
        }
    }
}
