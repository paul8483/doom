using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;

namespace Doom.Audio.Tests
{
    public class MidiReaderTests
    {
        private static string FreedoomPath =>
            Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");

        [Test]
        public void MusicScore_detects_midi_and_mus()
        {
            byte[] mus = MusReaderTests.BuildMus(1, 0, System.Array.Empty<ushort>(), new byte[] { 0x60 });
            Assert.That(MusicScore.Read(mus).Events[^1].Type, Is.EqualTo(MusEventType.ScoreEnd));

            // Minimal Type-0 MIDI: MThd + empty-ish MTrk with end-of-track only.
            byte[] midi = BuildMinimalMidi();
            MusSong song = MusicScore.Read(midi);
            Assert.That(song.Events[^1].Type, Is.EqualTo(MusEventType.ScoreEnd));
        }

        [Test]
        public void Freedoom_D_E1M1_is_midi_and_parses()
        {
            using var wad = WadFile.Open(FreedoomPath);
            byte[] lump = wad.ReadLump("D_E1M1");
            Assert.That(lump[0], Is.EqualTo((byte)'M'));
            Assert.That(lump[1], Is.EqualTo((byte)'T'));
            MusSong song = MidiReader.Read(lump);
            Assert.That(song.Events.Length, Is.GreaterThan(10));
            Assert.That(song.Events[^1].Type, Is.EqualTo(MusEventType.ScoreEnd));
            Assert.That(System.Array.Exists(song.Events, e => e.Type == MusEventType.Play), Is.True);
        }

        static byte[] BuildMinimalMidi()
        {
            // MThd: len=6, format=0, tracks=1, division=96
            // MTrk payload = 12 bytes
            return new byte[]
            {
                (byte)'M',(byte)'T',(byte)'h',(byte)'d', 0,0,0,6, 0,0, 0,1, 0,96,
                (byte)'M',(byte)'T',(byte)'r',(byte)'k', 0,0,0,12,
                0x00, 0x90, 60, 100,   // note on
                0x60, 0x80, 60, 0,     // delta 96, note off
                0x00, 0xFF, 0x2F, 0x00 // end of track
            };
        }
    }
}
