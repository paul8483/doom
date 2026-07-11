using System;

namespace Doom.Game
{
    /// Active sector mover phase for save/load.
    public enum MoverPhase : byte
    {
        None = 0,
        Moving = 1,
        Waiting = 2,
    }

    /// Which plane a sector mover animates.
    public enum MoverPlane : byte
    {
        Floor = 0,
        Ceiling = 1,
    }

    public sealed class SectorSnapshot : IEquatable<SectorSnapshot>
    {
        public int Index { get; }
        public float FloorHeight { get; }
        public float CeilingHeight { get; }
        public int LightLevel { get; }

        public bool HasMover { get; }
        public MoverPlane MoverPlane { get; }
        public MoverPhase MoverPhase { get; }
        /// +1 up / -1 down while moving; ignored when phase is None.
        public int MoverDirection { get; }
        public float MoverTarget { get; }
        public float MoverSpeed { get; }
        /// Remaining wait tics when phase is Waiting.
        public int MoverWaitTics { get; }

        public SectorSnapshot(
            int index,
            float floorHeight, float ceilingHeight, int lightLevel,
            bool hasMover,
            MoverPlane moverPlane, MoverPhase moverPhase,
            int moverDirection, float moverTarget, float moverSpeed, int moverWaitTics)
        {
            Index = index;
            FloorHeight = floorHeight;
            CeilingHeight = ceilingHeight;
            LightLevel = lightLevel;
            HasMover = hasMover;
            MoverPlane = moverPlane;
            MoverPhase = moverPhase;
            MoverDirection = moverDirection;
            MoverTarget = moverTarget;
            MoverSpeed = moverSpeed;
            MoverWaitTics = moverWaitTics;
        }

        public bool Equals(SectorSnapshot other)
        {
            if (other is null) return false;
            return Index == other.Index
                   && FloorHeight.Equals(other.FloorHeight)
                   && CeilingHeight.Equals(other.CeilingHeight)
                   && LightLevel == other.LightLevel
                   && HasMover == other.HasMover
                   && MoverPlane == other.MoverPlane
                   && MoverPhase == other.MoverPhase
                   && MoverDirection == other.MoverDirection
                   && MoverTarget.Equals(other.MoverTarget)
                   && MoverSpeed.Equals(other.MoverSpeed)
                   && MoverWaitTics == other.MoverWaitTics;
        }

        public override bool Equals(object obj) => Equals(obj as SectorSnapshot);
        public override int GetHashCode() =>
            HashCode.Combine(Index, FloorHeight, CeilingHeight, LightLevel, HasMover,
                (int)MoverPhase, MoverTarget, MoverWaitTics);
    }

    public sealed class LineSnapshot : IEquatable<LineSnapshot>
    {
        public int Index { get; }
        public bool Fired { get; }
        public bool SwitchOn { get; }

        public LineSnapshot(int index, bool fired, bool switchOn)
        {
            Index = index;
            Fired = fired;
            SwitchOn = switchOn;
        }

        public bool Equals(LineSnapshot other)
        {
            if (other is null) return false;
            return Index == other.Index && Fired == other.Fired && SwitchOn == other.SwitchOn;
        }

        public override bool Equals(object obj) => Equals(obj as LineSnapshot);
        public override int GetHashCode() => HashCode.Combine(Index, Fired, SwitchOn);
    }

    public sealed class ThingSnapshot : IEquatable<ThingSnapshot>
    {
        public int MapThingIndex { get; }
        public bool Present { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float AngleDegrees { get; }
        public int Health { get; }
        public int Frame { get; }
        public int Flags { get; }
        public SaveEntityId Target { get; }

        public ThingSnapshot(
            int mapThingIndex, bool present,
            float x, float y, float z, float angleDegrees,
            int health, int frame, int flags, SaveEntityId target)
        {
            MapThingIndex = mapThingIndex;
            Present = present;
            X = x;
            Y = y;
            Z = z;
            AngleDegrees = angleDegrees;
            Health = health;
            Frame = frame;
            Flags = flags;
            Target = target;
        }

        public bool Equals(ThingSnapshot other)
        {
            if (other is null) return false;
            return MapThingIndex == other.MapThingIndex
                   && Present == other.Present
                   && X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z)
                   && AngleDegrees.Equals(other.AngleDegrees)
                   && Health == other.Health && Frame == other.Frame && Flags == other.Flags
                   && Target.Equals(other.Target);
        }

        public override bool Equals(object obj) => Equals(obj as ThingSnapshot);
        public override int GetHashCode() =>
            HashCode.Combine(MapThingIndex, Present, X, Y, Health, Frame, Flags, Target);
    }

    public enum ProjectilePhase
    {
        Flying = 0,
        Exploding = 1,
    }

