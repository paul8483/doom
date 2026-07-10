using System;
using System.Collections.Generic;

namespace Doom.Game
{
    /// Immutable end-of-level stats snapshot for intermission.
    public readonly struct LevelStatsSnapshot
    {
        public readonly int Kills;
        public readonly int KillTotal;
        public readonly int Items;
        public readonly int ItemTotal;
        public readonly int Secrets;
        public readonly int SecretTotal;
        public readonly int Tics;

        public LevelStatsSnapshot(
            int kills, int killTotal, int items, int itemTotal,
            int secrets, int secretTotal, int tics)
        {
            Kills = kills;
            KillTotal = killTotal;
            Items = items;
            ItemTotal = itemTotal;
            Secrets = secrets;
            SecretTotal = secretTotal;
            Tics = tics;
        }

        public int KillPercent => Percent(Kills, KillTotal);
        public int ItemPercent => Percent(Items, ItemTotal);
        public int SecretPercent => Percent(Secrets, SecretTotal);

        static int Percent(int n, int total)
        {
            if (total <= 0) return 0;
            return (n * 100) / total;
        }
    }

    /// Pure level statistics. Counts never exceed totals; events are deduped by id.
    public sealed class LevelStats
    {
        readonly HashSet<int> killIds = new HashSet<int>();
        readonly HashSet<int> itemIds = new HashSet<int>();
        readonly HashSet<int> secretIds = new HashSet<int>();

        public int Kills => killIds.Count;
        public int KillTotal { get; private set; }
        public int Items => itemIds.Count;
        public int ItemTotal { get; private set; }
        public int Secrets => secretIds.Count;
        public int SecretTotal { get; private set; }
        public int Tics { get; private set; }

        public void SetTotals(int killTotal, int itemTotal, int secretTotal)
        {
            if (killTotal < 0) killTotal = 0;
            if (itemTotal < 0) itemTotal = 0;
            if (secretTotal < 0) secretTotal = 0;
            KillTotal = killTotal;
            ItemTotal = itemTotal;
            SecretTotal = secretTotal;
        }

        public void Reset()
        {
            killIds.Clear();
            itemIds.Clear();
            secretIds.Clear();
            KillTotal = ItemTotal = SecretTotal = 0;
            Tics = 0;
        }

        /// Returns true the first time this kill id is registered (and under total).
        public bool TryRegisterKill(int thingIndex)
        {
            if (thingIndex < 0) return false;
            if (killIds.Count >= KillTotal) return false;
            return killIds.Add(thingIndex);
        }

        public bool TryRegisterItem(int thingIndex)
        {
            if (thingIndex < 0) return false;
            if (itemIds.Count >= ItemTotal) return false;
            return itemIds.Add(thingIndex);
        }

        public bool TryRegisterSecret(int sectorIndex)
        {
            if (sectorIndex < 0) return false;
            if (secretIds.Count >= SecretTotal) return false;
            return secretIds.Add(sectorIndex);
        }

        public void AdvanceTics(int tics)
        {
            if (tics <= 0) return;
            if (Tics > int.MaxValue - tics) Tics = int.MaxValue;
            else Tics += tics;
        }

        public LevelStatsSnapshot Snapshot() =>
            new LevelStatsSnapshot(
                Kills, KillTotal, Items, ItemTotal, Secrets, SecretTotal, Tics);

        /// MF_COUNTITEM doomednums present in the E1 pickup set.
        public static bool IsCountItem(int doomedNum) => doomedNum switch
        {
            2014 => true, // health bonus
            2015 => true, // armor bonus
            2013 => true, // soulsphere
            2023 => true, // berserk
            2025 => true, // radiation suit
            _ => false,
        };
    }
}
