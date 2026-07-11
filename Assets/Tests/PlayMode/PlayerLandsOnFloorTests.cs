using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace Doom.Stage3.PlayTests
{
    public class PlayerLandsOnFloorTests
    {
        [TearDown]
        public void TearDown()
        {
            // Always restore real-time stepping so other tests are unaffected.
            Time.captureDeltaTime = 0f;
            // LogAssert.ignoreFailingMessages is a global static; reset it so it
            // doesn't leak into future PlayMode tests.
            LogAssert.ignoreFailingMessages = false;
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
            // finish (≈90 frames ≈ 1.5s of stepped time gives plenty of headroom).
            for (int i = 0; i < 90; i++) yield return null;

            var player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null, "Player GameObject must exist after MapLoader.Build");

            float initialY = player.transform.position.y;
            var cc = player.GetComponent<CharacterController>();
            Assert.That(cc, Is.Not.Null, "Player must have a CharacterController");

            // Spawn snaps feet to the Floor under the player-1 start. CharacterController
            // only latches isGrounded after a Move into the floor, so poll briefly.
            // With a fixed 1/60s step the controller presses into the floor each frame.
            bool grounded = false;
            for (int i = 0; i < 300; i++)   // up to ~5s of stepped time
            {
                if (cc.isGrounded) { grounded = true; break; }
                yield return null;
            }

            float landedY = player.transform.position.y;
            Debug.Log($"[PlayTest] initialY={initialY} landedY={landedY} " +
                      $"isGrounded={cc.isGrounded} groundedWithinBudget={grounded}");

            // Floor-snap must not leave the player at map-sky height (void view).
            Assert.That(initialY, Is.LessThan(50f),
                "Player should spawn on the start floor, not drop from map sky");
            Assert.That(landedY, Is.GreaterThan(-200f),
                "Player fell into the void — collider problem");
            Assert.That(Mathf.Abs(landedY - initialY), Is.LessThan(2f),
                "Spawn Y should already be near the floor (no long sky drop)");
            Assert.That(cc.isGrounded, Is.True,
                "Player should be standing on the floor (cc.isGrounded)");
        }
    }
}
