using System.IO;
using UnityEditor;
using UnityEngine;
using Doom.Graphics;
using Doom.Wad;

namespace Doom.MapBuild.Editor
{
    /// Diagnostic: dumps side-by-side PNGs comparing native (nearest ×4) with the
    /// Enhanced4X pipeline result (dedither → [bleed] → Super-xBR ×2 ×2) to
    /// Logs/upscale-preview/.
    public static class UpscalePreviewMenu
    {
        static readonly string[] WallNames =
        {
            "STARTAN2", "BROWN1", "STARG3", "BIGDOOR2", "MIDGRATE",
            // World redraw stage (2026-08-23/24): pilot + wave 1.
            "COMP2", "COMPTALL", "COMPBLUE", "COMPSPAN", "COMPWERD", "COMPTILE",
            "COMPUTE1", "COMPUTE2", "COMPUTE3", "COMPUTE4", "COMPSTA1",
            "COMPSTA2", "COMPWERA", "COMPWERB", "COMPWERE", "COMPWERF",
            "COMPVENT", "COMPOHSO", "COMPLIT3", "AQCOMP01",
            // Wave 2 (2026-08-24): STAR panel family + masonry.
            "STARTAN1", "STARTAN3", "STARG1", "STARG2", "STARG4",
            "STARGR1", "STARGR2", "STARBR1", "STARBR2",
            "BRICK10", "STONE", "STONE2", "STONE3",
            // Wave 3 (2026-08-24): BROWN rust metal + GRAY concrete (BROWN1
            // was already in the diagnostic list above).
            "BROWN96", "BROWN144", "BROWNGRN", "BROWNHUG", "BROWNPIP",
            "GRAY1", "GRAY2", "GRAY4", "GRAY5", "GRAY7", "GRAY8",
            "GRAYBIG", "GRAYPOIS", "GRAYTALL", "GRAYWIDE",
            // Wave 4 (2026-08-24): metal/supports/jambs, slad+nukage, TEKWALL,
            // crates, big doors (BIGDOOR2 was already in the diagnostic list).
            "SHAWN2", "SUPPORT2", "SUPPORT3", "METAL1", "METAL2", "METAL5",
            "METAL", "DOORSTOP", "DOORTRAK", "SLADWALL", "SLADPOIS",
            "NUKEDGE1", "NUKE24", "TEKWALL1", "TEKWALL2", "TEKWALL4", "MC17",
            "CRATE1", "CRATE2", "CRATELIT", "CRATWIDE", "BIGDOOR6",
            // Wave 4 tail (2026-08-24): LITE strips (GRAY re-roll names are
            // already in the wave-3 block above).
            "LITE5", "LITEBLU4", "LITE3",
            // Wave 5 (2026-08-25): the lift set.
            "PLAT1", "STEP2", "STEP3", "STEP4", "STEP5", "STEP6", "STEPTOP",
            // Wave 6 (2026-08-25): every remaining door.
            "BIGDOOR1", "BIGDOOR3", "BIGDOOR4", "AQDOOR01", "AQDOOR02",
            "DOOR1", "DOOR3", "DOORHI", "SPCDOOR3", "EXITDOOR",
            "EXITSIGN", "EXITSGN2", "DOORBLU", "DOORRED", "DOORYEL",
            // Wave 7 (2026-08-25): the rarities.
            "GSTONE1", "GSTONE2", "GSTVINE2", "MARBFAC3", "MARBGRAY",
            "SKIN2", "SKINEDGE", "SK_LEFT", "SK_RIGHT", "SKSNAKE1",
            "SKSNAKE2", "ASHWALL", "ASHWALL2", "ASHWALL4", "SP_HOT1",
            "ICKWALL1", "ICKWALL2", "ICKWALL3", "ICKWALL4", "CEMENT1",
            "CEMENT3", "CEMENT6", "CEMENT7", "CEMENT8", "BIGBRIK2",
            "A-BRICK3", "A-DBRI28", "A-CONCTE", "A-DROCK1", "PWHITE",
            "ZIMMER3", "BRONZE1", "BRONZE3", "BASE2", "BASE", "SHAWN1",
            "SHAWN02", "SHAWN3", "MC3", "MC5", "MC19", "TEKWALL3",
            "TEKWALL5", "PIPE2", "PLANET1", "STEP1", "WOOD1", "WOODMET1",
            "CRATINY", "CRATE3", "LITEBLU1", "LITEBLU3", "LITE4", "LITERED",
        };