    public sealed class ProjectileSnapshot : IEquatable<ProjectileSnapshot>
    {
        public int SpawnId { get; }
        public int Type { get; }
        public SaveEntityId Owner { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float VelX { get; }
        public float VelY { get; }
        public float VelZ { get; }
        public float RemainingLife { get; }
        public ProjectilePhase Phase { get; }
        public int FrameIndex { get; }
        public float ShotDirX { get; }
        public float ShotDirY { get; }
        public float ShotDirZ { get; }
        public bool SprayApplied { get; }

        public ProjectileSnapshot(
            int spawnId, int type, SaveEntityId owner,
            float x, float y, float z,
            float velX, float velY, float velZ,
            float remainingLife)
            : this(
                spawnId, type, owner,
                x, y, z, velX, velY, velZ, remainingLife,
                ProjectilePhase.Flying, 0, 0f, 0f, 0f, false)
        {
        }

        public ProjectileSnapshot(
            int spawnId, int type, SaveEntityId owner,
            float x, float y, float z,
            float velX, float velY, float velZ,
            float remainingLife,
            ProjectilePhase phase, int frameIndex,
            float shotDirX, float shotDirY, float shotDirZ,
            bool sprayApplied)
        {
            SpawnId = spawnId;
            Type = type;
            Owner = owner;
            X = x;
            Y = y;
            Z = z;
            VelX = velX;
            VelY = velY;
            VelZ = velZ;
            RemainingLife = remainingLife;
            Phase = phase;
            FrameIndex = frameIndex;
            ShotDirX = shotDirX;
            ShotDirY = shotDirY;
            ShotDirZ = shotDirZ;
            SprayApplied = sprayApplied;
        }

        public bool Equals(ProjectileSnapshot other)
        {
            if (other is null) return false;
            return SpawnId == other.SpawnId && Type == other.Type && Owner.Equals(other.Owner)
                   && X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z)
                   && VelX.Equals(other.VelX) && VelY.Equals(other.VelY) && VelZ.Equals(other.VelZ)
                   && RemainingLife.Equals(other.RemainingLife)
                   && Phase == other.Phase && FrameIndex == other.FrameIndex
                   && ShotDirX.Equals(other.ShotDirX)
                   && ShotDirY.Equals(other.ShotDirY)
                   && ShotDirZ.Equals(other.ShotDirZ)
                   && SprayApplied == other.SprayApplied;
        }

        public override bool Equals(object obj) => Equals(obj as ProjectileSnapshot);
        public override int GetHashCode() =>
            HashCode.Combine(
                HashCode.Combine(SpawnId, Type, Owner, X, Y, Z, RemainingLife),
                HashCode.Combine((int)Phase, FrameIndex, ShotDirX, ShotDirY, ShotDirZ, SprayApplied));
    }

