using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    /// Regression for floating pickups (E1M2 tag-6 switch): things are placed on
    /// the floor once at spawn, so a floor special moving the sector under them
    /// used to leave them hanging in the air (and saves preserved the hang).
    /// FloorAnchor re-clips floor-standing things to the moved plane, like
    /// vanilla DOOM's P_ChangeSector.
    public class FloorAnchorPlayTests
    {
        const float WorldScale = 1f / 32f;

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

        static int SectorUnder(Vector3 position)
        {
            var hits = Physics.RaycastAll(
                position + Vector3.up * 100f, Vector3.down, 300f);
            float bestY = float.NegativeInfinity;
            SectorRef best = null;
            foreach (var h in hits)
            {
                if (h.collider.gameObject.name != "Floor") continue;
                if (h.point.y <= bestY) continue;
                bestY = h.point.y;
                best = h.collider.GetComponent<SectorRef>();
            }
            return best != null ? best.SectorIndex : -1;
        }

        static ThingPickup FindGroundedPickup(out int sector)
        {
            foreach (var pickup in Object.FindObjectsByType<ThingPickup>(
                         FindObjectsSortMode.None))
            {
                int s = SectorUnder(pickup.transform.position);
                if (s < 0) continue;
                sector = s;
                return pickup;
            }
            sector = -1;
            return null;
        }

        [UnityTest]
        public IEnumerator Pickup_follows_a_lowering_floor()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null; yield return null;
            for (int i = 0; i < 90; i++) yield return null; // let MapLoader.Build finish

            var loader = Object.FindAnyObjectByType<MapLoader>();
            Assert.That(loader, Is.Not.Null, "MapLoader should exist");
            var heights = loader.RuntimeHeights;
            var geom = loader.Geometry;
            Assert.That(heights, Is.Not.Null);
            Assert.That(geom, Is.Not.Null);

            var pickup = FindGroundedPickup(out int sector);
            Assert.That(pickup, Is.Not.Null, "E1M1 should have a pickup over a floor");
            Assert.That(pickup.GetComponent<FloorAnchor>(), Is.Not.Null,
                "Spawned pickups should carry a FloorAnchor");

            float beforeY = pickup.transform.position.y;
            float startFloorRaw = heights.FloorRaw(sector);

            // Lower the sector floor by 32 DOOM units in small steps, rebuilding
            // each step — the same path a floor special / lift mover drives.
            Time.captureDeltaTime = 1f / 60f;
            const float totalDropRaw = 32f;
            const int steps = 16;
            for (int i = 1; i <= steps; i++)
            {
                heights.SetFloor(sector, startFloorRaw - totalDropRaw * (i / (float)steps));
                geom.RebuildSectorAndNeighbors(sector);
                yield return null;
            }
            Time.captureDeltaTime = 0f;

            float afterY = pickup.transform.position.y;
            float expected = beforeY - totalDropRaw * WorldScale; // dropped 1.0 m

            Debug.Log($"[PlayTest] pickup={pickup.name} sector={sector} " +
                      $"beforeY={beforeY} afterY={afterY} expected={expected}");

            Assert.That(afterY, Is.EqualTo(expected).Within(0.05f),
                "Pickup should ride down with the lowering floor, not hang in the air");
        }

        [UnityTest]
        public IEnumerator ReanchorAll_settles_a_hanging_pickup()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null; yield return null;
            for (int i = 0; i < 90; i++) yield return null; // let MapLoader.Build finish

            var loader = Object.FindAnyObjectByType<MapLoader>();
            Assert.That(loader, Is.Not.Null, "MapLoader should exist");
            var heights = loader.RuntimeHeights;
            Assert.That(heights, Is.Not.Null);

            var pickup = FindGroundedPickup(out int sector);
            Assert.That(pickup, Is.Not.Null, "E1M1 should have a pickup over a floor");

            float floorY = heights.FloorRaw(sector) * WorldScale;

            // Simulate a pre-FloorAnchor save: the thing was recorded hanging 2 m
            // over its sector floor (the E1M2 shells/clip case).
            pickup.transform.position += Vector3.up * 2f;

            FloorAnchor.ReanchorAll(heights, WorldScale);

            Assert.That(pickup.transform.position.y, Is.EqualTo(floorY).Within(1e-3f),
                "ReanchorAll should settle the hanging pickup back onto its floor");
        }
    }
}
