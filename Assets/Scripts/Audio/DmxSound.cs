using System;
using System.IO;

namespace Doom.Audio
{
    /// DMX format-3 digital sound decoder (DOOM <c>DS*</c> lumps).
    /// Header: UInt16 format, UInt16 sample rate, UInt32 sample count, then
    /// unsigned 8-bit mono PCM. Standard 16-byte lead-in and lead-out padding
    /// is stripped when the payload is long enough (Chocolate Doom / DMX).
    public static class DmxSound
    {
        private const int HeaderSize = 8;
        private const int PadBytes = 16;
        private const int MinLengthToStripPad = PadBytes * 2 + 1;

        public static DecodedSound Decode(byte[] lump)
        {
            if (lump == null)
                throw new InvalidDataException("DMX lump is null");
            if (lump.Length < HeaderSize)
                throw new InvalidDataException(
                    $"DMX lump too short: {lump.Length} bytes, need at least {HeaderSize}");

            ushort format = BitConverter.ToUInt16(lump, 0);
            if (format != 3)
                throw new InvalidDataException(
                    $"DMX format must be 3 (digital), got {format}");

            ushort sampleRate = BitConverter.ToUInt16(lump, 2);
            if (sampleRate == 0)
                throw new InvalidDataException("DMX sample rate must be > 0");

            uint declaredCount = BitConverter.ToUInt32(lump, 4);
            int remaining = lump.Length - HeaderSize;
            if (declaredCount > (uint)remaining)
                throw new InvalidDataException(
                    $"DMX sample count {declaredCount} exceeds remaining {remaining} bytes");

            if (declaredCount > int.MaxValue)
                throw new InvalidDataException(
                    $"DMX sample count {declaredCount} does not fit in Int32");

            int count = (int)declaredCount;
            var samples = new byte[count];
            Buffer.BlockCopy(lump, HeaderSize, samples, 0, count);

            // DMX pads 16 lead + 16 trail samples for DMA chunking; they are
            // not meant to be played. Freedoom (and commercial DOOM) padding is
            // not always unsigned silence, so strip whenever length allows a
            // non-empty useful payload — matching Chocolate Doom i_sdlsound.c.
            if (samples.Length >= MinLengthToStripPad)
            {
                int useful = samples.Length - PadBytes * 2;
                var trimmed = new byte[useful];
                Buffer.BlockCopy(samples, PadBytes, trimmed, 0, useful);
                samples = trimmed;
            }

            return new DecodedSound(sampleRate, samples);
        }
    }
}
