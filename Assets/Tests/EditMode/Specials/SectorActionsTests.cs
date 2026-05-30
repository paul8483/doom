using System.Collections.Generic;
using NUnit.Framework;
using Doom.Map;

namespace Doom.Specials.Tests
{
    public class SectorActionsTests
    {
        [Test]
        public void Tag_zero_targets_the_lines_own_back_sector_for_manual_door()
        {
            // Manual doors (tag 0, Push) act on the sector on the line's back side.
            MapData map = SpecialsTestMaps.DoorSetup(); // sector 1 is the door (back side)
            var targets = new List<int>(SectorActions.FindManualDoorTarget(map, lineIndex: SpecialsTestMaps.DoorLineIndex));
            Assert.That(targets, Is.EquivalentTo(new[] { 1 }));
        }

        [Test]
        public void Tagged_lines_target_all_sectors_with_that_tag()
        {
            MapData map = SpecialsTestMaps.TwoTaggedSectors(tag: 7); // sectors 0 and 1 tagged 7
            var targets = new List<int>(SectorActions.FindTaggedSectors(map, tag: 7));
            Assert.That(targets, Is.EquivalentTo(new[] { 0, 1 }));
        }

        [Test]
        public void Door_open_target_is_lowest_neighbor_ceiling_minus_4()
        {
            // door sector 1 ceil currently = its floor; neighbor sector 0 ceil = 128.
            MapData map = SpecialsTestMaps.DoorSetup();
            var h = new StaticSectorHeights(map);
            int target = SectorActions.ComputeTargetHeight(map, h, sectorIdx: 1,
                TargetSpec.LowestNeighborCeilingMinus4);
            Assert.That(target, Is.EqualTo(128 - 4));
        }

        [Test]
        public void Lift_down_target_is_lowest_neighbor_floor()
        {
            MapData map = SpecialsTestMaps.LiftSetup(); // lift sector floor 64, neighbor floor 0
            var h = new StaticSectorHeights(map);
            int target = SectorActions.ComputeTargetHeight(map, h, sectorIdx: SpecialsTestMaps.LiftSector,
                TargetSpec.LowestNeighborFloor);
            Assert.That(target, Is.EqualTo(0));
        }
    }
}
