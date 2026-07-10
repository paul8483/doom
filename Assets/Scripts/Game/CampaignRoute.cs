using System;
using System.Collections.Generic;

namespace Doom.Game
{
    /// How the player left the current map.
    public enum ExitKind
    {
        Normal,
        Secret
    }

    /// Result of resolving the next campaign step.
    public enum CampaignOutcome
    {
        NextMap,
        EpisodeComplete
    }

    /// Immutable resolve result. <see cref="NextMap"/> is set only for
    /// <see cref="CampaignOutcome.NextMap"/>.
    public readonly struct CampaignResolveResult
    {
        public CampaignOutcome Outcome { get; }
        public string NextMap { get; }
        /// True when a secret exit fell back to the normal route because the
        /// secret target map was missing from the available set.
        public bool UsedSecretFallback { get; }

        public CampaignResolveResult(CampaignOutcome outcome, string nextMap, bool usedSecretFallback)
        {
            Outcome = outcome;
            NextMap = nextMap;
            UsedSecretFallback = usedSecretFallback;
        }

        public static CampaignResolveResult Next(string map, bool secretFallback = false) =>
            new CampaignResolveResult(CampaignOutcome.NextMap, map, secretFallback);

        public static CampaignResolveResult Complete(bool secretFallback = false) =>
            new CampaignResolveResult(CampaignOutcome.EpisodeComplete, null, secretFallback);
    }

    /// Pure E1 campaign routing. Normal/secret transitions live in one table —
    /// never <c>map + 1</c> arithmetic. No Unity or WAD I/O.
    public static class CampaignRoute
    {
        // Canonical DOOM E1 route. Secret exit always targets E1M9 when present;
        // completing E1M9 returns to E1M4 (the map after the canonical secret entry).
        static readonly Dictionary<string, (string Normal, string Secret)> Table =
            new Dictionary<string, (string, string)>(StringComparer.Ordinal)
            {
                ["E1M1"] = ("E1M2", "E1M9"),
                ["E1M2"] = ("E1M3", "E1M9"),
                ["E1M3"] = ("E1M4", "E1M9"),
                ["E1M4"] = ("E1M5", "E1M9"),
                ["E1M5"] = ("E1M6", "E1M9"),
                ["E1M6"] = ("E1M7", "E1M9"),
                ["E1M7"] = ("E1M8", "E1M9"),
                ["E1M8"] = (null, "E1M9"), // null Normal = EpisodeComplete
                ["E1M9"] = ("E1M4", "E1M4"),
            };

        /// Accepts <c>ExMy</c> (case-insensitive). Rejects <c>MAPxx</c> and malformed names.
        public static bool TryNormalize(string raw, out string canonical)
        {
            canonical = null;
            if (string.IsNullOrEmpty(raw) || raw.Length != 4)
                return false;

            char e = char.ToUpperInvariant(raw[0]);
            char m = char.ToUpperInvariant(raw[2]);
            if (e != 'E' || m != 'M')
                return false;

            int episode = raw[1] - '0';
            int map = raw[3] - '0';
            if (episode < 1 || episode > 4 || map < 1 || map > 9)
                return false;

            canonical = $"E{episode}M{map}";
            return true;
        }

        /// Resolve the next campaign step for <paramref name="currentMap"/>.
        /// <paramref name="availableMaps"/> is the set of map markers present in the WAD
        /// (any casing); missing normal targets throw, missing secret targets fall back.
        public static CampaignResolveResult Resolve(
            string currentMap,
            ExitKind exit,
            IEnumerable<string> availableMaps)
        {
            if (availableMaps == null)
                throw new ArgumentNullException(nameof(availableMaps));

            if (!TryNormalize(currentMap, out string current))
                throw new ArgumentException($"Invalid campaign map name: '{currentMap}'.", nameof(currentMap));

            if (!Table.TryGetValue(current, out var routes))
                throw new ArgumentException($"No E1 campaign route for '{current}'.", nameof(currentMap));

            var available = BuildAvailableSet(availableMaps);

            if (exit == ExitKind.Secret)
            {
                string secretTarget = routes.Secret;
                if (available.Contains(secretTarget))
                    return CampaignResolveResult.Next(secretTarget);

                // Controlled fallback: secret exit without E1M9 (or return target) uses normal.
                return ResolveNormal(current, routes.Normal, available, secretFallback: true);
            }

            return ResolveNormal(current, routes.Normal, available, secretFallback: false);
        }

        static CampaignResolveResult ResolveNormal(
            string current,
            string normalTarget,
            HashSet<string> available,
            bool secretFallback)
        {
            if (normalTarget == null)
                return CampaignResolveResult.Complete(secretFallback);

            if (!available.Contains(normalTarget))
                throw new InvalidOperationException(
                    $"Campaign route from '{current}' targets missing map '{normalTarget}'.");

            return CampaignResolveResult.Next(normalTarget, secretFallback);
        }

        static HashSet<string> BuildAvailableSet(IEnumerable<string> availableMaps)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (string raw in availableMaps)
            {
                if (TryNormalize(raw, out string canonical))
                    set.Add(canonical);
            }
            return set;
        }
    }
}
