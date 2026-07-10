using System.Collections.Generic;

namespace Doom.Game
{
    /// Filesystem operations used by <see cref="SaveSlotStore"/>.
    /// Production uses <see cref="SystemSaveFileSystem"/>; tests inject memory/temp adapters.
    public interface ISaveFileSystem
    {
        bool DirectoryExists(string path);
        void CreateDirectory(string path);
        bool FileExists(string path);
        byte[] ReadAllBytes(string path);
        /// Writes all bytes and flushes to durable storage before returning.
        void WriteAllBytesFlushed(string path, byte[] data);
        void DeleteFile(string path);
        /// Atomically replaces <paramref name="destination"/> with <paramref name="source"/>.
        /// On success <paramref name="source"/> no longer exists.
        void ReplaceFile(string source, string destination);
        IEnumerable<string> EnumerateFiles(string directory, string searchPattern);
    }
}
