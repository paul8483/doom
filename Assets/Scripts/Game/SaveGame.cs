using System;

namespace Doom.Game
{
    /// Top-level full-world save: identity metadata + player + world snapshots.
    /// Binary encoding lives in <c>SaveGameCodec</c> (Task 12); this type is the
    /// in-memory contract.
    public sealed class SaveGame : IEquatable<SaveGame>
    {
        public const int FirstSupportedSchemaVersion = 1;
        public const int SchemaVersion = 5;
        /// ASCII 'D','S','A','V' — little-endian uint 0x56415344.
        public const uint Magic = 0x56415344;

        public int Version { get; }
        public string MapName { get; }
        /// Stable WAD identity string (length + content hash), computed at session start.
        public string WadIdentity { get; }
        public PlayerSnapshot Player { get; }
        public WorldSnapshot World { get; }

        public SaveGame(
            int version,
            string mapName,
            string wadIdentity,
            PlayerSnapshot player,
            WorldSnapshot world)
        {
            Version = version;
            MapName = mapName;
            WadIdentity = wadIdentity;
            Player = player;
            World = world;
        }

        public static bool TryCreate(
            string mapName,
            string wadIdentity,
            PlayerSnapshot player,
            WorldSnapshot world,
            out SaveGame save,
            out string error)
            => TryCreate(
                SchemaVersion, mapName, wadIdentity, player, world,
                out save, out error);

        public static bool TryCreate(
            int version,
            string mapName,
            string wadIdentity,
            PlayerSnapshot player,
            WorldSnapshot world,
            out SaveGame save,
            out string error)
        {
            save = null;
            error = null;

            if (version < FirstSupportedSchemaVersion || version > SchemaVersion)
            {
                error = "Unsupported save schema version: " + version;
                return false;
            }

            if (player == null)
            {
                error = "Player snapshot is required.";
                return false;
            }

            if (world == null)
            {
                error = "World snapshot is required.";
                return false;
            }

            if (string.IsNullOrEmpty(wadIdentity))
            {
                error = "WAD identity is required.";
                return false;
            }

            if (!CampaignRoute.TryNormalize(mapName, out string canonical))
            {
                error = "Invalid map name.";
                return false;
            }

            save = new SaveGame(version, canonical, wadIdentity, player, world);
            return true;
        }

        public bool Equals(SaveGame other)
        {
            if (other is null) return false;
            return Version == other.Version
                   && string.Equals(MapName, other.MapName, StringComparison.Ordinal)
                   && string.Equals(WadIdentity, other.WadIdentity, StringComparison.Ordinal)
                   && Equals(Player, other.Player)
                   && Equals(World, other.World);
        }

        public override bool Equals(object obj) => Equals(obj as SaveGame);

        public override int GetHashCode() =>
            HashCode.Combine(Version, MapName, WadIdentity,
                Player?.GetHashCode() ?? 0, World?.GetHashCode() ?? 0);
    }
}
