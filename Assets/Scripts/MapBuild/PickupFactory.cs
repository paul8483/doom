using UnityEngine;
using Doom.Things;

namespace Doom.MapBuild
{
    /// Spawns a floor-anchored pickup billboard + ThingPickup trigger (map items and death drops).
    public static class PickupFactory
    {
        public static GameObject Spawn(SpriteCache cache, float worldScale, int doomedNum,
                                       Vector3 feetPosition, Transform parent = null,
                                       int? forcedSpawnId = null)
        {
            if (!ThingTable.TryGet(doomedNum, out var def)) return null;

            var go = new GameObject($"Thing_{doomedNum}_{def.Sprite}",
                typeof(MeshFilter), typeof(MeshRenderer));
            if (parent != null)
                go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = feetPosition;

            var bb = go.AddComponent<SpriteBillboard>();
            bb.Init(cache, def.Sprite, def.Frame, worldScale,
                    doomAngleDeg: 0f, spawnCeiling: false, ceilingY: 0f);
            bb.SetPickupUpscale(true);
            bb.SetStaticFrame(def.Frame);
            cache.GetPickup(def.Sprite, def.Frame, 0);

            go.AddComponent<ThingPickup>().Init(doomedNum, worldScale);
            if (PickupAnimationTable.TryGet(doomedNum, out var animation))
            {
                foreach (int frame in animation.Frames)
                    for (int rot = 0; rot < 8; rot++)
                        cache.GetPickup(def.Sprite, frame, rot);
                go.AddComponent<PickupAnimator>().Init(bb, animation);
            }

            var registry = WorldStateRegistry.Instance;
            if (registry != null)
            {
                int spawnId = forcedSpawnId ?? registry.AllocateSpawnId();
                var id = go.AddComponent<RuntimeEntityIdentity>();
                id.Init(spawnId);
                registry.RegisterSpawned(id);
            }

            return go;
        }
    }
}