        static readonly string[] FlatNames =
        {
            "FLOOR4_8", "NUKAGE1", "CEIL3_5",
            // Wave 16 (2026-09-01): the AQF flat tail (39 names; the plate set
            // AQF016..AQF075 below arrives with the second generation pass).
            "AQF001", "AQF002", "AQF003", "AQF004", "AQF007", "AQF009",
            "AQF010", "AQF012", "AQF013", "AQF014", "AQF016", "AQF017",
            "AQF018", "AQF021", "AQF022", "AQF029", "AQF030", "AQF031",
            "AQF033", "AQF034", "AQF044", "AQF046", "AQF050", "AQF051",
            "AQF052", "AQF055", "AQF057", "AQF058", "AQF059", "AQF060",
            "AQF065", "AQF067", "AQF068", "AQF069", "AQF070", "AQF071",
            "AQF072", "AQF073", "AQF075",
        };

        [MenuItem("Tools/Doom/Dump Upscale Preview")]
        public static void Dump()
        {
            string wadPath = Path.Combine(
                Application.streamingAssetsPath, "wads", "freedoom1.wad");
            string outDir = Path.Combine(
                Path.GetDirectoryName(Application.dataPath), "Logs", "upscale-preview");
            Directory.CreateDirectory(outDir);

            using var wad = WadFile.Open(wadPath);
            var palette = new Palette(wad.ReadLump("PLAYPAL"));
            var textures = TextureSet.Load(wad);

            foreach (string name in WallNames)
            {
                if (!textures.Contains(name))
                {
                    Debug.LogWarning($"UpscalePreview: wall texture {name} missing, skipped");
                    continue;
                }
                WriteCompare(textures.Build(name, palette), PixelWrapMode.RepeatX, name, outDir);
            }

            foreach (string name in FlatNames)
            {
                if (wad.FindLump(name) < 0)
                {
                    Debug.LogWarning($"UpscalePreview: flat {name} missing, skipped");
                    continue;
                }
                WriteCompare(Flat.Decode(wad.ReadLump(name), palette), PixelWrapMode.RepeatXY, name, outDir);
            }

            Debug.Log($"UpscalePreview: wrote PNGs to {outDir}");
        }

        static void WriteCompare(DecodedImage native, PixelWrapMode wrap, string name, string outDir)
        {
            bool masked = HasTransparent(native);
            var x4 = TextureCache.BuildEnhanced4XDecoded(
                native, wrap, applyDedither: true, applyAlphaBleed: masked);

            // Left: native at nearest ×4 (what Classic texels look like up close).
            // Right: Super-xBR 4× at 1:1 — same on-screen size.
            int w = native.Width, h = native.Height;
            int panelW = w * 4;
            int outH = h * 4;
            const int gap = 16;
            int outW = panelW * 2 + gap;
            var colors = new Color32[outW * outH];
            var gapColor = new Color32(24, 24, 24, 255);
            var transparentBg = new Color32(60, 30, 60, 255);

            for (int y = 0; y < outH; y++)
            {
                int texRow = (outH - 1 - y) * outW;
                for (int x = 0; x < outW; x++)
                {
                    Color32 c;
                    if (x < panelW)
                    {
                        var p = native.GetPixel(x / 4, y / 4);
                        c = p.a == 0 ? transparentBg : new Color32(p.r, p.g, p.b, 255);
                    }
                    else if (x < panelW + gap)
                    {
                        c = gapColor;
                    }
                    else
                    {
                        var p = x4.GetPixel(x - panelW - gap, y);
                        c = p.a <= 128 ? transparentBg : new Color32(p.r, p.g, p.b, 255);
                    }
                    colors[texRow + x] = c;
                }
            }

            var tex = new Texture2D(outW, outH, TextureFormat.RGBA32, false);
            try
            {
                tex.SetPixels32(colors);
                tex.Apply(false);
                File.WriteAllBytes(
                    Path.Combine(outDir, $"{name}-4x.png"), tex.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        static bool HasTransparent(DecodedImage img)
        {
            for (int i = 3; i < img.Rgba.Length; i += 4)
                if (img.Rgba[i] == 0) return true;
            return false;
        }
    }
}
