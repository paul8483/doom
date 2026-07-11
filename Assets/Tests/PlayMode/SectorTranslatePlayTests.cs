using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Wad;
using Doom.Map;
using Doom.Specials;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    /// Regression for the lift/door jitter fix: a moving sector's Floor/Ceiling
    /// GameObject is TRANSLATED (its transform moves), not destroyed+recreated, so
    /// the persistent MeshCollider never re-cooks. Proven via the manual door, which
    /// moves the CEILING child: the same child instance must survive the move and end
    /// up at a higher local Y.
    public class SectorTranslatePlayTests
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
        public IEnumerator Moving_door_ceiling_child_is_translated_not_recreated()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null; yield return null;
            for (int i = 0; i < 90; i++) yield return null; // let MapLoader.Build finish

            var activator = Object.FindAnyObjectByType<LineActivator>();
            Assert.That(activator, Is.Not.Null, "LineActivator should be on the Player");

            // Find a manual (tag 0) door acting on its back sector that actually HAS a
            // rendered "Ceiling" child (sky ceilings have none — skip those).
            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var map = MapData.Load(wad, "E1M1");

            int doorLine = -1, doorSector = -1;
            Transform ceilingChild = null;
            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];
                if (ld.Special == 0 || ld.Tag != 0) continue;
                if (!LineSpecialTable.TryGet(ld.Special, out var sp)) continue;
                if (sp.Category != SpecialCategory.Door) continue;
                int back = ld.BackSideIdx >= 0 ? map.SideDefs[ld.BackSideIdx].SectorIdx : -1;
                if (back < 0) continue;

                var root = GameObject.Find($"Sector_{back}");
                if (root == null) continue;
                var cc = root.transform.Find("Ceiling");
                if (cc == null) continue; // sky ceiling — not useful for this test

                doorLine = i; doorSector = back; ceilingChild = cc; break;
            }

            Assert.That(doorLine, Is.GreaterThanOrEqualTo(0),
                "E1M1 should have a manual door whose back sector has a Ceiling child");
            Assert.That(ceilingChild, Is.Not.Null);

            int beforeId = ceilingChild.gameObject.GetInstanceID();
            float beforeY = ceilingChild.localPosition.y;
            // The ceiling child must carry a persistent MeshFilter mesh (we won't touch it).
            var beforeMesh = ceilingChild.GetComponent<MeshFilter>()?.sharedMesh;
            Assert.That(beforeMesh, Is.Not.Null, "Ceiling child should have a mesh");
            var beforeWalls = CaptureWallMeshes();
            Assert.That(beforeWalls.Count, Is.GreaterThan(0));

            // Deterministic stepping only around the motion.
            Time.captureDeltaTime = 1f / 60f;
            activator.ActivateLineForTest(doorLine);
            for (int i = 0; i < 120; i++) yield return null; // ~2s of motion
            Time.captureDeltaTime = 0f;

            // Re-fetch the Ceiling child fresh from the hierarchy: it must be the SAME
            // instance (translated, NOT destroyed+recreated) and have moved UP.
            var rootAfter = GameObject.Find($"Sector_{doorSector}");
            Assert.That(rootAfter, Is.Not.Null, "Sector root should still exist");
            var ceilAfter = rootAfter.transform.Find("Ceiling");
            Assert.That(ceilAfter, Is.Not.Null, "Ceiling child should still exist after the move");

            Debug.Log($"[PlayTest] door line={doorLine} sector={doorSector} " +
                      $"beforeId={beforeId} afterId={ceilAfter.gameObject.GetInstanceID()} " +
                      $"beforeY={beforeY} afterY={ceilAfter.localPosition.y}");

            Assert.That(ceilAfter.gameObject.GetInstanceID(), Is.EqualTo(beforeId),
                "Ceiling GameObject must be the SAME instance — translated, not recreated");
            Assert.That(ceilAfter.GetComponent<MeshFilter>()?.sharedMesh, Is.SameAs(beforeMesh),
                "Ceiling mesh must persist (no re-cook) across the move");
            Assert.That(ceilAfter.localPosition.y, Is.GreaterThan(beforeY + 1e-4f),
                "Ceiling should have been translated UP as the door opened");

            var afterWalls = CaptureWallMeshes();
            foreach (var pair in beforeWalls)
            {
                Assert.That(afterWalls.TryGetValue(pair.Key, out var mesh), Is.True,
                    $"Wall GameObject {pair.Key} must be pooled, not destroyed");
                Assert.That(mesh, Is.SameAs(pair.Value),
                    $"Wall mesh on GameObject {pair.Key} must be updated in place");
            }
        }

        static Dictionary<int, Mesh> CaptureWallMeshes()
        {
            var result = new Dictionary<int, Mesh>();
            foreach (var filter in Object.FindObjectsByType<MeshFilter>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!filter.gameObject.name.StartsWith("Wall_")) continue;
                result[filter.gameObject.GetInstanceID()] = filter.sharedMesh;
            }
            return result;
        }
    }
}