    /// Runtime death-drop / spawned pickup (not a map THINGS entry).
    public sealed class SpawnedPickupSnapshot : IEquatable<SpawnedPickupSnapshot>
    {
        public int SpawnId { get; }
        public int DoomedNum { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public SpawnedPickupSnapshot(int spawnId, int doomedNum, float x, float y, float z)
        {
            SpawnId = spawnId;
            DoomedNum = doomedNum;
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(SpawnedPickupSnapshot other)
        {
            if (other is null) return false;
            return SpawnId == other.SpawnId && DoomedNum == other.DoomedNum
                   && X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        public override bool Equals(object obj) => Equals(obj as SpawnedPickupSnapshot);
        public override int GetHashCode() => HashCode.Combine(SpawnId, DoomedNum, X, Y, Z);
    }

    /// Full mutable world state for a single map, excluding the player singleton.
    public sealed class WorldSnapshot : IEquatable<WorldSnapshot>
    {
        public int GameTic { get; }
        public int NextSpawnId { get; }
        public LevelStatsSnapshot Stats { get; }
        /// Sorted unique kill / item / secret ids for exact restore.
        public int[] KillIds { get; }
        public int[] ItemIds { get; }
        public int[] SecretIds { get; }
        public SectorSnapshot[] Sectors { get; }
        public LineSnapshot[] Lines { get; }
        public ThingSnapshot[] Things { get; }
        public ProjectileSnapshot[] Projectiles { get; }
        public SpawnedPickupSnapshot[] SpawnedPickups { get; }

        public WorldSnapshot(
            int gameTic,
            int nextSpawnId,
            LevelStatsSnapshot stats,
            int[] killIds,
            int[] itemIds,
            int[] secretIds,
            SectorSnapshot[] sectors,
            LineSnapshot[] lines,
            ThingSnapshot[] things,
            ProjectileSnapshot[] projectiles,
            SpawnedPickupSnapshot[] spawnedPickups)
        {
            GameTic = gameTic;
            NextSpawnId = nextSpawnId;
            Stats = stats;
            KillIds = DefensiveCopy(killIds);
            ItemIds = DefensiveCopy(itemIds);
            SecretIds = DefensiveCopy(secretIds);
            Sectors = DefensiveCopy(sectors);
            Lines = DefensiveCopy(lines);
            Things = DefensiveCopy(things);
            Projectiles = DefensiveCopy(projectiles);
            SpawnedPickups = DefensiveCopy(spawnedPickups);
        }

        public static bool TryCreate(
            int gameTic,
            int nextSpawnId,
            LevelStatsSnapshot stats,
            int[] killIds,
            int[] itemIds,
            int[] secretIds,
            SectorSnapshot[] sectors,
            LineSnapshot[] lines,
            ThingSnapshot[] things,
            ProjectileSnapshot[] projectiles,
            SpawnedPickupSnapshot[] spawnedPickups,
            out WorldSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = null;

            if (gameTic < 0)
            {
                error = "Game tic must be non-negative.";
                return false;
            }

            if (nextSpawnId < 0)
            {
                error = "NextSpawnId must be non-negative.";
                return false;
            }

            if (sectors == null || lines == null || things == null
                || projectiles == null || spawnedPickups == null
                || killIds == null || itemIds == null || secretIds == null)
            {
                error = "World arrays must not be null.";
                return false;
            }

            if (!IsSortedUniqueInts(killIds, out error)) return false;
            if (!IsSortedUniqueInts(itemIds, out error)) return false;
            if (!IsSortedUniqueInts(secretIds, out error)) return false;
            if (!IsSortedUnique(sectors, s => s.Index, out error)) return false;
            if (!IsSortedUnique(lines, l => l.Index, out error)) return false;
            if (!IsSortedUnique(things, t => t.MapThingIndex, out error)) return false;
            if (!IsSortedUnique(projectiles, p => p.SpawnId, out error)) return false;
            if (!IsSortedUnique(spawnedPickups, p => p.SpawnId, out error)) return false;

            // SpawnIds across projectiles and pickups must not collide.
            if (!DisjointSpawnIds(projectiles, spawnedPickups, out error)) return false;

            snapshot = new WorldSnapshot(
                gameTic, nextSpawnId, stats, killIds, itemIds, secretIds,
                sectors, lines, things, projectiles, spawnedPickups);
            return true;
        }

        static bool DisjointSpawnIds(
            ProjectileSnapshot[] projectiles, SpawnedPickupSnapshot[] pickups, out string error)
        {
            error = null;
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < projectiles.Length; i++)
            {
                if (!seen.Add(projectiles[i].SpawnId))
                {
                    error = "Duplicate SpawnId across runtime entities.";
                    return false;
                }
            }

            for (int i = 0; i < pickups.Length; i++)
            {
                if (!seen.Add(pickups[i].SpawnId))
                {
                    error = "Duplicate SpawnId across runtime entities.";
                    return false;
                }
            }

            return true;
        }

        static T[] DefensiveCopy<T>(T[] src)
        {
            if (src == null || src.Length == 0) return Array.Empty<T>();
            var copy = new T[src.Length];
            Array.Copy(src, copy, src.Length);
            return copy;
        }

        static bool IsSortedUniqueInts(int[] items, out string error)
        {
            error = null;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] < 0)
                {
                    error = "World entity ids must be non-negative.";
                    return false;
                }

                if (i > 0 && items[i] <= items[i - 1])
                {
                    error = "Id arrays must be sorted by unique ascending id.";
                    return false;
                }
            }

            return true;
        }

        static bool IsSortedUnique<T>(T[] items, Func<T, int> key, out string error)
        {
            error = null;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null)
                {
                    error = "World array entries must not be null.";
                    return false;
                }

                int k = key(items[i]);
                if (k < 0)
                {
                    error = "World entity ids must be non-negative.";
                    return false;
                }

                if (i > 0 && k <= key(items[i - 1]))
                {
                    error = "World arrays must be sorted by unique ascending id.";
                    return false;
                }
            }

            return true;
        }

        public bool Equals(WorldSnapshot other)
        {
            if (other is null) return false;
            if (GameTic != other.GameTic || NextSpawnId != other.NextSpawnId) return false;
            if (!StatsEquals(Stats, other.Stats)) return false;
            return IntArraysEqual(KillIds, other.KillIds)
                   && IntArraysEqual(ItemIds, other.ItemIds)
                   && IntArraysEqual(SecretIds, other.SecretIds)
                   && ArraysEqual(Sectors, other.Sectors)
                   && ArraysEqual(Lines, other.Lines)
                   && ArraysEqual(Things, other.Things)
                   && ArraysEqual(Projectiles, other.Projectiles)
                   && ArraysEqual(SpawnedPickups, other.SpawnedPickups);
        }

        static bool StatsEquals(LevelStatsSnapshot a, LevelStatsSnapshot b) =>
            a.Kills == b.Kills && a.KillTotal == b.KillTotal
            && a.Items == b.Items && a.ItemTotal == b.ItemTotal
            && a.Secrets == b.Secrets && a.SecretTotal == b.SecretTotal
            && a.Tics == b.Tics;

        static bool IntArraysEqual(int[] a, int[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        static bool ArraysEqual<T>(T[] a, T[] b) where T : IEquatable<T>
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i].Equals(b[i])) return false;
            }

            return true;
        }

        public override bool Equals(object obj) => Equals(obj as WorldSnapshot);

        public override int GetHashCode() =>
            HashCode.Combine(GameTic, NextSpawnId, Sectors.Length, Lines.Length,
                Things.Length, Projectiles.Length, SpawnedPickups.Length);
    }
}
