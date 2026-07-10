using System;
using System.Collections.Generic;

namespace Doom.Game
{
    /// Pure campaign session: current map, carry-over inventory, episode completion.
    /// No Unity types. Position is never stored here — full saves use a separate snapshot.
    public sealed class SessionState
    {
        readonly List<string> availableMaps = new List<string>();

        public bool IsActive { get; private set; }
        public string CurrentMap { get; private set; }
        /// Null means the next spawn uses a fresh pistol start.
        public PlayerCarryState Carry { get; private set; }
        public bool EpisodeComplete { get; private set; }
        public ExitKind? LastExitKind { get; private set; }
        public bool LastUsedSecretFallback { get; private set; }

        public IReadOnlyList<string> AvailableMaps => availableMaps;

        /// Start a new campaign on <paramref name="startMap"/>. Clears carry and completion.
        public void BeginNewGame(string startMap, IEnumerable<string> available)
        {
            if (available == null) throw new ArgumentNullException(nameof(available));
            if (!CampaignRoute.TryNormalize(startMap, out string canonical))
                throw new ArgumentException($"Invalid start map '{startMap}'.", nameof(startMap));

            availableMaps.Clear();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string raw in available)
            {
                if (CampaignRoute.TryNormalize(raw, out string map) && seen.Add(map))
                    availableMaps.Add(map);
            }

            if (!seen.Contains(canonical))
                throw new InvalidOperationException(
                    $"Start map '{canonical}' is not in the available map set.");

            IsActive = true;
            CurrentMap = canonical;
            Carry = null;
            EpisodeComplete = false;
            LastExitKind = null;
            LastUsedSecretFallback = false;
        }

        /// Capture leaving inventory (keys/powers are caller-cleared / omitted), resolve
        /// the next map, and store carry for the subsequent spawn.
        public CampaignResolveResult Advance(ExitKind exit, PlayerCarryState leavingState)
        {
            EnsureActive();
            if (leavingState == null) throw new ArgumentNullException(nameof(leavingState));

            var result = CampaignRoute.Resolve(CurrentMap, exit, availableMaps);
            LastExitKind = exit;
            LastUsedSecretFallback = result.UsedSecretFallback;

            if (result.Outcome == CampaignOutcome.EpisodeComplete)
            {
                EpisodeComplete = true;
                Carry = null;
                return result;
            }

            CurrentMap = result.NextMap;
            Carry = leavingState;
            EpisodeComplete = false;
            return result;
        }

        /// Death restart / map retry: stay on CurrentMap, drop carry-over.
        public void RestartCurrentMap()
        {
            EnsureActive();
            Carry = null;
            EpisodeComplete = false;
            LastExitKind = null;
            LastUsedSecretFallback = false;
        }

        /// Full session teardown (Quit to Main / New Game prep).
        public void Clear()
        {
            IsActive = false;
            CurrentMap = null;
            Carry = null;
            EpisodeComplete = false;
            LastExitKind = null;
            LastUsedSecretFallback = false;
            availableMaps.Clear();
        }

        void EnsureActive()
        {
            if (!IsActive)
                throw new InvalidOperationException("Session is not active.");
        }
    }
}
