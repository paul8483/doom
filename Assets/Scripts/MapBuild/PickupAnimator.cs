using UnityEngine;
using Doom.Things;

namespace Doom.MapBuild
{
    /// 35 Hz looping pickup state animation derived from the authoritative level
    /// tic. It is presentation-only, so save/load restores phase through GameTic
    /// without adding per-pickup snapshot fields.
    public sealed class PickupAnimator : MonoBehaviour
    {
        SpriteBillboard billboard;
        PickupAnimation animation;
        int index;
        int testTic;

        public int FrameForTest =>
            animation != null && animation.Frames.Length > 0
                ? animation.Frames[index] : -1;

        public void Init(SpriteBillboard billboard, PickupAnimation animation)
        {
            this.billboard = billboard;
            this.animation = animation;
            testTic = 0;
            ApplyTic(LevelStatsTracker.Instance != null
                ? LevelStatsTracker.Instance.Stats.Tics : 0);
        }

        void Update()
        {
            if (LevelStatsTracker.Instance != null)
                ApplyTic(LevelStatsTracker.Instance.Stats.Tics);
        }

        public void AdvanceTicsForTest(int tics)
        {
            if (tics <= 0) return;
            testTic += tics;
            ApplyTic(testTic);
        }

        void ApplyTic(int gameTic)
        {
            if (billboard == null || animation == null ||
                animation.Frames == null || animation.Tics == null ||
                animation.Frames.Length == 0 ||
                animation.Frames.Length != animation.Tics.Length)
                return;

            int cycle = 0;
            for (int i = 0; i < animation.Tics.Length; i++)
                cycle += System.Math.Max(1, animation.Tics[i]);
            int phase = cycle > 0 ? gameTic % cycle : 0;
            if (phase < 0) phase += cycle;

            int next = 0;
            while (next + 1 < animation.Frames.Length)
            {
                int duration = System.Math.Max(1, animation.Tics[next]);
                if (phase < duration) break;
                phase -= duration;
                next++;
            }
            if (index == next && billboard.CurrentFrame == animation.Frames[next]) return;
            index = next;
            billboard.SetFrame(animation.Frames[index]);
        }
    }
}
