using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Doom.Game;

namespace Doom.Game.Tests
{
    public class SaveSlotStoreTests
    {
        [Test]
        public void Write_Read_round_trips_via_memory_filesystem()
        {
            var fs = new MemorySaveFileSystem();
            var store = new SaveSlotStore("saves", fs);
            SaveGame save = BuildSave("E1M1", "wad:a");

            store.Write("slot1", save);
            Assert.That(store.Exists("slot1"), Is.True);
            Assert.That(store.TryRead("slot1", out SaveGame loaded, out _), Is.True);
            Assert.That(loaded, Is.EqualTo(save));
        }

        [Test]
        public void Write_failure_preserves_previous_valid_slot()
        {
            var fs = new MemorySaveFileSystem();
            var store = new SaveSlotStore("saves", fs);
            SaveGame first = BuildSave("E1M1", "wad:a");
            SaveGame second = BuildSave("E1M2", "wad:a");

            store.Write("slot1", first);
            fs.FailNextWrite = true;
            Assert.Throws<IOException>(() => store.Write("slot1", second));

            Assert.That(store.TryRead("slot1", out SaveGame loaded, out _), Is.True);
            Assert.That(loaded.MapName, Is.EqualTo("E1M1"));
            Assert.That(loaded, Is.EqualTo(first));
        }

        [Test]
        public void Replace_failure_preserves_previous_valid_slot_and_cleans_temp()
        {
            var fs = new MemorySaveFileSystem();
            var store = new SaveSlotStore("saves", fs);
            SaveGame first = BuildSave("E1M1", "wad:a");
            SaveGame second = BuildSave("E1M3", "wad:a");

            store.Write("slot1", first);
            fs.FailNextReplace = true;
            Assert.Throws<IOException>(() => store.Write("slot1", second));

            Assert.That(store.TryRead("slot1", out SaveGame loaded, out _), Is.True);
            Assert.That(loaded.MapName, Is.EqualTo("E1M1"));

            // Next safe access cleans orphaned temps.
            store.ListSlots();
            Assert.That(
                fs.EnumerateFiles("saves", "*" + SaveSlotStore.TempExtension).Any(),
                Is.False);
        }

        [Test]
        public void Flush_failure_preserves_previous_valid_slot()
        {
            var fs = new MemorySaveFileSystem();
            var store = new SaveSlotStore("saves", fs);
            SaveGame first = BuildSave("E1M1", "wad:a");
            store.Write("slot1", first);

            fs.FailNextFlush = true;
            Assert.Throws<IOException>(() => store.Write("slot1", BuildSave("E1M2", "wad:a")));
            Assert.That(store.TryRead("slot1", out SaveGame loaded, out _), Is.True);
            Assert.That(loaded.MapName, Is.EqualTo("E1M1"));
        }

        [Test]
        public void Slot_name_rejects_path_separators()
        {
            var store = new SaveSlotStore("saves", new MemorySaveFileSystem());
            SaveGame save = BuildSave("E1M1", "wad:a");
            Assert.Throws<ArgumentException>(() => store.Write("../evil", save));
            Assert.Throws<ArgumentException>(() => store.Write("a/b", save));
            Assert.Throws<ArgumentException>(() => store.Write("a\\b", save));
            Assert.That(SaveSlotStore.IsValidSlotName("slot1"), Is.True);
        }

        [Test]
        public void ListSlots_reads_headers_only_and_skips_corrupt()
        {
            var fs = new MemorySaveFileSystem();
            var store = new SaveSlotStore("saves", fs);
            store.Write("alpha", BuildSave("E1M1", "wad:a"));
            store.Write("beta", BuildSave("E1M2", "wad:a"));

            // Corrupt a third file directly.
            fs.CreateDirectory("saves");
            fs.WriteAllBytesFlushed(
                Path.Combine("saves", "gamma" + SaveSlotStore.FileExtension),
                new byte[] { 1, 2, 3, 4 });

            var list = store.ListSlots();
            Assert.That(list.Count, Is.EqualTo(2));
            Assert.That(list[0].SlotName, Is.EqualTo("alpha"));
            Assert.That(list[0].MapName, Is.EqualTo("E1M1"));
            Assert.That(list[1].SlotName, Is.EqualTo("beta"));
            Assert.That(list[1].MapName, Is.EqualTo("E1M2"));
            Assert.That(list[0].PayloadLength, Is.GreaterThan(0));
        }

        [Test]
        public void SystemSaveFileSystem_round_trips_in_temp_directory()
        {
            string root = Path.Combine(Path.GetTempPath(), "doom-save-tests-" + Guid.NewGuid());
            try
            {
                var store = new SaveSlotStore(root, new SystemSaveFileSystem());
                SaveGame save = BuildSave("E1M1", "wad:temp");
                store.Write("quick", save);
                Assert.That(store.TryRead("quick", out SaveGame loaded, out _), Is.True);
                Assert.That(loaded, Is.EqualTo(save));
                Assert.That(store.ListSlots().Count, Is.EqualTo(1));
                store.Delete("quick");
                Assert.That(store.Exists("quick"), Is.False);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        static SaveGame BuildSave(string map, string wad)
        {
            Assert.That(PlayerSnapshot.TryCreate(
                0, 0, 0, 0, 0,
                100, 0, ArmorKind.None,
                AmmoModel.StartBullets, 0, false,
                true, true, false, false,
                WeaponId.Pistol, false, WeaponId.Fist,
                0, false, 0, 0,
                out var player, out _), Is.True);
            Assert.That(WorldSnapshot.TryCreate(
                0, 0, default,
                Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(),
                Array.Empty<SectorSnapshot>(), Array.Empty<LineSnapshot>(),
                Array.Empty<ThingSnapshot>(), Array.Empty<ProjectileSnapshot>(),
                Array.Empty<SpawnedPickupSnapshot>(),
                out var world, out _), Is.True);
            Assert.That(SaveGame.TryCreate(map, wad, player, world, out var save, out _), Is.True);
            return save;
        }
    }
}
