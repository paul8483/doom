using System.Collections.Generic;

namespace Doom.Map
{
    public sealed class SectorPolygon
    {
        public int SectorIdx { get; }
        public bool IsValid { get; }
        public IReadOnlyList<int> Outer { get; }
        public IReadOnlyList<IReadOnlyList<int>> Holes { get; }

        public SectorPolygon(int sectorIdx, bool isValid,
                             IReadOnlyList<int> outer,
                             IReadOnlyList<IReadOnlyList<int>> holes)
        {
            SectorIdx = sectorIdx;
            IsValid = isValid;
            Outer = outer;
            Holes = holes;
        }

        public static SectorPolygon Invalid(int sectorIdx)
            => new SectorPolygon(sectorIdx, false,
                                 System.Array.Empty<int>(),
                                 System.Array.Empty<IReadOnlyList<int>>());
    }
}
