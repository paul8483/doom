using UnityEngine;
using Doom.Game;
using Doom.Things;

namespace Doom.MapBuild
{
    /// Pickup for items (Stage 6e: full E1 set via PlayerInventory / ItemRules).
    ///
    /// Ports P_TouchSpecialThing: XY distance &lt; playerRadius+itemRadius, and
    /// item Z within [toucherZ-8, toucherZ+height]. Physics triggers alone are
    /// unreliable here — thick wall colliders and CharacterController capsule
    /// math leave wall-hugged items unreachable even when DOOM distance says
    /// they should be taken (E1M2 medikit at 672,-1520).
    public sealed class ThingPickup : MonoBehaviour
    {
        const float PlayerRadiusDoom = 16f;
        const float DefaultItemRadiusDoom = 20f;
        const float TouchAboveDoom = 56f; // toucher height
        const float TouchBelowDoom = 8f;

        int doomedNum;
        int mapThingIndex = -1;
        float touchRadiusSq;
        float touchAbove;
        float touchBelow;
        PlayerInventory inventory;
        Transform playerBody;
        bool collected;

        public int DoomedNum => doomedNum;
        /// Index into MapData.Things, or -1 for runtime drops (not counted as items).
        public int MapThingIndex => mapThingIndex;
        /// Vanilla MF_DROPPED: a death drop gives half the ammo of a placed item.
        public bool Dropped { get; private set; }

        public void Init(int doomedNum, float worldScale, int mapThingIndex = -1, bool dropped = false)
        {
            this.doomedNum = doomedNum;
            this.mapThingIndex = mapThingIndex;
            Dropped = dropped;

            float itemRadius = DefaultItemRadiusDoom;
            if (ThingTable.TryGet(doomedNum, out var def) && def.Radius > 0)
                itemRadius = def.Radius;

            float touch = (PlayerRadiusDoom + itemRadius) * worldScale;
            touchRadiusSq = touch * touch;
            touchAbove = TouchAboveDoom * worldScale;
            touchBelow = TouchBelowDoom * worldScale;
        }

        void Update()
        {
            if (collected) return;
            // Items next to the WAD player start were being collected during
            // the restore frames of a save load (the player stands at the start
            // until the snapshot moves them): nothing is picked up while the
            // level is still loading.
            var flow = GameFlowController.Instance;
            if (flow != null && flow.State == GameFlowState.Loading) return;
            if (!ResolvePlayer()) return;

            Vector3 delta = playerBody.position - transform.position;
            float xySq = delta.x * delta.x + delta.z * delta.z;
            if (xySq > touchRadiusSq) return;

            // DOOM: delta = thing->z - toucher->z; reject if > height or < -8.
            float doomDelta = transform.position.y - playerBody.position.y;
            if (doomDelta > touchAbove || doomDelta < -touchBelow) return;

            TryCollect(inventory);
        }

        bool ResolvePlayer()
        {
            if (inventory != null && playerBody != null) return true;
            var go = GameObject.Find("Player");
            if (go == null) return false;
            inventory = go.GetComponent<PlayerInventory>();
            playerBody = go.transform;
            return inventory != null;
        }

        void TryCollect(PlayerInventory inv)
        {
            if (collected || inv == null) return;
            if (!inv.TryPickup(doomedNum, Dropped)) return;

            collected = true;

            if (mapThingIndex >= 0 && LevelStats.IsCountItem(doomedNum))
                LevelStatsTracker.Instance?.RegisterItem(mapThingIndex);

            var mapId = GetComponent<MapThingIdentity>();
            if (mapId != null && WorldStateRegistry.Instance != null)
                WorldStateRegistry.Instance.UnregisterMapThing(mapId.MapThingIndex);

            var spawnId = GetComponent<RuntimeEntityIdentity>();
            if (spawnId != null && WorldStateRegistry.Instance != null)
                WorldStateRegistry.Instance.UnregisterSpawned(spawnId.SpawnId);

            Destroy(gameObject);
        }
    }
}
