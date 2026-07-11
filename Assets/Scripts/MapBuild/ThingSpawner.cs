using UnityEngine;
using Doom.Game;
using Doom.Map;
using Doom.MapBuild.Rendering;
using Doom.Things;

namespace Doom.MapBuild
{
    /// Turns MapData.Things into billboard GameObjects under a parent. Player and
    /// deathmatch starts are skipped. Vertical placement is by raycast against the
    /// already-built sector colliders (down for floor things, up for ceiling things).
    public sealed class ThingSpawner
    {
        readonly SpriteCache cache;
        readonly float worldScale;
        readonly int worldMask;
        readonly SoundSystem sound;

        public ThingSpawner(SpriteCache cache, float worldScale, SoundSystem sound = null)
        {
            this.cache = cache;
            this.worldScale = worldScale;
            this.sound = sound;
            this.worldMask = ~0; // all layers; map geometry is on Default
        }

        // Player starts 1–4, deathmatch start 11 — spawn points, not objects.
        static bool IsSpawnPoint(int type)
            => type >= 1 && type <= 4 || type == 11;

        public int SpawnAll(MapData map, Transform parent, float fallbackY, Transform playerTransform)
        {
            int count = 0;
            int floorMisses = 0;
            int seedCounter = 0;
            for (int thingIndex = 0; thingIndex < map.Things.Length; thingIndex++)
            {
                var t = map.Things[thingIndex];
                if (IsSpawnPoint(t.Type)) continue;
                if (!ThingTable.TryGet(t.Type, out var def)) continue;

                float x = t.X * worldScale;
                float z = t.Y * worldScale;
                bool ceiling = def.Has(ThingFlags.SpawnCeiling);

                float feetY = fallbackY;
                float ceilY = fallbackY + def.Height * worldScale;
                if (!ResolveVertical(x, z, fallbackY, ceiling, def, ref feetY, ref ceilY))
                    floorMisses++;

                var go = new GameObject($"Thing_{t.Type}_{def.Sprite}",
                    typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, worldPositionStays: false);
                go.transform.position = new Vector3(x, feetY, z);

                var identity = go.AddComponent<MapThingIdentity>();
                identity.Init(thingIndex, t.Type, t.Flags);
                WorldStateRegistry.Instance?.RegisterMapThing(identity);

                var bb = go.AddComponent<SpriteBillboard>();
                bb.Init(cache, def.Sprite, def.Frame, worldScale,
                        doomAngleDeg: t.Angle, spawnCeiling: ceiling, ceilingY: ceilY);
                if (t.Type == 58)
                    bb.SetSpectre(true);

                // Pre-warm the cache for all 8 rotations while the WAD is still open.
                // SpriteCache.Get is lazy and reads from the WAD on first access; by the
                // time LateUpdate runs, MapLoader's `using var wad` has disposed the stream.
                // Fetching all rotations now bakes them into the in-memory material cache.
                for (int rot = 0; rot < 8; rot++)
                {
                    cache.Get(def.Sprite, def.Frame, rot);
                    if (t.Type == 58)
                        cache.GetSpectre(def.Sprite, def.Frame, rot);
                }

                CapsuleCollider col = null;
                if (def.Has(ThingFlags.Solid))
                {
                    col = go.AddComponent<CapsuleCollider>();
                    float r = def.Radius * worldScale;
                    float h = Mathf.Max(def.Height * worldScale, 2f * r);
                    col.radius = r;
                    col.height = h;
                    col.center = new Vector3(0f, h * 0.5f, 0f);
                }

                if (def.Has(ThingFlags.Shootable) && def.Health > 0)
                {
                    bool countKill = def.Has(ThingFlags.CountKill);
                    var eh = go.AddComponent<EnemyHealth>();
                    eh.Init(def.Health, def.CorpseFrame, bb, col,
                            countKill: countKill, noBlood: t.Type == BarrelRules.DoomEdNum);
                    eh.SetMapThingIndex(thingIndex);
                    if (def.CorpseFrame >= 0)
                        cache.Get(def.Sprite, def.CorpseFrame, 0); // pre-warm while the WAD is open

                    if (t.Type == BarrelRules.DoomEdNum)
                    {
                        var be = go.AddComponent<BarrelExplosion>();
                        be.Init(bb, col, cache, worldScale, sound);
                        eh.SetBarrel(be);
                        foreach (int f in BarrelRules.ExplodeFrames)
                            cache.Get(BarrelRules.ExplodeSprite, f, 0);
                    }
                    else if (MonsterTable.TryGet(t.Type, out var mdef))
                    {
                        bool ambush = (t.Flags & 0x0008) != 0;
                        var mc = go.AddComponent<MonsterController>();
                        mc.Init(mdef, ambush, def.CorpseFrame, cache, worldScale, playerTransform,
                                bb, col, eh, new DoomRandom(seedCounter++), t.Type, sound);
                        eh.SetController(mc);
                        foreach (var seq in new[] { mdef.Stand, mdef.Run, mdef.Attack, mdef.Pain, mdef.Death })
                            foreach (int f in seq.Frames)
                                for (int rot = 0; rot < 8; rot++) cache.Get(def.Sprite, f, rot);
                        if (mdef.HasMissile)
                        {
                            foreach (int f in mdef.MissileFlyFrames) cache.Get(mdef.MissileSprite, f, 0);
                            foreach (int f in mdef.MissileExplodeFrames) cache.Get(mdef.MissileSprite, f, 0);
                        }
                    }
                }

                // E1 pickups (Stage 6e) — full ItemRules set.
                if (ItemRules.IsPickup(t.Type))
                    go.AddComponent<ThingPickup>().Init(t.Type, worldScale, thingIndex);

                // Enhanced sticky decoration lights (presentation only; pooled).
                if (EnhancedEmissionTable.TryGet(t.Type, out var emission))
                {
                    var lights = EnhancedLightSystem.Instance;
                    if (lights != null)
                    {
                        float midY = def.Height * worldScale * 0.5f;
                        var offset = new Vector3(0f, midY, 0f);
                        int handle = lights.RegisterSticky(
                            go.transform.position + offset,
                            emission,
                            worldScale,
                            go.transform,
                            offset);
                        bb.BindEmissionLight(handle);
                    }
                }

                // Pre-warm death-drop sprites while the WAD is still open.
                if (DeathDropTable.TryGet(t.Type, out int dropNum) &&
                    ThingTable.TryGet(dropNum, out var dropDef))
                    cache.Get(dropDef.Sprite, dropDef.Frame, 0);

                count++;
            }
            if (floorMisses > 0)
                Debug.LogWarning($"ThingSpawner: {floorMisses}/{count} things found no floor " +
                                 $"underneath (placed at fallback Y={fallbackY:0.00})");
            return count;
        }

