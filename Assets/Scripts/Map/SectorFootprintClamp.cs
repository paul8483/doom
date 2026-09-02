using System;

namespace Doom.Map
{
    /// Smallest horizontal shift that keeps an oriented rectangular footprint
    /// inside a sector's boundary lines.
    ///
    /// A lying 3D corpse occupies a slab on the floor (~50 × 47 DOOM units for
    /// a sergeant), while gameplay only knows the thing's centre point. A
    /// billboard hides the difference — it stands on its centre — but a slab
    /// whose centre sits on a sector edge hangs half over the neighbour, and
    /// as soon as that neighbour's floor moves (a lift, a lowering floor) the
    /// overhanging half is cut off by the step or floats in the air (E1M3 lift
    /// 47, 2026-09-02). The thing itself never moves; only the presentation
    /// pivot takes this shift, capped so it can never drift far from the
    /// gameplay origin.
    ///
    /// Boundary lines are those with the sector on exactly one side; the
    /// sector lies on the RIGHT of a front line's v1→v2 direction (DOOM
    /// convention), so the inward normal is (dy, −dx) for the front side and
    /// the opposite for the back side. Only lines the footprint can actually
    /// reach are considered, so the far arm of a concave sector does not push
    /// a corpse that is nowhere near it.
    public static class SectorFootprintClamp
    {
        public const int MaxIterations = 8;
        const double Epsilon = 1e-6;

        /// <param name="axisXx">Direction of the footprint's local X axis in
        /// map space (unnormalised is fine; the Z axis is its +90° CCW turn).</param>
        /// <param name="halfX">Half extent along local X, DOOM units.</param>
        /// <param name="halfZ">Half extent along local Z, DOOM units.</param>
        /// <param name="maxShift">Cap on the resulting shift length.</param>
        /// <returns>true when a non-zero shift was produced.</returns>
        public static bool TryClamp(
            MapData map, int sector,
            double centerX, double centerY,
            double axisXx, double axisXy,
            double halfX, double halfZ,
            double maxShift,
            out double shiftX, out double shiftY)
        {
            shiftX = 0; shiftY = 0;
            if (map == null || sector < 0 || sector >= map.Sectors.Length) return false;
            if (halfX < 0 || halfZ < 0 || maxShift <= 0) return false;

            double axisLen = Math.Sqrt(axisXx * axisXx + axisXy * axisXy);
            if (axisLen < Epsilon) { axisXx = 1; axisXy = 0; }
            else { axisXx /= axisLen; axisXy /= axisLen; }
            double axisZx = -axisXy, axisZy = axisXx;

            double cx = centerX, cy = centerY;
            double reach = halfX + halfZ;
            var lines = map.LineDefs;
            var sides = map.SideDefs;
            var verts = map.Vertexes;

            for (int iter = 0; iter < MaxIterations; iter++)
            {
                bool moved = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    bool front = SectorOf(sides, line.FrontSideIdx) == sector;
                    bool back = SectorOf(sides, line.BackSideIdx) == sector;
                    if (front == back) continue; // not a boundary of this sector

                    var v1 = verts[line.V1];
                    var v2 = verts[line.V2];
                    double dx = v2.X - v1.X, dy = v2.Y - v1.Y;
                    double len = Math.Sqrt(dx * dx + dy * dy);
                    if (len < Epsilon) continue;
                    double nx = front ? dy / len : -dy / len;
                    double ny = front ? -dx / len : dx / len;

                    // Segment proximity: a line the footprint cannot touch has
                    // no say (concave sectors).
                    double t = ((cx - v1.X) * dx + (cy - v1.Y) * dy) / (len * len);
                    if (t < 0) t = 0; else if (t > 1) t = 1;
                    double px = v1.X + t * dx, py = v1.Y + t * dy;
                    double dist = Math.Sqrt((cx - px) * (cx - px) + (cy - py) * (cy - py));
                    if (dist > reach + Epsilon) continue;

                    double min = double.PositiveInfinity;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        double sx = (corner & 1) == 0 ? -1 : 1;
                        double sz = (corner & 2) == 0 ? -1 : 1;
                        double kx = cx + sx * halfX * axisXx + sz * halfZ * axisZx;
                        double ky = cy + sx * halfX * axisXy + sz * halfZ * axisZy;
                        double support = (kx - v1.X) * nx + (ky - v1.Y) * ny;
                        if (support < min) min = support;
                    }
                    if (min < -Epsilon)
                    {
                        cx -= min * nx;
                        cy -= min * ny;
                        moved = true;
                    }
                }
                if (!moved) break;
            }

            shiftX = cx - centerX;
            shiftY = cy - centerY;
            double mag = Math.Sqrt(shiftX * shiftX + shiftY * shiftY);
            if (mag <= Epsilon) { shiftX = 0; shiftY = 0; return false; }
            if (mag > maxShift)
            {
                shiftX *= maxShift / mag;
                shiftY *= maxShift / mag;
            }
            return true;
        }

        static int SectorOf(SideDef[] sides, int sideIdx) =>
            sideIdx >= 0 && sideIdx < sides.Length ? sides[sideIdx].SectorIdx : -1;
    }
}
