using System.Collections.Generic;
using System.IO;
using Doom.Map;
using Doom.Wad;
using NUnit.Framework;
using UnityEngine;

namespace Doom.Specials.Tests
{
    public class E1CompatibilityInventoryTests
    {
        static readonly HashSet<int> CrusherFamily = new()
        {
            6, 25, 44, 49, 57, 72, 73, 74, 77, 141,
        };

        [Test]
        public void Freedoom_E1_scroll_and_crusher_inventory_is_runtime_supported()
        {
            string path = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var occurrences = new Dictionary<int, List<string>>();

            for (int episodeMap = 1; episodeMap <= 9; episodeMap++)
            {
                string mapName = $"E1M{episodeMap}";
                var map = MapData.Load(wad, mapName);
                for (int line = 0; line < map.LineDefs.Length; line++)
                {
                    int special = map.LineDefs[line].Special;
                    if (special != 48 && special != 85 &&
                        !CrusherFamily.Contains(special))
                        continue;
                    if (!occurrences.TryGetValue(special, out var locations))
                    {
                        locations = new List<string>();
                        occurrences[special] = locations;
                    }
                    locations.Add($"{mapName}:{line}");
                }
            }

            Assert.That(occurrences.ContainsKey(48), Is.True,
                "Freedoom E1 compatibility fixture should exercise classic wall scrolling");
            foreach (var pair in occurrences)
            {
                TestContext.Progress.WriteLine(
                    $"special {pair.Key}: {string.Join(", ", pair.Value)}");
                if (pair.Key == 48 || pair.Key == 85)
                    Assert.That(WallScrollRules.TryGetUnitsPerTic(pair.Key, out _), Is.True);
                else
                {
                    Assert.That(CrusherRules.TryGet(pair.Key, out _), Is.True);
                    Assert.That(LineSpecialTable.TryGet(pair.Key, out var special), Is.True);
                    Assert.That(special.IsExecutable, Is.True);
                }
            }
        }
    }
}
