using UnityEngine;
using Doom.Specials;

namespace Doom.MapBuild
{
    /// Drives one sector's floor or ceiling toward a target height over time.
    /// Supports the door cycle (open → wait → close) and one-shot floor/lift moves.
    public sealed class SectorMover : MonoBehaviour
    {
        public enum Surface { Floor, Ceiling }
        public enum Phase { MovingToTarget, Waiting, Returning, Done }

        RuntimeSectorHeights heights;
        SectorGeometry geometry;
        int sector;
        Surface surface;
        float target;       // DOOM units
        float origin;       // DOOM units (for cycle return)
        float speedUnitsPerSec;
        bool cycle;         // door/lift: return to origin after a wait
        float waitSeconds;
        Phase phase;
        float waitTimer;

        // DOOM speeds (units/tic × 35 tics/sec). Normal door ≈ 2 u/tic, fast ≈ 8.
        public static float SpeedUnitsPerSec(MoveSpeed s) => s switch
        {
            MoveSpeed.Slow => 35f,    // ~1 u/tic
            MoveSpeed.Normal => 70f,  // ~2 u/tic
            MoveSpeed.Fast => 140f,   // ~4 u/tic
            MoveSpeed.Turbo => 280f,  // ~8 u/tic
            _ => 70f
        };

        public void Begin(RuntimeSectorHeights heights, SectorGeometry geometry, int sector,
                          Surface surface, float target, float speedUnitsPerSec,
                          bool cycle, float waitSeconds)
        {
            this.heights = heights; this.geometry = geometry; this.sector = sector;
            this.surface = surface; this.target = target; this.speedUnitsPerSec = speedUnitsPerSec;
            this.cycle = cycle; this.waitSeconds = waitSeconds;
            origin = Current();
            phase = Phase.MovingToTarget;
        }

        float Current() => surface == Surface.Floor ? heights.FloorRaw(sector) : heights.CeilRaw(sector);
        void Set(float v) { if (surface == Surface.Floor) heights.SetFloor(sector, v); else heights.SetCeil(sector, v); }

        void Update()
        {
            if (phase == Phase.Done) { Destroy(this); return; }
            if (phase == Phase.Waiting)
            {
                waitTimer -= Time.deltaTime;
                if (waitTimer <= 0f) phase = Phase.Returning;
                return;
            }

            float goal = phase == Phase.Returning ? origin : target;
            float cur = Current();
            int before = Mathf.RoundToInt(cur);
            float step = speedUnitsPerSec * Time.deltaTime;
            float next = Mathf.MoveTowards(cur, goal, step);
            Set(next);

            if (Mathf.RoundToInt(next) != before)
                geometry.RebuildSectorAndNeighbors(sector);

            if (Mathf.Approximately(next, goal))
            {
                if (phase == Phase.MovingToTarget && cycle)
                { phase = Phase.Waiting; waitTimer = waitSeconds; }
                else
                { geometry.RebuildSectorAndNeighbors(sector); phase = Phase.Done; }
            }
        }
    }
}
