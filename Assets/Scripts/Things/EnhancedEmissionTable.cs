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

            // Warm yellow lamps / candles — strong enough to read a floor pool in dark sectors.
            void Lamp(int n, float intensity, float range, float importance, bool shadow = false)
                => d[n] = new EnhancedEmissionDef(
                    intensity, range, 1f, 0.85f, 0.45f, importance, shadow);

            // Colored firesticks.
            void Fire(int n, float r, float g, float b, float intensity, float range, float importance)
                => d[n] = new EnhancedEmissionDef(intensity, range, r, g, b, importance, wantsShadow: true);

            // Cool tech pillars / lamps (Freedoom ELEC often reads as a powered column).
            void Tech(int n, float intensity, float range, float importance)
                => d[n] = new EnhancedEmissionDef(
                    intensity, range, 0.55f, 0.75f, 1f, importance, wantsShadow: false);

            Lamp(2028, intensity: 2.8f, range: 192f, importance: 1.0f);         // COLU floor lamp
            Lamp(35, intensity: 3.0f, range: 208f, importance: 1.05f);           // CBRA candelabra
            Lamp(34, intensity: 1.4f, range: 96f, importance: 0.55f);            // CAND candle
            Lamp(29, intensity: 1.8f, range: 128f, importance: 0.7f);            // POL3 skulls+candles
            // Doom II tech lamps (harmless if absent from the IWAD).
            Lamp(85, intensity: 2.6f, range: 192f, importance: 0.95f);           // TLMP
            Lamp(86, intensity: 2.2f, range: 160f, importance: 0.85f);           // TLP2

            Tech(48, intensity: 2.0f, range: 160f, importance: 0.8f);            // ELEC techno pillar

            Fire(44, 0.35f, 0.45f, 1f, 2.2f, 176f, 0.95f);   // TBLU
            Fire(45, 0.35f, 1f, 0.4f, 2.2f, 176f, 0.95f);    // TGRN
            Fire(46, 1f, 0.35f, 0.25f, 2.2f, 176f, 0.95f);   // TRED
            Fire(55, 0.35f, 0.45f, 1f, 1.6f, 128f, 0.75f);   // SMBT
            Fire(56, 0.35f, 1f, 0.4f, 1.6f, 128f, 0.75f);    // SMGT
            Fire(57, 1f, 0.35f, 0.25f, 1.6f, 128f, 0.75f);   // SMRT
            Fire(70, 1f, 0.45f, 0.15f, 2.6f, 192f, 1.05f);   // FCAN burning barrel

            return d;
        }
    }
}
