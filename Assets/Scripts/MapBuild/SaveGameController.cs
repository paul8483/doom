using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Orchestrates full-world save/load: capture, codec, slot store, pending restore.
    public sealed class SaveGameController : MonoBehaviour
    {
        public const int SlotCount = 6;
        public const string PreviewSceneName = LevelTransitionController.PreviewSceneName;

        public static SaveGameController Instance { get; private set; }

        ISaveStorage store;
        string pendingOverwriteSlot;
        string pendingDeleteSlot;

        public string LastError { get; private set; }

        public static SaveGameController Ensure()
        {
            if (Instance != null) return Instance;
            var host = GameSessionHost.Ensure();
            var ctrl = host.GetComponent<SaveGameController>();
            if (ctrl == null) ctrl = host.gameObject.AddComponent<SaveGameController>();
            return ctrl;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            if (store == null)
                store = new SaveSlotStore();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// Inject a temp/memory store for PlayMode tests.
        public void SetStoreForTests(ISaveStorage storage)
        {
            store = storage ?? throw new ArgumentNullException(nameof(storage));
            ClearConfirmations();
        }

        public IReadOnlyList<SaveSlotInfo> ListSlots() =>
            store?.ListSlots() ?? Array.Empty<SaveSlotInfo>();

        public bool SlotExists(int slotIndex) =>
            store != null && store.Exists(SlotName(slotIndex));

        public static string SlotName(int slotIndex) => $"slot{slotIndex}";

        /// Save only while paused (or Playing→Paused). Overwrite requires a second call
        /// with <paramref name="confirmOverwrite"/> when the slot already exists.
        public bool TrySave(int slotIndex, bool confirmOverwrite = false)
        {
            LastError = null;
            ClearDeleteConfirm();

            var flow = GameFlowController.Ensure();
            if (flow.State == GameFlowState.Playing)
                flow.RequestPause();
            if (flow.State != GameFlowState.Paused)
            {
                LastError = "Save is only available while paused.";
                return false;
            }

            if (slotIndex < 0 || slotIndex >= SlotCount)
            {
                LastError = "Invalid save slot.";
                return false;
            }

            string name = SlotName(slotIndex);
            if (store.Exists(name) && !confirmOverwrite)
            {
                string key = name;
                if (pendingOverwriteSlot == key)
                {
                    // Second activation without explicit flag still counts as confirm.
                    confirmOverwrite = true;
                }
                else
                {
                    pendingOverwriteSlot = key;
                    LastError = $"Slot {slotIndex} occupied — select again to overwrite.";
                    return false;
                }
            }

            if (!TryCaptureSave(out SaveGame save, out string error))
            {
                LastError = error;
                return false;
            }

            try
            {
                store.Write(name, save);
            }
            catch (Exception ex)
            {
                LastError = "Failed to write save: " + ex.Message;
                return false;
            }

            ClearConfirmations();
            var host = GameSessionHost.Instance;
            if (host != null && WorldStateRegistry.Instance != null)
                host.SyncSpawnIdFrom(WorldStateRegistry.Instance);
            return true;
        }

        /// Validate envelope/WAD/map, set PendingRestore, reload scene. Does not
        /// mutate the current level on failure.
        public bool TryLoad(int slotIndex)
        {
            LastError = null;
            ClearConfirmations();

            var flow = GameFlowController.Ensure();
            if (flow.State != GameFlowState.MainMenu && flow.State != GameFlowState.Paused)
            {
                LastError = "Load is only available from the main or pause menu.";
                return false;
            }

            if (slotIndex < 0 || slotIndex >= SlotCount)
            {
                LastError = "Invalid save slot.";
                return false;
            }

            string name = SlotName(slotIndex);
            if (!store.TryRead(name, out SaveGame save, out string error))
            {
                LastError = error ?? "Save slot not found.";
                return false;
            }

            var host = GameSessionHost.Ensure();
            string wadPath = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            host.EnsureWadIdentity(wadPath);

            if (!string.Equals(save.WadIdentity, host.WadIdentity, StringComparison.Ordinal))
            {
                LastError = "Save is for a different WAD.";
                return false;
            }

            if (!CampaignRoute.TryNormalize(save.MapName, out _))
            {
                LastError = "Save has an invalid map name.";
                return false;
            }

            // Preflight OK — commit pending restore and reload.
            host.ClearPendingRestore();
            host.SetPendingRestore(save);
            host.SetNextSpawnId(0);
            host.Session.BeginNewGame(save.MapName, GameFlowController.CollectAvailableMaps());

            flow.EnterLoading();
            SceneManager.LoadScene(PreviewSceneName, LoadSceneMode.Single);
            return true;
        }

        public bool TryDelete(int slotIndex, bool confirm = false)
        {
            LastError = null;
            ClearOverwriteConfirm();

            if (slotIndex < 0 || slotIndex >= SlotCount)
            {
                LastError = "Invalid save slot.";
                return false;
            }

            string name = SlotName(slotIndex);
            if (!store.Exists(name))
            {
                LastError = "Save slot is empty.";
                return false;
            }

            if (!confirm)
            {
                if (pendingDeleteSlot == name)
                    confirm = true;
                else
                {
                    pendingDeleteSlot = name;
                    LastError = $"Slot {slotIndex} — select again to delete.";
                    return false;
                }
            }

            try
            {
                store.Delete(name);
            }
            catch (Exception ex)
            {
                LastError = "Failed to delete save: " + ex.Message;
                return false;
            }

            ClearConfirmations();
            return true;
        }

        public bool TryCaptureSave(out SaveGame save, out string error)
        {
            save = null;
            error = null;

            var registry = WorldStateRegistry.Instance;
            if (registry == null)
            {
                error = "No world registry.";
                return false;
            }

            var player = GameObject.Find("Player");
            if (player == null)
            {
                error = "No player.";
                return false;
            }

            if (!WorldSnapshotCapture.TryCapture(registry, out WorldSnapshot world, out error))
                return false;

            var health = player.GetComponent<PlayerHealth>();
            var weapons = player.GetComponent<PlayerWeapons>();
            var inventory = player.GetComponent<PlayerInventory>();
            var pc = player.GetComponent<PlayerController>();
            if (health == null || weapons == null || inventory == null)
            {
                error = "Player components missing.";
                return false;
            }

            var pos = player.transform.position;
            float yaw = player.transform.eulerAngles.y;
            float pitch = pc != null ? pc.PitchDegrees : 0f;
            var playerSnap = PlayerSnapshot.Capture(
                pos.x, pos.y, pos.z, yaw, pitch,
                health.Model, weapons.Ammo, weapons.Loadout,
                inventory.Keys, inventory.Powers, weapons.Rng);

            var host = GameSessionHost.Ensure();
            string wadPath = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            host.EnsureWadIdentity(wadPath);

            string mapName = !string.IsNullOrEmpty(MapLoader.MapNameOverride)
                ? MapLoader.MapNameOverride
                : (host.Session != null && host.Session.IsActive
                    ? host.Session.CurrentMap
                    : null);
            if (string.IsNullOrEmpty(mapName))
            {
                var loader = UnityEngine.Object.FindAnyObjectByType<MapLoader>();
                mapName = loader != null ? loader.LoadedMapName : "E1M1";
            }

            if (!SaveGame.TryCreate(mapName, host.WadIdentity, playerSnap, world, out save, out error))
                return false;

            host.SyncSpawnIdFrom(registry);
            return true;
        }

        void ClearConfirmations()
        {
            pendingOverwriteSlot = null;
            pendingDeleteSlot = null;
        }

        void ClearOverwriteConfirm() => pendingOverwriteSlot = null;
        void ClearDeleteConfirm() => pendingDeleteSlot = null;
    }
}
