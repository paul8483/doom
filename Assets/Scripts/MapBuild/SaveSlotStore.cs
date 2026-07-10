using System;
using System.Collections.Generic;
using System.IO;
using Doom.Game;
using UnityEngine;

namespace Doom.MapBuild
{
    /// Unity-facing save slot store. Production root is under
    /// <see cref="Application.persistentDataPath"/>; tests inject a
    /// <see cref="Doom.Game.SaveSlotStore"/> with a memory/temp filesystem.
    public sealed class SaveSlotStore : ISaveStorage
    {
        public const string SavesFolderName = "saves";

        readonly Doom.Game.SaveSlotStore inner;

        public SaveSlotStore()
            : this(Path.Combine(Application.persistentDataPath, SavesFolderName),
                new SystemSaveFileSystem())
        {
        }

        public SaveSlotStore(string rootDirectory, ISaveFileSystem fileSystem)
            : this(new Doom.Game.SaveSlotStore(rootDirectory, fileSystem))
        {
        }

        public SaveSlotStore(Doom.Game.SaveSlotStore inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public string RootDirectory => inner.RootDirectory;

        public void Write(string slotName, SaveGame save) => inner.Write(slotName, save);

        public bool TryRead(string slotName, out SaveGame save, out string error) =>
            inner.TryRead(slotName, out save, out error);

        public bool Exists(string slotName) => inner.Exists(slotName);

        public void Delete(string slotName) => inner.Delete(slotName);

        public IReadOnlyList<SaveSlotInfo> ListSlots() => inner.ListSlots();
    }
}
