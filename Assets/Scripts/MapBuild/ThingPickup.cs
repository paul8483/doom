using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Pickup trigger for items (Stage 6e: full E1 set via PlayerInventory / ItemRules).
    /// Horizontal touch radius matches DOOM (16 item + 16 player). Vertical range
    /// mirrors P_TouchSpecialThing: item may sit up to player-height above the
    /// toucher, or 8 units below — so weapons in wall niches / on ledges stay
    /// reachable from the floor in front of them.
    public sealed class ThingPickup : MonoBehaviour
    {
        const float TouchRadiusDoom = 32f;
        const float TouchAboveDoom = 56f; // toucher height
        const float TouchBelowDoom = 8f;

        int doomedNum;
        int mapThingIndex = -1;

        public int DoomedNum => doomedNum;
        /// Index into MapData.Things, or -1 for runtime drops (not counted as items).
        public int MapThingIndex => mapThingIndex;

        public void Init(int doomedNum, float worldScale, int mapThingIndex = -1)
        {
            this.doomedNum = doomedNum;
            this.mapThingIndex = mapThingIndex;

            float r = TouchRadiusDoom * worldScale;
            float yMin = -TouchAboveDoom * worldScale;
            float yMax = TouchBelowDoom * worldScale;
            float span = yMax - yMin;

            var trig = gameObject.AddComponent<CapsuleCollider>();
            trig.isTrigger = true;
            trig.radius = r;
            trig.height = Mathf.Max(span, 2f * r);
            trig.center = new Vector3(0f, (yMin + yMax) * 0.5f, 0f);
        }

        void OnTriggerEnter(Collider other) => TryCollect(other);

        void OnTriggerStay(Collider other) => TryCollect(other);

        void TryCollect(Collider other)
        {
            if (other == null) return;
            var inv = other.GetComponentInParent<PlayerInventory>();
            if (inv == null) return;
            if (!inv.TryPickup(doomedNum)) return;

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
