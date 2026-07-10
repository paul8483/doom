using System;
using System.IO;
using NUnit.Framework;

namespace Doom.Audio.Tests
{
    public class MusReaderTests
    {
        [Test]
        public void Read_header_and_empty_score_end()
        {
            // Minimal score: single score-end event (type 6), no delay.
            byte[] lump = BuildMus(
                primary: 1, secondary: 0,
                instruments: new ushort[] { 0, 48 },
                score: new byte[] { 0x60 }); // channel 0, type 6, no last-bit needed

            MusSong song = MusReader.Read(lump);
            Assert.That(song.PrimaryChannels, Is.EqualTo(1));
            Assert.That(song.SecondaryChannels, Is.EqualTo(0));
            Assert.That(song.Instruments, Is.EqualTo(new ushort[] { 0, 48 }));
            Assert.That(song.Events.Length, Is.EqualTo(1));
            Assert.That(song.Events[0].Type, Is.EqualTo(MusEventType.ScoreEnd));
        }

        [Test]
        public void Read_play_release_pitch_controller_system_and_delays()
        {
            // Group: play note 60 vol 100 (no delay bit), then release 60 with delay 5.
            // Then pitch 32 with delay encoded as VLQ 0x81 0x00 (= 128).
            // Then controller program-change 0→12 (no delay), system 10 with delay 1.
            // Score end.
            var score = new System.Collections.Generic.List<byte>();
            score.Add(0x10);           // play ch0
            score.Add(0x80 | 60);      // note 60 + volume follows
            score.Add(100);            // volume
            score.Add(0x80);           // release ch0, last in group
            score.Add(60);             // note
            score.Add(5);              // delay 5
            score.Add(0xA0 | 0x80);    // pitch ch0, last
            score.Add(32);             // pitch value
            score.Add(0x81);           // VLQ high
            score.Add(0x00);           // VLQ low → 128
            score.Add(0x40);           // controller ch0
            score.Add(0);              // program change
            score.Add(12);             // patch 12
            score.Add(0x30 | 0x80);    // system ch0, last
            score.Add(10);             // all sounds off-ish
            score.Add(1);              // delay 1
            score.Add(0x60);           // score end

            MusSong song = MusReader.Read(BuildMus(1, 0, new ushort[] { 12 }, score.ToArray()));
            Assert.That(song.Events.Length, Is.EqualTo(6)); // 5 score events + ScoreEnd

            Assert.That(song.Events[0].Type, Is.EqualTo(MusEventType.Play));
            Assert.That(song.Events[0].Data1, Is.EqualTo(60));
            Assert.That(song.Events[0].Data2, Is.EqualTo(100));
            Assert.That(song.Events[0].DelayTicks, Is.EqualTo(0));
            Assert.That(song.Events[0].HasExplicitVolume, Is.True);

            Assert.That(song.Events[1].Type, Is.EqualTo(MusEventType.Release));
            Assert.That(song.Events[1].Data1, Is.EqualTo(60));
            Assert.That(song.Events[1].DelayTicks, Is.EqualTo(5));

            Assert.That(song.Events[2].Type, Is.EqualTo(MusEventType.Pitch));
            Assert.That(song.Events[2].Data1, Is.EqualTo(32));
            Assert.That(song.Events[2].DelayTicks, Is.EqualTo(128));

            Assert.That(song.Events[3].Type, Is.EqualTo(MusEventType.Controller));
            Assert.That(song.Events[3].Data1, Is.EqualTo(0));
            Assert.That(song.Events[3].Data2, Is.EqualTo(12));

            Assert.That(song.Events[4].Type, Is.EqualTo(MusEventType.System));
            Assert.That(song.Events[4].Data1, Is.EqualTo(10));
            Assert.That(song.Events[4].DelayTicks, Is.EqualTo(1));

            Assert.That(song.Events[5].Type, Is.EqualTo(MusEventType.ScoreEnd));
        }

        [Test]
        public void Read_rejects_bad_signature_short_and_out_of_range_score()
        {
            Assert.Throws<InvalidDataException>(() => MusReader.Read(null));
            Assert.Throws<InvalidDataException>(() => MusReader.Read(new byte[8]));

            byte[] badSig = BuildMus(1, 0, Array.Empty<ushort>(), new byte[] { 0x60 });
            badSig[0] = (byte)'X';
            Assert.Throws<InvalidDataException>(() => MusReader.Read(badSig));

            // scoreStart points past end
            byte[] over = BuildMus(1, 0, Array.Empty<ushort>(), new byte[] { 0x60 });
            over[6] = 0xFF;
            over[7] = 0xFF;
            Assert.Throws<InvalidDataException>(() => MusReader.Read(over));
        }

        [Test]
        public void Read_rejects_unknown_event_and_bad_system_controller()
        {
            byte[] unknown = BuildMus(1, 0, Array.Empty<ushort>(), new byte[] { 0x50 }); // type 5
            Assert.Throws<InvalidDataException>(() => MusReader.Read(unknown));

            byte[] badSys = BuildMus(1, 0, Array.Empty<ushort>(), new byte[] { 0x30, 3, 0x60 });
            Assert.Throws<InvalidDataException>(() => MusReader.Read(badSys));
        }

        [Test]
        public void MusicLumpName_ExMy_maps_and_MAPxx_rejected()
        {
            Assert.That(MusicLumpName.ForMap("E1M1"), Is.EqualTo("D_E1M1"));
            Assert.That(MusicLumpName.ForMap("e2m9"), Is.EqualTo("D_E2M9"));
            Assert.That(MusicLumpName.TryForMap("MAP01", out _), Is.False);
            Assert.Throws<ArgumentException>(() => MusicLumpName.ForMap("MAP01"));
            Assert.That(MusicLumpName.TryForMap("", out _), Is.False);
            Assert.That(MusicLumpName.TryForMap("E1MX", out _), Is.False);
        }

        /// Builds a MUS lump: header + instruments + score. scoreLength/scoreStart computed.
        internal static byte[] BuildMus(
            ushort primary, ushort secondary, ushort[] instruments, byte[] score)
        {
            int header = 16;
            int instBytes = instruments.Length * 2;
            int scoreStart = header + instBytes;
            var lump = new byte[scoreStart + score.Length];
            lump[0] = (byte)'M';
            lump[1] = (byte)'U';
            lump[2] = (byte)'S';
            lump[3] = 0x1A;
            WriteU16(lump, 4, (ushort)score.Length);
            WriteU16(lump, 6, (ushort)scoreStart);
            WriteU16(lump, 8, primary);
            WriteU16(lump, 10, secondary);
            WriteU16(lump, 12, (ushort)instruments.Length);
            WriteU16(lump, 14, 0);
            for (int i = 0; i < instruments.Length; i++)
                WriteU16(lump, header + i * 2, instruments[i]);
            Buffer.BlockCopy(score, 0, lump, scoreStart, score.Length);
            return lump;
        }

        private static void WriteU16(byte[] buf, int offset, ushort value)
        {
            buf[offset] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)(value >> 8);
        }
    }
}
