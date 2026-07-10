using System;
using System.Collections.Generic;
using System.IO;

namespace Doom.Game
{
    /// Slot listing entry from envelope headers only (payload not decoded).
    public sealed class SaveSlotInfo
    {
        public string SlotName { get; }
        public int Version { get; }
        public string MapName { get; }
        public string WadIdentity { get; }
        public int PayloadLength { get; }

        public SaveSlotInfo(
            string slotName, int version, string mapName, string wadIdentity, int payloadLength)
        {
            SlotName = slotName;
            Version = version;
            MapName = mapName;
            WadIdentity = wadIdentity;
            PayloadLength = payloadLength;
        }
    }

    /// Atomic slot storage: write temp → flush → replace. Failed writes leave the
    /// previous valid slot intact. Slot names reject path separators.
    public sealed class SaveSlotStore
    {
        public const string FileExtension = ".dsav";
        public const string TempExtension = ".dsav.tmp";

        readonly string rootDirectory;
        readonly ISaveFileSystem fs;

        public SaveSlotStore(string rootDirectory, ISaveFileSystem fileSystem)
        {
            if (string.IsNullOrEmpty(rootDirectory))
                throw new ArgumentException("Root directory is required.", nameof(rootDirectory));
            this.rootDirectory = rootDirectory;
            fs = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public string RootDirectory => rootDirectory;

        public void Write(string slotName, SaveGame save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            string safe = ValidateSlotName(slotName);
            EnsureRoot();
            CleanupTempFiles();

            byte[] bytes = SaveGameCodec.Encode(save);
            string slotPath = SlotPath(safe);
            string tempPath = TempPath(safe);

            try
            {
                fs.WriteAllBytesFlushed(tempPath, bytes);
                fs.ReplaceFile(tempPath, slotPath);
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }
        }

        public SaveGame Read(string slotName)
        {
            if (!TryRead(slotName, out SaveGame save, out string error))
                throw new SaveFormatException(error);
            return save;
        }

        public bool TryRead(string slotName, out SaveGame save, out string error)
        {
            save = null;
            error = null;
            string safe;
            try
            {
                safe = ValidateSlotName(slotName);
            }
            catch (ArgumentException ex)
            {
                error = ex.Message;
                return false;
            }

            CleanupTempFiles();
            string path = SlotPath(safe);
            if (!fs.FileExists(path))
            {
                error = "Save slot not found.";
                return false;
            }

            byte[] data;
            try
            {
                data = fs.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                error = "Failed to read save slot: " + ex.Message;
                return false;
            }

            return SaveGameCodec.TryDecode(data, out save, out error);
        }

        public bool Exists(string slotName)
        {
            string safe = ValidateSlotName(slotName);
            return fs.FileExists(SlotPath(safe));
        }

        public void Delete(string slotName)
        {
            string safe = ValidateSlotName(slotName);
            CleanupTempFiles();
            string path = SlotPath(safe);
            if (fs.FileExists(path))
                fs.DeleteFile(path);
        }

        /// Lists valid slots by reading envelope headers only (no payload decode).
        public IReadOnlyList<SaveSlotInfo> ListSlots()
        {
            EnsureRoot();
            CleanupTempFiles();

            var results = new List<SaveSlotInfo>();
            foreach (string path in fs.EnumerateFiles(rootDirectory, "*" + FileExtension))
            {
                string fileName = Path.GetFileName(path);
                if (string.IsNullOrEmpty(fileName)
                    || !fileName.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (fileName.EndsWith(TempExtension, StringComparison.OrdinalIgnoreCase))
                    continue;

                string slotName = fileName.Substring(0, fileName.Length - FileExtension.Length);
                if (!IsValidSlotName(slotName))
                    continue;

                byte[] data;
                try
                {
                    data = fs.ReadAllBytes(path);
                }
                catch
                {
                    continue;
                }

                // Header path verifies checksum over raw payload bytes without decoding DTOs.
                if (!SaveGameCodec.TryReadHeader(data, verifyChecksum: true,
                        out SaveGameHeader header, out _))
                    continue;

                results.Add(new SaveSlotInfo(
                    slotName, header.Version, header.MapName, header.WadIdentity,
                    header.PayloadLength));
            }

            results.Sort((a, b) => string.CompareOrdinal(a.SlotName, b.SlotName));
            return results;
        }

        public static string ValidateSlotName(string slotName)
        {
            if (string.IsNullOrWhiteSpace(slotName))
                throw new ArgumentException("Slot name is required.", nameof(slotName));
            if (!IsValidSlotName(slotName))
                throw new ArgumentException(
                    "Slot name must not contain path separators or be empty.", nameof(slotName));
            return slotName;
        }

        public static bool IsValidSlotName(string slotName)
        {
            if (string.IsNullOrWhiteSpace(slotName)) return false;
            if (slotName.IndexOfAny(new[] { '/', '\\', ':' }) >= 0) return false;
            if (slotName.Contains("..")) return false;
            return true;
        }

        string SlotPath(string safeSlotName) =>
            Path.Combine(rootDirectory, safeSlotName + FileExtension);

        string TempPath(string safeSlotName) =>
            Path.Combine(rootDirectory, safeSlotName + TempExtension);

        void EnsureRoot()
        {
            if (!fs.DirectoryExists(rootDirectory))
                fs.CreateDirectory(rootDirectory);
        }

        void CleanupTempFiles()
        {
            if (!fs.DirectoryExists(rootDirectory)) return;
            foreach (string path in fs.EnumerateFiles(rootDirectory, "*" + TempExtension))
                TryDelete(path);
        }

        void TryDelete(string path)
        {
            try
            {
                if (fs.FileExists(path))
                    fs.DeleteFile(path);
            }
            catch
            {
                // Best-effort cleanup; next safe access will retry.
            }
        }
    }

    /// Real System.IO adapter for production and temp-directory tests.
    public sealed class SystemSaveFileSystem : ISaveFileSystem
    {
        public bool DirectoryExists(string path) => Directory.Exists(path);

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        public bool FileExists(string path) => File.Exists(path);

        public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

        public void WriteAllBytesFlushed(string path, byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            using (var fs = new FileStream(
                       path, FileMode.Create, FileAccess.Write, FileShare.None,
                       bufferSize: 4096, options: FileOptions.None))
            {
                fs.Write(data, 0, data.Length);
                fs.Flush(flushToDisk: true);
            }
        }

        public void DeleteFile(string path) => File.Delete(path);

        public void ReplaceFile(string source, string destination)
        {
            if (File.Exists(destination))
            {
                // Atomic on Windows when destination exists.
                File.Replace(source, destination, destination + ".bak", ignoreMetadataErrors: true);
                try { File.Delete(destination + ".bak"); } catch { /* ignore */ }
            }
            else
            {
                File.Move(source, destination);
            }
        }

        public IEnumerable<string> EnumerateFiles(string directory, string searchPattern)
        {
            if (!Directory.Exists(directory))
                yield break;
            foreach (string path in Directory.EnumerateFiles(directory, searchPattern))
                yield return path;
        }
    }

    /// In-memory filesystem for EditMode tests (injectable write/replace failures).
    public sealed class MemorySaveFileSystem : ISaveFileSystem
    {
        readonly Dictionary<string, byte[]> files =
            new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> directories =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public bool FailNextWrite { get; set; }
        public bool FailNextFlush { get; set; }
        public bool FailNextReplace { get; set; }

        public bool DirectoryExists(string path) =>
            directories.Contains(NormalizeDir(path));

        public void CreateDirectory(string path) => directories.Add(NormalizeDir(path));

        public bool FileExists(string path) => files.ContainsKey(NormalizeFile(path));

        public byte[] ReadAllBytes(string path)
        {
            if (!files.TryGetValue(NormalizeFile(path), out byte[] data))
                throw new FileNotFoundException("File not found.", path);
            var copy = new byte[data.Length];
            Buffer.BlockCopy(data, 0, copy, 0, data.Length);
            return copy;
        }

        public void WriteAllBytesFlushed(string path, byte[] data)
        {
            if (FailNextWrite)
            {
                FailNextWrite = false;
                throw new IOException("Simulated write failure.");
            }

            if (FailNextFlush)
            {
                FailNextFlush = false;
                throw new IOException("Simulated flush failure.");
            }

            if (data == null) throw new ArgumentNullException(nameof(data));
            var copy = new byte[data.Length];
            Buffer.BlockCopy(data, 0, copy, 0, data.Length);
            files[NormalizeFile(path)] = copy;
            directories.Add(NormalizeDir(Path.GetDirectoryName(path) ?? ""));
        }

        public void DeleteFile(string path) => files.Remove(NormalizeFile(path));

        public void ReplaceFile(string source, string destination)
        {
            if (FailNextReplace)
            {
                FailNextReplace = false;
                throw new IOException("Simulated replace failure.");
            }

            string src = NormalizeFile(source);
            string dst = NormalizeFile(destination);
            if (!files.TryGetValue(src, out byte[] data))
                throw new FileNotFoundException("Source not found.", source);
            files[dst] = data;
            files.Remove(src);
        }

        public IEnumerable<string> EnumerateFiles(string directory, string searchPattern)
        {
            string dir = NormalizeDir(directory);
            string pattern = searchPattern ?? "*";
            foreach (var kv in files)
            {
                string fileDir = NormalizeDir(Path.GetDirectoryName(kv.Key) ?? "");
                if (!string.Equals(fileDir, dir, StringComparison.OrdinalIgnoreCase))
                    continue;
                string name = Path.GetFileName(kv.Key);
                if (MatchSimple(name, pattern))
                    yield return kv.Key;
            }
        }

        static bool MatchSimple(string name, string pattern)
        {
            // Supports "*" prefix/suffix patterns used by the store.
            if (pattern == "*" || pattern == "*.*") return true;
            if (pattern.StartsWith("*", StringComparison.Ordinal)
                && name.EndsWith(pattern.Substring(1), StringComparison.OrdinalIgnoreCase))
                return true;
            return string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);
        }

        static string NormalizeFile(string path) =>
            (path ?? "").Replace('/', Path.DirectorySeparatorChar);

        static string NormalizeDir(string path)
        {
            string n = NormalizeFile(path).TrimEnd(Path.DirectorySeparatorChar);
            return n;
        }
    }
}
