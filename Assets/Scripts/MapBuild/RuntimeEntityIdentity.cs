using UnityEngine;

namespace Doom.MapBuild
{
    /// Stable identity for a runtime-spawned entity (death drop / projectile).
    public sealed class RuntimeEntityIdentity : MonoBehaviour
    {
        public int SpawnId { get; private set; } = -1;

        public void Init(int spawnId)
        {
            if (spawnId < 0)
                throw new System.ArgumentOutOfRangeException(nameof(spawnId));
            SpawnId = spawnId;
        }
    }
}
