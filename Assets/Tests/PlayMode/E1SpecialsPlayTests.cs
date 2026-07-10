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
    public class E1SpecialsPlayTests
    {
        [SetUp]
        public void SetUp()
        {
            MapLoader.MapNameOverride = null;
            GameSessionHost.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            MapLoader.MapNameOverride = null;
            GameSessionHost.ResetForTests();
        }

        [UnityTest]
        public IEnumerator E1M2_player_teleport_moves_to_landing()
        {
            LogAssert.ignoreFailingMessages = true;
            MapLoader.MapNameOverride = "E1M2";
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null; yield return null;
            for (int i = 0; i < 120; i++) yield return null;

            var activator = Object.FindAnyObjectByType<LineActivator>();
            Assert.That(activator, Is.Not.Null);

            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var map = MapData.Load(wad, "E1M2");
            var landings = TeleportRules.CollectLandings(map);
            Assert.That(landings.Length, Is.GreaterThan(0));

            int teleportLine = -1;
            TeleportLanding expected = default;
            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];
                if (ld.Special != 97) continue;
                if (!TeleportRules.TrySelect(map, ld.Tag, landings, out expected)) continue;
                teleportLine = i;
                break;
            }
            Assert.That(teleportLine, Is.GreaterThanOrEqualTo(0), "E1M2 needs a WR teleport (97)");

            var player = activator.transform;
            Vector3 before = player.position;
            bool moved = activator.ActivateTeleportForTest(teleportLine);
            yield return null;

            Assert.That(moved, Is.True, "player should teleport");
            const float scale = 1f / 32f;
            float dx = player.position.x - expected.X * scale;
            float dz = player.position.z - expected.Y * scale;
            Assert.That(dx * dx + dz * dz, Is.LessThan(0.25f),
                $"player should land near teleport destination; before={before} after={player.position} " +
                $"expected=({expected.X},{expected.Y})");
            Assert.That((player.position - before).sqrMagnitude, Is.GreaterThan(0.01f));
        }

        [UnityTest]
        public IEnumerator Monster_only_teleport_ignores_player()
        {
            LogAssert.ignoreFailingMessages = true;
            MapLoader.MapNameOverride = "E1M7";
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null; yield return null;
            for (int i = 0; i < 120; i++) yield return null;

            var activator = Object.FindAnyObjectByType<LineActivator>();
            Assert.That(activator, Is.Not.Null);

            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var map = MapData.Load(wad, "E1M7");

            int monOnly = -1;
            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                if (map.LineDefs[i].Special == 125 || map.LineDefs[i].Special == 126)
                {
                    monOnly = i;
                    break;
                }
            }
            Assert.That(monOnly, Is.GreaterThanOrEqualTo(0), "E1M7 should have monster-only teleport");

            Vector3 before = activator.transform.position;
            bool moved = activator.ActivateTeleportForTest(monOnly);
            yield return null;
            Assert.That(moved, Is.False);
            Assert.That((activator.transform.position - before).sqrMagnitude, Is.LessThan(0.01f));
        }
    }
}
