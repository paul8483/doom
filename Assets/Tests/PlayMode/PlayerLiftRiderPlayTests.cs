using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    /// Regression for the rising-lift jitter fix (PlayerLiftRider): when the floor
    /// under the player rises, the rider snaps the player's feet to the moving floor
    /// surface each LateUpdate, so the player rides UP in lockstep with the floor.
    /// Without the rider the CharacterController does not track a moving static
    /// collider smoothly — it jerks and lags rather than rising the full amount.
    public class PlayerLiftRiderPlayTests
    {
        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true; // PhysX cook warnings on E1M1
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator Player_rides_up_with_a_rising_floor()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null; yield return null;
            for (int i = 0; i < 90; i++) yield return null; // let MapLoader.Build finish

            var loader = Object.FindAnyObjectByType<MapLoader>();
            Assert.That(loader, Is.Not.Null, "MapLoader should exist");
            var rider = Object.FindAnyObjectByType<PlayerLiftRider>();
            Assert.That(rider, Is.Not.Null, "PlayerLiftRider should be on the Player");
            var cc = Object.FindAnyObjectByType<CharacterController>();
            Assert.That(cc, Is.Not.Null, "Player CharacterController should exist");
            var player = cc.transform;

            // Let the player settle on the floor so the downward raycast resolves a
            // sector. Deterministic stepping ONLY while we drive physics (reset in
            // TearDown). Bounded loop.
            Time.captureDeltaTime = 1f / 60f;
            int settle = 0;
            while (settle < 240 && (!cc.isGrounded || rider.CurrentSectorForTest < 0))
            {
                yield return null;
                settle++;
            }
            Assert.That(cc.isGrounded, Is.True, "Player should have settled on the floor");

            int sector = rider.CurrentSectorForTest;
            Assert.That(sector, Is.GreaterThanOrEqualTo(0),
                "Rider should resolve a sector under the settled player");

            var heights = loader.RuntimeHeights;
            var geom = loader.Geometry;
            Assert.That(heights, Is.Not.Null);
            Assert.That(geom, Is.Not.Null);

            float worldScale = 1f / 32f;
            float startFloorRaw = heights.FloorRaw(sector);
            float beforeY = player.position.y;

            // Raise the sector floor by 32 DOOM units across ~30 small steps, rebuilding
            // each step so the floor collider actually moves up under the player.
            const float totalRaiseRaw = 32f;
            const int steps = 30;
            for (int i = 1; i <= steps; i++)
            {
                float newFloorRaw = startFloorRaw + totalRaiseRaw * (i / (float)steps);
                heights.SetFloor(sector, newFloorRaw);
                geom.RebuildSectorAndNeighbors(sector);
                yield return null;
            }
            // A few extra frames for the rider's LateUpdate to fully close the gap.
            for (int i = 0; i < 5; i++) yield return null;
            Time.captureDeltaTime = 0f;

            float afterY = player.position.y;
            float rose = afterY - beforeY;
            float expected = totalRaiseRaw * worldScale; // 1.0 m

            Debug.Log($"[PlayTest] sector={sector} beforeY={beforeY} afterY={afterY} " +
                      $"rose={rose} expected={expected}");

            // The player must have ridden up close to the full floor rise (within 0.1 m).
            Assert.That(rose, Is.EqualTo(expected).Within(0.1f),
                "Player should ride up with the rising floor (carried by PlayerLiftRider)");
        }
    }
}
