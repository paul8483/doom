using System;
using System.Collections.Generic;

namespace Doom.Game
{
    public enum SoundPriority
    {
        Ambient = 0,
        World = 1,
        Monster = 2,
        Player = 3,
        Critical = 4,
    }

    public enum SoundPitchVariation
    {
        None,
        DoomWide,
    }

    public enum SoundCueContext
    {
        World,
        Monster,
    }

    public readonly struct SoundCueMetadata
    {
        public SoundCueMetadata(SoundPriority priority, SoundPitchVariation pitchVariation)
        {
            Priority = priority;
            PitchVariation = pitchVariation;
        }

        public SoundPriority Priority { get; }
        public SoundPitchVariation PitchVariation { get; }
    }

    public readonly struct SoundChannelState
    {
        public SoundChannelState(bool active, bool loop, SoundPriority priority, long startedAt)
        {
            Active = active;
            Loop = loop;
            Priority = priority;
            StartedAt = startedAt;
        }

        public bool Active { get; }
        public bool Loop { get; }
        public SoundPriority Priority { get; }
        public long StartedAt { get; }
    }

    /// Pure channel-selection and cue-variation rules used by the Unity SFX pool.
    public static class SoundPlaybackPolicy
    {
        private static readonly HashSet<string> CriticalWorldCues = new(StringComparer.Ordinal)
        {
            "DSTELEPT",
        };

        private static readonly HashSet<string> StableWorldCues = new(StringComparer.Ordinal)
        {
            // Continuous machinery must not change pitch when a channel is resumed.
            "DSSTNMOV",
        };

        private static readonly HashSet<string> VariableWorldCues = new(StringComparer.Ordinal)
        {
            "DSDOROPN", "DSDORCLS", "DSPSTOP", "DSSWTCHN",
            "DSFIRSHT", "DSFIRXPL", "DSRXPLOD", "DSBAREXP",
            "DSPLASMA", "DSBFG", "DSBFGX",
        };

        private static readonly string[] MonsterPrefixes =
        {
            "DSPOS", "DSSGT", "DSBG", "DSDMACT", "DSCLAW", "DSFIRSHT",
        };

        public static SoundCueMetadata Describe(string lumpName, bool local, bool loop = false,
                                                SoundCueContext context = SoundCueContext.World)
        {
            string name = Normalize(lumpName);
            if (local)
                return new SoundCueMetadata(
                    IsCriticalLocal(name) ? SoundPriority.Critical : SoundPriority.Player,
                    SoundPitchVariation.None);

            if (CriticalWorldCues.Contains(name))
                return new SoundCueMetadata(SoundPriority.Critical, SoundPitchVariation.DoomWide);

            bool monster = context == SoundCueContext.Monster || IsMonsterCue(name);
            // Loops (door / lift motors) acquire a channel at World priority:
            // at Ambient they lose the fight against every one-shot when all
            // channels are busy and the mover runs silent.
            SoundPriority priority = monster ? SoundPriority.Monster
                : SoundPriority.World;
            bool variable = !loop && !StableWorldCues.Contains(name)
                && (monster || VariableWorldCues.Contains(name));
            return new SoundCueMetadata(
                priority,
                variable ? SoundPitchVariation.DoomWide : SoundPitchVariation.None);
        }

        /// Returns an idle channel first. Otherwise steals the lowest-priority,
        /// oldest eligible one-shot. Tracked loops and higher-priority cues are protected.
        public static int SelectChannel(IReadOnlyList<SoundChannelState> channels,
                                        SoundPriority incomingPriority)
        {
            if (channels == null) throw new ArgumentNullException(nameof(channels));

            for (int i = 0; i < channels.Count; i++)
                if (!channels[i].Active && !channels[i].Loop)
                    return i;

            int best = -1;
            for (int i = 0; i < channels.Count; i++)
            {
                SoundChannelState candidate = channels[i];
                if (candidate.Loop || candidate.Priority > incomingPriority) continue;
                if (best < 0 ||
                    candidate.Priority < channels[best].Priority ||
                    (candidate.Priority == channels[best].Priority &&
                     candidate.StartedAt < channels[best].StartedAt))
                {
                    best = i;
                }
            }
            return best;
        }

        /// Vanilla-style 128-based pitch variation, expressed as Unity playback rate.
        public static float ResolvePitch(SoundCueMetadata metadata, DoomRandom random)
        {
            if (metadata.PitchVariation == SoundPitchVariation.None) return 1f;
            if (random == null) throw new ArgumentNullException(nameof(random));
            int pitch = 128 + 16 - (random.Next() & 31);
            return pitch / 128f;
        }

        private static bool IsCriticalLocal(string name) =>
            name == "DSPLDETH" || name == "DSPDIEHI";

        private static bool IsMonsterCue(string name)
        {
            for (int i = 0; i < MonsterPrefixes.Length; i++)
                if (name.StartsWith(MonsterPrefixes[i], StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static string Normalize(string lumpName) =>
            string.IsNullOrEmpty(lumpName) ? string.Empty : lumpName.ToUpperInvariant();
    }
}
