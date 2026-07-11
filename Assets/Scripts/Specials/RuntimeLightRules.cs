using System;
using Doom.Map;

namespace Doom.Specials
{
    /// Sector / linedef light effect kinds (p_lights.c / p_spec.c).
    public enum SectorLightKind : byte
    {
        None = 0,
        Flicker = 1,      // sector special 1
        Strobe = 2,       // 2,3,4,12,13 + linedef 17
        Glow = 3,         // 8
        FireFlicker = 4,  // 17
    }

    /// Mutable per-sector light thinker state. Profile-independent.
    public struct SectorLightState
    {
        public SectorLightKind Kind;
        public int Light;
        public int MinLight;
        public int MaxLight;
        public int Count;
        public int Direction; // glow: +1 / -1
        public int BrightTime;
        public int DarkTime;

        public static SectorLightState Static(int light) => new SectorLightState
        {
            Kind = SectorLightKind.None,
            Light = ClampLight(light),
            MinLight = ClampLight(light),
            MaxLight = ClampLight(light),
            Count = 0,
            Direction = 0,
            BrightTime = 0,
            DarkTime = 0,
        };

        public static int ClampLight(int light)
        {
            if (light < 0) return 0;
            if (light > 255) return 255;
            return light;
        }
    }

    /// Pure DOOM light rules. Randomness injected so Specials stays free of Doom.Game.
    public static class RuntimeLightRules
    {
        public const int StrobeBright = 5;
        public const int FastDark = 15;
        public const int SlowDark = 35;
        public const int GlowSpeed = 8;

        /// Map sector special → light kind. Damage/secret bits ignored here.
        public static bool TryKindFromSectorSpecial(int special, out SectorLightKind kind)
        {
            switch (special)
            {
                case 1:
                    kind = SectorLightKind.Flicker;
                    return true;
                case 2:
                case 4:
                case 12:
                    kind = SectorLightKind.Strobe;
                    return true;
                case 3:
                case 13:
                    kind = SectorLightKind.Strobe;
                    return true;
                case 8:
                    kind = SectorLightKind.Glow;
                    return true;
                case 17:
                    kind = SectorLightKind.FireFlicker;
                    return true;
                default:
                    kind = SectorLightKind.None;
                    return false;
            }
        }

        public static int StrobeDarkTime(int sectorSpecial) =>
            sectorSpecial == 3 || sectorSpecial == 13 ? SlowDark : FastDark;

        /// Build initial thinker from SECTORS light + special + neighbor lights.
        public static SectorLightState InitFromSector(
            int lightLevel, int sectorSpecial, int lowestNeighborLight)
        {
            lightLevel = SectorLightState.ClampLight(lightLevel);
            if (!TryKindFromSectorSpecial(sectorSpecial, out var kind))
                return SectorLightState.Static(lightLevel);

            int min = SectorLightState.ClampLight(lowestNeighborLight);
            if (min == lightLevel) min = 0;

            switch (kind)
            {
                case SectorLightKind.Flicker:
                    return new SectorLightState
                    {
                        Kind = kind,
                        Light = lightLevel,
                        MinLight = min,
                        MaxLight = lightLevel,
                        Count = 1,
                        Direction = 0,
                        BrightTime = 0,
                        DarkTime = 0,
                    };
                case SectorLightKind.Strobe:
                    return new SectorLightState
                    {
                        Kind = kind,
                        Light = lightLevel,
                        MinLight = min,
                        MaxLight = lightLevel,
                        Count = StrobeBright,
                        Direction = 1, // 1 = bright phase
                        BrightTime = StrobeBright,
                        DarkTime = StrobeDarkTime(sectorSpecial),
                    };
                case SectorLightKind.Glow:
                    return new SectorLightState
                    {
                        Kind = kind,
                        Light = lightLevel,
                        MinLight = min,
                        MaxLight = lightLevel,
                        Count = 0,
                        Direction = -1,
                        BrightTime = 0,
                        DarkTime = 0,
                    };
                case SectorLightKind.FireFlicker:
                    return new SectorLightState
                    {
                        Kind = kind,
                        Light = lightLevel,
                        MinLight = min,
                        MaxLight = lightLevel,
                        Count = 4,
                        Direction = 0,
                        BrightTime = 0,
                        DarkTime = 0,
                    };
                default:
                    return SectorLightState.Static(lightLevel);
            }
        }

