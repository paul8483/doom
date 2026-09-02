using UnityEngine;
using Doom.Game;
using Doom.Map;
using Doom.Specials;

namespace Doom.MapBuild
{
    /// Player gunfire wakes monsters: player's sector -> NoiseAlert flood ->
    /// NotifyNoise() on monsters standing in heard sectors. Fist is silent.
    public sealed class NoiseAlertSystem : MonoBehaviour
    {
        MapData map;
        ISectorHeights heights;
        PlayerWeapons weapons;
        Transform player;

        public void Init(MapData map, ISectorHeights heights,
                         PlayerWeapons weapons, Transform player)
        {
            this.map = map; this.heights = heights;
            this.weapons = weapons; this.player = player;
            weapons.Committed += OnCommitted;
        }

        void OnDestroy() { if (weapons != null) weapons.Committed -= OnCommitted; }

        void OnCommitted(WeaponDef def)
        {
            if (def.Ammo == AmmoType.None) return;   // кулак не шумит
            int source = SectorUnder(player.position);
            if (source < 0) return;
            var heard = NoiseAlert.Compute(map, heights, source);
            var monsters = MonsterController.Active;
            for (int i = 0; i < monsters.Count; i++)
            {
                var mc = monsters[i];
                if (mc == null) continue;
                int s = SectorUnder(mc.transform.position);
                if (s >= 0 && heard.Contains(s)) mc.NotifyNoise();
            }
        }

        static int SectorUnder(Vector3 pos)
        {
            if (Physics.Raycast(pos + Vector3.up * 0.1f, Vector3.down, out var hit,
                                100f, ~0, QueryTriggerInteraction.Ignore))
            {
                var sr = hit.collider.GetComponent<SectorRef>();
                if (sr != null) return sr.SectorIndex;
            }
            return -1;
        }
    }
}
