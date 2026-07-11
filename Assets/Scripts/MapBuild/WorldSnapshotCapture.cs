using System;
using System.Collections.Generic;
using UnityEngine;
using Doom.Game;
using Doom.Map;

namespace Doom.MapBuild
{
    /// Builds a <see cref="WorldSnapshot"/> from the live registry and runtime
    /// components. Does not use FindObjectsByType for identity — only for
    /// discovering active SectorMover components attached to the player.
    public static class WorldSnapshotCapture
    {
        public static bool TryCapture(WorldStateRegistry registry, out WorldSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;
            if (registry == null)
            {
                error = "Registry is required.";
                return false;
            }

            var map = registry.Map;
            var heights = registry.Heights;
            if (map == null || heights == null)
            {
                error = "Registry is not bound to a map.";
                return false;
            }

            var moversBySector = IndexMovers();

            var sectors = new SectorSnapshot[map.Sectors.Length];
            for (int i = 0; i < map.Sectors.Length; i++)
            {
                moversBySector.TryGetValue(i, out var mover);
                sectors[i] = CaptureSector(i, map, heights, mover);
            }

            var lines = CaptureLines(registry.Lines, map.LineDefs.Length);

            var things = new List<ThingSnapshot>();
            foreach (int id in registry.SortedMapThingIds())
            {
                if (!registry.TryGetMapThing(id, out var identity) || identity == null)
                    continue;
                things.Add(CaptureThing(identity, registry));
            }

            // Map things that were picked up / destroyed are absent from the registry;
            // emit Present=false stubs for every THINGS index that had a spawnable thing
            // so restore can hide them. Indices still registered cover living/dead GOs.
            var present = new HashSet<int>();
            foreach (var t in things) present.Add(t.MapThingIndex);
            for (int i = 0; i < map.Things.Length; i++)
            {
                if (present.Contains(i)) continue;
                if (IsSpawnPoint(map.Things[i].Type)) continue;
                if (!Doom.Things.ThingTable.TryGet(map.Things[i].Type, out _)) continue;
                things.Add(new ThingSnapshot(
                    i, present: false,
                    0f, 0f, 0f, 0f,
                    health: 0, frame: 0, flags: map.Things[i].Flags,
                    SaveEntityId.None));
            }
            things.Sort((a, b) => a.MapThingIndex.CompareTo(b.MapThingIndex));

            var projectiles = new List<ProjectileSnapshot>();
            var pickups = new List<SpawnedPickupSnapshot>();
            foreach (int spawnId in registry.SortedSpawnIds())
            {
                if (!registry.TryGetSpawned(spawnId, out var identity) || identity == null)
                    continue;
                var proj = identity.GetComponent<IProjectileSnapshotSource>();
                if (proj != null)
                {
                    var projSnap = proj.CaptureSnapshot(spawnId, registry);
                    if (projSnap != null)
                        projectiles.Add(projSnap);
                    continue;
                }

                var pickup = identity.GetComponent<ThingPickup>();
                if (pickup != null)
                {
                    var p = identity.transform.position;
                    pickups.Add(new SpawnedPickupSnapshot(
                        spawnId, pickup.DoomedNum, p.x, p.y, p.z));
                }
            }

            var statsTracker = registry.StatsTracker;
            var stats = statsTracker != null
                ? statsTracker.Stats.Snapshot()
                : default;
            int[] killIds = statsTracker != null
                ? statsTracker.Stats.CaptureKillIds()
                : Array.Empty<int>();
            int[] itemIds = statsTracker != null
                ? statsTracker.Stats.CaptureItemIds()
                : Array.Empty<int>();
            int[] secretIds = statsTracker != null
                ? statsTracker.Stats.CaptureSecretIds()
                : Array.Empty<int>();

            return WorldSnapshot.TryCreate(
                gameTic: stats.Tics,
                nextSpawnId: registry.NextSpawnId,
                stats: stats,
                killIds: killIds,
                itemIds: itemIds,
                secretIds: secretIds,
                sectors: sectors,
                lines: lines,
                things: things.ToArray(),
                projectiles: projectiles.ToArray(),
                spawnedPickups: pickups.ToArray(),
                out snapshot,
                out error);
        }

        static bool IsSpawnPoint(int type) => type >= 1 && type <= 4 || type == 11;

        static Dictionary<int, SectorMover> IndexMovers()
        {
            var dict = new Dictionary<int, SectorMover>();
            foreach (var mover in UnityEngine.Object.FindObjectsByType<SectorMover>(
                         FindObjectsSortMode.None))
            {
                if (mover == null || !mover.TryCapture(out int sector, out _, out _, out _,
                        out _, out _, out _, out _))
                    continue;
                // One mover per sector is the runtime invariant.
                dict[sector] = mover;
            }
            return dict;
        }

        static SectorSnapshot CaptureSector(
            int index, MapData map, RuntimeSectorHeights heights, SectorMover mover)
        {
            float floor = heights.FloorRaw(index);
            float ceil = heights.CeilRaw(index);
            int light = map.Sectors[index].LightLevel;

            if (mover == null || !mover.TryCapture(
                    out _, out var plane, out var phase, out int dir,
                    out float target, out float speed, out int waitTics, out bool hasMover)
                || !hasMover)
            {
                return new SectorSnapshot(
                    index, floor, ceil, light,
                    hasMover: false, MoverPlane.Floor, MoverPhase.None,
                    0, 0f, 0f, 0);
            }

            return new SectorSnapshot(
                index, floor, ceil, light,
                hasMover: true, plane, phase, dir, target, speed, waitTics);
        }

        static LineSnapshot[] CaptureLines(LineActivator activator, int lineCount)
        {
            if (activator == null || lineCount <= 0)
                return Array.Empty<LineSnapshot>();

            activator.CaptureFired(out bool[] fired);
            var list = new List<LineSnapshot>();
            for (int i = 0; i < fired.Length; i++)
            {
                if (!fired[i]) continue;
                list.Add(new LineSnapshot(i, fired: true, switchOn: false));
            }
            return list.ToArray();
        }

        static ThingSnapshot CaptureThing(MapThingIdentity identity, WorldStateRegistry registry)
        {
            var go = identity.gameObject;
            var pos = go.transform.position;
            float angle = 0f;
            int frame = 0;
            int health = 0;
            var target = SaveEntityId.None;

            var bb = go.GetComponent<SpriteBillboard>();
            if (bb != null)
            {
                angle = bb.DoomAngleDegrees;
                frame = bb.CurrentFrame;
            }

            var eh = go.GetComponent<EnemyHealth>();
            if (eh != null)
                health = eh.Health;

            var mc = go.GetComponent<MonsterController>();
            if (mc != null)
                target = registry.ResolveEntity(mc.TargetForTest);

            return new ThingSnapshot(
                identity.MapThingIndex,
                present: true,
                pos.x, pos.y, pos.z, angle,
                health, frame, identity.MapFlags, target);
        }
    }
}
