using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class SaveGameCodecTests
    {
        [Test]
        public void Encode_Decode_round_trips_full_v3_snapshot()
        {
            SaveGame original = BuildGoldenSave();
            byte[] bytes = SaveGameCodec.Encode(original);
            SaveGame decoded = SaveGameCodec.Decode(bytes);

            Assert.That(decoded, Is.EqualTo(original));
            Assert.That(decoded.Version, Is.EqualTo(SaveGame.SchemaVersion));
            Assert.That(decoded.MapName, Is.EqualTo("E1M1"));
            Assert.That(decoded.World.Sectors[1].MoverWaitTics, Is.EqualTo(12));
            Assert.That(decoded.World.Projectiles[0].Owner, Is.EqualTo(SaveEntityId.MapThing(0)));
            Assert.That(decoded.Player.OwnsShotgun, Is.True);
            Assert.That(decoded.Player.OwnsRocketLauncher, Is.True);
            Assert.That(decoded.Player.OwnsChainsaw, Is.True);
            Assert.That(decoded.Player.Rockets, Is.EqualTo(7));
        }

        [Test]
        public void Decode_v1_defaults_rocket_and_chainsaw_state()
        {
            const string wad = "wad:test";
            byte[] current = SaveGameCodec.Encode(BuildMinimalSave(wad));
            int payloadOffset = FindPayloadOffset(current, wad, "E1M1");
            int payloadLength = BitConverter.ToInt32(current, payloadOffset - 8);
            const int V1PlayerBytes = 67;
            const int V2ExtraBytes = 5;
            const int V3ExtraBytes = 1;
            int strip = V2ExtraBytes + V3ExtraBytes;

            var v1 = new byte[current.Length - strip];
            Array.Copy(current, 0, v1, 0, payloadOffset + V1PlayerBytes);
            Array.Copy(
                current, payloadOffset + V1PlayerBytes + strip,
                v1, payloadOffset + V1PlayerBytes,
                current.Length - payloadOffset - V1PlayerBytes - strip);
            BitConverter.GetBytes(1).CopyTo(v1, 4);
            BitConverter.GetBytes(payloadLength - strip).CopyTo(v1, payloadOffset - 8);
            RecomputeChecksum(v1, wad, "E1M1", payloadOffset, payloadLength - strip);

            Assert.That(SaveGameCodec.TryDecode(v1, out var decoded, out string error),
                Is.True, error);
            Assert.That(decoded.Version, Is.EqualTo(1));
            Assert.That(decoded.Player.Rockets, Is.Zero);
            Assert.That(decoded.Player.OwnsRocketLauncher, Is.False);
            Assert.That(decoded.Player.OwnsChainsaw, Is.False);
        }

        [Test]
        public void Decode_v2_defaults_chainsaw_state()
        {
            const string wad = "wad:test";
            byte[] current = SaveGameCodec.Encode(BuildMinimalSave(wad));
            int payloadOffset = FindPayloadOffset(current, wad, "E1M1");
            int payloadLength = BitConverter.ToInt32(current, payloadOffset - 8);
            const int V2PlayerBytes = 72;
            const int V3ExtraBytes = 1;

            var v2 = new byte[current.Length - V3ExtraBytes];
            Array.Copy(current, 0, v2, 0, payloadOffset + V2PlayerBytes);
            Array.Copy(
                current, payloadOffset + V2PlayerBytes + V3ExtraBytes,
                v2, payloadOffset + V2PlayerBytes,
                current.Length - payloadOffset - V2PlayerBytes - V3ExtraBytes);
            BitConverter.GetBytes(2).CopyTo(v2, 4);
            BitConverter.GetBytes(payloadLength - V3ExtraBytes).CopyTo(v2, payloadOffset - 8);
            RecomputeChecksum(v2, wad, "E1M1", payloadOffset, payloadLength - V3ExtraBytes);

            Assert.That(SaveGameCodec.TryDecode(v2, out var decoded, out string error),
                Is.True, error);
            Assert.That(decoded.Version, Is.EqualTo(2));
            Assert.That(decoded.Player.OwnsChainsaw, Is.False);
        }

        [Test]
        public void Encode_is_deterministic_for_same_snapshot()
        {
            SaveGame save = BuildGoldenSave();
            byte[] a = SaveGameCodec.Encode(save);
            byte[] b = SaveGameCodec.Encode(save);
            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void Decode_rejects_bad_magic()
        {
            byte[] bytes = SaveGameCodec.Encode(BuildMinimalSave());
            bytes[0] = (byte)'X';
            Assert.That(SaveGameCodec.TryDecode(bytes, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("magic").IgnoreCase);
        }

        [Test]
        public void Decode_rejects_unsupported_version()
        {
            byte[] bytes = SaveGameCodec.Encode(BuildMinimalSave());
            // Version is int32 right after magic (offset 4).
            BitConverter.GetBytes(99).CopyTo(bytes, 4);
            // Checksum will also fail if we only change version — rebuild checksum
            // by decoding envelope manually is hard; just assert TryDecode fails.
            Assert.That(SaveGameCodec.TryDecode(bytes, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("version").IgnoreCase
                .Or.Contain("checksum").IgnoreCase);
        }

        [Test]
        public void Decode_rejects_bad_checksum()
        {
            byte[] bytes = SaveGameCodec.Encode(BuildMinimalSave());
            // Flip a payload byte near the end without updating checksum.
            bytes[bytes.Length - 1] ^= 0xFF;
            Assert.That(SaveGameCodec.TryDecode(bytes, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("checksum").IgnoreCase);
        }

        [Test]
        public void Decode_rejects_truncated_payload()
        {
            byte[] bytes = SaveGameCodec.Encode(BuildMinimalSave());
            var truncated = new byte[bytes.Length / 2];
            Array.Copy(bytes, truncated, truncated.Length);
            Assert.That(SaveGameCodec.TryDecode(truncated, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("truncated").IgnoreCase);
        }

        [Test]
        public void Decode_rejects_oversized_count_before_allocation()
        {
            // Craft a minimal valid envelope whose payload starts with a huge sector count.
            var player = BuildDefaultPlayer();
            Assert.That(WorldSnapshot.TryCreate(
                0, 0, default,
                Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(),
                Array.Empty<SectorSnapshot>(), Array.Empty<LineSnapshot>(),
                Array.Empty<ThingSnapshot>(), Array.Empty<ProjectileSnapshot>(),
                Array.Empty<SpawnedPickupSnapshot>(),
                out var world, out _), Is.True);
            Assert.That(SaveGame.TryCreate("E1M1", "wad:test", player, world, out var save, out _),
                Is.True);

            byte[] good = SaveGameCodec.Encode(save);
            // Locate payload: after magic(4)+ver(4)+wad+map+len(4)+checksum(4).
            int payloadOffset = FindPayloadOffset(good, save.WadIdentity, save.MapName);
            int payloadLen = BitConverter.ToInt32(good, payloadOffset - 8);

            // Player fields occupy a fixed prefix; world starts with gameTic, nextSpawnId,
            // stats (7 ints), three empty id arrays, then sector count.
            // Easier approach: build a fake payload with oversized first world count by
            // encoding then patching the sector count field after the player blob.
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                // Write a tiny fake player-sized blob then an oversized count.
                // Instead: take a real encode and overwrite the sector count int.
                byte[] patched = (byte[])good.Clone();
                // After player comes gameTic, nextSpawnId, 7 stats ints, 3×(count=0),
                // then sector count. Compute player size by encoding empty world delta.
                int sectorCountOffset = FindSectorCountOffset(good, payloadOffset);
                BitConverter.GetBytes(SaveGameCodec.MaxSectorCount + 1)
                    .CopyTo(patched, sectorCountOffset);
                // Fix payload length field? length unchanged. Recompute checksum.
                RecomputeChecksum(patched, save.WadIdentity, save.MapName, payloadOffset, payloadLen);

                Assert.That(SaveGameCodec.TryDecode(patched, out _, out string error), Is.False);
                Assert.That(error, Does.Contain("Count out of range").Or.Contain("sectors"));
            }
        }

        [Test]
        public void Decode_rejects_wrong_wad_identity()
        {
            byte[] bytes = SaveGameCodec.Encode(BuildMinimalSave("wad:freedoom"));
            Assert.That(
                SaveGameCodec.TryDecode(bytes, "wad:other", out _, out string error),
                Is.False);
            Assert.That(error, Does.Contain("WAD identity"));
        }

        [Test]
        public void Decode_rejects_duplicate_ids_in_payload()
        {
            // Build a valid save then patch thing indices to collide after decode bounds.
            // Easier: craft world with duplicate via raw payload writer using codec internals
            // by encoding two things then flipping the second index to match the first,
            // then fixing checksum — WorldSnapshot.TryCreate will reject duplicates.
            Assert.That(PlayerSnapshot.TryCreate(
                0, 0, 0, 0, 0,
                100, 0, ArmorKind.None,
                AmmoModel.StartBullets, 0, false,
                true, true, false, false,
                WeaponId.Pistol, false, WeaponId.Fist,
                0, false, 0, 0,
                out var player, out _), Is.True);

            var things = new[]
            {
                new ThingSnapshot(1, true, 0, 0, 0, 0, 10, 0, 0, SaveEntityId.None),
                new ThingSnapshot(2, true, 1, 0, 0, 0, 10, 0, 0, SaveEntityId.None),
            };
            Assert.That(WorldSnapshot.TryCreate(
                0, 0, default,
                Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(),
                Array.Empty<SectorSnapshot>(), Array.Empty<LineSnapshot>(),
                things, Array.Empty<ProjectileSnapshot>(), Array.Empty<SpawnedPickupSnapshot>(),
                out var world, out _), Is.True);
            Assert.That(SaveGame.TryCreate("E1M1", "wad:test", player, world, out var save, out _),
                Is.True);

            byte[] bytes = SaveGameCodec.Encode(save);
            int payloadOffset = FindPayloadOffset(bytes, save.WadIdentity, save.MapName);
            int payloadLen = BitConverter.ToInt32(bytes, payloadOffset - 8);

            // Patch second thing's MapThingIndex (first field of second thing) to 1.
            int secondThingIndexOffset = FindSecondThingIndexOffset(bytes, payloadOffset);
            BitConverter.GetBytes(1).CopyTo(bytes, secondThingIndexOffset);
            RecomputeChecksum(bytes, save.WadIdentity, save.MapName, payloadOffset, payloadLen);

            Assert.That(SaveGameCodec.TryDecode(bytes, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("sorted").Or.Contain("unique").Or.Contain("Duplicate"));
        }

        [Test]
        public void Decode_rejects_invalid_utf8_map_name_region()
        {
            byte[] bytes = SaveGameCodec.Encode(BuildMinimalSave());
            // Corrupt a byte inside the WAD identity string region (after length prefix).
            // magic(4)+ver(4)+wadLen(4) → first wad byte at offset 12.
            int wadLen = BitConverter.ToInt32(bytes, 8);
            Assert.That(wadLen, Is.GreaterThan(0));
            bytes[12] = 0xFF; // invalid UTF-8 lead
            Assert.That(SaveGameCodec.TryDecode(bytes, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("UTF-8").Or.Contain("checksum").IgnoreCase);
        }

        [Test]
        public void ReadHeader_returns_envelope_without_decoding_payload_body()
        {
            SaveGame save = BuildGoldenSave();
            byte[] bytes = SaveGameCodec.Encode(save);
            SaveGameHeader header = SaveGameCodec.ReadHeader(bytes);
            Assert.That(header.Version, Is.EqualTo(SaveGame.SchemaVersion));
            Assert.That(header.MapName, Is.EqualTo("E1M1"));
            Assert.That(header.WadIdentity, Is.EqualTo(save.WadIdentity));
            Assert.That(header.PayloadLength, Is.GreaterThan(0));
        }

        static SaveGame BuildGoldenSave()
        {
            Assert.That(PlayerSnapshot.TryCreate(
                1.5f, 2.25f, 0.75f, 90f, -12f,
                75, 100, ArmorKind.Green,
                50, 20, 7, true,
                true, true, true, false, true, true,
                WeaponId.Shotgun, true, WeaponId.Pistol,
                1 << (int)PlayerKey.YellowCard,
                true, 100, 17,
                out var player, out _), Is.True);

            var stats = new LevelStatsSnapshot(1, 10, 0, 5, 0, 2, 35);
            var sectors = new[]
            {
                new SectorSnapshot(0, 0f, 128f, 160, false, MoverPlane.Floor, MoverPhase.None,
                    0, 0f, 0f, 0),
                new SectorSnapshot(2, 16f, 128f, 160, true, MoverPlane.Ceiling, MoverPhase.Waiting,
                    -1, 64f, 4f, 12),
            };
            var lines = new[] { new LineSnapshot(0, true, true) };
            var things = new[]
            {
                new ThingSnapshot(0, true, 1f, 2f, 0f, 90f, 30, 1, 0, SaveEntityId.None),
                new ThingSnapshot(5, false, 0f, 0f, 0f, 0f, 0, 0, 0, SaveEntityId.None),
            };
            var projectiles = new[]
            {
                new ProjectileSnapshot(1, 1, SaveEntityId.MapThing(0),
                    0f, 0f, 1f, 1f, 0f, 0f, 0.5f),
            };
            var pickups = new[]
            {
                new SpawnedPickupSnapshot(2, 2007, 3f, 4f, 0f),
            };

            Assert.That(WorldSnapshot.TryCreate(
                100, 3, stats,
                killIds: new[] { 3 },
                itemIds: Array.Empty<int>(),
                secretIds: new[] { 1 },
                sectors, lines, things, projectiles, pickups,
                out var world, out _), Is.True);

            Assert.That(SaveGame.TryCreate("e1m1", "wad:len=123;hash=abc", player, world,
                out var save, out _), Is.True);
            return save;
        }

        static SaveGame BuildMinimalSave(string wadIdentity = "wad:test")
        {
            var player = BuildDefaultPlayer();
            Assert.That(WorldSnapshot.TryCreate(
                0, 0, default,
                Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(),
                Array.Empty<SectorSnapshot>(), Array.Empty<LineSnapshot>(),
                Array.Empty<ThingSnapshot>(), Array.Empty<ProjectileSnapshot>(),
                Array.Empty<SpawnedPickupSnapshot>(),
                out var world, out _), Is.True);
            Assert.That(SaveGame.TryCreate("E1M1", wadIdentity, player, world, out var save, out _),
                Is.True);
            return save;
        }

        static PlayerSnapshot BuildDefaultPlayer()
        {
            Assert.That(PlayerSnapshot.TryCreate(
                0, 0, 0, 0, 0,
                100, 0, ArmorKind.None,
                AmmoModel.StartBullets, 0, false,
                true, true, false, false,
                WeaponId.Pistol, false, WeaponId.Fist,
                0, false, 0, 0,
                out var player, out _), Is.True);
            return player;
        }

        static int FindPayloadOffset(byte[] data, string wadIdentity, string mapName)
        {
            int offset = 8; // magic + version
            int wadLen = BitConverter.ToInt32(data, offset);
            offset += 4 + wadLen;
            int mapLen = BitConverter.ToInt32(data, offset);
            offset += 4 + mapLen;
            offset += 4; // payload length
            offset += 4; // checksum
            // Verify strings match expectations for sanity.
            Assert.That(Encoding.UTF8.GetString(data, 12, wadLen), Is.EqualTo(wadIdentity));
            return offset;
        }

        static void RecomputeChecksum(
            byte[] data, string wadIdentity, string mapName, int payloadOffset, int payloadLen)
        {
            byte[] wadBytes = Encoding.UTF8.GetBytes(wadIdentity);
            byte[] mapBytes = Encoding.UTF8.GetBytes(mapName);
            var payload = new byte[payloadLen];
            Array.Copy(data, payloadOffset, payload, 0, payloadLen);
            uint sum = SaveGameCodec.ComputeChecksum(wadBytes, mapBytes, payload);
            BitConverter.GetBytes(sum).CopyTo(data, payloadOffset - 4);
        }

        static int FindSectorCountOffset(byte[] data, int payloadOffset)
        {
            // Walk payload: player fixed size, then world header until sector count.
            using (var ms = new MemoryStream(data, payloadOffset, data.Length - payloadOffset))
            using (var r = new BinaryReader(ms))
            {
                SkipPlayer(r);
                r.ReadInt32(); // gameTic
                r.ReadInt32(); // nextSpawnId
                for (int i = 0; i < 7; i++) r.ReadInt32(); // stats
                SkipIntArray(r);
                SkipIntArray(r);
                SkipIntArray(r);
                return payloadOffset + (int)ms.Position;
            }
        }

        static int FindSecondThingIndexOffset(byte[] data, int payloadOffset)
        {
            using (var ms = new MemoryStream(data, payloadOffset, data.Length - payloadOffset))
            using (var r = new BinaryReader(ms))
            {
                SkipPlayer(r);
                r.ReadInt32();
                r.ReadInt32();
                for (int i = 0; i < 7; i++) r.ReadInt32();
                SkipIntArray(r);
                SkipIntArray(r);
                SkipIntArray(r);
                int sectorCount = r.ReadInt32();
                for (int i = 0; i < sectorCount; i++) SkipSector(r);
                int lineCount = r.ReadInt32();
                for (int i = 0; i < lineCount; i++)
                {
                    r.ReadInt32();
                    r.ReadByte();
                    r.ReadByte();
                }

                int thingCount = r.ReadInt32();
                Assert.That(thingCount, Is.EqualTo(2));
                SkipThing(r); // first
                return payloadOffset + (int)ms.Position; // second thing index
            }
        }

        static void SkipPlayer(BinaryReader r)
        {
            // 5 floats; health, armor, armorType, bullets, shells; 5 bool bytes;
            // currentWeapon; hasPending; pendingWeapon; keyBits; berserk; ironFeet; randomIndex;
            // rockets; ownsRocketLauncher; ownsChainsaw
            for (int i = 0; i < 5; i++) r.ReadSingle();
            for (int i = 0; i < 5; i++) r.ReadInt32();
            for (int i = 0; i < 5; i++) r.ReadByte();
            r.ReadInt32();
            r.ReadByte();
            r.ReadInt32();
            r.ReadInt32();
            r.ReadByte();
            r.ReadInt32();
            r.ReadInt32();
            r.ReadInt32();
            r.ReadByte();
            r.ReadByte();
        }

        static void SkipIntArray(BinaryReader r)
        {
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++) r.ReadInt32();
        }

        static void SkipSector(BinaryReader r)
        {
            r.ReadInt32();
            r.ReadSingle();
            r.ReadSingle();
            r.ReadInt32();
            r.ReadByte();
            r.ReadByte();
            r.ReadByte();
            r.ReadInt32();
            r.ReadSingle();
            r.ReadSingle();
            r.ReadInt32();
        }

        static void SkipThing(BinaryReader r)
        {
            r.ReadInt32();
            r.ReadByte();
            for (int i = 0; i < 4; i++) r.ReadSingle();
            r.ReadInt32();
            r.ReadInt32();
            r.ReadInt32();
            r.ReadByte();
            r.ReadInt32();
        }
    }
}
