using System;
using System.IO;
using System.Text;

namespace Doom.Game
{
    /// Envelope metadata readable without decoding the full player/world payload.
    public sealed class SaveGameHeader
    {
        public int Version { get; }
        public string MapName { get; }
        public string WadIdentity { get; }
        public int PayloadLength { get; }

        public SaveGameHeader(int version, string mapName, string wadIdentity, int payloadLength)
        {
            Version = version;
            MapName = mapName;
            WadIdentity = wadIdentity;
            PayloadLength = payloadLength;
        }
    }

    /// Little-endian binary encode/decode for <see cref="SaveGame"/>.
    /// Envelope: magic, schema version, WAD identity, map name, payload length, checksum.
    /// Checksum (FNV-1a 32) covers identity metadata + payload bytes.
    public static class SaveGameCodec
    {
        public const int MaxStringBytes = 512;
        public const int MaxPayloadBytes = 16 * 1024 * 1024;
        public const int MaxIdArrayCount = 8192;
        public const int MaxSectorCount = 8192;
        public const int MaxLineCount = 32768;
        public const int MaxThingCount = 8192;
        public const int MaxProjectileCount = 2048;
        public const int MaxSpawnedPickupCount = 2048;

        static readonly UTF8Encoding Utf8Strict = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

        public static byte[] Encode(SaveGame save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (save.Version != SaveGame.SchemaVersion)
                throw new SaveFormatException("Unsupported schema version for encode: " + save.Version);

            byte[] payload = EncodePayload(save);
            byte[] wadBytes = Utf8Strict.GetBytes(save.WadIdentity);
            byte[] mapBytes = Utf8Strict.GetBytes(save.MapName);
            if (wadBytes.Length > MaxStringBytes || mapBytes.Length > MaxStringBytes)
                throw new SaveFormatException("Identity string exceeds maximum length.");

            uint checksum = ComputeChecksum(wadBytes, mapBytes, payload);

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                w.Write(SaveGame.Magic);
                w.Write(save.Version);
                WriteBytes(w, wadBytes);
                WriteBytes(w, mapBytes);
                w.Write(payload.Length);
                w.Write(checksum);
                w.Write(payload);
                w.Flush();
                return ms.ToArray();
            }
        }

        public static SaveGame Decode(byte[] data) => Decode(data, expectedWadIdentity: null);

        /// <param name="expectedWadIdentity">
        /// When non-null, the envelope WAD identity must match exactly or decode fails.
        /// </param>
        public static SaveGame Decode(byte[] data, string expectedWadIdentity)
        {
            if (!TryDecode(data, expectedWadIdentity, out SaveGame save, out string error))
                throw new SaveFormatException(error);
            return save;
        }

        public static bool TryDecode(byte[] data, out SaveGame save, out string error) =>
            TryDecode(data, null, out save, out error);

