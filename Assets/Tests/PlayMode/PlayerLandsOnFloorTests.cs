using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    public class PlayerLandsOnFloorTests
    {
        [TearDown]
        public void TearDown()
        {
            // Always restore real-time stepping so other tests are unaffected.
            Time.captureDeltaTime = 0f;
        }

        [UnityTest]
        public IEnumerator Player_lands_on_floor_at_E1M1_start()
        {
            // Building the E1M1 block-out cooks ~182 MeshColliders; a handful of
            // degenerate sectors make PhysX emit non-fatal "cleaning the mesh failed"
            // error logs. Those are expected for the raw block-out geometry and are
            // unrelated to whether the player lands, so don't let them fail the test.
            LogAssert.ignoreFailingMessages = true;

            // Force a fixed, realistic per-frame time step. In headless -batchmode the
            // engine runs thousands of fps, so Time.deltaTime per frame is ~0.00006s.
            // That is too small for the CharacterController's gravity/ground-stick Move
            // to ever press into the floor, so cc.isGrounded would never latch true.
            // captureDeltaTime pins every frame to 1/60s of game time → deterministic
            // fall and a Move large enough to register ground contact. Each
            // `yield return null` now advances exactly 1/60s of simulated time.
            Time.captureDeltaTime = 1f / 60f;

            // Empty scene — MapLoader.AutoBootstrap creates the GameObject itself.
            // MapLoader re-bootstraps on SceneManager.sceneLoaded, so loading the
            // preview scene at runtime spawns a fresh MapLoader → Player.
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);

            // Wait for the scene to load, MapLoader.Start to run, and the build to
            // finish (≈60 frames = ~1s of stepped time gives plenty of headroom).
            for (int i = 0; i < 90; i++) yield return null;

            var player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null, "Player GameObject must exist after MapLoader.Build");

            float initialY = player.transform.position.y;
            var cc = player.GetComponent<CharacterController>();
            Assert.That(cc, Is.Not.Null, "Player must have a CharacterController");

            // Fall and settle. Spawn = bounds.max.y + 5, so the drop distance varies
            // with the map; poll until grounded rather than guessing a frame count.
            // With a fixed 1/60s step the controller presses into the floor each frame,
            // so isGrounded latches and stays true once the player is resting.
            bool grounded = false;
            for (int i = 0; i < 300; i++)   // up to ~5s of stepped time
            {
                if (cc.isGrounded) { grounded = true; break; }
                yield return null;
            }

            float landedY = player.transform.position.y;
            Debug.Log($"[PlayTest] initialY={initialY} landedY={landedY} " +
                      $"isGrounded={cc.isGrounded} groundedWithinBudget={grounded}");

            Assert.That(landedY, Is.LessThan(initialY),
                "Player should have fallen under gravity");
            Assert.That(landedY, Is.GreaterThan(-200f),
                "Player fell into the void — collider problem");
            Assert.That(cc.isGrounded, Is.True,
                "Player should be standing on the floor (cc.isGrounded)");
        }
    }
}
