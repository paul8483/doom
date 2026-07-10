using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Doom.Audio
{
    /// Parses Standard MIDI Files (SMF Type 0/1) into <see cref="MusSong"/> events
    /// so the same OPL sequencer can play Freedoom <c>D_*</c> lumps (which are MIDI,
    /// not classic DMX MUS). Timing is converted to the 140 Hz MUS tick clock.
    public static class MidiReader
    {
        public const int MusTickRate = 140;
        private const int DefaultTempoMicroseconds = 500_000; // 120 BPM

        public static MusSong Read(byte[] lump)
        {
            if (lump == null)
                throw new InvalidDataException("MIDI lump is null");
            if (lump.Length < 14)
                throw new InvalidDataException("MIDI lump too short for MThd");
            if (lump[0] != 'M' || lump[1] != 'T' || lump[2] != 'h' || lump[3] != 'd')
                throw new InvalidDataException(
                    $"MIDI signature must be MThd, got {Describe(lump, 4)}");

            int headerLen = ReadU32Be(lump, 4);
            if (headerLen < 6)
                throw new InvalidDataException($"MIDI header length {headerLen} too small");
            int format = ReadU16Be(lump, 8);
            int trackCount = ReadU16Be(lump, 10);
            int division = ReadU16Be(lump, 12);
            if ((division & 0x8000) != 0)
                throw new InvalidDataException("MIDI SMPTE division is not supported");
            if (division <= 0)
                throw new InvalidDataException("MIDI division must be > 0");
            if (format > 1)
                throw new InvalidDataException($"MIDI format {format} is not supported");

            int pos = 8 + headerLen;
            var absolute = new List<AbsEvent>(1024);
            int tempo = DefaultTempoMicroseconds;

            for (int t = 0; t < trackCount; t++)
            {
                if (pos + 8 > lump.Length)
                    throw new InvalidDataException("MIDI truncated before track header");
                if (lump[pos] != 'M' || lump[pos + 1] != 'T' ||
                    lump[pos + 2] != 'r' || lump[pos + 3] != 'k')
                    throw new InvalidDataException(
                        $"MIDI expected MTrk at {pos}, got {Describe(lump, pos, 4)}");
                int trackLen = ReadU32Be(lump, pos + 4);
                int trackStart = pos + 8;
                int trackEnd = trackStart + trackLen;
                if (trackEnd > lump.Length)
                    throw new InvalidDataException("MIDI track exceeds lump length");
                ParseTrack(lump, trackStart, trackEnd, absolute, ref tempo);
                pos = trackEnd;
            }

            absolute.Sort((a, b) =>
            {
                int c = a.Tick.CompareTo(b.Tick);
                return c != 0 ? c : a.Order.CompareTo(b.Order);
            });

            var events = ToMusEvents(absolute, division, tempo);
            return new MusSong(0, 0, 0, 0, Array.Empty<ushort>(), events);
        }

        private static void ParseTrack(
            byte[] lump, int start, int end, List<AbsEvent> into, ref int tempo)
        {
            int pos = start;
            long tick = 0;
            int runningStatus = -1;
            int orderBase = into.Count;

            while (pos < end)
            {
                tick += ReadVlq(lump, ref pos, end);
                if (pos >= end) break;

                int statusByte = lump[pos];
                if (statusByte < 0x80)
                {
                    if (runningStatus < 0)
                        throw new InvalidDataException("MIDI running status missing");
                    statusByte = runningStatus;
                }
                else
                {
                    pos++;
                    if (statusByte < 0xF0)
                        runningStatus = statusByte;
                }

                int type = statusByte & 0xF0;
                int channel = statusByte & 0x0F;

                if (statusByte == 0xFF)
                {
                    if (pos >= end) throw new InvalidDataException("MIDI meta truncated");
                    byte meta = lump[pos++];
                    int len = ReadVlq(lump, ref pos, end);
                    if (pos + len > end) throw new InvalidDataException("MIDI meta length OOB");
                    if (meta == 0x2F) // end of track
                    {
                        pos = end;
                        break;
                    }
                    if (meta == 0x51 && len == 3)
                    {
                        tempo = (lump[pos] << 16) | (lump[pos + 1] << 8) | lump[pos + 2];
                        if (tempo <= 0) tempo = DefaultTempoMicroseconds;
                    }
                    pos += len;
                    continue;
                }

                if (statusByte == 0xF0 || statusByte == 0xF7)
                {
                    int len = ReadVlq(lump, ref pos, end);
                    pos += len;
                    continue;
                }

                byte data1, data2 = 0;
                switch (type)
                {
                    case 0x80: // note off
                    case 0x90: // note on
                    case 0xA0: // poly aftertouch
                    case 0xB0: // CC
                    case 0xE0: // pitch bend
                        data1 = ReadByte(lump, ref pos, end);
                        data2 = ReadByte(lump, ref pos, end);
                        break;
                    case 0xC0: // program
                    case 0xD0: // channel aftertouch
                        data1 = ReadByte(lump, ref pos, end);
                        break;
                    default:
                        // Unknown / system — skip conservatively
                        continue;
                }

                into.Add(new AbsEvent(tick, orderBase++, (byte)type, (byte)channel, data1, data2));
            }
        }

        private static MusEvent[] ToMusEvents(List<AbsEvent> absolute, int division, int tempoUs)
        {
            var list = new List<MusEvent>(absolute.Count + 1);
            long prevMusTick = 0;
            // Accumulate fractional MUS ticks so we don't lose time to truncation.
            double musTickAccum = 0;

            for (int i = 0; i < absolute.Count; i++)
            {
                AbsEvent e = absolute[i];
                double musTickExact = MidiTicksToMusTicks(e.Tick, division, tempoUs);
                long musTick = (long)Math.Round(musTickExact);
                if (musTick < prevMusTick) musTick = prevMusTick;

                if (!TryMap(e, out MusEventType type, out byte musCh, out byte d1, out byte d2,
                        out bool hasVol))
                    continue;

                // Attach delay to the previous emitted event.
                if (list.Count > 0 && musTick > prevMusTick)
                {
                    int delay = (int)Math.Min(int.MaxValue, musTick - prevMusTick);
                    MusEvent prev = list[list.Count - 1];
                    list[list.Count - 1] = new MusEvent(
                        prev.Type, prev.Channel, prev.Data1, prev.Data2, delay, prev.HasExplicitVolume);
                    prevMusTick = musTick;
                }

                list.Add(new MusEvent(type, musCh, d1, d2, 0, hasVol));
                musTickAccum = musTick;
            }

            list.Add(new MusEvent(MusEventType.ScoreEnd, 0, 0, 0, 0));
            return list.ToArray();
        }

        private static bool TryMap(
            AbsEvent e, out MusEventType type, out byte musCh, out byte d1, out byte d2,
            out bool hasVol)
        {
            type = default; d1 = 0; d2 = 0; hasVol = false;
            // MIDI ch 9 (0-based) → MUS percussion 15; others map 0..8,10..15 → 0..14
            musCh = e.Channel == 9 ? (byte)15 : (byte)(e.Channel < 9 ? e.Channel : e.Channel - 1);

            switch (e.Status)
            {
                case 0x80:
                    type = MusEventType.Release;
                    d1 = (byte)(e.Data1 & 0x7F);
                    return true;
                case 0x90:
                    if (e.Data2 == 0)
                    {
                        type = MusEventType.Release;
                        d1 = (byte)(e.Data1 & 0x7F);
                        return true;
                    }
                    type = MusEventType.Play;
                    d1 = (byte)(e.Data1 & 0x7F);
                    d2 = (byte)(e.Data2 & 0x7F);
                    hasVol = true;
                    return true;
                case 0xC0:
                    type = MusEventType.Controller;
                    d1 = 0; // program change
                    d2 = (byte)(e.Data1 & 0x7F);
                    return true;
                case 0xB0:
                    return MapController(e.Data1, e.Data2, out type, out d1, out d2);
                case 0xE0:
                {
                    type = MusEventType.Pitch;
                    int bend14 = e.Data1 | (e.Data2 << 7); // 0..16383, center 8192
                    int mus = bend14 >> 6; // 0..255 approx
                    if (mus > 255) mus = 255;
                    d1 = (byte)mus;
                    return true;
                }
                default:
                    return false;
            }
        }

        private static bool MapController(
            byte cc, byte value, out MusEventType type, out byte d1, out byte d2)
        {
            type = MusEventType.Controller;
            d1 = 0;
            d2 = (byte)(value & 0x7F);
            switch (cc)
            {
                case 1: d1 = 2; return true;  // modulation
                case 7: d1 = 3; return true;  // volume
                case 10: d1 = 4; return true; // pan
                case 11: d1 = 5; return true; // expression
                case 64: // sustain — treat as system-ish no-op for OPL
                    return false;
                case 121: // reset all controllers
                    type = MusEventType.System; d1 = 14; d2 = 0; return true;
                case 120: // all sound off
                case 123: // all notes off
                    type = MusEventType.System; d1 = 11; d2 = 0; return true;
                default:
                    return false;
            }
        }

        private static double MidiTicksToMusTicks(long midiTick, int division, int tempoUs) =>
            midiTick * (double)MusTickRate * tempoUs / (division * 1_000_000.0);

        private static int ReadVlq(byte[] lump, ref int pos, int end)
        {
            int value = 0;
            for (int i = 0; i < 4; i++)
            {
                if (pos >= end) throw new InvalidDataException("MIDI VLQ truncated");
                byte b = lump[pos++];
                value = (value << 7) | (b & 0x7F);
                if ((b & 0x80) == 0) return value;
            }
            throw new InvalidDataException("MIDI VLQ unterminated");
        }

        private static byte ReadByte(byte[] lump, ref int pos, int end)
        {
            if (pos >= end) throw new InvalidDataException("MIDI truncated");
            return lump[pos++];
        }

        private static int ReadU16Be(byte[] buf, int offset) =>
            (buf[offset] << 8) | buf[offset + 1];

        private static int ReadU32Be(byte[] buf, int offset) =>
            (buf[offset] << 24) | (buf[offset + 1] << 16) | (buf[offset + 2] << 8) | buf[offset + 3];

        private static string Describe(byte[] lump, int count) => Describe(lump, 0, count);

        private static string Describe(byte[] lump, int offset, int count)
        {
            var sb = new StringBuilder(count * 2);
            for (int i = 0; i < count && offset + i < lump.Length; i++)
                sb.AppendFormat("{0:X2}", lump[offset + i]);
            return sb.ToString();
        }

        private readonly struct AbsEvent
        {
            public AbsEvent(long tick, int order, byte status, byte channel, byte data1, byte data2)
            {
                Tick = tick; Order = order; Status = status; Channel = channel;
                Data1 = data1; Data2 = data2;
            }
            public long Tick { get; }
            public int Order { get; }
            public byte Status { get; }
            public byte Channel { get; }
            public byte Data1 { get; }
            public byte Data2 { get; }
        }
    }

    /// Detects MUS vs MIDI and returns a <see cref="MusSong"/> for the OPL player.
    public static class MusicScore
    {
        public static MusSong Read(byte[] lump)
        {
            if (lump == null || lump.Length < 4)
                throw new InvalidDataException("Music lump is null or too short");

            if (lump[0] == 'M' && lump[1] == 'U' && lump[2] == 'S' && lump[3] == 0x1A)
                return MusReader.Read(lump);

            if (lump[0] == 'M' && lump[1] == 'T' && lump[2] == 'h' && lump[3] == 'd')
                return MidiReader.Read(lump);

            throw new InvalidDataException(
                $"Unrecognized music format (expected MUS or MIDI), got " +
                $"{lump[0]:X2}{lump[1]:X2}{lump[2]:X2}{lump[3]:X2}");
        }
    }
}
