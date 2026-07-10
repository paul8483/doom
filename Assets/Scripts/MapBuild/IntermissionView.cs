using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Static WAD-backed intermission summary. Confirm via
    /// <see cref="LevelTransitionController.ConfirmIntermission"/> (or auto when
    /// ImmediateConfirmForTests). Exact original WI animation is not required.
    public sealed class IntermissionView : MonoBehaviour
    {
        HudTextureCache textures;
        LevelStatsSnapshot stats;
        string finishedMap;
        string nextMap;
        bool visible;

        public bool IsVisible => visible;
        public LevelStatsSnapshot Stats => stats;

        public void Show(
            HudTextureCache textures,
            LevelStatsSnapshot stats,
            string finishedMap,
            string nextMap)
        {
            this.textures = textures;
            this.stats = stats;
            this.finishedMap = finishedMap ?? "";
            this.nextMap = nextMap ?? "";
            visible = true;
            enabled = true;
        }

        public void Hide()
        {
            visible = false;
            enabled = false;
        }

        void OnGUI()
        {
            if (!visible || Event.current.type != EventType.Repaint) return;

            var t = VirtualScreenRenderer.ComputeForScreen();

            if (textures != null && textures.TryGet("INTERPIC", out var bg))
            {
                var r = VirtualScreenRenderer.ToScreenSnapped(t, 0, 0, bg.Width, bg.Height);
                GUI.DrawTexture(r, bg.Texture);
            }
            else
            {
                // Fallback dark fill when INTERPIC missing.
                var r = VirtualScreenRenderer.ToScreen(t, 0, 0, 320, 200);
                GUI.DrawTexture(r, Texture2D.blackTexture);
            }

            DrawLabel(t, "WIF", 0, 2); // "Finished!"
            DrawMapName(t, finishedMap, 0, 20);

            DrawStatLine(t, "WIOSTK", stats.KillPercent, 50, 50);
            DrawStatLine(t, "WIOSTI", stats.ItemPercent, 50, 70);
            DrawStatLine(t, "WIOSTS", stats.SecretPercent, 50, 90);
            DrawTime(t, stats.Tics, 50, 120);

            if (!string.IsNullOrEmpty(nextMap))
            {
                DrawLabel(t, "WIENTER", 0, 160);
                DrawMapName(t, nextMap, 0, 175);
            }
        }

        void DrawStatLine(
            in VirtualScreenRenderer.Transform t, string labelPatch, int percent, float x, float y)
        {
            DrawLabel(t, labelPatch, x, y);
            DrawWiNumber(t, percent, x + 120, y);
            DrawLabel(t, "WIPCNT", x + 160, y);
        }

        void DrawTime(in VirtualScreenRenderer.Transform t, int tics, float x, float y)
        {
            DrawLabel(t, "WITIME", x, y);
            int totalSec = tics / 35;
            int mins = totalSec / 60;
            int secs = totalSec % 60;
            DrawWiNumber(t, mins, x + 100, y);
            DrawLabel(t, "WICOLON", x + 120, y);
            // Seconds as two digits.
            DrawWiDigit(t, secs / 10, x + 130, y);
            DrawWiDigit(t, secs % 10, x + 142, y);
        }

        void DrawMapName(in VirtualScreenRenderer.Transform t, string map, float x, float y)
        {
            // WILV0n for E1Mn (n = map-1).
            if (CampaignRoute.TryNormalize(map, out string canon) &&
                canon.Length == 4 && canon[1] == '1')
            {
                int m = canon[3] - '1';
                if (m >= 0 && m <= 8)
                    DrawLabel(t, $"WILV0{m}", x + 80, y);
            }
        }

        void DrawWiNumber(in VirtualScreenRenderer.Transform t, int value, float rightX, float y)
        {
            if (value < 0) value = 0;
            string s = value.ToString();
            float x = rightX;
            for (int i = s.Length - 1; i >= 0; i--)
            {
                if (!DrawWiDigit(t, s[i] - '0', x - 12, y)) break;
                x -= 12;
            }
        }

        bool DrawWiDigit(in VirtualScreenRenderer.Transform t, int digit, float x, float y)
        {
            if (digit < 0 || digit > 9) return false;
            return DrawLabel(t, "WINUM" + digit, x, y);
        }

        bool DrawLabel(in VirtualScreenRenderer.Transform t, string name, float x, float y)
        {
            if (textures == null || !textures.TryGet(name, out var e)) return false;
            var r = VirtualScreenRenderer.ToScreenSnapped(t, x, y, e.Width, e.Height);
            GUI.DrawTexture(r, e.Texture);
            return true;
        }
    }
}