        /// Linedef 17: start strobing tagged sector (fast dark).
        public static SectorLightState StartStrobe(int currentLight, int lowestNeighborLight)
        {
            int max = SectorLightState.ClampLight(currentLight);
            int min = SectorLightState.ClampLight(lowestNeighborLight);
            if (min == max) min = 0;
            return new SectorLightState
            {
                Kind = SectorLightKind.Strobe,
                Light = max,
                MinLight = min,
                MaxLight = max,
                Count = StrobeBright,
                Direction = 1,
                BrightTime = StrobeBright,
                DarkTime = FastDark,
            };
        }

        public static int LowestNeighborLight(MapData map, int sectorIdx, Func<int, int> lightOf)
        {
            int best = int.MaxValue;
            bool any = false;
            foreach (int n in Neighbors.OfSector(map, sectorIdx))
            {
                any = true;
                int l = lightOf(n);
                if (l < best) best = l;
            }
            return any ? best : lightOf(sectorIdx);
        }

        public static int HighestNeighborLight(MapData map, int sectorIdx, Func<int, int> lightOf)
        {
            int best = int.MinValue;
            bool any = false;
            foreach (int n in Neighbors.OfSector(map, sectorIdx))
            {
                any = true;
                int l = lightOf(n);
                if (l > best) best = l;
            }
            return any ? best : lightOf(sectorIdx);
        }

        /// Instant linedef light change. bright &lt; 0 → highest neighbor; else absolute.
        public static int ResolveLinedefTarget(
            MapData map, int sectorIdx, int bright, Func<int, int> lightOf)
        {
            if (bright < 0)
                return HighestNeighborLight(map, sectorIdx, lightOf);
            return SectorLightState.ClampLight(bright);
        }

        /// Advance one tic. `random` is P_Random (0..255); unused kinds ignore it.
        public static SectorLightState Tick(SectorLightState state, Func<int> random)
        {
            if (state.Kind == SectorLightKind.None)
                return state;

            switch (state.Kind)
            {
                case SectorLightKind.Flicker:
                    return TickFlicker(state, random);
                case SectorLightKind.Strobe:
                    return TickStrobe(state);
                case SectorLightKind.Glow:
                    return TickGlow(state);
                case SectorLightKind.FireFlicker:
                    return TickFire(state, random);
                default:
                    return state;
            }
        }

        static SectorLightState TickFlicker(SectorLightState s, Func<int> random)
        {
            s.Count--;
            if (s.Count > 0) return s;
            int r = random != null ? random() : 0;
            if ((r & 3) != 0)
            {
                s.Light = s.MaxLight;
                s.Count = (r & 7) + 1;
            }
            else
            {
                s.Light = s.MinLight;
                s.Count = (r & 7) + 1;
            }
            return s;
        }

        static SectorLightState TickStrobe(SectorLightState s)
        {
            s.Count--;
            if (s.Count > 0) return s;
            if (s.Light == s.MinLight)
            {
                s.Light = s.MaxLight;
                s.Count = s.BrightTime > 0 ? s.BrightTime : StrobeBright;
                s.Direction = 1;
            }
            else
            {
                s.Light = s.MinLight;
                s.Count = s.DarkTime > 0 ? s.DarkTime : FastDark;
                s.Direction = 0;
            }
            return s;
        }

        static SectorLightState TickGlow(SectorLightState s)
        {
            s.Light += s.Direction * GlowSpeed;
            if (s.Light <= s.MinLight)
            {
                s.Light = s.MinLight;
                s.Direction = 1;
            }
            else if (s.Light >= s.MaxLight)
            {
                s.Light = s.MaxLight;
                s.Direction = -1;
            }
            return s;
        }

        static SectorLightState TickFire(SectorLightState s, Func<int> random)
        {
            s.Count--;
            if (s.Count > 0) return s;
            int r = random != null ? random() : 0;
            int amount = (r & 3) * 16;
            s.Light = s.MaxLight - amount;
            if (s.Light < s.MinLight) s.Light = s.MinLight;
            s.Count = 4;
            return s;
        }

        /// Linedef special → action. Returns false if not a light linedef.
        public static bool TryLinedefAction(int special, out int absoluteBright, out bool startStrobe)
        {
            absoluteBright = 0;
            startStrobe = false;
            switch (special)
            {
                case 35:
                case 79:
                case 139:
                    absoluteBright = 35;
                    return true;
                case 13:
                case 81:
                case 138:
                    absoluteBright = 255;
                    return true;
                case 12:
                case 80:
                    absoluteBright = -1; // highest neighbor
                    return true;
                case 104:
                    absoluteBright = -2; // lowest neighbor (EV_LightTurnOn style off)
                    return true;
                case 17:
                    startStrobe = true;
                    return true;
                default:
                    return false;
            }
        }
    }
}
