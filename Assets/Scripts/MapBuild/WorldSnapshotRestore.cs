using System;
using UnityEngine;
using Doom.Game;
using Doom.Map;
using Doom.Things;

namespace Doom.MapBuild
{
    /// Applies a <see cref="SaveGame"/> after MapLoader finishes the static WAD build.
    /// Order: sectors/lines → map things → spawned pickups → targets/projectiles →
    /// player → stats/spawn ids.
    public static class WorldSnapshotRestore
    {
        public static bool TryApply(
            SaveGame save,
            WorldStateRegistry registry,
            MapLoader loader,
            SpriteCache spriteCache,
            float worldScale,
            GameObject player,
            SoundSystem sound,
            out string error)
        {
            error = null;
            if (save == null) { error = "Save is required."; return false; }
            if (registry == null) { error = "Registry is required."; return false; }
            if (loader == null) { error = "MapLoader is required."; return false; }
            if (player == null) { error = "Player is required."; return false; }

            var world = save.World;
            var map = registry.Map;
            var heights = registry.Heights;
            var geometry = loader.Geometry;
            if (map == null || heights == null || geometry == null)
            {
                error = "Registry/loader not bound.";
                return false;
            }

            try
            {
                RestoreSectors(world, map, heights, geometry, player, registry.Lines);
                RestoreLines(world, map, registry.Lines);
                RestoreMapThings(world, registry);
                RestoreSpawnedPickups(world, registry, spriteCache, worldScale, player);
                ResolveMonsterTargets(world, registry, player.transform);
                RestoreProjectiles(world, registry, spriteCache, worldScale, sound, player);
                RestorePlayer(save.Player, player);
                RestoreStats(world, registry);
                registry.SetNextSpawnId(world.NextSpawnId);
                return true;
            }
            catch (Exception ex)
            {
                error = "Restore failed: " + ex.Message;
                Debug.LogException(ex);
                return false;
            }
        }

        static void RestoreSectors(
            WorldSnapshot world, MapData map, RuntimeSectorHeights heights,
            SectorGeometry geometry, GameObject player, LineActivator lines)
        {
            if (world.Sectors == null) return;

            foreach (var s in world.Sectors)
            {
                if (s.Index < 0 || s.Index >= map.Sectors.Length) continue;

                float prevFloor = heights.FloorRaw(s.Index);
                float prevCeil = heights.CeilRaw(s.Index);
                heights.SetFloor(s.Index, s.FloorHeight);
                heights.SetCeil(s.Index, s.CeilingHeight);

                bool changed = !Mathf.Approximately(prevFloor, s.FloorHeight)
                               || !Mathf.Approximately(prevCeil, s.CeilingHeight);
                if (changed || s.HasMover)
                    geometry.RebuildSectorAndNeighbors(s.Index);

                if (!s.HasMover) continue;

                var surface = s.MoverPlane == MoverPlane.Ceiling
                    ? SectorMover.Surface.Ceiling
                    : SectorMover.Surface.Floor;
                float returnOrigin = s.MoverPlane == MoverPlane.Ceiling
                    ? map.Sectors[s.Index].CeilingHeight
                    : map.Sectors[s.Index].FloorHeight;

                int sectorIndex = s.Index;
                var mover = player.AddComponent<SectorMover>();
                mover.BeginFromSnapshot(
                    heights, geometry, sectorIndex, surface,
                    s.MoverTarget, s.MoverSpeed,
                    s.MoverPhase, s.MoverWaitTics, returnOrigin,
                    onDone: () => lines?.SetSectorMoving(sectorIndex, false));
                lines?.SetSectorMoving(sectorIndex, true);
            }
        }

        static void RestoreLines(WorldSnapshot world, MapData map, LineActivator lines)
        {
            if (lines == null || map == null) return;
            var fired = new bool[map.LineDefs.Length];
            if (world.Lines != null)
            {
                foreach (var line in world.Lines)
                {
                    if (line.Index < 0 || line.Index >= fired.Length) continue;
                    if (line.Fired) fired[line.Index] = true;
                }
            }
            lines.RestoreFired(fired);
        }

