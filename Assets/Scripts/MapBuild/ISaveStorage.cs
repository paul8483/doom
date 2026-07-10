using System.Collections.Generic;
using Doom.Game;

namespace Doom.MapBuild
{
    /// High-level save slot API used by menus / session host.
    public interface ISaveStorage
    {
        void Write(string slotName, SaveGame save);
        bool TryRead(string slotName, out SaveGame save, out string error);
        bool Exists(string slotName);
        void Delete(string slotName);
        IReadOnlyList<SaveSlotInfo> ListSlots();
    }
}
