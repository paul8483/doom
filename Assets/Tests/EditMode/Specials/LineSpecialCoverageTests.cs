using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;
using Doom.Map;

namespace Doom.Specials.Tests
{
    public class LineSpecialCoverageTests
    {
        private static string FreedoomPath =>
            Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");

        [Test]
        public void Every_E1M1_linedef_special_is_in_the_table()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var map = MapData.Load(wad, "E1M1");

            var missing = new SortedSet<int>();
            foreach (var ld in map.LineDefs)
            {
                int sp = ld.Special;
                if (sp == 0) continue;
                if (!LineSpecialTable.TryGet(sp, out _)) missing.Add(sp);
            }
            Assert.That(missing, Is.Empty,
                "E1M1 linedef specials missing from LineSpecialTable: " +
                string.Join(", ", missing));
        }
    }
}
