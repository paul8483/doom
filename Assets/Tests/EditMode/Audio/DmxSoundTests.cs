using System;
using System.IO;
using NUnit.Framework;

namespace Doom.Audio.Tests
{
    public class DmxSoundTests
    {
        [Test]
        public void Decode_format3_reads_rate_and_unsigned_pcm()
        {
            // Short payload (< 33 samples): padding is not stripped.
            byte[] lump = BuildDmx(format: 3, rate: 11025, samples: new byte[] { 0, 128, 255 });
            DecodedSound s = DmxSound.Decode(lump);
            Assert.That(s.SampleRate, Is.EqualTo(11025));
            Assert.That(s.Samples, Is.EqualTo(new byte[] { 0, 128, 255 }));
        }

        [Test]
        public void Decode_strips_16_lead_and_trail_when_payload_long_enough()
        {
            // 16 pad + 3 useful + 16 pad. Padding need not be silence (Freedoom).
            var samples = new byte[35];
            for (int i = 0; i < 16; i++) samples[i] = (byte)(40 + i);
            samples[16] = 10;
            samples[17] = 20;
            samples[18] = 30;
            for (int i = 0; i < 16; i++) samples[19 + i] = (byte)(200 + i);

            DecodedSound s = DmxSound.Decode(BuildDmx(3, 22050, samples));
            Assert.That(s.SampleRate, Is.EqualTo(22050));
            Assert.That(s.Samples, Is.EqualTo(new byte[] { 10, 20, 30 }));
        }

        [Test]
        public void Decode_does_not_strip_when_payload_too_short_for_padding()
        {
            var samples = new byte[32]; // exactly pad*2 — no useful remainder
            for (int i = 0; i < samples.Length; i++) samples[i] = 128;
            DecodedSound s = DmxSound.Decode(BuildDmx(3, 11025, samples));
            Assert.That(s.Samples.Length, Is.EqualTo(32));
        }

        [TestCase(2)]
        [TestCase(4)]
        public void Decode_rejects_non_digital_format(int format)
        {
            byte[] lump = BuildDmx(format, 11025, new byte[] { 128 });
            Assert.Throws<InvalidDataException>(() => DmxSound.Decode(lump));
        }

        [Test]
        public void Decode_rejects_null_short_header_zero_rate_and_overdeclared()
        {
            Assert.Throws<InvalidDataException>(() => DmxSound.Decode(null));
            Assert.Throws<InvalidDataException>(() => DmxSound.Decode(new byte[7]));

            byte[] zeroRate = BuildDmx(3, 0, new byte[] { 128 });
            Assert.Throws<InvalidDataException>(() => DmxSound.Decode(zeroRate));

            // Declared count larger than remaining bytes after header.
            var over = new byte[12];
            WriteU16(over, 0, 3);
            WriteU16(over, 2, 11025);
            WriteU32(over, 4, 100); // claims 100 samples, only 4 bytes present
            Assert.Throws<InvalidDataException>(() => DmxSound.Decode(over));
        }

        internal static byte[] BuildDmx(int format, int rate, byte[] samples)
        {
            var lump = new byte[8 + samples.Length];
            WriteU16(lump, 0, format);
            WriteU16(lump, 2, rate);
            WriteU32(lump, 4, (uint)samples.Length);
            Buffer.BlockCopy(samples, 0, lump, 8, samples.Length);
            return lump;
        }

        private static void WriteU16(byte[] buf, int offset, int value)
        {
            buf[offset] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static void WriteU32(byte[] buf, int offset, uint value)
        {
            buf[offset] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
            buf[offset + 2] = (byte)((value >> 16) & 0xFF);
            buf[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}
