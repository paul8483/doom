using System.Collections;
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
    public class SectorActionPlayTests
    {
        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator E1M1_door_line_opens_its_sector_ceiling()
        {
            // Cooking E1M1's ~182 MeshColliders makes PhysX emit non-fatal
            // "cleaning the mesh failed" logs for a few degenerate sectors. Those are
            // unrelated to door motion, so don't let them fail this test.
            LogAssert.ignoreFailingMessages = true;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null; yield return null;
            // Let MapLoader.Build finish (geometry + Player + LineActivator wiring).
            for (int i = 0; i < 90; i++) yield return null;

            var activator = Object.FindAnyObjectByType<LineActivator>();
            Assert.That(activator, Is.Not.Null, "LineActivator should be on the Player");

            // Pick a door linedef from the map. Prefer a manual (tag 0) door that acts
            // on its own back sector; fall back to a tagged door if none exist.
            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var map = MapData.Load(wad, "E1M1");

            int doorLine = -1, doorSector = -1;

            // First pass: manual doors (tag 0, act on back sector).
            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];
                if (ld.Special == 0) continue;
                if (!LineSpecialTable.TryGet(ld.Special, out var sp)) continue;
                if (sp.Category != SpecialCategory.Door) continue;
                if (ld.Tag != 0) continue;
                int back = ld.BackSideIdx >= 0 ? map.SideDefs[ld.BackSideIdx].SectorIdx : -1;
                if (back < 0) continue;
                doorLine = i; doorSector = back; break;
            }

            // Fallback: tagged door lines acting on a sector that carries the tag.
            if (doorLine < 0)
            {
                for (int i = 0; i < map.LineDefs.Length && doorLine < 0; i++)
                {
                    var ld = map.LineDefs[i];
                    if (ld.Special == 0 || ld.Tag == 0) continue;
                    if (!LineSpecialTable.TryGet(ld.Special, out var sp)) continue;
                    if (sp.Category != SpecialCategory.Door) continue;
                    for (int s = 0; s < map.Sectors.Length; s++)
                    {
                        if (map.Sectors[s].Tag == ld.Tag)
                        {
                            doorLine = i; doorSector = s; break;
                        }
                    }
                }
            }

            Assert.That(doorLine, Is.GreaterThanOrEqualTo(0), "E1M1 should have a door line");
            Assert.That(doorSector, Is.GreaterThanOrEqualTo(0), "Door should resolve to a sector");

            float beforeCeil = activator.GetSectorCeilForTest(doorSector);
            activator.ActivateLineForTest(doorLine);
            for (int i = 0; i < 120; i++) yield return null; // ~2s of motion

            float afterCeil = activator.GetSectorCeilForTest(doorSector);
            Debug.Log($"[PlayTest] door line={doorLine} sector={doorSector} " +
                      $"beforeCeil={beforeCeil} afterCeil={afterCeil}");
            Assert.That(afterCeil, Is.GreaterThan(beforeCeil),
                "door ceiling should rise when opened");
        }
    }
}
