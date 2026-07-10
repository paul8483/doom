using System;
using System.IO;
using System.Text;

namespace Doom.Audio
{
    [Flags]
    public enum GenMidiFlags : ushort
    {
        None = 0,
        FixedPitch = 0x0001,
        DoubleVoice = 0x0004,
    }

    /// One OPL operator pair (16 bytes) inside a GENMIDI instrument.
    public readonly struct GenMidiVoice
    {
        public GenMidiVoice(
            byte modTremolo, byte modAttack, byte modSustain, byte modWaveform,
            byte modScale, byte modLevel, byte feedback,
            byte carTremolo, byte carAttack, byte carSustain, byte carWaveform,
            byte carScale, byte carLevel, short baseNoteOffset)
        {
            ModTremolo = modTremolo;
            ModAttack = modAttack;
            ModSustain = modSustain;
            ModWaveform = modWaveform;
            ModScale = modScale;
            ModLevel = modLevel;
            Feedback = feedback;
            CarTremolo = carTremolo;
            CarAttack = carAttack;
            CarSustain = carSustain;
            CarWaveform = carWaveform;
            CarScale = carScale;
            CarLevel = carLevel;
            BaseNoteOffset = baseNoteOffset;
        }

        public byte ModTremolo { get; }
        public byte ModAttack { get; }
        public byte ModSustain { get; }
        public byte ModWaveform { get; }
        public byte ModScale { get; }
        public byte ModLevel { get; }
        public byte Feedback { get; }
        public byte CarTremolo { get; }
        public byte CarAttack { get; }
        public byte CarSustain { get; }
        public byte CarWaveform { get; }
        public byte CarScale { get; }
        public byte CarLevel { get; }
        public short BaseNoteOffset { get; }
    }

    /// One of 175 GENMIDI instruments (36 bytes). Layout matches Chocolate Doom
    /// <c>genmidi_instr_t</c> / LittleMUS <c>MUS_instrument</c>.
    public readonly struct GenMidiInstrument
    {
        public GenMidiInstrument(
            GenMidiFlags flags, byte fineTuning, byte fixedNote,
            GenMidiVoice voice0, GenMidiVoice voice1)
        {
            Flags = flags;
            FineTuning = fineTuning;
            FixedNote = fixedNote;
            Voice0 = voice0;
            Voice1 = voice1;
        }

        public GenMidiFlags Flags { get; }
        public byte FineTuning { get; }
        public byte FixedNote { get; }
        public GenMidiVoice Voice0 { get; }
        public GenMidiVoice Voice1 { get; }

        public bool IsFixedPitch => (Flags & GenMidiFlags.FixedPitch) != 0;
        public bool IsDoubleVoice => (Flags & GenMidiFlags.DoubleVoice) != 0;
    }

    /// Parses the WAD <c>GENMIDI</c> lump (#OPL_II# + 175 × 36-byte records).
    public sealed class GenMidiBank
    {
        public const string HeaderSignature = "#OPL_II#";
        public const int InstrumentCount = 175;
        public const int MelodicCount = 128;
        public const int PercussionCount = 47;
        public const int InstrumentBytes = 36;
        public const int HeaderBytes = 8;
        public const int MinimumLumpBytes = HeaderBytes + InstrumentCount * InstrumentBytes;

        private readonly GenMidiInstrument[] _instruments;

        private GenMidiBank(GenMidiInstrument[] instruments)
        {
            _instruments = instruments;
        }

        public int Count => _instruments.Length;

        public GenMidiInstrument this[int index] => _instruments[index];

        public GenMidiInstrument Melodic(int program)
        {
            if (program < 0 || program >= MelodicCount)
                throw new ArgumentOutOfRangeException(nameof(program));
            return _instruments[program];
        }

        public GenMidiInstrument Percussion(int midiNote)
        {
            // DOOM maps percussion MIDI notes 35..81 → instruments 128..174
            int index = MelodicCount + (midiNote - 35);
            if (index < MelodicCount || index >= InstrumentCount)
                throw new ArgumentOutOfRangeException(nameof(midiNote));
            return _instruments[index];
        }

        public static GenMidiBank Read(byte[] lump)
        {
            if (lump == null)
                throw new InvalidDataException("GENMIDI lump is null");
            if (lump.Length < MinimumLumpBytes)
                throw new InvalidDataException(
                    $"GENMIDI lump too short: {lump.Length} bytes, need at least {MinimumLumpBytes}");

            string sig = Encoding.ASCII.GetString(lump, 0, HeaderBytes);
            if (sig != HeaderSignature)
                throw new InvalidDataException(
                    $"GENMIDI signature must be {HeaderSignature}, got '{sig}'");

            var instruments = new GenMidiInstrument[InstrumentCount];
            for (int i = 0; i < InstrumentCount; i++)
            {
                int offset = HeaderBytes + i * InstrumentBytes;
                instruments[i] = ReadInstrument(lump, offset);
            }

            return new GenMidiBank(instruments);
        }

        private static GenMidiInstrument ReadInstrument(byte[] lump, int offset)
        {
            var flags = (GenMidiFlags)ReadU16(lump, offset);
            byte fineTuning = lump[offset + 2];
            byte fixedNote = lump[offset + 3];
            GenMidiVoice v0 = ReadVoice(lump, offset + 4);
            GenMidiVoice v1 = ReadVoice(lump, offset + 20);
            return new GenMidiInstrument(flags, fineTuning, fixedNote, v0, v1);
        }

        private static GenMidiVoice ReadVoice(byte[] lump, int offset)
        {
            return new GenMidiVoice(
                lump[offset + 0],
                lump[offset + 1],
                lump[offset + 2],
                lump[offset + 3],
                lump[offset + 4],
                lump[offset + 5],
                lump[offset + 6],
                lump[offset + 7],
                lump[offset + 8],
                lump[offset + 9],
                lump[offset + 10],
                lump[offset + 11],
                lump[offset + 12],
                // offset+13 unused
                (short)ReadU16(lump, offset + 14));
        }

        private static ushort ReadU16(byte[] buf, int offset) =>
            (ushort)(buf[offset] | (buf[offset + 1] << 8));
    }
}
