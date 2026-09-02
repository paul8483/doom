using UnityEngine;

namespace Doom.MapBuild
{
    /// Shared RaycastNonAlloc buffer for the per-frame floor probes (lift
    /// rider, floor damage, secret poll). Main thread only; consume the hits
    /// before the next query.
    public static class RaycastScratch
    {
        public static readonly RaycastHit[] Hits = new RaycastHit[32];
    }
}
