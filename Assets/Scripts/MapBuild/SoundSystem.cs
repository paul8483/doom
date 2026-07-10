using System.Collections.Generic;
using UnityEngine;

namespace Doom.MapBuild
{
    /// Pooled 2D/3D playback for DMX sound effects. Music uses a separate source.
    public sealed class SoundSystem : MonoBehaviour
    {
        private SoundCache cache;
        private readonly List<AudioSource> pool = new();
        private readonly Dictionary<object, AudioSource> loops = new();
        private float worldScale = 1f / 32f;
        private float sfxVolume = 1f;

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

        public int ActiveLoopCount => loops.Count;
        public SoundCache Cache => cache;

        public void Init(SoundCache soundCache, float scale, int poolSize = 16, float volume = 1f)
        {
            cache = soundCache ?? throw new System.ArgumentNullException(nameof(soundCache));
            worldScale = scale;
            sfxVolume = Mathf.Clamp01(volume);

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
            }
        }

        public AudioSource PlayLocal(string lumpName)
        {
            var clip = Resolve(lumpName);
            if (clip == null) return null;
            var src = AcquireOneShot();
            if (src == null) return null;
            ConfigureLocal(src);
            src.transform.position = transform.position;
            src.clip = clip;
            src.volume = sfxVolume;
            src.Play();
            NotePlayed(lumpName);
            return src;
        }

        public AudioSource PlayAt(string lumpName, Vector3 position)
        {
            var clip = Resolve(lumpName);
            if (clip == null) return null;
            var src = AcquireOneShot();
            if (src == null) return null;
            ConfigureWorld(src);
            src.transform.position = position;
            src.clip = clip;
            src.volume = sfxVolume;
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
            var src = AcquireOneShot();
            if (src == null) return;
            ConfigureWorld(src);
            src.loop = true;
            src.transform.position = position;
            src.clip = clip;
            src.volume = sfxVolume;
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
                src.Stop();
                src.loop = false;
                src.clip = null;
            }
            if (!string.IsNullOrEmpty(stopLump))
                PlayAt(stopLump, src != null ? src.transform.position : transform.position);
        }

        void OnDestroy()
        {
            foreach (var kv in loops)
            {
                if (kv.Value != null) kv.Value.Stop();
            }
            loops.Clear();
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
        }

        private AudioSource AcquireOneShot()
        {
            // Prefer an idle non-loop source.
            for (int i = 0; i < pool.Count; i++)
            {
                var src = pool[i];
                if (src == null) continue;
                if (IsLoopSource(src)) continue;
                if (!src.isPlaying) return src;
            }

            // Steal the quietest/oldest one-shot (not a tracked loop).
            AudioSource best = null;
            float bestVol = float.MaxValue;
            for (int i = 0; i < pool.Count; i++)
            {
                var src = pool[i];
                if (src == null || IsLoopSource(src)) continue;
                if (src.volume < bestVol)
                {
                    bestVol = src.volume;
                    best = src;
                }
            }
            if (best != null)
            {
                best.Stop();
                best.loop = false;
            }
            return best;
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

        private static string Normalize(string lumpName) => lumpName.ToUpperInvariant();
    }
}
