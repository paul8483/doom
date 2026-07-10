namespace Doom.Audio
{
    /// <summary>Minimal buffered-register OPL3 output device used by <see cref="MusOplPlayer"/>.</summary>
    public interface IOplChip
    {
        void Reset(int sampleRate);
        void WriteRegister(int register, byte value);
        /// <summary>Render stereo frames into interleaved L,R float samples in [-1,1].</summary>
        void Render(float[] stereoInterleaved, int frameOffset, int frameCount);
    }
}