        static void RestoreMapThings(WorldSnapshot world, WorldStateRegistry registry)
        {
            if (world.Things == null) return;

            foreach (var thing in world.Things)
            {
                if (!thing.Present)
                {
                    if (registry.TryGetMapThing(thing.MapThingIndex, out var absent) &&
                        absent != null)
                    {
                        registry.UnregisterMapThing(thing.MapThingIndex);
                        UnityEngine.Object.Destroy(absent.gameObject);
                    }
                    continue;
                }

                if (!registry.TryGetMapThing(thing.MapThingIndex, out var identity) ||
                    identity == null)
                    continue;

                var go = identity.gameObject;
                go.transform.position = new Vector3(thing.X, thing.Y, thing.Z);

                var mc = go.GetComponent<MonsterController>();
                if (mc != null)
                {
                    mc.ApplySnapshotRestore(thing.Health, thing.Frame, thing.AngleDegrees,
                        dead: thing.Health <= 0);
                    continue;
                }

                var bb = go.GetComponent<SpriteBillboard>();
                if (bb != null)
                {
                    bb.SetDoomAngle(thing.AngleDegrees);
                    if (thing.Frame >= 0) bb.SetFrame(thing.Frame);
                }

                var eh = go.GetComponent<EnemyHealth>();
                if (eh != null)
                    eh.RestoreHealth(thing.Health);
            }
        }

        static void RestoreSpawnedPickups(
            WorldSnapshot world, WorldStateRegistry registry,
            SpriteCache spriteCache, float worldScale, GameObject player)
        {
            if (world.SpawnedPickups == null || spriteCache == null) return;
            Transform parent = player.transform.parent;
            var thingsRoot = GameObject.Find("Things");
            if (thingsRoot != null) parent = thingsRoot.transform;

            foreach (var pickup in world.SpawnedPickups)
            {
                if (registry.TryGetSpawned(pickup.SpawnId, out _))
                    continue;
                PickupFactory.Spawn(
                    spriteCache, worldScale, pickup.DoomedNum,
                    new Vector3(pickup.X, pickup.Y, pickup.Z),
                    parent, forcedSpawnId: pickup.SpawnId);
            }
        }

        static void ResolveMonsterTargets(
            WorldSnapshot world, WorldStateRegistry registry, Transform player)
        {
            if (world.Things == null) return;
            foreach (var thing in world.Things)
            {
                if (!thing.Present || thing.Health <= 0) continue;
                if (!registry.TryGetMapThing(thing.MapThingIndex, out var identity) ||
                    identity == null)
                    continue;
                var mc = identity.GetComponent<MonsterController>();
                if (mc == null) continue;

                Transform target = player;
                if (!thing.Target.IsNone &&
                    registry.TryResolve(thing.Target, out var resolved) &&
                    resolved != null)
                    target = resolved;
                mc.SetTarget(target);
            }
        }

        static void RestoreProjectiles(
            WorldSnapshot world, WorldStateRegistry registry,
            SpriteCache spriteCache, float worldScale, SoundSystem sound,
            GameObject player)
        {
            if (world.Projectiles == null || spriteCache == null) return;
            var rng = player.GetComponent<PlayerWeapons>()?.Rng ?? new DoomRandom();

            foreach (var proj in world.Projectiles)
            {
                if (registry.TryGetSpawned(proj.SpawnId, out _)) continue;
                if (!MonsterTable.TryGet(proj.Type, out var def) || def == null) continue;
                if (string.IsNullOrEmpty(def.MissileSprite)) continue;

                EnemyHealth owner = null;
                if (!proj.Owner.IsNone &&
                    registry.TryResolve(proj.Owner, out var ownerTf) &&
                    ownerTf != null)
                    owner = ownerTf.GetComponent<EnemyHealth>();

                Projectile.LaunchFromSnapshot(
                    spriteCache, def, worldScale, rng, proj, owner, sound);
            }
        }

        static void RestorePlayer(PlayerSnapshot snap, GameObject player)
        {
            if (snap == null) return;

            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = new Vector3(snap.X, snap.Y, snap.Z);
            if (cc != null) cc.enabled = true;

            var pc = player.GetComponent<PlayerController>();
            pc?.SetView(snap.YawDegrees, snap.PitchDegrees);

            var health = player.GetComponent<PlayerHealth>();
            var weapons = player.GetComponent<PlayerWeapons>();
            var inventory = player.GetComponent<PlayerInventory>();
            if (health == null || weapons == null || inventory == null) return;

            snap.ApplyTo(
                health.Model, weapons.Ammo, weapons.Loadout,
                inventory.Keys, inventory.Powers, weapons.Rng);
            health.SyncDeathFlagFromModel();
        }

        static void RestoreStats(WorldSnapshot world, WorldStateRegistry registry)
        {
            var tracker = registry.StatsTracker;
            if (tracker == null) return;
            var s = world.Stats;
            tracker.Stats.RestoreIds(
                s.KillTotal, s.ItemTotal, s.SecretTotal, s.Tics,
                world.KillIds, world.ItemIds, world.SecretIds);
        }
    }
}
