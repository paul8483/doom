using UnityEngine;

namespace Doom.MapBuild
{
    /// Pickup trigger for weapon/ammo things (Stage 6c: weapons and ammo only).
    /// Touch radius in the spirit of DOOM (16-unit item + 16-unit player radius).
    public sealed class ThingPickup : MonoBehaviour
    {
        int doomedNum;

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
            var weapons = other.GetComponent<PlayerWeapons>();
            if (weapons == null) return;
            if (weapons.Pickup(doomedNum))
                Destroy(gameObject);
        }
    }
}
