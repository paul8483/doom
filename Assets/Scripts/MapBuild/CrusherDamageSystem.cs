using UnityEngine;
using Doom.Specials;

namespace Doom.MapBuild
{
    /// Applies vanilla crusher damage without modifying player/monster health owners.
    public sealed class CrusherDamageSystem : MonoBehaviour
    {
        SectorMover mover;
        RuntimeSectorHeights heights;
        int sector;
        float worldScale;
        Collider floorCollider;
        float fallbackTics;
        int nextDamageTic = -1;
        PlayerHealth[] players;
        EnemyHealth[] enemies;
        float nextActorRefresh;

        public bool IsObstructed { get; private set; }

        public void Begin(
            SectorMover mover, RuntimeSectorHeights heights, SectorGeometry geometry,
            int sector, float worldScale)
        {
            this.mover = mover;
            this.heights = heights;
            this.sector = sector;
            this.worldScale = worldScale;
            var root = geometry?.GetSectorRoot(sector);
            floorCollider = root != null ? root.Find("Floor")?.GetComponent<Collider>() : null;
        }

        void Update()
        {
            if (mover == null || !mover.IsCrusher)
            {
                Destroy(this);
                return;
            }

            IsObstructed = mover.IsCrusherDescending && HasCrushedActor(applyDamage: false);
            if (!IsObstructed)
            {
                nextDamageTic = -1;
                return;
            }

            int gameTic;
            if (LevelStatsTracker.Instance != null)
                gameTic = LevelStatsTracker.Instance.Stats.Tics;
            else
            {
                fallbackTics += Time.deltaTime * CrusherRules.TicsPerSecond;
                gameTic = Mathf.FloorToInt(fallbackTics);
            }

            if (nextDamageTic < 0)
                nextDamageTic = gameTic + CrusherRules.DamageCadenceTics;
            while (gameTic >= nextDamageTic)
            {
                HasCrushedActor(applyDamage: true);
                nextDamageTic += CrusherRules.DamageCadenceTics;
            }
        }

        bool HasCrushedActor(bool applyDamage)
        {
            if (heights == null || floorCollider == null) return false;
            float clearance = (heights.CeilRaw(sector) - heights.FloorRaw(sector)) * worldScale;
            bool crushed = false;

            // Refresh the actor lists on a slow cadence instead of scanning the
            // scene every frame per active crusher.
            if (players == null || enemies == null || Time.unscaledTime >= nextActorRefresh)
            {
                players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
                enemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
                nextActorRefresh = Time.unscaledTime + 0.5f;
            }

            foreach (var player in players)
            {
                if (player == null || player.IsDead) continue;
                if (!TryGetBounds(player.gameObject, out var bounds)
                    || bounds.size.y <= clearance || !IsOverSector(bounds.center))
                    continue;
                crushed = true;
                if (applyDamage) player.TakeDamage(CrusherRules.Damage);
            }

            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.IsDead) continue;
                if (!TryGetBounds(enemy.gameObject, out var bounds)
                    || bounds.size.y <= clearance || !IsOverSector(bounds.center))
                    continue;
                crushed = true;
                if (applyDamage) enemy.TakeDamage(CrusherRules.Damage);
            }

            return crushed;
        }

        bool IsOverSector(Vector3 point)
        {
            float ceilingY = heights.CeilRaw(sector) * worldScale;
            var ray = new Ray(new Vector3(point.x, ceilingY + 64f * worldScale, point.z), Vector3.down);
            return floorCollider.Raycast(ray, out _, 8192f * worldScale);
        }

        static bool TryGetBounds(GameObject actor, out Bounds bounds)
        {
            var cc = actor.GetComponent<CharacterController>();
            if (cc != null && cc.enabled)
            {
                bounds = cc.bounds;
                return true;
            }
            var collider = actor.GetComponent<Collider>();
            if (collider != null && collider.enabled)
            {
                bounds = collider.bounds;
                return true;
            }
            bounds = default;
            return false;
        }
    }
}
