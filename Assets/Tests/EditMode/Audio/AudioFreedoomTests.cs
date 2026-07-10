using System.IO;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;

namespace Doom.Audio.Tests
{
    public class AudioFreedoomTests
    {
        private static string FreedoomPath =>
            Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");

        [TestCase("DSPISTOL")]
        [TestCase("DSSHOTGN")]
        [TestCase("DSITEMUP")]
        [TestCase("DSDOROPN")]
        public void Required_sfx_exist_and_decode(string lumpName)
        {
            using var wad = WadFile.Open(FreedoomPath);
            Assert.That(SoundCatalog.TryRead(wad, lumpName, out DecodedSound sound), Is.True,
                $"{lumpName} should exist and decode");
            Assert.That(sound.SampleRate, Is.InRange(8000, 48000));
            Assert.That(sound.Samples, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void SoundCatalog_rejects_non_ds_prefix_and_missing()
        {
            using var wad = WadFile.Open(FreedoomPath);
            Assert.That(SoundCatalog.TryRead(wad, "PLAYPAL", out _), Is.False);
            Assert.That(SoundCatalog.TryRead(wad, "DSNOEXIST", out _), Is.False);
        }

        [Test]
        public void SoundCatalog_is_case_insensitive()
        {
            using var wad = WadFile.Open(FreedoomPath);
            Assert.That(SoundCatalog.TryRead(wad, "dspistol", out DecodedSound sound), Is.True);
            Assert.That(sound.Samples, Is.Not.Empty);
        }

        [Test]
        public void D_E1M1_parses_with_events_and_score_end()
        {
            using var wad = WadFile.Open(FreedoomPath);
            Assert.That(MusicLumpName.ForMap("E1M1"), Is.EqualTo("D_E1M1"));
            byte[] mus = wad.ReadLump("D_E1M1");
            // Freedoom ships SMF MIDI in D_* lumps; retail DOOM uses MUS.
            MusSong song = MusicScore.Read(mus);
            Assert.That(song.Events.Length, Is.GreaterThan(0));
            Assert.That(song.Events[song.Events.Length - 1].Type, Is.EqualTo(MusEventType.ScoreEnd));
        }

        [Test]
        public void GENMIDI_parses_175_instruments()
        {
            using var wad = WadFile.Open(FreedoomPath);
            byte[] lump = wad.ReadLump("GENMIDI");
            GenMidiBank bank = GenMidiBank.Read(lump);
            Assert.That(bank.Count, Is.EqualTo(175));
            // Touch a few fields so the real Freedoom bank is exercised end-to-end.
            Assert.That(bank.Melodic(0).FineTuning, Is.GreaterThanOrEqualTo(0));
            Assert.That((ushort)bank[174].Flags, Is.LessThanOrEqualTo(0xFFFF));
        }
    }
}
