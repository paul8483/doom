using System;
using UnityEngine;
using Doom.Audio;

namespace Doom.MapBuild
{
    /// Synthesizes map MUS/MIDI through GENMIDI/OPL on a dedicated 2D AudioSource.
    /// Uses OnAudioFilterRead (not streaming AudioClip callbacks) so playback works
    /// in Windows standalone builds where stream PCM hooks are often silent.
    [RequireComponent(typeof(AudioSource))]
    public sealed class MusicPlayer : MonoBehaviour
    {
        // OnAudioFilterRead delivers buffers at the mixer's OUTPUT rate; the
        // OPL core must resample to that rate or the score plays fast/sharp
        // (44100 fixed on a 48 kHz device = +8.8% tempo, +1.5 semitones).
        static int SampleRate =>
            AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
        const int CarrierFrames = 1024;
        // Serialises the audio-thread Render against main-thread Stop/Init
        // (both touch the same OPL chip).
        readonly object synthGate = new object();

        MusOplPlayer player;
        AudioClip clip;
        AudioSource source;
        float volume = 0.6f;
        volatile bool stopped;
        long renderedFrames;

        /// Lump name of the active track (e.g. D_E1M1), or null if disabled.
        public string TrackName { get; private set; }

        public float Volume => volume;
        public bool IsPaused { get; private set; }

        /// Cumulative stereo frames filled by the PCM path (test probe).
        public long RenderedFrames => System.Threading.Interlocked.Read(ref renderedFrames);

        public bool IsActive => player != null && source != null && !stopped;

        /// Active track name (test probe); null if music disabled.
        public string ClipName => TrackName;

        /// Force-render frames through the sequencer (batchmode-safe test probe).
        public int RenderForTest(int frames)
        {
            if (player == null || stopped || frames <= 0) return 0;
            var buf = new float[frames * 2];
            // Same gate as OnAudioFilterRead: the audio thread renders the
            // same OPL chip, and two concurrent Render calls race inside
            // NukedOpl's write queue (NRE in OPL3_Generate4Ch — a rare
            // PlayMode flake of Audio_bootstrap_and_music, 2026-09-05).
            lock (synthGate)
            {
                if (player == null || stopped) return 0;
                player.Render(buf, frames);
            }
            System.Threading.Interlocked.Add(ref renderedFrames, frames);
            return frames;
        }

        /// <summary>
        /// Parse MUS + GENMIDI and start looping playback. Returns false on failure
        /// (gameplay continues without music).
        /// </summary>
        public bool Init(byte[] musLump, byte[] genMidiLump, string trackName, float musicVolume)
        {
            StopInternal();
            volume = Mathf.Clamp01(musicVolume);
            TrackName = trackName;

            if (musLump == null || genMidiLump == null)
            {
                Debug.LogWarning("MusicPlayer: missing MUS or GENMIDI bytes");
                return false;
            }

            try
            {
                MusSong song = MusicScore.Read(musLump);
                GenMidiBank bank = GenMidiBank.Read(genMidiLump);
                player = new MusOplPlayer(song, bank, new NukedOplChip(), SampleRate);
                player.Start(loop: true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"MusicPlayer: failed to init '{trackName}': {e.Message}");
                player = null;
                TrackName = null;
                return false;
            }

            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            source.volume = 1f; // applied in OnAudioFilterRead

            string clipName = string.IsNullOrEmpty(trackName) ? "MUS" : trackName;
            clip = AudioClip.Create(clipName, CarrierFrames, 2, SampleRate, stream: false);
            clip.SetData(new float[CarrierFrames * 2], 0);

            source.clip = clip;
            source.Stop();
            stopped = false;
            System.Threading.Interlocked.Exchange(ref renderedFrames, 0);
            source.Play();
            IsPaused = false;
            return true;
        }

        /// Runtime volume without restarting the sequencer.
        public void SetVolume(float musicVolume)
        {
            volume = Mathf.Clamp01(musicVolume);
        }

        public void Pause()
        {
            if (source == null || stopped) return;
            source.Pause();
            IsPaused = true;
        }

        public void Resume()
        {
            if (source == null || stopped) return;
            source.UnPause();
            IsPaused = false;
        }

        public void EnsurePlayback()
        {
            if (player == null || stopped || source == null || IsPaused) return;
            if (!source.isPlaying)
                source.Play();
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (channels != 2 || stopped || IsPaused)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            var synth = player;
            if (synth == null)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            int frames = data.Length / channels;
            try
            {
                lock (synthGate)
                {
                    if (stopped || !ReferenceEquals(synth, player))
                    {
                        Array.Clear(data, 0, data.Length);
                        return;
                    }
                    synth.Render(data, frames);
                }
                if (volume < 0.999f)
                {
                    for (int i = 0; i < data.Length; i++)
                        data[i] *= volume;
                }
                System.Threading.Interlocked.Add(ref renderedFrames, frames);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"MusicPlayer: render failed: {e.Message}");
                Array.Clear(data, 0, data.Length);
            }
        }

        void OnDestroy() => StopInternal();

        void StopInternal()
        {
            stopped = true;
            IsPaused = false;
            if (source != null)
            {
                source.Stop();
                source.clip = null;
            }
            if (clip != null)
            {
                Destroy(clip);
                clip = null;
            }
            if (player != null)
            {
                lock (synthGate)
                {
                    try { player.Stop(); } catch { /* ignore */ }
                    player = null;
                }
            }
        }
    }
}
