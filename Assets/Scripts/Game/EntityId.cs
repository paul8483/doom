using System;

namespace Doom.Game
{
    /// Kind of stable world entity referenced by a save snapshot.
    public enum EntityKind : byte
    {
        None = 0,
        /// Index into the map's THINGS lump.
        MapThing = 1,
        /// Session-local SpawnId for drops / projectiles created at runtime.
        Spawned = 2,
    }

    /// Stable entity reference for save/load. Named SaveEntityId to avoid clashing
    /// with UnityEngine.EntityId (Unity 6). Never stores Unity instance IDs.
    public readonly struct SaveEntityId : IEquatable<SaveEntityId>
    {
        public EntityKind Kind { get; }
        /// THINGS index when Kind == MapThing; SpawnId when Kind == Spawned.
        public int Index { get; }

        SaveEntityId(EntityKind kind, int index)
        {
            Kind = kind;
            Index = index;
        }

        public static SaveEntityId None => default;

        public bool IsNone => Kind == EntityKind.None;

        public static SaveEntityId MapThing(int thingIndex)
        {
            if (thingIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(thingIndex));
            return new SaveEntityId(EntityKind.MapThing, thingIndex);
        }

        public static SaveEntityId Spawned(int spawnId)
        {
            if (spawnId < 0)
                throw new ArgumentOutOfRangeException(nameof(spawnId));
            return new SaveEntityId(EntityKind.Spawned, spawnId);
        }

        public static bool TryCreate(EntityKind kind, int index, out SaveEntityId id)
        {
            id = default;
            if (kind == EntityKind.None)
            {
                if (index != 0) return false;
                id = None;
                return true;
            }

            if (index < 0) return false;
            if (kind != EntityKind.MapThing && kind != EntityKind.Spawned) return false;
            id = new SaveEntityId(kind, index);
            return true;
        }

        public bool Equals(SaveEntityId other) => Kind == other.Kind && Index == other.Index;
        public override bool Equals(object obj) => obj is SaveEntityId other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((byte)Kind, Index);
        public override string ToString() =>
            Kind == EntityKind.None ? "None" : $"{Kind}:{Index}";

        public static bool operator ==(SaveEntityId a, SaveEntityId b) => a.Equals(b);
        public static bool operator !=(SaveEntityId a, SaveEntityId b) => !a.Equals(b);
    }
}
