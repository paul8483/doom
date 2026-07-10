using System;

namespace Doom.Audio
{
    public enum MusEventType : byte
    {
        Release = 0,
        Play = 1,
        Pitch = 2,
        System = 3,
        Controller = 4,
        ScoreEnd = 6,
    }

    public readonly struct MusEvent
    {
        public MusEvent(
            MusEventType type, byte channel, byte data1, byte data2, int delayTicks,
            bool hasExplicitVolume = false)
        {
            Type = type;
            Channel = channel;
            Data1 = data1;
            Data2 = data2;
            DelayTicks = delayTicks;
            HasExplicitVolume = hasExplicitVolume;
        }

        public MusEventType Type { get; }
        public byte Channel { get; }
        /// Note / pitch / controller number / system controller.
        public byte Data1 { get; }
        /// Volume (play) or controller value; 0 when unused.
        public byte Data2 { get; }
        /// True when a play event carried a velocity byte, including velocity zero.
        public bool HasExplicitVolume { get; }
        /// Delay in MUS ticks after this event (0 if more events follow in the group).
        public int DelayTicks { get; }
    }

    public sealed class MusSong
    {
        public MusSong(
            ushort scoreLength,
            ushort scoreStart,
            ushort primaryChannels,
            ushort secondaryChannels,
            ushort[] instruments,
            MusEvent[] events)
        {
            ScoreLength = scoreLength;
            ScoreStart = scoreStart;
            PrimaryChannels = primaryChannels;
            SecondaryChannels = secondaryChannels;
            Instruments = instruments ?? Array.Empty<ushort>();
            Events = events ?? Array.Empty<MusEvent>();
        }

        public ushort ScoreLength { get; }
        public ushort ScoreStart { get; }
        public ushort PrimaryChannels { get; }
        public ushort SecondaryChannels { get; }
        public ushort[] Instruments { get; }
        public MusEvent[] Events { get; }
    }
}
