using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Pickup trigger for items (Stage 6e: full E1 set via PlayerInventory / ItemRules).
    /// Touch radius in the spirit of DOOM (16-unit item + 16-unit player radius).
    public sealed class ThingPickup : MonoBehaviour
    {
        int doomedNum;

        public int DoomedNum => doomedNum;

        public void Init(int doomedNum, float worldScale)
        {
            this.doomedNum = doomedNum;
            var trig = gameObject.AddComponent<SphereCollider>();
            trig.isTrigger = true;
            trig.radius = 32f * worldScale;
            trig.center = new Vector3(0f, 32f * worldScale, 0f);
        }

        void OnTriggerEnter(Collider other)
        {
            var inv = other.GetComponent<PlayerInventory>();
            if (inv == null) return;
            if (inv.TryPickup(doomedNum))
                Destroy(gameObject);
        }
    }
}