        public static bool TryDecode(
            byte[] data, string expectedWadIdentity, out SaveGame save, out string error)
        {
            save = null;
            error = null;
            try
            {
                if (data == null || data.Length < 20)
                {
                    error = "Save data is truncated.";
                    return false;
                }

                using (var ms = new MemoryStream(data, writable: false))
                using (var r = new BinaryReader(ms, Encoding.ASCII, leaveOpen: true))
                {
                    uint magic = r.ReadUInt32();
                    if (magic != SaveGame.Magic)
                    {
                        error = "Bad save magic.";
                        return false;
                    }

                    int version = r.ReadInt32();
                    if (version < SaveGame.FirstSupportedSchemaVersion
                        || version > SaveGame.SchemaVersion)
                    {
                        error = "Unsupported save schema version: " + version;
                        return false;
                    }

                    byte[] wadBytes = ReadBoundedBytes(r, MaxStringBytes, "WAD identity");
                    byte[] mapBytes = ReadBoundedBytes(r, MaxStringBytes, "map name");
                    string wadIdentity = DecodeUtf8(wadBytes, "WAD identity");
                    string mapName = DecodeUtf8(mapBytes, "map name");

                    if (expectedWadIdentity != null
                        && !string.Equals(wadIdentity, expectedWadIdentity, StringComparison.Ordinal))
                    {
                        error = "WAD identity does not match the current WAD.";
                        return false;
                    }

                    int payloadLength = r.ReadInt32();
                    if (payloadLength < 0 || payloadLength > MaxPayloadBytes)
                    {
                        error = "Payload length out of range.";
                        return false;
                    }

                    uint expectedChecksum = r.ReadUInt32();
                    if (ms.Position + payloadLength > ms.Length)
                    {
                        error = "Save data is truncated.";
                        return false;
                    }

                    byte[] payload = r.ReadBytes(payloadLength);
                    if (payload.Length != payloadLength)
                    {
                        error = "Save data is truncated.";
                        return false;
                    }

                    uint actual = ComputeChecksum(wadBytes, mapBytes, payload);
                    if (actual != expectedChecksum)
                    {
                        error = "Save checksum mismatch.";
                        return false;
                    }

                    if (!TryDecodePayload(
                            payload, version, out PlayerSnapshot player, out WorldSnapshot world,
                            out error))
                        return false;

                    if (!SaveGame.TryCreate(
                            version, mapName, wadIdentity, player, world, out save, out error))
                        return false;

                    // Preserve the encoded version so older saves can be loaded and
                    // upgraded naturally the next time a fresh current save is captured.
                    if (save.Version != version)
                    {
                        error = "Decoded version mismatch.";
                        return false;
                    }

                    return true;
                }
            }
            catch (SaveFormatException ex)
            {
                error = ex.Message;
                return false;
            }
            catch (EndOfStreamException)
            {
                error = "Save data is truncated.";
                return false;
            }
            catch (DecoderFallbackException)
            {
                error = "Invalid UTF-8 in save identity string.";
                return false;
            }
            catch (IOException ex)
            {
                error = "Save read failed: " + ex.Message;
                return false;
            }
        }

        /// Reads envelope fields and verifies checksum without parsing the payload body.
        public static SaveGameHeader ReadHeader(byte[] data)
        {
            if (!TryReadHeader(data, verifyChecksum: true, out SaveGameHeader header, out string error))
                throw new SaveFormatException(error);
            return header;
        }

        public static bool TryReadHeader(
            byte[] data, bool verifyChecksum, out SaveGameHeader header, out string error)
        {
            header = null;
            error = null;
            try
            {
                if (data == null || data.Length < 20)
                {
                    error = "Save data is truncated.";
                    return false;
                }

                using (var ms = new MemoryStream(data, writable: false))
                using (var r = new BinaryReader(ms, Encoding.ASCII, leaveOpen: true))
                {
                    uint magic = r.ReadUInt32();
                    if (magic != SaveGame.Magic)
                    {
                        error = "Bad save magic.";
                        return false;
                    }

                    int version = r.ReadInt32();
                    byte[] wadBytes = ReadBoundedBytes(r, MaxStringBytes, "WAD identity");
                    byte[] mapBytes = ReadBoundedBytes(r, MaxStringBytes, "map name");
                    string wadIdentity = DecodeUtf8(wadBytes, "WAD identity");
                    string mapName = DecodeUtf8(mapBytes, "map name");

                    int payloadLength = r.ReadInt32();
                    if (payloadLength < 0 || payloadLength > MaxPayloadBytes)
                    {
                        error = "Payload length out of range.";
                        return false;
                    }

                    uint expectedChecksum = r.ReadUInt32();
                    if (ms.Position + payloadLength > ms.Length)
                    {
                        error = "Save data is truncated.";
                        return false;
                    }

                    if (verifyChecksum)
                    {
                        byte[] payload = r.ReadBytes(payloadLength);
                        if (payload.Length != payloadLength)
                        {
                            error = "Save data is truncated.";
                            return false;
                        }

                        if (ComputeChecksum(wadBytes, mapBytes, payload) != expectedChecksum)
                        {
                            error = "Save checksum mismatch.";
                            return false;
                        }
                    }

                    header = new SaveGameHeader(version, mapName, wadIdentity, payloadLength);
                    return true;
                }
            }
            catch (SaveFormatException ex)
            {
                error = ex.Message;
                return false;
            }
            catch (EndOfStreamException)
            {
                error = "Save data is truncated.";
                return false;
            }
            catch (DecoderFallbackException)
            {
                error = "Invalid UTF-8 in save identity string.";
                return false;
            }
        }

