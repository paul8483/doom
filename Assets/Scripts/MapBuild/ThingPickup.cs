using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Pickup trigger for items (Stage 6e: full E1 set via PlayerInventory / ItemRules).
    /// Touch radius in the spirit of DOOM (16-unit item + 16-unit player radius).
    public sealed class ThingPickup : MonoBehaviour
    {
        int doomedNum;
        int mapThingIndex = -1;

        public int DoomedNum => doomedNum;
        /// Index into MapData.Things, or -1 for runtime drops (not counted as items).
        public int MapThingIndex => mapThingIndex;

        public void Init(int doomedNum, float worldScale, int mapThingIndex = -1)
        {
            this.doomedNum = doomedNum;
            this.mapThingIndex = mapThingIndex;
            var trig = gameObject.AddComponent<SphereCollider>();
            trig.isTrigger = true;
            trig.radius = 32f * worldScale;
            trig.center = new Vector3(0f, 32f * worldScale, 0f);
        }

        void OnTriggerEnter(Collider other)
        {
            var inv = other.GetComponent<PlayerInventory>();
            if (inv == null) return;
            if (!inv.TryPickup(doomedNum)) return;

            if (mapThingIndex >= 0 && LevelStats.IsCountItem(doomedNum))
                LevelStatsTracker.Instance?.RegisterItem(mapThingIndex);

            Destroy(gameObject);
        }
    }
}
