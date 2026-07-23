using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    public class SpriteSpawnPlayTests
    {
        /// BuildRoutine is a yielding coroutine (loading plate, warm phases) —
        /// wait for it to finish instead of assuming a fixed frame count.
        static IEnumerator WaitForMapBuild()
        {
            for (int i = 0; i < 30000; i++)
            {
                var loader = Object.FindAnyObjectByType<MapLoader>();
                if (loader != null && loader.LastBuildSeconds > 0f)
                    yield break;
                yield return null;
            }

            Assert.Fail("MapLoader build did not finish in time");
        }

        [UnityTest]
        public IEnumerator E1M1_spawns_sprite_things_with_renderers()
        {
            SceneManager.LoadScene("Stage2_MapPreview");
            yield return null;            // let AfterSceneLoad bootstrap run
            yield return null;            // let the scene swap land
            yield return WaitForMapBuild();
            // Give physics/colliders + a billboard LateUpdate a couple of frames.
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();

            var billboards = Object.FindObjectsByType<SpriteBillboard>(
                FindObjectsSortMode.None);
            Assert.That(billboards.Length, Is.GreaterThan(0),
                "Expected at least one spawned sprite on E1M1");

            // At least one billboard has a MeshRenderer.
            Assert.That(billboards.Any(b => b.GetComponent<MeshRenderer>() != null), Is.True);

            // At least one solid thing got a collider.
            var solid = Object.FindObjectsByType<CapsuleCollider>(FindObjectsSortMode.None)
                .Where(c => c.GetComponent<SpriteBillboard>() != null);
            Assert.That(solid.Any(), Is.True,
                "Expected at least one solid billboard with a CapsuleCollider");
        }

        [UnityTest]
        public IEnumerator Player_start_does_not_spawn_a_sprite()
        {
            SceneManager.LoadScene("Stage2_MapPreview");
            yield return null;
            yield return null;
            yield return WaitForMapBuild();
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();

            // No billboard should sit exactly on the player's start type. We assert
            // indirectly: the Player GameObject exists and has no SpriteBillboard.
            var player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null);
            Assert.That(player.GetComponent<SpriteBillboard>(), Is.Null);
        }
    }
}
