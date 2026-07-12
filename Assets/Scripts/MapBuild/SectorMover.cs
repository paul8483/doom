using UnityEngine;
using Doom.Game;
using Doom.Specials;

namespace Doom.MapBuild
{
    /// Drives one sector's floor or ceiling toward a target height over time.
    /// Supports the door cycle (open → wait → close) and one-shot floor/lift moves.
    public sealed class SectorMover : MonoBehaviour
    {
        public enum Surface { Floor, Ceiling }
        public enum Phase { MovingToTarget, Waiting, Returning, Stopped, Done }

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
        System.Action onDone;
        SoundSystem sound;
        MoverSoundProfile sfx;
        Vector3 soundOrigin;
        object loopKey;
        bool stopPlayed;
        MoverBehavior behavior;
        bool crusherSlows;
        CrusherDamageSystem crusherDamage;

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
                          bool cycle, float waitSeconds, System.Action onDone = null,
                          SoundSystem sound = null, MoverSoundProfile sfx = default,
                          Vector3 soundOrigin = default)
        {
            this.heights = heights; this.geometry = geometry; this.sector = sector;
            this.surface = surface; this.target = target; this.speedUnitsPerSec = speedUnitsPerSec;
            this.cycle = cycle; this.waitSeconds = waitSeconds; this.onDone = onDone;
            this.sound = sound; this.sfx = sfx;
            this.soundOrigin = soundOrigin;
            loopKey = this;
            stopPlayed = false;
            behavior = cycle ? MoverBehavior.Cycle : MoverBehavior.OneShot;
            crusherSlows = false;
            origin = Current();
            phase = Phase.MovingToTarget;
            PlayStartOrLoop();
        }

        /// Resume a mover from a save snapshot. Heights must already be applied.
        /// Waiting phase uses <paramref name="returnOrigin"/> as the post-wait goal
        /// (typically the WAD static height for that plane). Moving phase does a
        /// one-shot travel to <paramref name="targetHeight"/>.
        public void BeginFromSnapshot(
            RuntimeSectorHeights heights, SectorGeometry geometry, int sector,
            Surface surface, float targetHeight, float speedUnitsPerSec,
            MoverPhase moverPhase, int waitTics, float returnOrigin,
            MoverBehavior moverBehavior, bool moverCycle,
            System.Action onDone = null, float worldScale = 1f / 32f)
        {
            this.heights = heights; this.geometry = geometry; this.sector = sector;
            this.surface = surface; this.speedUnitsPerSec = speedUnitsPerSec;
            this.onDone = onDone;
            this.sound = null;
            this.sfx = default;
            this.soundOrigin = default;
            loopKey = this;
            stopPlayed = false;
            behavior = moverBehavior;
            cycle = moverCycle;
            origin = returnOrigin;

            if (moverPhase == MoverPhase.Waiting)
            {
                target = targetHeight;
                waitSeconds = Mathf.Max(0f, waitTics / 35f);
                phase = Phase.Waiting;
                waitTimer = waitSeconds;
            }
            else
            {
                target = targetHeight;
                waitSeconds = 0f;
                phase = moverPhase == MoverPhase.Returning ? Phase.Returning
                    : moverPhase == MoverPhase.Stopped ? Phase.Stopped
                    : Phase.MovingToTarget;
            }
            if (behavior == MoverBehavior.Crusher)
            {
                crusherSlows = speedUnitsPerSec <= 35.01f;
                crusherDamage = gameObject.AddComponent<CrusherDamageSystem>();
                crusherDamage.Begin(this, heights, geometry, sector, worldScale);
            }
        }

        public void BeginCrusher(
            RuntimeSectorHeights heights, SectorGeometry geometry, int sector,
            float targetHeight, float speedUnitsPerSec, bool cycle, bool slowsWhenCrushing,
            float worldScale, System.Action onDone = null,
            SoundSystem sound = null, bool silent = false,
            Vector3 soundOrigin = default)
        {
            Begin(heights, geometry, sector, Surface.Ceiling, targetHeight,
                speedUnitsPerSec, cycle, 0f, onDone, sound,
                silent ? default : MoverSoundProfile.FloorOrLift, soundOrigin);
            behavior = MoverBehavior.Crusher;
            crusherSlows = slowsWhenCrushing;
            crusherDamage = gameObject.AddComponent<CrusherDamageSystem>();
            crusherDamage.Begin(this, heights, geometry, sector, worldScale);
        }

        public int SectorIndex => sector;
        public bool IsCrusher => behavior == MoverBehavior.Crusher;
        public bool IsCrusherDescending =>
            IsCrusher && phase == Phase.MovingToTarget;
        public bool IsStopped => phase == Phase.Stopped;

        public void StopCrusher()
        {
            if (!IsCrusher || phase == Phase.Done) return;
            phase = Phase.Stopped;
            if (sound != null && !string.IsNullOrEmpty(sfx.LoopLump))
                sound.StopLoop(loopKey, sfx.StopLump);
            else
                StopLoopOnly();
        }

