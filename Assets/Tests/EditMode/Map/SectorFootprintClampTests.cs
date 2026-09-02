using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;

namespace Doom.Map.Tests
{
    /// A lying 3D corpse is a slab; its centre may sit right on a sector edge
    /// (killed on a lift standing level with the floor next to it). The clamp
    /// returns the smallest capped shift that puts the whole slab inside the
    /// sector so a later floor move cannot slice it (E1M3 lift 47, 2026-09-02).
    public class SectorFootprintClampTests
    {
        static string WadPath => Path.Combine(
            Application.dataPath, "StreamingAssets", "wads", "freedoom1.wad");

        /// One 64x64 sector wound the way real WADs are: the front side lies
        /// on the RIGHT of v1->v2 (SyntheticMapBuilder.SingleSquareSector
        /// runs counter-clockwise, which no DOOM editor produces and which
        /// puts every inward normal outside).
        static MapData ClockwiseSquare()
        {
            var verts = new[]
            {
                new Vertex(0, 0), new Vertex(0, 64),
                new Vertex(64, 64), new Vertex(64, 0),
            };
            var lines = new[]
            {
                new LineDef(0, 1, 0, 0, 0, 0, -1),
                new LineDef(1, 2, 0, 0, 0, 1, -1),
                new LineDef(2, 3, 0, 0, 0, 2, -1),
                new LineDef(3, 0, 0, 0, 0, 3, -1),
            };
            var sides = new[]
            {
                new SideDef(0, 0, "-", "-", "W", 0), new SideDef(0, 0, "-", "-", "W", 0),
                new SideDef(0, 0, "-", "-", "W", 0), new SideDef(0, 0, "-", "-", "W", 0),
            };
            var sectors = new[] { new Sector(0, 128, "F", "F", 0, 0, 0) };
            return new MapData("SQUARE", verts, lines, sides, sectors, new Thing[0]);
        }

        [Test]
        public void Footprint_over_a_one_sided_edge_is_pushed_inside()
        {
            var map = ClockwiseSquare();
            // 40 x 30 slab, axis along +x, centred 5 units from the west wall.
            bool shifted = SectorFootprintClamp.TryClamp(
                map, 0, 5, 32, 1, 0, halfX: 20, halfZ: 15, maxShift: 32,
                out double dx, out double dy);

            Assert.IsTrue(shifted);
            Assert.AreEqual(15.0, dx, 1e-6, "west edge at x=0 needs the centre at x=20");
            Assert.AreEqual(0.0, dy, 1e-6, "north/south walls are 17 units clear");
        }

        [Test]
        public void Footprint_already_inside_is_left_alone()
        {
            var map = ClockwiseSquare();
            bool shifted = SectorFootprintClamp.TryClamp(
                map, 0, 32, 32, 1, 0, halfX: 20, halfZ: 15, maxShift: 32,
                out double dx, out double dy);

            Assert.IsFalse(shifted);
            Assert.AreEqual(0.0, dx);
            Assert.AreEqual(0.0, dy);
        }

        [Test]
        public void Rotated_footprint_uses_its_own_axes()
        {
            var map = ClockwiseSquare();
            // Same slab turned 90°: local X runs along +y, so the 20-unit half
            // extent now points at the south wall (y=0) and the 15-unit one
            // along x.
            bool shifted = SectorFootprintClamp.TryClamp(
                map, 0, 32, 5, 0, 1, halfX: 20, halfZ: 15, maxShift: 32,
                out double dx, out double dy);

            Assert.IsTrue(shifted);
            Assert.AreEqual(0.0, dx, 1e-6);
            Assert.AreEqual(15.0, dy, 1e-6, "south edge at y=0 needs the centre at y=20");
        }

        [Test]
        public void Shift_is_capped()
        {
            var map = ClockwiseSquare();
            bool shifted = SectorFootprintClamp.TryClamp(
                map, 0, 5, 32, 1, 0, halfX: 20, halfZ: 15, maxShift: 10,
                out double dx, out double dy);

            Assert.IsTrue(shifted);
            Assert.AreEqual(10.0, Math.Sqrt(dx * dx + dy * dy), 1e-6,
                "the visual never drifts more than the cap from the thing");
            Assert.Greater(dx, 0, "the capped shift keeps its direction");
        }

        [Test]
        public void Slab_too_big_for_the_sector_does_not_blow_up()
        {
            var map = ClockwiseSquare(); // 64 wide
            bool shifted = SectorFootprintClamp.TryClamp(
                map, 0, 5, 32, 1, 0, halfX: 40, halfZ: 15, maxShift: 32,
                out double dx, out double dy);

            Assert.IsTrue(shifted);
            Assert.LessOrEqual(Math.Sqrt(dx * dx + dy * dy), 32.0 + 1e-6);
            Assert.IsFalse(double.IsNaN(dx) || double.IsNaN(dy));
        }

        /// The reported case: the sergeant corpse (thing 103 of the slot-0
        /// save) lies 0.5 units inside the E1M3 lift (sector 47, tag 1) on its
        /// south edge — line 261 to sector 34, floor 0 while the lift tops out
        /// at 128. SPOSL0 is a 50 x 47 slab, so half of it hung over the pit.
        [Test]
        public void E1M3_lift_corpse_is_moved_fully_onto_the_lift()
        {
            if (!File.Exists(WadPath)) Assert.Ignore("freedoom1.wad missing");
            using var wad = WadFile.Open(WadPath);
            var map = MapData.Load(wad, "E1M3");

            const int lift = 47;
            const double cx = -1374.2, cy = 1696.5, angle = 252.2;
            const double halfX = 25.0, halfZ = 23.5;
            // Mesh local X in map space for a DOOM facing a: (sin a, -cos a)
            // (the pivot yaw is 90 - a about Unity Y).
            double rad = angle * Math.PI / 180.0;
            double ax = Math.Sin(rad), ay = -Math.Cos(rad);

            bool shifted = SectorFootprintClamp.TryClamp(
                map, lift, cx, cy, ax, ay, halfX, halfZ, maxShift: 32,
                out double dx, out double dy);

            Assert.IsTrue(shifted, "half the slab hangs over line 261");
            Assert.Greater(dy, 20.0, "the slab must move north, onto the lift");
            Assert.LessOrEqual(Math.Sqrt(dx * dx + dy * dy), 32.0 + 1e-6);

            // Every corner of the shifted slab lies within the lift's box
            // (x -1440..-1312, y 1696..1760).
            double zx = -ay, zy = ax;
            double ncx = cx + dx, ncy = cy + dy;
            for (int c = 0; c < 4; c++)
            {
                double sx = (c & 1) == 0 ? -1 : 1, sz = (c & 2) == 0 ? -1 : 1;
                double kx = ncx + sx * halfX * ax + sz * halfZ * zx;
                double ky = ncy + sx * halfX * ay + sz * halfZ * zy;
                Assert.That(kx, Is.InRange(-1440.0 - 1e-6, -1312.0 + 1e-6), "corner x");
                Assert.That(ky, Is.InRange(1696.0 - 1e-6, 1760.0 + 1e-6), "corner y");
            }
        }
    }
}
