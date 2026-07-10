using UnityEngine;
using Doom.Map;
using Doom.Specials;

namespace Doom.MapBuild
{
    /// Deals DOOM floor damage: while the player is grounded on a sector whose
    /// Special is a damaging type, applies that damage every ~0.914s (32 of 35
    /// tics) — matching P_PlayerInSpecialSector's `leveltime & 0x1f` cadence.
    public sealed class FloorDamageSystem : MonoBehaviour
    {
        const float TicInterval = 32f / 35f;   // ~0.914s

        MapData map;
        float worldScale;
        PlayerHealth health;
        CharacterController cc;
        PlayerInventory inventory;
        float timer;

        public void Init(MapData map, float worldScale, PlayerHealth health, CharacterController cc)
        {
            this.map = map;
            this.worldScale = worldScale;
            this.health = health;
            this.cc = cc;
            timer = 0f;
        }

        public void SetInventory(PlayerInventory inv) => inventory = inv;

        void Update()
        {
            if (map == null || health == null || health.IsDead) return;
            timer += Time.deltaTime;
            if (timer < TicInterval) return;
            timer -= TicInterval;
            TryApplyFloorDamageOnce();
        }

        /// Runs one floor-damage check now. Returns the damage applied (0 if the
        /// player isn't grounded on a damaging sector). Public so tests can drive it
        /// deterministically without waiting for the accumulator.
        public int TryApplyFloorDamageOnce()
        {
            if (cc != null && !cc.isGrounded) return 0;
            if (inventory != null && inventory.Powers.IronFeetTics > 0) return 0;
            int special = SectorSpecialUnderPlayer();
            if (special < 0) return 0;
            int dmg = SectorDamageTable.DamagePerTick(special);
            if (dmg > 0) health.TakeDamage(dmg);

            if (ExitSectorRules.ShouldExitAfterDamage(special, health.Health))
            {
                var ctrl = LevelTransitionController.Ensure();
                ctrl.TryRequestExit(new LevelExitRequest(Doom.Game.ExitKind.Normal, -1));
            }

            return dmg;
        }

        /// The Special of the sector whose floor the player stands on, or -1 if a
        /// downward raycast finds no SectorRef. Public so tests can assert the
        /// raycast→SectorRef chain resolves.
        public int SectorSpecialUnderPlayer()
        {
            int idx = SectorIndexUnderPlayer();
            if (idx < 0) return -1;
            return map.Sectors[idx].Special;
        }

        /// Sector index under the player's feet, or -1 if unresolved.
        public int SectorIndexUnderPlayer()
        {
            if (map == null) return -1;

            Vector3 origin = transform.position + Vector3.up * (16f * worldScale);
            float range = 48f * worldScale;

            var hits = Physics.RaycastAll(origin, Vector3.down, range,
                                          ~0, QueryTriggerInteraction.Ignore);

            float bestDist = float.MaxValue;
            SectorRef bestRef = null;
            foreach (var hit in hits)
            {
                if (cc != null && hit.collider == (Collider)(object)cc) continue;
                var sref = hit.collider.GetComponentInParent<SectorRef>();
                if (sref == null) continue;
                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    bestRef = sref;
                }
            }

            if (bestRef == null || bestRef.SectorIndex < 0 ||
                bestRef.SectorIndex >= map.Sectors.Length)
                return -1;

            return bestRef.SectorIndex;
        }
    }
}
