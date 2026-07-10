using System;
using System.Collections.Generic;
using UnityEngine;
using Doom.Game;
using Doom.Map;

namespace Doom.MapBuild
{
    /// Indexes mutable world entities by stable map / spawn ids. Built during
    /// MapLoader.Build; never relies on FindObjectsByType order for identity.
    public sealed class WorldStateRegistry : MonoBehaviour
    {
        public static WorldStateRegistry Instance { get; private set; }

        readonly Dictionary<int, MapThingIdentity> mapThings = new Dictionary<int, MapThingIdentity>();
        readonly Dictionary<int, RuntimeEntityIdentity> spawned = new Dictionary<int, RuntimeEntityIdentity>();

        MapData map;
        RuntimeSectorHeights heights;
        LineActivator lines;
        LevelStatsTracker stats;
        int nextSpawnId;

        public MapData Map => map;
        public RuntimeSectorHeights Heights => heights;
        public LineActivator Lines => lines;
        public LevelStatsTracker StatsTracker => stats;
        public int NextSpawnId => nextSpawnId;
        public int MapThingCount => mapThings.Count;
        public int SpawnedCount => spawned.Count;

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
            mapThings.Clear();
            spawned.Clear();
        }

        public void Bind(
            MapData mapData,
            RuntimeSectorHeights runtimeHeights,
            LineActivator lineActivator,
            LevelStatsTracker statsTracker,
            int startingSpawnId = 0)
        {
            map = mapData ?? throw new ArgumentNullException(nameof(mapData));
            heights = runtimeHeights ?? throw new ArgumentNullException(nameof(runtimeHeights));
            lines = lineActivator;
            stats = statsTracker;
            nextSpawnId = startingSpawnId < 0 ? 0 : startingSpawnId;
        }

        public void SetNextSpawnId(int value) =>
            nextSpawnId = value < 0 ? 0 : value;

        public void RegisterMapThing(MapThingIdentity identity)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            int id = identity.MapThingIndex;
            if (id < 0) throw new ArgumentException("MapThingIndex must be set.", nameof(identity));
            if (mapThings.ContainsKey(id))
                throw new InvalidOperationException($"Duplicate MapThingIndex {id}.");
            mapThings[id] = identity;
        }

        public void UnregisterMapThing(int mapThingIndex) => mapThings.Remove(mapThingIndex);

        public bool TryGetMapThing(int mapThingIndex, out MapThingIdentity identity) =>
            mapThings.TryGetValue(mapThingIndex, out identity);

        public int AllocateSpawnId()
        {
            int id = nextSpawnId++;
            return id;
        }

        public void RegisterSpawned(RuntimeEntityIdentity identity)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            int id = identity.SpawnId;
            if (id < 0) throw new ArgumentException("SpawnId must be set.", nameof(identity));
            if (spawned.ContainsKey(id))
                throw new InvalidOperationException($"Duplicate SpawnId {id}.");
            spawned[id] = identity;
            if (id >= nextSpawnId) nextSpawnId = id + 1;
        }

        public void UnregisterSpawned(int spawnId) => spawned.Remove(spawnId);

        public bool TryGetSpawned(int spawnId, out RuntimeEntityIdentity identity) =>
            spawned.TryGetValue(spawnId, out identity);

        public IEnumerable<int> SortedMapThingIds()
        {
            var keys = new List<int>(mapThings.Keys);
            keys.Sort();
            return keys;
        }

        public IEnumerable<int> SortedSpawnIds()
        {
            var keys = new List<int>(spawned.Keys);
            keys.Sort();
            return keys;
        }

        /// Resolve a live Transform to an SaveEntityId (map thing, or None for player/unknown).
        public SaveEntityId ResolveEntity(Transform t)
        {
            if (t == null) return SaveEntityId.None;
            var mapId = t.GetComponent<MapThingIdentity>();
            if (mapId != null && mapId.MapThingIndex >= 0)
                return SaveEntityId.MapThing(mapId.MapThingIndex);
            var spawnId = t.GetComponent<RuntimeEntityIdentity>();
            if (spawnId != null && spawnId.SpawnId >= 0)
                return SaveEntityId.Spawned(spawnId.SpawnId);
            return SaveEntityId.None;
        }

        /// Resolve a saved entity id to a live Transform (map thing or spawned).
        public bool TryResolve(SaveEntityId id, out Transform transform)
        {
            transform = null;
            if (id.IsNone) return false;
            if (id.Kind == EntityKind.MapThing)
            {
                if (!TryGetMapThing(id.Index, out var mapThing) || mapThing == null)
                    return false;
                transform = mapThing.transform;
                return true;
            }

            if (id.Kind == EntityKind.Spawned)
            {
                if (!TryGetSpawned(id.Index, out var spawnedId) || spawnedId == null)
                    return false;
                transform = spawnedId.transform;
                return true;
            }

            return false;
        }
    }
}
