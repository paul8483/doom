using System;
using NukedOpl;

namespace Doom.Audio
{
    /// <summary>IOplChip adapter for the vendored Nuked OPL3 emulator.</summary>
    public sealed class NukedOplChip : IOplChip
    {
        private readonly Opl3 _opl = new Opl3();
        private readonly Opl3Chip _chip = new Opl3Chip();
        private short[] _scratch = new short[2048];

        public void Reset(int sampleRate)
        {
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            _opl.Reset(_chip, sampleRate);
            // Opl3Chip.Reset resets queued entries but does not dequeue them.
            _chip.writebuf.Clear();
        }

        public void WriteRegister(int register, byte value) =>
            _opl.WriteRegBuffered(_chip, register, value);

        public void Render(float[] stereoInterleaved, int frameOffset, int frameCount)
        {
            if (stereoInterleaved == null)
                throw new ArgumentNullException(nameof(stereoInterleaved));
            if (frameOffset < 0 || frameCount < 0 ||
                frameOffset > stereoInterleaved.Length / 2 - frameCount)
                throw new ArgumentOutOfRangeException(nameof(frameCount));

            int samples = frameCount * 2;
            if (_scratch.Length < samples)
                Array.Resize(ref _scratch, samples);

            _opl.GenerateStream(_chip, _scratch.AsSpan(0, samples), frameCount);
            int destination = frameOffset * 2;
            for (int i = 0; i < samples; i++)
                stereoInterleaved[destination + i] = _scratch[i] / 32768f;
        }
    }
}
