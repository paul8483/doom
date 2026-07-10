namespace Doom.Audio
{
    /// Decoded DMX digital sound: unsigned 8-bit mono PCM at the lump sample rate.
    public sealed class DecodedSound
    {
        public int SampleRate { get; }
        public byte[] Samples { get; }

        public DecodedSound(int sampleRate, byte[] samples)
        {
            SampleRate = sampleRate;
            Samples = samples;
        }
    }
}