        public static uint ComputeChecksum(byte[] wadIdentityUtf8, byte[] mapNameUtf8, byte[] payload)
        {
            uint hash = 2166136261u;
            hash = Fnv1a(hash, wadIdentityUtf8);
            hash = Fnv1a(hash, mapNameUtf8);
            hash = Fnv1a(hash, payload);
            return hash;
        }

        static uint Fnv1a(uint hash, byte[] data)
        {
            if (data == null) return hash;
            for (int i = 0; i < data.Length; i++)
            {
                hash ^= data[i];
                hash *= 16777619u;
            }

            return hash;
        }

        static byte[] EncodePayload(SaveGame save)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                WritePlayer(w, save.Player);
                WriteWorld(w, save.World);
                w.Flush();
                return ms.ToArray();
            }
        }

        static bool TryDecodePayload(
            byte[] payload, int version,
            out PlayerSnapshot player, out WorldSnapshot world, out string error)
        {
            player = null;
            world = null;
            error = null;
            try
            {
                using (var ms = new MemoryStream(payload, writable: false))
                using (var r = new BinaryReader(ms, Encoding.ASCII, leaveOpen: true))
                {
                    if (!TryReadPlayer(r, version, out player, out error)) return false;
                    if (!TryReadWorld(r, version, out world, out error)) return false;
                    if (ms.Position != ms.Length)
                    {
                        error = "Trailing bytes in save payload.";
                        return false;
                    }

                    return true;
                }
            }
            catch (EndOfStreamException)
            {
                error = "Save data is truncated.";
                return false;
            }
            catch (SaveFormatException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        static void WritePlayer(BinaryWriter w, PlayerSnapshot p)
        {
            w.Write(p.X);
            w.Write(p.Y);
            w.Write(p.Z);
            w.Write(p.YawDegrees);
            w.Write(p.PitchDegrees);
            w.Write(p.Health);
            w.Write(p.Armor);
            w.Write((int)p.ArmorType);
            w.Write(p.Bullets);
            w.Write(p.Shells);
            WriteBool(w, p.HasBackpack);
            WriteBool(w, p.OwnsFist);
            WriteBool(w, p.OwnsPistol);
            WriteBool(w, p.OwnsShotgun);
            WriteBool(w, p.OwnsChaingun);
            w.Write((int)p.CurrentWeapon);
            WriteBool(w, p.HasPendingWeapon);
            w.Write((int)p.PendingWeapon);
            w.Write(p.KeyBits);
            WriteBool(w, p.Berserk);
            w.Write(p.IronFeetTics);
            w.Write(p.RandomIndex);
            w.Write(p.Rockets);
            WriteBool(w, p.OwnsRocketLauncher);
            WriteBool(w, p.OwnsChainsaw);
            w.Write(p.Cells);
            WriteBool(w, p.OwnsPlasmaRifle);
            WriteBool(w, p.OwnsBfg9000);
        }

        static bool TryReadPlayer(
            BinaryReader r, int version, out PlayerSnapshot player, out string error)
        {
            float x = r.ReadSingle();
            float y = r.ReadSingle();
            float z = r.ReadSingle();
            float yaw = r.ReadSingle();
            float pitch = r.ReadSingle();
            int health = r.ReadInt32();
            int armor = r.ReadInt32();
            int armorTypeRaw = r.ReadInt32();
            int bullets = r.ReadInt32();
            int shells = r.ReadInt32();
            bool hasBackpack = ReadBool(r);
            bool ownsFist = ReadBool(r);
            bool ownsPistol = ReadBool(r);
            bool ownsShotgun = ReadBool(r);
            bool ownsChaingun = ReadBool(r);
            int currentWeapon = r.ReadInt32();
            bool hasPending = ReadBool(r);
            int pendingWeapon = r.ReadInt32();
            int keyBits = r.ReadInt32();
            bool berserk = ReadBool(r);
            int ironFeet = r.ReadInt32();
            int randomIndex = r.ReadInt32();
            int rockets = 0;
            bool ownsRocketLauncher = false;
            bool ownsChainsaw = false;
            int cells = 0;
            bool ownsPlasmaRifle = false;
            bool ownsBfg9000 = false;
            if (version >= 2)
            {
                rockets = r.ReadInt32();
                ownsRocketLauncher = ReadBool(r);
            }
            if (version >= 3)
                ownsChainsaw = ReadBool(r);
            if (version >= 4)
            {
                cells = r.ReadInt32();
                ownsPlasmaRifle = ReadBool(r);
                ownsBfg9000 = ReadBool(r);
            }

            if (!Enum.IsDefined(typeof(ArmorKind), armorTypeRaw))
            {
                player = null;
                error = "Invalid armor type in save.";
                return false;
            }

            if (!Enum.IsDefined(typeof(WeaponId), currentWeapon)
                || !Enum.IsDefined(typeof(WeaponId), pendingWeapon))
            {
                player = null;
                error = "Invalid weapon id in save.";
                return false;
            }

            return PlayerSnapshot.TryCreate(
                x, y, z, yaw, pitch,
                health, armor, (ArmorKind)armorTypeRaw,
                bullets, shells, rockets, cells, hasBackpack,
                ownsFist, ownsPistol, ownsShotgun, ownsChaingun, ownsRocketLauncher,
                ownsChainsaw, ownsPlasmaRifle, ownsBfg9000,
                (WeaponId)currentWeapon, hasPending, (WeaponId)pendingWeapon,
                keyBits, berserk, ironFeet, randomIndex,
                out player, out error);
        }

        static void WriteWorld(BinaryWriter w, WorldSnapshot world)
        {
            w.Write(world.GameTic);
            w.Write(world.NextSpawnId);
            WriteStats(w, world.Stats);
            WriteIntArray(w, world.KillIds, MaxIdArrayCount);
            WriteIntArray(w, world.ItemIds, MaxIdArrayCount);
            WriteIntArray(w, world.SecretIds, MaxIdArrayCount);

            WriteCount(w, world.Sectors.Length, MaxSectorCount, "sectors");
            for (int i = 0; i < world.Sectors.Length; i++)
                WriteSector(w, world.Sectors[i]);

            WriteCount(w, world.Lines.Length, MaxLineCount, "lines");
            for (int i = 0; i < world.Lines.Length; i++)
                WriteLine(w, world.Lines[i]);

            WriteCount(w, world.Things.Length, MaxThingCount, "things");
            for (int i = 0; i < world.Things.Length; i++)
                WriteThing(w, world.Things[i]);

            WriteCount(w, world.Projectiles.Length, MaxProjectileCount, "projectiles");
            for (int i = 0; i < world.Projectiles.Length; i++)
                WriteProjectile(w, world.Projectiles[i]);

            WriteCount(w, world.SpawnedPickups.Length, MaxSpawnedPickupCount, "spawned pickups");
            for (int i = 0; i < world.SpawnedPickups.Length; i++)
                WritePickup(w, world.SpawnedPickups[i]);
        }

        static bool TryReadWorld(
            BinaryReader r, int version, out WorldSnapshot world, out string error)
        {
            world = null;
            int gameTic = r.ReadInt32();
            int nextSpawnId = r.ReadInt32();
            var stats = ReadStats(r);

            if (!TryReadIntArray(r, MaxIdArrayCount, "kill ids", out int[] killIds, out error))
                return false;
            if (!TryReadIntArray(r, MaxIdArrayCount, "item ids", out int[] itemIds, out error))
                return false;
            if (!TryReadIntArray(r, MaxIdArrayCount, "secret ids", out int[] secretIds, out error))
                return false;

            if (!TryReadCount(r, MaxSectorCount, "sectors", out int sectorCount, out error))
                return false;
            var sectors = new SectorSnapshot[sectorCount];
            for (int i = 0; i < sectorCount; i++)
                sectors[i] = ReadSector(r, version);

            if (!TryReadCount(r, MaxLineCount, "lines", out int lineCount, out error))
                return false;
            var lines = new LineSnapshot[lineCount];
            for (int i = 0; i < lineCount; i++)
                lines[i] = ReadLine(r);

            if (!TryReadCount(r, MaxThingCount, "things", out int thingCount, out error))
                return false;
            var things = new ThingSnapshot[thingCount];
            for (int i = 0; i < thingCount; i++)
            {
                if (!TryReadThing(r, version, out things[i], out error))
                    return false;
            }

            if (!TryReadCount(r, MaxProjectileCount, "projectiles", out int projCount, out error))
                return false;
            var projectiles = new ProjectileSnapshot[projCount];
            for (int i = 0; i < projCount; i++)
            {
                if (!TryReadProjectile(r, version, out projectiles[i], out error))
                    return false;
            }

            if (!TryReadCount(r, MaxSpawnedPickupCount, "spawned pickups", out int pickupCount,
                    out error))
                return false;
            var pickups = new SpawnedPickupSnapshot[pickupCount];
            for (int i = 0; i < pickupCount; i++)
                pickups[i] = ReadPickup(r, version);

            return WorldSnapshot.TryCreate(
                gameTic, nextSpawnId, stats,
                killIds, itemIds, secretIds,
                sectors, lines, things, projectiles, pickups,
                out world, out error);
        }

        static void WriteStats(BinaryWriter w, LevelStatsSnapshot s)
        {
            w.Write(s.Kills);
            w.Write(s.KillTotal);
            w.Write(s.Items);
            w.Write(s.ItemTotal);
            w.Write(s.Secrets);
            w.Write(s.SecretTotal);
            w.Write(s.Tics);
        }

        static LevelStatsSnapshot ReadStats(BinaryReader r) =>
            new LevelStatsSnapshot(
                r.ReadInt32(), r.ReadInt32(),
                r.ReadInt32(), r.ReadInt32(),
                r.ReadInt32(), r.ReadInt32(),
                r.ReadInt32());

        static void WriteSector(BinaryWriter w, SectorSnapshot s)
        {
            w.Write(s.Index);
            w.Write(s.FloorHeight);
            w.Write(s.CeilingHeight);
            w.Write(s.LightLevel);
            WriteBool(w, s.HasMover);
            w.Write((byte)s.MoverPlane);
            w.Write((byte)s.MoverPhase);
            w.Write(s.MoverDirection);
            w.Write(s.MoverTarget);
            w.Write(s.MoverSpeed);
            w.Write(s.MoverWaitTics);
            w.Write(s.LightCount);
            w.Write((byte)s.MoverBehavior);
            WriteBool(w, s.MoverCycle);
            w.Write(s.MoverOrigin);
            WriteBool(w, s.MoverSilent);
        }

        static SectorSnapshot ReadSector(BinaryReader r, int version)
        {
            int index = r.ReadInt32();
            float floor = r.ReadSingle();
            float ceiling = r.ReadSingle();
            int light = r.ReadInt32();
            bool hasMover = ReadBool(r);
            byte plane = r.ReadByte();
            byte phase = r.ReadByte();
            int dir = r.ReadInt32();
            float target = r.ReadSingle();
            float speed = r.ReadSingle();
            int wait = r.ReadInt32();
            int lightCount = version >= 5 ? r.ReadInt32() : 0;
            byte behavior = version >= 6 ? r.ReadByte() : (byte)MoverBehavior.OneShot;
            bool cycle = version >= 6 && ReadBool(r);
            float origin = version >= 6 ? r.ReadSingle() : 0f;
            bool silent = version >= 8 && ReadBool(r);
            if (!Enum.IsDefined(typeof(MoverPlane), plane)
                || !Enum.IsDefined(typeof(MoverPhase), phase)
                || !Enum.IsDefined(typeof(MoverBehavior), behavior))
                throw new SaveFormatException("Invalid mover enum in sector snapshot.");
            return new SectorSnapshot(
                index, floor, ceiling, light, hasMover,
                (MoverPlane)plane, (MoverPhase)phase, dir, target, speed, wait, lightCount,
                (MoverBehavior)behavior, cycle, origin, silent);
        }

        static void WriteLine(BinaryWriter w, LineSnapshot line)
        {
            w.Write(line.Index);
            WriteBool(w, line.Fired);
            WriteBool(w, line.SwitchOn);
        }

        static LineSnapshot ReadLine(BinaryReader r) =>
            new LineSnapshot(r.ReadInt32(), ReadBool(r), ReadBool(r));

        static void WriteThing(BinaryWriter w, ThingSnapshot t)
        {
            w.Write(t.MapThingIndex);
            WriteBool(w, t.Present);
            w.Write(t.X);
            w.Write(t.Y);
            w.Write(t.Z);
            w.Write(t.AngleDegrees);
            w.Write(t.Health);
            w.Write(t.Frame);
            w.Write(t.Flags);
            WriteEntityId(w, t.Target);
            // v7: monster brain bookkeeping.
            var ai = t.Ai;
            WriteBool(w, ai.Present);
            if (ai.Present)
            {
                w.Write((byte)ai.State);
                w.Write(ai.SeqIndex);
                w.Write(ai.Tics);
                w.Write((byte)ai.Dir);
                w.Write(ai.Moves);
                w.Write(ai.Reaction);
                WriteBool(w, ai.Attacked);
                WriteBool(w, ai.Hit);
                WriteBool(w, ai.Extreme);
            }
        }

        static bool TryReadThing(
            BinaryReader r, int version, out ThingSnapshot thing, out string error)
        {
            thing = null;
            error = null;
            int index = r.ReadInt32();
            bool present = ReadBool(r);
            float x = r.ReadSingle();
            float y = r.ReadSingle();
            float z = r.ReadSingle();
            float angle = r.ReadSingle();
            int health = r.ReadInt32();
            int frame = r.ReadInt32();
            int flags = r.ReadInt32();
            if (!TryReadEntityId(r, out SaveEntityId target, out error))
                return false;
            var ai = MonsterAiSnapshot.None;
            if (version >= 7 && ReadBool(r))
            {
                int stateRaw = r.ReadByte();
                int seqIndex = r.ReadInt32();
                int tics = r.ReadInt32();
                int dirRaw = r.ReadByte();
                int moves = r.ReadInt32();
                int reaction = r.ReadInt32();
                bool attacked = ReadBool(r);
                bool hit = ReadBool(r);
                bool extreme = ReadBool(r);
                if (!Enum.IsDefined(typeof(MonsterState), stateRaw) ||
                    !Enum.IsDefined(typeof(Dir8), dirRaw))
                {
                    error = "Invalid monster AI state in save.";
                    return false;
                }
                ai = new MonsterAiSnapshot(
                    (MonsterState)stateRaw, seqIndex, tics, (Dir8)dirRaw, moves,
                    reaction, attacked, hit, extreme);
            }
            thing = new ThingSnapshot(
                index, present, x, y, z, angle, health, frame, flags, target, ai);
            return true;
        }

        static void WriteProjectile(BinaryWriter w, ProjectileSnapshot p)
        {
            w.Write(p.SpawnId);
            w.Write(p.Type);
            WriteEntityId(w, p.Owner);
            w.Write(p.X);
            w.Write(p.Y);
            w.Write(p.Z);
            w.Write(p.VelX);
            w.Write(p.VelY);
            w.Write(p.VelZ);
            w.Write(p.RemainingLife);
            w.Write((int)p.Phase);
            w.Write(p.FrameIndex);
            w.Write(p.ShotDirX);
            w.Write(p.ShotDirY);
            w.Write(p.ShotDirZ);
            WriteBool(w, p.SprayApplied);
        }

        static bool TryReadProjectile(
            BinaryReader r, int version, out ProjectileSnapshot projectile, out string error)
        {
            projectile = null;
            int spawnId = r.ReadInt32();
            int type = r.ReadInt32();
            if (!TryReadEntityId(r, out SaveEntityId owner, out error))
                return false;

            float x = r.ReadSingle();
            float y = r.ReadSingle();
            float z = r.ReadSingle();
            float velX = r.ReadSingle();
            float velY = r.ReadSingle();
            float velZ = r.ReadSingle();
            float remainingLife = r.ReadSingle();
            var phase = ProjectilePhase.Flying;
            int frameIndex = 0;
            float shotDirX = 0f;
            float shotDirY = 0f;
            float shotDirZ = 0f;
            bool sprayApplied = false;
            if (version >= 4)
            {
                int phaseRaw = r.ReadInt32();
                if (!Enum.IsDefined(typeof(ProjectilePhase), phaseRaw))
                {
                    error = "Invalid projectile phase in save.";
                    return false;
                }
                phase = (ProjectilePhase)phaseRaw;
                frameIndex = r.ReadInt32();
                shotDirX = r.ReadSingle();
                shotDirY = r.ReadSingle();
                shotDirZ = r.ReadSingle();
                sprayApplied = ReadBool(r);
            }

            projectile = new ProjectileSnapshot(
                spawnId, type, owner,
                x, y, z, velX, velY, velZ, remainingLife,
                phase, frameIndex, shotDirX, shotDirY, shotDirZ, sprayApplied);
            return true;
        }

        static void WritePickup(BinaryWriter w, SpawnedPickupSnapshot p)
        {
            w.Write(p.SpawnId);
            w.Write(p.DoomedNum);
            w.Write(p.X);
            w.Write(p.Y);
            w.Write(p.Z);
            WriteBool(w, p.Dropped); // v7
        }

        static SpawnedPickupSnapshot ReadPickup(BinaryReader r, int version)
        {
            int spawnId = r.ReadInt32();
            int doomedNum = r.ReadInt32();
            float x = r.ReadSingle();
            float y = r.ReadSingle();
            float z = r.ReadSingle();
            bool dropped = version >= 7 && ReadBool(r);
            return new SpawnedPickupSnapshot(spawnId, doomedNum, x, y, z, dropped);
        }

        static void WriteEntityId(BinaryWriter w, SaveEntityId id)
        {
            w.Write((byte)id.Kind);
            w.Write(id.Index);
        }

        static bool TryReadEntityId(BinaryReader r, out SaveEntityId id, out string error)
        {
            error = null;
            byte kindRaw = r.ReadByte();
            int index = r.ReadInt32();
            if (!SaveEntityId.TryCreate((EntityKind)kindRaw, index, out id))
            {
                error = "Invalid entity id in save.";
                return false;
            }

            return true;
        }

        static void WriteIntArray(BinaryWriter w, int[] values, int maxCount)
        {
            WriteCount(w, values.Length, maxCount, "id array");
            for (int i = 0; i < values.Length; i++)
                w.Write(values[i]);
        }

        static bool TryReadIntArray(
            BinaryReader r, int maxCount, string label, out int[] values, out string error)
        {
            values = null;
            if (!TryReadCount(r, maxCount, label, out int count, out error))
                return false;
            values = count == 0 ? Array.Empty<int>() : new int[count];
            for (int i = 0; i < count; i++)
                values[i] = r.ReadInt32();
            return true;
        }

        static void WriteCount(BinaryWriter w, int count, int max, string label)
        {
            if (count < 0 || count > max)
                throw new SaveFormatException("Count out of range for " + label + ".");
            w.Write(count);
        }

        static bool TryReadCount(
            BinaryReader r, int max, string label, out int count, out string error)
        {
            count = r.ReadInt32();
            if (count < 0 || count > max)
            {
                error = "Count out of range for " + label + ".";
                return false;
            }

            error = null;
            return true;
        }

        static void WriteBytes(BinaryWriter w, byte[] bytes)
        {
            w.Write(bytes.Length);
            w.Write(bytes);
        }

        static byte[] ReadBoundedBytes(BinaryReader r, int max, string label)
        {
            int length = r.ReadInt32();
            if (length < 0 || length > max)
                throw new SaveFormatException(label + " length out of range.");
            byte[] bytes = r.ReadBytes(length);
            if (bytes.Length != length)
                throw new SaveFormatException("Save data is truncated.");
            return bytes;
        }

        static string DecodeUtf8(byte[] bytes, string label)
        {
            try
            {
                return Utf8Strict.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                throw new SaveFormatException("Invalid UTF-8 in " + label + ".");
            }
        }

        static void WriteBool(BinaryWriter w, bool value) => w.Write(value ? (byte)1 : (byte)0);

        static bool ReadBool(BinaryReader r)
        {
            byte b = r.ReadByte();
            if (b == 0) return false;
            if (b == 1) return true;
            throw new SaveFormatException("Invalid boolean value in save.");
        }
    }
}
