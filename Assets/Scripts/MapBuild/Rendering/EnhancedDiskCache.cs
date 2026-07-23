using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Doom.Graphics;

namespace Doom.MapBuild.Rendering
{
    /// Session disk pack cache for Enhanced CPU results. Resolution order at
    /// warm: store → disk → compute. Pack path:
    /// <c>EnhancedCache/&lt;sha256&gt;-v&lt;pipeline&gt;.bin</c>. Errors are logged and
    /// treated as misses — never fatal.
    public sealed class EnhancedDiskCache
    {
        public const string FolderName = "EnhancedCache";
        public const string FileExtension = ".bin";
        public const string TempExtension = ".bin.tmp";

        public static EnhancedDiskCache Instance { get; } = new EnhancedDiskCache();

        static string rootOverride;
        static bool enabled =
#if UNITY_EDITOR
            false;
#else
            true;
#endif

        readonly ConcurrentDictionary<DiskKey, EnhancedJobResult> entries =
            new ConcurrentDictionary<DiskKey, EnhancedJobResult>();

        readonly object ioLock = new object();

        byte[] wadHash;
        string packPath;
        string boundWadPath;
        volatile bool loaded;
        volatile bool dirty;
        volatile bool loadFailed;
        Task loadTask;
        Task flushTask;
        long packFileBytes;

        EnhancedDiskCache() { }

        /// Player builds default on; Editor/PlayMode default off (CI cleanliness).
        public static bool Enabled => enabled;

        public int Count => entries.Count;

        public bool IsLoaded => loaded;

        public string PackPath => packPath;

        public long PackFileBytes => packFileBytes;

        public byte[] WadHash => wadHash;

        /// Enable disk cache against an isolated root (PlayMode/EditMode tests).
        public static void EnableForTests(string rootDirectory)
        {
            if (string.IsNullOrEmpty(rootDirectory))
                throw new ArgumentException("Root directory is required.", nameof(rootDirectory));
            rootOverride = rootDirectory;
            enabled = true;
        }

        /// Disable writes/lookups and clear session state (test teardown).
        public static void ResetForTests()
        {
            Instance.ClearSession();
            rootOverride = null;
#if UNITY_EDITOR
            enabled = false;
#else
            enabled = true;
#endif
        }

        public static string ResolveRootDirectory()
        {
            if (!string.IsNullOrEmpty(rootOverride))
                return rootOverride;
#if UNITY_EDITOR
            return Path.Combine(Path.GetTempPath(), "doom-enhanced-cache");
#else
            return Path.Combine(Application.persistentDataPath, FolderName);
#endif
        }

        public static byte[] ComputeWadSha256(string wadPath)
        {
            if (string.IsNullOrEmpty(wadPath) || !File.Exists(wadPath))
                throw new FileNotFoundException("WAD not found for SHA-256.", wadPath);

            using var sha = SHA256.Create();
            using var fs = new FileStream(
                wadPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return sha.ComputeHash(fs);
        }

        public static string ToHex(byte[] hash)
        {
            if (hash == null) throw new ArgumentNullException(nameof(hash));
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }

        public static string BuildPackFileName(byte[] hash, int pipelineVersion) =>
            $"{ToHex(hash)}-v{pipelineVersion}{FileExtension}";

        /// Bind a WAD path and kick off a background pack load (if present).
        public void BindWad(string wadPath)
        {
            if (!enabled) return;
            if (string.IsNullOrEmpty(wadPath))
                throw new ArgumentException("WAD path is required.", nameof(wadPath));

            if (string.Equals(boundWadPath, wadPath, StringComparison.Ordinal)
                && wadHash != null
                && (loaded || loadTask != null))
            {
                return;
            }

            ClearSession();
            boundWadPath = wadPath;

            try
            {
                wadHash = ComputeWadSha256(wadPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"EnhancedDiskCache: SHA-256 failed: {e.Message}");
                loadFailed = true;
                loaded = true;
                return;
            }

            string root = ResolveRootDirectory();
            packPath = Path.Combine(
                root, BuildPackFileName(wadHash, EnhancedPipelineVersion.Value));

            loadTask = Task.Run(LoadPackSafe);
        }

        /// Yield until the background pack load finishes (or disk is disabled).
        public IEnumerator WaitUntilLoaded()
        {
            if (!enabled || loadTask == null)
            {
                loaded = true;
                yield break;
            }

            while (loadTask != null && !loadTask.IsCompleted)
                yield return null;

            // Ensure LoadPackSafe finished setting flags even if raced.
            if (!loaded && !loadFailed)
                loaded = true;
        }

        public bool TryGet(
            EnhancedJobKind kind,
            string itemId,
            EnhancedLayerConfig layers,
            out EnhancedJobResult result)
        {
            result = null;
            if (!enabled || !loaded || string.IsNullOrEmpty(itemId))
                return false;

            var key = new DiskKey(kind, itemId, ToFlags(layers));
            if (entries.TryGetValue(key, out result) && result != null && result.Success)
                return true;

            result = null;
            return false;
        }

        /// Publish a successful CPU result into the in-memory index and mark dirty.
        public void Publish(
            EnhancedJobKind kind,
            string itemId,
            EnhancedLayerConfig layers,
            EnhancedJobResult result)
        {
            if (!enabled || string.IsNullOrEmpty(itemId))
                return;
            if (result == null || !result.Success)
                return;
            if (wadHash == null)
                return;

            var key = new DiskKey(kind, itemId, ToFlags(layers));
            entries[key] = result;
            dirty = true;
        }

        /// Fire-and-forget rewrite of the pack file (temp + atomic replace).
        public void ScheduleFlush()
        {
            if (!enabled || !dirty || wadHash == null || string.IsNullOrEmpty(packPath))
                return;

            lock (ioLock)
            {
                if (flushTask != null && !flushTask.IsCompleted)
                    return;
                flushTask = Task.Run(FlushSafe);
            }
        }

        /// Synchronous flush for tests / measurement.
        public void FlushBlocking()
        {
            if (!enabled || wadHash == null || string.IsNullOrEmpty(packPath))
                return;

            Task pending;
            lock (ioLock) pending = flushTask;
            if (pending != null)
            {
                try { pending.Wait(); }
                catch { /* flush logged its own failure */ }
            }

            FlushSafe();
        }

        /// Wait until any scheduled flush completes (tests).
        public IEnumerator WaitUntilFlushCompletes()
        {
            Task task;
            lock (ioLock) task = flushTask;
            if (task == null) yield break;
            while (!task.IsCompleted)
                yield return null;
        }

        void ClearSession()
        {
            entries.Clear();
            wadHash = null;
            packPath = null;
            boundWadPath = null;
            loaded = false;
            dirty = false;
            loadFailed = false;
            packFileBytes = 0;
            loadTask = null;
            flushTask = null;
        }

        void LoadPackSafe()
        {
            try
            {
                if (string.IsNullOrEmpty(packPath) || !File.Exists(packPath))
                {
                    loaded = true;
                    return;
                }

                byte[] data;
                try
                {
                    data = File.ReadAllBytes(packPath);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"EnhancedDiskCache: read failed: {e.Message}");
                    loaded = true;
                    loadFailed = true;
                    return;
                }

                packFileBytes = data.LongLength;
                if (!EnhancedCacheCodec.TryDecode(
                        data, wadHash, EnhancedPipelineVersion.Value,
                        out var packEntries, out string error))
                {
                    Debug.LogWarning(
                        $"EnhancedDiskCache: ignoring pack ({error}); will recompute.");
                    TryDeleteCorruptPack();
                    loaded = true;
                    return;
                }

                for (int i = 0; i < packEntries.Count; i++)
                {
                    var e = packEntries[i];
                    var key = new DiskKey(e.Kind, e.ItemId, e.LayerFlags);
                    entries[key] = e.Result;
                }

                loaded = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"EnhancedDiskCache: load faulted: {e.Message}");
                loadFailed = true;
                loaded = true;
            }
        }

