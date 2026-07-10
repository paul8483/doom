using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Doom.Audio;
using Doom.Wad;

namespace Doom.MapBuild
{
    /// Decodes DMX <c>DS*</c> lumps into mono <see cref="AudioClip"/>s while the
    /// WAD is open. Misses (missing/corrupt) are cached as null with one warning.
    public sealed class SoundCache : IDisposable
    {
        private readonly WadFile wad;
        private readonly Dictionary<string, AudioClip> clips = new(StringComparer.Ordinal);
        private readonly HashSet<string> warned = new(StringComparer.Ordinal);
        private bool wadClosed;
        private bool disposed;

        public SoundCache(WadFile wad)
        {
            this.wad = wad ?? throw new ArgumentNullException(nameof(wad));
        }

        /// Mark that the owning <see cref="WadFile"/> has been disposed. Further
        /// uncached <see cref="Get"/> calls return null without reading the WAD.
        public void NotifyWadClosed() => wadClosed = true;

        public bool IsCached(string lumpName)
        {
            if (string.IsNullOrEmpty(lumpName)) return false;
            return clips.ContainsKey(Normalize(lumpName));
        }

        /// Returns a cached clip, or null if the lump is missing/corrupt/unavailable.
        public AudioClip Get(string lumpName)
        {
            if (disposed || string.IsNullOrEmpty(lumpName)) return null;
            string name = Normalize(lumpName);
            if (clips.TryGetValue(name, out var cached)) return cached;

            if (wadClosed)
            {
                WarnOnce(name, $"SoundCache: '{name}' requested after WAD close and was not pre-warmed");
                clips[name] = null;
                return null;
            }

            if (!name.StartsWith("DS", StringComparison.Ordinal))
            {
                WarnOnce(name, $"SoundCache: '{name}' is not a DS* sound lump");
                clips[name] = null;
                return null;
            }

            try
            {
                if (!SoundCatalog.TryRead(wad, name, out DecodedSound decoded) || decoded == null)
                {
                    WarnOnce(name, $"SoundCache: missing or invalid sound '{name}'");
                    clips[name] = null;
                    return null;
                }

                var clip = ToClip(name, decoded);
                clips[name] = clip;
                return clip;
            }
            catch (ObjectDisposedException)
            {
                wadClosed = true;
                WarnOnce(name, $"SoundCache: '{name}' requested after WAD close and was not pre-warmed");
                clips[name] = null;
                return null;
            }
            catch (InvalidDataException e)
            {
                WarnOnce(name, $"SoundCache: failed to decode '{name}': {e.Message}");
                clips[name] = null;
                return null;
            }
        }

        public void DestroyAll()
        {
            foreach (var kv in clips)
            {
                if (kv.Value != null)
                    UnityEngine.Object.Destroy(kv.Value);
            }
            clips.Clear();
            disposed = true;
        }

        public void Dispose() => DestroyAll();

        private static AudioClip ToClip(string name, DecodedSound decoded)
        {
            int n = decoded.Samples.Length;
            var clip = AudioClip.Create(name, Mathf.Max(1, n), 1, decoded.SampleRate, stream: false);
            var samples = new float[Mathf.Max(1, n)];
            for (int i = 0; i < n; i++)
                samples[i] = (decoded.Samples[i] - 128) / 128f;
            if (n == 0) samples[0] = 0f;
            clip.SetData(samples, 0);
            return clip;
        }

        private void WarnOnce(string name, string message)
        {
            if (!warned.Add(name)) return;
            Debug.LogWarning(message);
        }

        private static string Normalize(string lumpName) => lumpName.ToUpperInvariant();
    }
}
