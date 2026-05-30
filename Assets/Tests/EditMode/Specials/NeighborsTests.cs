using System.Collections.Generic;
using NUnit.Framework;
using Doom.Map;

namespace Doom.Specials.Tests
{
    public class NeighborsTests
    {
        [Test]
        public void Adjacent_sectors_are_neighbors_via_shared_twosided_line()
        {
            // Build (or load) a MapData with sectors 0 and 1 sharing one 2-sided line.
            MapData map = SpecialsTestMaps.TwoAdjacentSectors(floor0: 0, ceil0: 128,
                                                              floor1: 0, ceil1: 128);
            var n0 = new HashSet<int>(Neighbors.OfSector(map, 0));
            Assert.That(n0.Contains(1), Is.True);
            Assert.That(n0.Contains(0), Is.False); // not its own neighbor
        }
    }
}
