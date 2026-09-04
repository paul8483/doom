using System.Collections.Generic;
using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Pooled 2D/3D playback for DMX sound effects. Music uses a separate source.
    public sealed class SoundSystem : MonoBehaviour
    {
        private SoundCache cache;
        private readonly List<AudioSource> pool = new();
        private readonly List<SoundChannelState> channelSnapshot = new();
        private readonly Dictionary<AudioSource, PlaybackState> playback = new();
        private readonly Dictionary<object, AudioSource> loops = new();
        private DoomRandom pitchRandom = new();
        private long playSequence;
        private float worldScale = 1f / 32f;
        private float sfxVolume = 1f;

        private sealed class PlaybackState
        {
            public SoundPriority Priority;
            public long StartedAt;
        }

        /// Last lump name passed to PlayLocal/PlayAt/PlayLoop (test probe).
        public string LastPlayedLump { get; private set; }

        /// True if <paramref name="lumpName"/> was played at least once this session.
        public bool WasPlayed(string lumpName)
        {
            if (string.IsNullOrEmpty(lumpName) || string.IsNullOrEmpty(LastPlayedLump))
                return false;
            return string.Equals(LastPlayedLump, lumpName, System.StringComparison.OrdinalIgnoreCase)
                   || played.Contains(Normalize(lumpName));
        }

        private readonly HashSet<string> played = new(System.StringComparer.Ordinal);
        private readonly Dictionary<string, int> playCounts = new(System.StringComparer.Ordinal);

        /// How many times <paramref name="lumpName"/> was started this session (test probe).
        public int PlayCountForTest(string lumpName) =>
            !string.IsNullOrEmpty(lumpName) && playCounts.TryGetValue(Normalize(lumpName), out int n) ? n : 0;

        public int ActiveLoopCount => loops.Count;
        public SoundCache Cache => cache;
        public float Volume => sfxVolume;

        public void Init(SoundCache soundCache, float scale, int poolSize = 16, float volume = 1f,
                         int randomSeed = 0)
        {
            cache = soundCache ?? throw new System.ArgumentNullException(nameof(soundCache));
            worldScale = scale;
            pitchRandom = new DoomRandom(randomSeed);
            playSequence = 0;
            SetVolume(volume);

            for (int i = pool.Count; i < poolSize; i++)
            {
                var go = new GameObject($"SfxSource_{i}");
                go.transform.SetParent(transform, worldPositionStays: false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.dopplerLevel = 0f;
                src.rolloffMode = AudioRolloffMode.Linear;
                pool.Add(src);
                playback[src] = new PlaybackState();
            }

            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null && !playback.ContainsKey(pool[i]))
                    playback[pool[i]] = new PlaybackState();
        }

        /// Runtime volume for existing and future pooled sources.
        public void SetVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            for (int i = 0; i < pool.Count; i++)
            {
                var src = pool[i];
                if (src != null) src.volume = sfxVolume;
            }
        }

        public AudioSource PlayLocal(string lumpName)
        {
            var clip = Resolve(lumpName);
            if (clip == null) return null;
            SoundCueMetadata metadata = SoundPlaybackPolicy.Describe(lumpName, local: true);
            var src = AcquireOneShot(metadata);
            if (src == null) return null;
            ResetSource(src);
            ConfigureLocal(src);
            src.transform.position = transform.position;
            src.clip = clip;
            src.volume = sfxVolume;
            src.pitch = SoundPlaybackPolicy.ResolvePitch(metadata, pitchRandom);
            MarkStarted(src, metadata);
            src.Play();
            NotePlayed(lumpName);
            return src;
        }

        public AudioSource PlayAt(string lumpName, Vector3 position,
                                  SoundCueContext context = SoundCueContext.World)
        {
            var clip = Resolve(lumpName);
            if (clip == null) return null;
            SoundCueMetadata metadata = SoundPlaybackPolicy.Describe(
                lumpName, local: false, context: context);
            var src = AcquireOneShot(metadata);
            if (src == null) return null;
            ResetSource(src);
            ConfigureWorld(src);
            src.transform.position = position;
            src.clip = clip;
            src.volume = sfxVolume;
            src.pitch = SoundPlaybackPolicy.ResolvePitch(metadata, pitchRandom);
            MarkStarted(src, metadata);
            src.Play();
            NotePlayed(lumpName);
            return src;
        }

        public void PlayLoop(string lumpName, object ownerKey, Vector3 position)
        {
            if (ownerKey == null) return;
            if (loops.TryGetValue(ownerKey, out var existing) && existing != null)
            {
                existing.transform.position = position;
                if (!existing.isPlaying && existing.clip != null)
                    existing.Play();
                return;
            }

            var clip = Resolve(lumpName);
            if (clip == null) return;
            SoundCueMetadata metadata = SoundPlaybackPolicy.Describe(lumpName, local: false, loop: true);
            var src = AcquireOneShot(metadata);
            if (src == null) return;
            ResetSource(src);
            ConfigureWorld(src);
            src.loop = true;
            src.transform.position = position;
            src.clip = clip;
            src.volume = sfxVolume;
            src.pitch = SoundPlaybackPolicy.ResolvePitch(metadata, pitchRandom);
            MarkStarted(src, metadata);
            src.Play();
            loops[ownerKey] = src;
            NotePlayed(lumpName);
        }

        public void StopLoop(object ownerKey, string stopLump = null)
        {
            if (ownerKey == null) return;
            if (!loops.TryGetValue(ownerKey, out var src)) return;
            loops.Remove(ownerKey);
            if (src != null)
            {
                Vector3 stopPosition = src.transform.position;
                ResetSource(src);
                if (!string.IsNullOrEmpty(stopLump))
                    PlayAt(stopLump, stopPosition);
                return;
            }
            if (!string.IsNullOrEmpty(stopLump))
                PlayAt(stopLump, transform.position);
        }

        void OnDestroy()
        {
            foreach (var kv in loops)
            {
                if (kv.Value != null) kv.Value.Stop();
            }
            loops.Clear();
            playback.Clear();
            cache?.DestroyAll();
            cache = null;
        }

        private AudioClip Resolve(string lumpName)
        {
            if (cache == null || string.IsNullOrEmpty(lumpName)) return null;
            return cache.Get(lumpName);
        }

        private void NotePlayed(string lumpName)
        {
            string name = Normalize(lumpName);
            LastPlayedLump = name;
            played.Add(name);
            playCounts[name] = PlayCountForTest(name) + 1;
        }

        private AudioSource AcquireOneShot(SoundCueMetadata metadata)
        {
            channelSnapshot.Clear();
            for (int i = 0; i < pool.Count; i++)
            {
                var src = pool[i];
                if (src == null)
                {
                    channelSnapshot.Add(new SoundChannelState(
                        true, true, SoundPriority.Critical, long.MaxValue));
                    continue;
                }

                bool isLoop = IsLoopSource(src);
                PlaybackState state = playback[src];
                channelSnapshot.Add(new SoundChannelState(
                    isLoop || src.isPlaying,
                    isLoop,
                    state.Priority,
                    state.StartedAt));
            }

            int selected = SoundPlaybackPolicy.SelectChannel(channelSnapshot, metadata.Priority);
            return selected >= 0 ? pool[selected] : null;
        }

        private bool IsLoopSource(AudioSource src)
        {
            foreach (var kv in loops)
                if (kv.Value == src) return true;
            return false;
        }

        private void ConfigureLocal(AudioSource src)
        {
            src.spatialBlend = 0f;
            src.loop = false;
            src.minDistance = 1f;
            src.maxDistance = 500f;
        }

        private void ConfigureWorld(AudioSource src)
        {
            src.spatialBlend = 1f;
            src.loop = false;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 160f * worldScale;
            src.maxDistance = 1200f * worldScale;
            src.dopplerLevel = 0f;
        }

        private void MarkStarted(AudioSource src, SoundCueMetadata metadata)
        {
            PlaybackState state = playback[src];
            state.Priority = metadata.Priority;
            state.StartedAt = ++playSequence;
        }

        private void ResetSource(AudioSource src)
        {
            src.Stop();
            src.clip = null;
            src.loop = false;
            src.pitch = 1f;
            src.volume = sfxVolume;
            src.spatialBlend = 0f;
            src.minDistance = 1f;
            src.maxDistance = 500f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.dopplerLevel = 0f;
            if (playback.TryGetValue(src, out PlaybackState state))
            {
                state.Priority = SoundPriority.Ambient;
                state.StartedAt = 0;
            }
        }

        private static string Normalize(string lumpName) => lumpName.ToUpperInvariant();
    }
}
