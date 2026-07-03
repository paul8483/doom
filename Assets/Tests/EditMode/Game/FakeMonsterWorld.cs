using System.Collections.Generic;

namespace Doom.Game.Tests
{
    /// Сценарный мир: тесты выставляют поля, мозг читает/командует.
    public sealed class FakeMonsterWorld : IMonsterWorld
    {
        public bool SeesFront, Sees360;
        public float Dist = 1000f;
        public float TargetRadius = 16f;
        public float Dx, Dy;
        public HashSet<Dir8> Blocked = new HashSet<Dir8>();
        public bool BlockedByDoor;

        public List<string> Log = new List<string>();
        public int LastFrame = -1;

        public bool CanSeeTarget(bool frontOnly) => frontOnly ? SeesFront : (SeesFront || Sees360);
        public float DistanceToTarget() => Dist;
        public float TargetRadiusUnits() => TargetRadius;
        public void TargetDelta(out float dx, out float dy) { dx = Dx; dy = Dy; }
        public void FaceTarget() => Log.Add("face");

        public StepResult TryStep(Dir8 dir)
        {
            if (Blocked.Contains(dir))
                return BlockedByDoor ? StepResult.BlockedByDoor : StepResult.Blocked;
            Log.Add($"step:{dir}");
            return StepResult.Moved;
        }
        public void UseDoor() => Log.Add("door");

        public void MeleeHit(int damage) => Log.Add($"melee:{damage}");
        public void FireHitscan(int count) => Log.Add($"hitscan:{count}");
        public void LaunchMissile() => Log.Add("missile");

        public void SetFrame(int frame) { LastFrame = frame; }
        public void OnDeathStarted() => Log.Add("death-start");
        public void OnBecameCorpse() => Log.Add("corpse");
    }
}
