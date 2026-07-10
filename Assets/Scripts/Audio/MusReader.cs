using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Doom.Audio
{
    /// Parses DMX MUS lumps into a flat event list with per-event delays.
    /// Layout matches Chocolate Doom <c>mus2mid.c</c> / ModdingWiki MUS format.
    public static class MusReader
    {
        private static readonly byte[] Signature = { (byte)'M', (byte)'U', (byte)'S', 0x1A };
        private const int HeaderSize = 16; // id[4] + 6×UInt16

        public static MusSong Read(byte[] lump)
        {
            if (lump == null)
                throw new InvalidDataException("MUS lump is null");
            if (lump.Length < HeaderSize)
                throw new InvalidDataException(
                    $"MUS lump too short: {lump.Length} bytes, need at least {HeaderSize}");

            for (int i = 0; i < 4; i++)
            {
                if (lump[i] != Signature[i])
                    throw new InvalidDataException(
                        $"MUS signature must be MUS\\x1A, got {DescribeSig(lump)}");
            }

            ushort scoreLength = ReadU16(lump, 4);
            ushort scoreStart = ReadU16(lump, 6);
            ushort primaryChannels = ReadU16(lump, 8);
            ushort secondaryChannels = ReadU16(lump, 10);
            ushort instrumentCount = ReadU16(lump, 12);
            // bytes 14–15: unused / instruments reserved

            int instrumentsEnd = HeaderSize + instrumentCount * 2;
            if (instrumentsEnd > lump.Length)
                throw new InvalidDataException(
                    $"MUS instrument list ({instrumentCount} × 2) exceeds lump length {lump.Length}");

            if (scoreStart < instrumentsEnd || scoreStart >= lump.Length)
                throw new InvalidDataException(
                    $"MUS scoreStart {scoreStart} out of range (instruments end {instrumentsEnd}, lump {lump.Length})");

            long scoreEndExclusive = (long)scoreStart + scoreLength;
            if (scoreEndExclusive > lump.Length)
                throw new InvalidDataException(
                    $"MUS score range [{scoreStart}..{scoreEndExclusive}) exceeds lump length {lump.Length}");

            var instruments = new ushort[instrumentCount];
            for (int i = 0; i < instrumentCount; i++)
                instruments[i] = ReadU16(lump, HeaderSize + i * 2);

            var events = ParseScore(lump, scoreStart, scoreLength);
            return new MusSong(
                scoreLength, scoreStart, primaryChannels, secondaryChannels,
                instruments, events);
        }

        private static MusEvent[] ParseScore(byte[] lump, int scoreStart, int scoreLength)
        {
            int end = scoreStart + scoreLength;
            int pos = scoreStart;
            var list = new List<MusEvent>(256);
            bool hitEnd = false;

            while (!hitEnd)
            {
                if (pos >= end)
                    throw new InvalidDataException("MUS score ended without score-end event");

                // Drain a group of events until the delay bit (bit 7) is set,
                // or score-end is hit.
                while (!hitEnd)
                {
                    if (pos >= end)
                        throw new InvalidDataException("MUS score truncated mid-event group");

                    byte descriptor = lump[pos++];
                    byte channel = (byte)(descriptor & 0x0F);
                    int typeNibble = (descriptor & 0x70) >> 4;
                    bool lastInGroup = (descriptor & 0x80) != 0;

                    MusEventType type;
                    byte data1 = 0;
                    byte data2 = 0;
                    bool hasExplicitVolume = false;

                    switch (typeNibble)
                    {
                        case 0: // release
                            type = MusEventType.Release;
                            data1 = ReadByte(lump, ref pos, end, "release note");
                            break;

                        case 1: // play / note on
                            type = MusEventType.Play;
                            data1 = ReadByte(lump, ref pos, end, "play note");
                            if ((data1 & 0x80) != 0)
                            {
                                data1 = (byte)(data1 & 0x7F);
                                data2 = (byte)(ReadByte(lump, ref pos, end, "play volume") & 0x7F);
                                hasExplicitVolume = true;
                            }
                            break;

                        case 2: // pitch wheel
                            type = MusEventType.Pitch;
                            data1 = ReadByte(lump, ref pos, end, "pitch");
                            break;

                        case 3: // system event (valueless controller)
                            type = MusEventType.System;
                            data1 = ReadByte(lump, ref pos, end, "system controller");
                            if (data1 < 10 || data1 > 14)
                                throw new InvalidDataException(
                                    $"MUS system controller {data1} out of range 10..14");
                            break;

                        case 4: // controller / program change
                            type = MusEventType.Controller;
                            data1 = ReadByte(lump, ref pos, end, "controller number");
                            data2 = ReadByte(lump, ref pos, end, "controller value");
                            if (data1 != 0 && (data1 < 1 || data1 > 9))
                                throw new InvalidDataException(
                                    $"MUS controller number {data1} out of range 0..9");
                            break;

                        case 6: // score end
                            type = MusEventType.ScoreEnd;
                            hitEnd = true;
                            break;

                        default:
                            throw new InvalidDataException(
                                $"MUS unknown event type nibble {typeNibble}");
                    }

                    int delay = 0;
                    if (hitEnd)
                    {
                        list.Add(new MusEvent(type, channel, data1, data2, 0, hasExplicitVolume));
                        break;
                    }

                    if (lastInGroup)
                    {
                        delay = ReadVariableLength(lump, ref pos, end);
                        list.Add(new MusEvent(type, channel, data1, data2, delay, hasExplicitVolume));
                        break;
                    }

                    list.Add(new MusEvent(type, channel, data1, data2, 0, hasExplicitVolume));
                }
            }

            return list.ToArray();
        }

        private static byte ReadByte(byte[] lump, ref int pos, int end, string what)
        {
            if (pos >= end)
                throw new InvalidDataException($"MUS truncated reading {what}");
            return lump[pos++];
        }

        private static int ReadVariableLength(byte[] lump, ref int pos, int end)
        {
            int value = 0;
            for (int guard = 0; guard < 5; guard++)
            {
                if (pos >= end)
                    throw new InvalidDataException("MUS truncated reading variable-length delay");
                byte b = lump[pos++];
                value = (value << 7) | (b & 0x7F);
                if ((b & 0x80) == 0)
                    return value;
            }
            throw new InvalidDataException("MUS variable-length delay unterminated");
        }

        private static ushort ReadU16(byte[] buf, int offset) =>
            (ushort)(buf[offset] | (buf[offset + 1] << 8));

        private static string DescribeSig(byte[] lump)
        {
            var sb = new StringBuilder(16);
            for (int i = 0; i < 4 && i < lump.Length; i++)
                sb.AppendFormat("{0:X2}", lump[i]);
            return sb.ToString();
        }
    }
}
