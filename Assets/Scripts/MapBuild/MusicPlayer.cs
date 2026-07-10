using System;
using System.IO;
using UnityEngine;
using Doom.Audio;

namespace Doom.MapBuild
{
    /// Streams map MUS music through GENMIDI/OPL into a Unity AudioClip.
    /// Bytes are copied while the WAD is open; the PCM callback never touches Unity
    /// APIs beyond filling the provided buffer, and never allocates.
    public sealed class MusicPlayer : MonoBehaviour
    {
        const int SampleRate = 44100;
        const int StreamFrames = 4096;

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

        /// Cumulative stereo frames filled by the PCM callback (test probe).
        public long RenderedFrames => System.Threading.Interlocked.Read(ref renderedFrames);

        public bool IsActive => player != null && source != null && !stopped;

        /// Streaming clip name (test probe); null if music disabled.
        public string ClipName => clip != null ? clip.name : null;

        /// Force-render frames through the sequencer (batchmode-safe test probe —
        /// Unity may not invoke the PCM callback without an audio device).
        public int RenderForTest(int frames)
        {
            if (player == null || stopped || frames <= 0) return 0;
            var buf = new float[frames * 2];
            player.Render(buf, frames);
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

            source = gameObject.GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false; // sequencer loops; do not use AudioSource.loop
            source.spatialBlend = 0f;
            source.volume = volume;
            source.Stop();

            string clipName = string.IsNullOrEmpty(trackName) ? "MUS" : trackName;
            clip = AudioClip.Create(
                clipName, StreamFrames, 2, SampleRate, stream: true, OnPcmRead, OnPcmSetPosition);
            source.clip = clip;
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
            if (source != null) source.volume = volume;
        }

        /// Pause playback; sequencer and AudioSource position are preserved.
        public void Pause()
        {
            if (source == null || stopped) return;
            source.Pause();
            IsPaused = true;
        }

        /// Resume after <see cref="Pause"/> without restarting the sequencer.
        public void Resume()
        {
            if (source == null || stopped) return;
            source.UnPause();
            IsPaused = false;
        }

        void OnPcmRead(float[] data)
        {
            if (stopped || player == null)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            int frames = data.Length / 2;
            try
            {
                player.Render(data, frames);
                System.Threading.Interlocked.Add(ref renderedFrames, frames);
            }
            catch
            {
                Array.Clear(data, 0, data.Length);
            }
        }

        void OnPcmSetPosition(int position)
        {
            // Streaming clip; position resets are ignored — sequencer owns timeline.
        }

        void OnDestroy() => StopInternal();

        void OnDisable() => StopInternal();

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
                try { player.Stop(); } catch { /* ignore */ }
                player = null;
            }
        }
    }
}
