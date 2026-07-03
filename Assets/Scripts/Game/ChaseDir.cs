namespace Doom.Game
{
    /// Port of P_NewChaseDir (p_enemy.c). Deltas in DOOM units, +y = north.
    public static class ChaseDir
    {
        public delegate bool TryStepFn(Dir8 dir);

        static readonly Dir8[] Opposite =
        {
            Dir8.West, Dir8.SouthWest, Dir8.South, Dir8.SouthEast,
            Dir8.East, Dir8.NorthEast, Dir8.North, Dir8.NorthWest, Dir8.None
        };
        // diags[(deltay<0)*2 + (deltax>0)] as in the original.
        static readonly Dir8[] Diags =
            { Dir8.NorthWest, Dir8.NorthEast, Dir8.SouthWest, Dir8.SouthEast };

        /// Picks a new movement direction; returns Dir8.None when cornered.
        /// movecount = P_Random()&15 on success (moves before re-deciding).
        public static Dir8 NewChaseDir(float dx, float dy, Dir8 current,
                                       DoomRandom r, TryStepFn tryStep, out int movecount)
        {
            movecount = 0;
            Dir8 turnaround = Opposite[(int)current];

            Dir8 d1 = dx > 10f ? Dir8.East : dx < -10f ? Dir8.West : Dir8.None;
            Dir8 d2 = dy < -10f ? Dir8.South : dy > 10f ? Dir8.North : Dir8.None;

            // Try a direct diagonal first.
            if (d1 != Dir8.None && d2 != Dir8.None)
            {
                var diag = Diags[((dy < 0f ? 1 : 0) << 1) + (dx > 0f ? 1 : 0)];
                if (diag != turnaround && tryStep(diag)) return Ok(diag, r, out movecount);
            }

            // Randomly (or when |dy|>|dx|) swap axis priorities.
            if (r.Next() > 200 || System.Math.Abs(dy) > System.Math.Abs(dx))
                (d1, d2) = (d2, d1);
            if (d1 == turnaround) d1 = Dir8.None;
            if (d2 == turnaround) d2 = Dir8.None;

            if (d1 != Dir8.None && tryStep(d1)) return Ok(d1, r, out movecount);
            if (d2 != Dir8.None && tryStep(d2)) return Ok(d2, r, out movecount);

            // Keep the old direction if it still works.
            if (current != Dir8.None && tryStep(current)) return Ok(current, r, out movecount);

            // Random sweep over all eight, direction of sweep randomized.
            if ((r.Next() & 1) != 0)
            {
                for (var d = Dir8.East; d <= Dir8.SouthEast; d++)
                    if (d != turnaround && tryStep(d)) return Ok(d, r, out movecount);
            }
            else
            {
                for (var d = Dir8.SouthEast; d >= Dir8.East; d--)
                    if (d != turnaround && tryStep(d)) return Ok(d, r, out movecount);
            }

            // Cornered: take the turnaround as the last resort.
            if (turnaround != Dir8.None && tryStep(turnaround)) return Ok(turnaround, r, out movecount);
            return Dir8.None;
        }

        static Dir8 Ok(Dir8 d, DoomRandom r, out int movecount)
        {
            movecount = r.Next() & 15;
            return d;
        }
    }
}
