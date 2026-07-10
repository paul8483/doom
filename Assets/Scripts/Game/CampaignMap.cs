using System;

namespace Doom.Game
{
    /// Validated DOOM episode map name in canonical <c>ExMy</c> form (uppercase).
    public readonly struct CampaignMap : IEquatable<CampaignMap>
    {
        public string Name { get; }

        CampaignMap(string canonicalName) => Name = canonicalName;

        public static bool TryParse(string raw, out CampaignMap map)
        {
            map = default;
            if (!CampaignRoute.TryNormalize(raw, out string canonical))
                return false;
            map = new CampaignMap(canonical);
            return true;
        }

        public bool Equals(CampaignMap other) =>
            string.Equals(Name, other.Name, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is CampaignMap other && Equals(other);

        public override int GetHashCode() =>
            Name != null ? StringComparer.Ordinal.GetHashCode(Name) : 0;

        public override string ToString() => Name ?? "";

        public static bool operator ==(CampaignMap a, CampaignMap b) => a.Equals(b);
        public static bool operator !=(CampaignMap a, CampaignMap b) => !a.Equals(b);
    }
}
