using System.Collections.Generic;

namespace Doom.Things
{
    /// Presentation-only light parameters for known emissive decorations.
    /// Intensity/range are DOOM-unit friendly scalars consumed by MapBuild after
    /// multiplying by worldScale for range. Not saved; not gameplay.
    public readonly struct EnhancedEmissionDef
    {
        public readonly float Intensity;
        public readonly float RangeDoom;
        public readonly float ColorR;
        public readonly float ColorG;
        public readonly float ColorB;
        public readonly float Importance;
        public readonly bool WantsShadow;

        public EnhancedEmissionDef(
            float intensity,
            float rangeDoom,
            float colorR,
            float colorG,
            float colorB,
            float importance,
            bool wantsShadow = false)
        {
            Intensity = intensity;
            RangeDoom = rangeDoom;
            ColorR = colorR;
            ColorG = colorG;
            ColorB = colorB;
            Importance = importance;
            WantsShadow = wantsShadow;
        }
    }

    /// Explicit doomednum → emission lookup. Unknown types get no Unity Light
    /// (material emission alone is enough).
    public static class EnhancedEmissionTable
    {
        static readonly Dictionary<int, EnhancedEmissionDef> Defs = Build();

        public static bool TryGet(int doomEdNum, out EnhancedEmissionDef def)
            => Defs.TryGetValue(doomEdNum, out def);

        public static bool Contains(int doomEdNum) => Defs.ContainsKey(doomEdNum);

        public static IEnumerable<int> AllDoomEdNums => Defs.Keys;

        static Dictionary<int, EnhancedEmissionDef> Build()
        {
            var d = new Dictionary<int, EnhancedEmissionDef>();

            // Warm yellow lamps / candles.
            void Lamp(int n, float intensity, float range, float importance, bool shadow = false)
                => d[n] = new EnhancedEmissionDef(
                    intensity, range, 1f, 0.85f, 0.45f, importance, shadow);

            // Colored firesticks.
            void Fire(int n, float r, float g, float b, float intensity, float range, float importance)
                => d[n] = new EnhancedEmissionDef(intensity, range, r, g, b, importance, wantsShadow: true);

            Lamp(2028, intensity: 1.4f, range: 128f, importance: 0.7f);          // COLU floor lamp
            Lamp(35, intensity: 1.6f, range: 160f, importance: 0.8f);            // CBRA candelabra
            Lamp(34, intensity: 0.7f, range: 64f, importance: 0.4f);             // CAND candle
            Lamp(29, intensity: 0.9f, range: 96f, importance: 0.5f);             // POL3 skulls+candles

            Fire(44, 0.35f, 0.45f, 1f, 1.5f, 144f, 0.85f);   // TBLU
            Fire(45, 0.35f, 1f, 0.4f, 1.5f, 144f, 0.85f);    // TGRN
            Fire(46, 1f, 0.35f, 0.25f, 1.5f, 144f, 0.85f);   // TRED
            Fire(55, 0.35f, 0.45f, 1f, 1.1f, 112f, 0.7f);    // SMBT
            Fire(56, 0.35f, 1f, 0.4f, 1.1f, 112f, 0.7f);     // SMGT
            Fire(57, 1f, 0.35f, 0.25f, 1.1f, 112f, 0.7f);    // SMRT
            Fire(70, 1f, 0.45f, 0.15f, 1.8f, 160f, 0.95f);   // FCAN burning barrel

            return d;
        }
    }
}