        void FlushSafe()
        {
            lock (ioLock)
            {
                if (!dirty || wadHash == null || string.IsNullOrEmpty(packPath))
                    return;

                try
                {
                    var list = new List<EnhancedCacheCodec.PackEntry>(entries.Count);
                    foreach (var kv in entries)
                    {
                        if (kv.Value == null || !kv.Value.Success) continue;
                        list.Add(new EnhancedCacheCodec.PackEntry
                        {
                            Kind = kv.Key.Kind,
                            ItemId = kv.Key.ItemId,
                            LayerFlags = kv.Key.LayerFlags,
                            Result = kv.Value,
                        });
                    }

                    byte[] bytes = EnhancedCacheCodec.Encode(
                        wadHash, EnhancedPipelineVersion.Value, list);

                    string root = Path.GetDirectoryName(packPath);
                    if (!string.IsNullOrEmpty(root) && !Directory.Exists(root))
                        Directory.CreateDirectory(root);

                    string tempPath = packPath + ".tmp";
                    try
                    {
                        WriteAllBytesFlushed(tempPath, bytes);
                        ReplaceFile(tempPath, packPath);
                        packFileBytes = bytes.LongLength;
                        dirty = false;
                    }
                    catch (Exception e)
                    {
                        TryDelete(tempPath);
                        Debug.LogWarning($"EnhancedDiskCache: write failed: {e.Message}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"EnhancedDiskCache: flush faulted: {e.Message}");
                }
            }
        }

        void TryDeleteCorruptPack()
        {
            try
            {
                if (!string.IsNullOrEmpty(packPath) && File.Exists(packPath))
                    File.Delete(packPath);
            }
            catch
            {
                // ignore
            }
        }

        static byte ToFlags(EnhancedLayerConfig layers) =>
            EnhancedCacheCodec.PackLayerFlags(
                layers.WorldDedither,
                layers.WorldUpscale4X,
                layers.SpritesUpscale4X,
                layers.UiUpscale4X);

        static void WriteAllBytesFlushed(string path, byte[] data)
        {
            using var fs = new FileStream(
                path, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 4096, options: FileOptions.None);
            fs.Write(data, 0, data.Length);
            fs.Flush(flushToDisk: true);
        }

        static void ReplaceFile(string source, string destination)
        {
            if (File.Exists(destination))
            {
                File.Replace(source, destination, destination + ".bak",
                    ignoreMetadataErrors: true);
                try { File.Delete(destination + ".bak"); }
                catch { /* ignore */ }
            }
            else
            {
                File.Move(source, destination);
            }
        }

        static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // ignore
            }
        }

        readonly struct DiskKey : IEquatable<DiskKey>
        {
            public readonly EnhancedJobKind Kind;
            public readonly string ItemId;
            public readonly byte LayerFlags;

            public DiskKey(EnhancedJobKind kind, string itemId, byte layerFlags)
            {
                Kind = kind;
                ItemId = itemId;
                LayerFlags = layerFlags;
            }

            public bool Equals(DiskKey other) =>
                Kind == other.Kind
                && LayerFlags == other.LayerFlags
                && string.Equals(ItemId, other.ItemId, StringComparison.Ordinal);

            public override bool Equals(object obj) =>
                obj is DiskKey other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(
                    (int)Kind,
                    ItemId != null ? StringComparer.Ordinal.GetHashCode(ItemId) : 0,
                    LayerFlags);
        }
    }
}