        // Find floor (and, for hanging things, ceiling) Y under the XZ.
        // Returns true if a floor surface was found.
        bool ResolveVertical(float x, float z, float fallbackY, bool ceiling,
                             ThingDef def, ref float feetY, ref float ceilY)
        {
            const float Far = 10000f;
            Vector3 fromAbove = new Vector3(x, fallbackY + Far, z);

            // Pick the FLOOR specifically. A plain raycast against all colliders
            // returns the nearest surface from the top — often a wall's floor-to-
            // ceiling slab — which would lift the thing to ceiling/sky height
            // (floating). Floor GameObjects are named "Floor" by MapLoader; walls
            // ("Wall_*") and the player capsule are ignored. With multiple floor
            // hits (overlapping geometry) we take the highest.
            var hits = Physics.RaycastAll(fromAbove, Vector3.down, 2f * Far, worldMask);
            bool found = false;
            float floorY = float.NegativeInfinity;
            foreach (var h in hits)
            {
                if (h.collider.gameObject.name != "Floor") continue;
                if (h.point.y > floorY) { floorY = h.point.y; found = true; }
            }
            if (found) feetY = floorY;

            if (ceiling)
            {
                // Ceilings have no collider; the up-ray hits the surrounding wall
                // tops, which sit at the sector's ceiling height — good enough to
                // hang from. Fall back to feet + height if nothing is hit.
                Vector3 fromBelow = new Vector3(x, feetY, z);
                if (Physics.Raycast(fromBelow, Vector3.up, out var hitCeil, 2f * Far, worldMask,
                                    QueryTriggerInteraction.Ignore))
                    ceilY = hitCeil.point.y;
                else
                    ceilY = feetY + def.Height * worldScale;
            }

            return found;
        }
    }
}
