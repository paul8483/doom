namespace Doom.Game
{
    public enum StepResult { Moved, Blocked, BlockedByDoor }

    /// Everything the monster FSM asks of / commands in the world.
    /// Distances and deltas are in DOOM units; +y = north (Unity +z).
    public interface IMonsterWorld
    {
        bool CanSeeTarget(bool frontOnly);
        float DistanceToTarget();
        float TargetRadiusUnits();
        void TargetDelta(out float dx, out float dy);
        void FaceTarget();

        StepResult TryStep(Dir8 dir);
        void UseDoor();                 // blocked by a door: use it like the player's E

        void MeleeHit(int damage);
        void FireHitscan(int count);    // count bullets, spread/damage rolled by the world
        void LaunchMissile();

        void SetFrame(int frame);       // current sprite frame (rotations stay live)
        void OnDeathStarted();          // first death frame: collider off
        void OnBecameCorpse();          // death sequence over: static corpse frame
    }
}
