using System;
using System.Collections.Concurrent;
using Doom.Graphics;

namespace Doom.MapBuild.Rendering
{
    /// Process-lifetime CPU cache of Enhanced job results. Survives scene reloads;
    /// GPU objects stay per-map. Resolution order at warm: store → disk → compute.
    public sealed class EnhancedVariantStore
    {
        public static EnhancedVariantStore Instance { get; } = new EnhancedVariantStore();

        readonly ConcurrentDictionary<EnhancedVariantKey, EnhancedJobResult> entries =
            new ConcurrentDictionary<EnhancedVariantKey, EnhancedJobResult>();

        string boundWadIdentity;
        long approximateCpuBytes;

        /// WAD identity currently bound; null until <see cref="BindWadIdentity"/>.
        public string BoundWadIdentity => boundWadIdentity;

        public int Count => entries.Count;

        /// Rough CPU footprint of stored buffers (RGBA bytes across mip levels).
        public long ApproximateCpuBytes => approximateCpuBytes;

        EnhancedVariantStore() { }

        /// Bind the active WAD. Mismatch with a previous bind clears the store.
        public void BindWadIdentity(string wadIdentity)
        {
            if (string.IsNullOrEmpty(wadIdentity))
                throw new ArgumentException("WAD identity is required.", nameof(wadIdentity));

            if (boundWadIdentity != null
                && !string.Equals(boundWadIdentity, wadIdentity, StringComparison.Ordinal))
            {
                Clear();
            }

            boundWadIdentity = wadIdentity;
        }

        public bool TryGet(
            EnhancedJobKind kind,
            string itemId,
            EnhancedLayerConfig layers,
            out EnhancedJobResult result)
        {
            result = null;
            if (string.IsNullOrEmpty(boundWadIdentity) || string.IsNullOrEmpty(itemId))
                return false;

            var key = new EnhancedVariantKey(
                boundWadIdentity, kind, itemId, layers, EnhancedPipelineVersion.Value);
            return TryGetExact(key, out result);
        }

        public bool TryGetExact(EnhancedVariantKey key, out EnhancedJobResult result)
        {
            if (entries.TryGetValue(key, out result) && result != null && result.Success)
                return true;
            result = null;
            return false;
        }

        /// Publish a successful CPU result. Failures are ignored (native fallback
        /// stays per-cache, not session-wide).
        public void Publish(
            EnhancedJobKind kind,
            string itemId,
            EnhancedLayerConfig layers,
            EnhancedJobResult result)
        {
            if (string.IsNullOrEmpty(boundWadIdentity) || string.IsNullOrEmpty(itemId))
                return;
            if (result == null || !result.Success)
                return;

            var key = new EnhancedVariantKey(
                boundWadIdentity, kind, itemId, layers, EnhancedPipelineVersion.Value);
            PublishExact(key, result);
        }

        public void PublishExact(EnhancedVariantKey key, EnhancedJobResult result)
        {
            if (result == null || !result.Success)
                return;

            long bytes = EstimateBytes(result);
            if (entries.TryGetValue(key, out var prior) && prior != null)
                approximateCpuBytes -= EstimateBytes(prior);

            entries[key] = result;
            approximateCpuBytes += bytes;
            if (approximateCpuBytes < 0) approximateCpuBytes = 0;
        }

        public void Clear()
        {
            entries.Clear();
            approximateCpuBytes = 0;
            // Keep boundWadIdentity — caller may re-publish under the same WAD.
        }

        /// Full reset for PlayMode/EditMode teardown (identity + entries).
        public static void ResetForTests()
        {
            Instance.entries.Clear();
            Instance.approximateCpuBytes = 0;
            Instance.boundWadIdentity = null;
        }

        public static long EstimateBytes(EnhancedJobResult result)
        {
            if (result == null || !result.Success) return 0;
            long total = 0;
            if (result.AlbedoMips != null)
                total += SumMipBytes(result.AlbedoMips);
            if (result.NormalMips != null)
                total += SumMipBytes(result.NormalMips);
            if (result.Rgba != null && result.Rgba.Rgba != null)
                total += result.Rgba.Rgba.LongLength;
            return total;
        }

        static long SumMipBytes(PaletteMipChain chain)
        {
            long total = 0;
            for (int i = 0; i < chain.Count; i++)
            {
                var img = chain[i];
                if (img?.Rgba != null)
                    total += img.Rgba.LongLength;
            }
            return total;
        }
    }

    /// Cache key for <see cref="EnhancedVariantStore"/> (and future disk index).
    public readonly struct EnhancedVariantKey : IEquatable<EnhancedVariantKey>
    {
        public readonly string WadIdentity;
        public readonly EnhancedJobKind Kind;
        public readonly string ItemId;
        public readonly EnhancedLayerConfig Layers;
        public readonly int PipelineVersion;

        public EnhancedVariantKey(
            string wadIdentity,
            EnhancedJobKind kind,
            string itemId,
            EnhancedLayerConfig layers,
            int pipelineVersion)
        {
            WadIdentity = wadIdentity ?? throw new ArgumentNullException(nameof(wadIdentity));
            Kind = kind;
            ItemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
            Layers = layers;
            PipelineVersion = pipelineVersion;
        }

        public bool Equals(EnhancedVariantKey other) =>
            Kind == other.Kind
            && PipelineVersion == other.PipelineVersion
            && Layers.Equals(other.Layers)
            && string.Equals(WadIdentity, other.WadIdentity, StringComparison.Ordinal)
            && string.Equals(ItemId, other.ItemId, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is EnhancedVariantKey other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(
                WadIdentity != null ? StringComparer.Ordinal.GetHashCode(WadIdentity) : 0,
                (int)Kind,
                ItemId != null ? StringComparer.Ordinal.GetHashCode(ItemId) : 0,
                Layers,
                PipelineVersion);

        public static bool operator ==(EnhancedVariantKey a, EnhancedVariantKey b) => a.Equals(b);
        public static bool operator !=(EnhancedVariantKey a, EnhancedVariantKey b) => !a.Equals(b);
    }
}