        public void ResumeCrusher()
        {
            if (!IsCrusher || phase != Phase.Stopped) return;
            phase = Current() <= target ? Phase.Returning : Phase.MovingToTarget;
            PlayStartOrLoop();
        }

        float Current() => surface == Surface.Floor ? heights.FloorRaw(sector) : heights.CeilRaw(sector);
        void Set(float v) { if (surface == Surface.Floor) heights.SetFloor(sector, v); else heights.SetCeil(sector, v); }

        void Update()
        {
            if (phase == Phase.Done) { Destroy(this); return; }
            if (phase == Phase.Stopped) return;
            if (phase == Phase.Waiting)
            {
                waitTimer -= Time.deltaTime;
                if (waitTimer <= 0f)
                {
                    phase = Phase.Returning;
                    if (!string.IsNullOrEmpty(sfx.ReturnLump))
                        sound?.PlayAt(sfx.ReturnLump, soundOrigin);
                    else if (!string.IsNullOrEmpty(sfx.LoopLump))
                        sound?.PlayLoop(sfx.LoopLump, loopKey, soundOrigin);
                }
                return;
            }

            float goal = phase == Phase.Returning ? origin : target;
            float cur = Current();
            int before = Mathf.RoundToInt(cur);
            float actualSpeed = speedUnitsPerSec;
            if (crusherSlows && phase == Phase.MovingToTarget
                && crusherDamage != null && crusherDamage.IsObstructed)
                actualSpeed *= CrusherRules.CrushingSlowdown;
            float step = actualSpeed * Time.deltaTime;
            float next = Mathf.MoveTowards(cur, goal, step);
            Set(next);

            if (Mathf.RoundToInt(next) != before)
                geometry?.RebuildSectorAndNeighbors(sector);

            if (Mathf.Approximately(next, goal))
            {
                if (behavior == MoverBehavior.Crusher && phase == Phase.MovingToTarget && cycle)
                {
                    phase = Phase.Returning;
                }
                else if (behavior == MoverBehavior.Crusher && phase == Phase.Returning && cycle)
                {
                    phase = Phase.MovingToTarget;
                }
                else if (phase == Phase.MovingToTarget && cycle)
                {
                    StopLoopOnly();
                    phase = Phase.Waiting;
                    waitTimer = waitSeconds;
                }
                else
                {
                    Finish();
                }
            }
        }

        void OnDisable() => StopLoopOnly();
        void OnDestroy() => StopLoopOnly();

        void PlayStartOrLoop()
        {
            if (!string.IsNullOrEmpty(sfx.StartLump))
                sound?.PlayAt(sfx.StartLump, soundOrigin);
            if (!string.IsNullOrEmpty(sfx.LoopLump))
                sound?.PlayLoop(sfx.LoopLump, loopKey, soundOrigin);
        }

        void StopLoopOnly()
        {
            if (sound == null || loopKey == null) return;
            if (string.IsNullOrEmpty(sfx.LoopLump)) return;
            sound.StopLoop(loopKey, stopLump: null);
        }

        void Finish()
        {
            geometry?.RebuildSectorAndNeighbors(sector);
            if (!stopPlayed && !string.IsNullOrEmpty(sfx.StopLump))
            {
                sound?.StopLoop(loopKey, sfx.StopLump);
                stopPlayed = true;
            }
            else
                StopLoopOnly();
            phase = Phase.Done;
            onDone?.Invoke();
        }

        /// Capture authoritative mover state for save. Returns false if Done/uninitialized.
        public bool TryCapture(
            out int sectorIndex,
            out MoverPlane plane,
            out MoverPhase moverPhase,
            out int direction,
            out float targetHeight,
            out float speed,
            out int waitTics,
            out bool active,
            out MoverBehavior moverBehavior,
            out bool moverCycle,
            out float moverOrigin)
        {
            sectorIndex = sector;
            plane = surface == Surface.Floor ? MoverPlane.Floor : MoverPlane.Ceiling;
            targetHeight = target;
            speed = speedUnitsPerSec;
            waitTics = 0;
            direction = 0;
            moverPhase = MoverPhase.None;
            active = false;
            moverBehavior = behavior;
            moverCycle = cycle;
            moverOrigin = origin;

            if (heights == null || phase == Phase.Done)
                return false;

            active = true;
            float cur = Current();
            switch (phase)
            {
                case Phase.MovingToTarget:
                    moverPhase = MoverPhase.Moving;
                    direction = target >= cur ? 1 : -1;
                    break;
                case Phase.Returning:
                    moverPhase = MoverPhase.Returning;
                    direction = origin >= cur ? 1 : -1;
                    break;
                case Phase.Stopped:
                    moverPhase = MoverPhase.Stopped;
                    direction = 0;
                    break;
                case Phase.Waiting:
                    moverPhase = MoverPhase.Waiting;
                    waitTics = Mathf.Max(0, Mathf.RoundToInt(waitTimer * 35f));
                    direction = origin >= target ? 1 : -1;
                    break;
                default:
                    active = false;
                    return false;
            }

            return true;
        }
    }
}
