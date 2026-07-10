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
    }
}
