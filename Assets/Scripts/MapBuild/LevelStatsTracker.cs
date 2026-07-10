using UnityEngine;
using Doom.Game;
using Doom.Map;
using Doom.Things;

namespace Doom.MapBuild
{
    /// Scene-side level stats: totals from the map, events from kills/pickups/secrets.
    public sealed class LevelStatsTracker : MonoBehaviour
    {
        public static LevelStatsTracker Instance { get; private set; }

        public LevelStats Stats { get; } = new LevelStats();

        MapData map;
        FloorDamageSystem floor;
        float ticAccum;
        readonly System.Collections.Generic.HashSet<int> visitedSecrets =
            new System.Collections.Generic.HashSet<int>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Init(MapData map, FloorDamageSystem floor)
        {
            this.map = map;
            this.floor = floor;
            Stats.Reset();
            visitedSecrets.Clear();
            ComputeTotals(map);
        }

        public void RegisterKill(int mapThingIndex) => Stats.TryRegisterKill(mapThingIndex);

        public void RegisterItem(int mapThingIndex) => Stats.TryRegisterItem(mapThingIndex);

        void Update()
        {
            ticAccum += Time.deltaTime;
            const float Tic = 1f / 35f;
            int steps = 0;
            while (ticAccum >= Tic)
            {
                ticAccum -= Tic;
                steps++;
            }
            if (steps > 0)
                Stats.AdvanceTics(steps);

            PollSecret();
        }

        void PollSecret()
        {
            if (floor == null || map == null) return;
            int special = floor.SectorSpecialUnderPlayer();
            if (special != 9) return;

            int sector = floor.SectorIndexUnderPlayer();
            if (sector < 0) return;
            if (visitedSecrets.Add(sector))
                Stats.TryRegisterSecret(sector);
        }

        void ComputeTotals(MapData map)
        {
            if (map == null) return;
            int kills = 0, items = 0, secrets = 0;
            for (int i = 0; i < map.Things.Length; i++)
            {
                int type = map.Things[i].Type;
                if (ThingTable.TryGet(type, out var def) && def.Has(ThingFlags.CountKill))
                    kills++;
                if (LevelStats.IsCountItem(type))
                    items++;
            }

            for (int i = 0; i < map.Sectors.Length; i++)
                if (map.Sectors[i].Special == 9)
                    secrets++;

            Stats.SetTotals(kills, items, secrets);
        }
    }
}
