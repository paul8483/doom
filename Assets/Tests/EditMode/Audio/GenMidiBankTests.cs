using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace Doom.Audio.Tests
{
    public class GenMidiBankTests
    {
        [Test]
        public void Read_valid_bank_parses_175_instruments()
        {
            byte[] lump = BuildBank(out GenMidiInstrument expected0);
            GenMidiBank bank = GenMidiBank.Read(lump);
            Assert.That(bank.Count, Is.EqualTo(175));
            Assert.That(bank[0].Flags, Is.EqualTo(expected0.Flags));
            Assert.That(bank[0].FineTuning, Is.EqualTo(expected0.FineTuning));
            Assert.That(bank[0].FixedNote, Is.EqualTo(expected0.FixedNote));
            Assert.That(bank[0].Voice0.ModTremolo, Is.EqualTo(0x21));
            Assert.That(bank[0].Voice0.BaseNoteOffset, Is.EqualTo((short)-12));
            Assert.That(bank[0].IsDoubleVoice, Is.True);
            Assert.That(bank[174].FineTuning, Is.EqualTo(128));
        }

        [Test]
        public void Read_rejects_bad_signature_and_short_lump()
        {
            Assert.Throws<InvalidDataException>(() => GenMidiBank.Read(null));
            Assert.Throws<InvalidDataException>(() => GenMidiBank.Read(new byte[10]));

            byte[] bad = BuildBank(out _);
            bad[0] = (byte)'X';
            Assert.Throws<InvalidDataException>(() => GenMidiBank.Read(bad));

            byte[] shortLump = new byte[GenMidiBank.MinimumLumpBytes - 1];
            Encoding.ASCII.GetBytes(GenMidiBank.HeaderSignature).CopyTo(shortLump, 0);
            Assert.Throws<InvalidDataException>(() => GenMidiBank.Read(shortLump));
        }

        [Test]
        public void Melodic_and_percussion_indexers()
        {
            GenMidiBank bank = GenMidiBank.Read(BuildBank(out _));
            Assert.That(bank.Melodic(0).FineTuning, Is.EqualTo(100));
            // MIDI note 35 → instrument 128
            Assert.That(bank.Percussion(35).FineTuning, Is.EqualTo(128));
            Assert.Throws<ArgumentOutOfRangeException>(() => bank.Melodic(128));
            Assert.Throws<ArgumentOutOfRangeException>(() => bank.Percussion(34));
        }

        internal static byte[] BuildBank(out GenMidiInstrument first)
        {
            var lump = new byte[GenMidiBank.MinimumLumpBytes];
            Encoding.ASCII.GetBytes(GenMidiBank.HeaderSignature).CopyTo(lump, 0);

            // Instrument 0: double-voice, fineTune 100, fixedNote 60, voice0 with -12 offset
            int o = GenMidiBank.HeaderBytes;
            WriteU16(lump, o, (ushort)GenMidiFlags.DoubleVoice);
            lump[o + 2] = 100;
            lump[o + 3] = 60;
            lump[o + 4] = 0x21; // mod tremolo
            // leave rest of voice0 zero except base note offset at +18
            WriteU16(lump, o + 4 + 14, unchecked((ushort)(short)-12));

            // Remaining instruments: fineTune 128
            for (int i = 1; i < GenMidiBank.InstrumentCount; i++)
            {
                int off = GenMidiBank.HeaderBytes + i * GenMidiBank.InstrumentBytes;
                lump[off + 2] = 128;
            }

            first = new GenMidiInstrument(
                GenMidiFlags.DoubleVoice, 100, 60,
                new GenMidiVoice(0x21, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -12),
                default);
            return lump;
        }

        private static void WriteU16(byte[] buf, int offset, ushort value)
        {
            buf[offset] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)(value >> 8);
        }
    }
}
