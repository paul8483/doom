using Doom.Game;

namespace Doom.MapBuild
{
    public interface IProjectileSnapshotSource
    {
        ProjectileSnapshot CaptureSnapshot(int spawnId, WorldStateRegistry registry);
    }
}
