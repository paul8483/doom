using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Full-screen loading plate shown while MapLoader builds (New Game, Load, level exit).
    /// Lives on GameSessionHost so it survives scene reload; paints via OnGUI like menus.
    public sealed class LoadingView : MonoBehaviour
    {
        const float BarX = 60f;
        const float BarY = 178f;
        const float BarW = 200f;
        const float BarH = 8f;

        HudTextureCache textures;
        string mapName = "";
        string status = "LOADING";
        float progress01;
        bool visible;

        public bool IsVisible => visible;
        public float Progress01 => progress01;
        public string Status => status;
        public string MapName => mapName;

        public static LoadingView Ensure()
        {
            var host = GameSessionHost.Ensure();
            var view = host.GetComponent<LoadingView>();
            if (view == null) view = host.gameObject.AddComponent<LoadingView>();
            return view;
        }

        public void Show(HudTextureCache textures, string mapName)
        {
            this.textures = textures;
            this.mapName = mapName ?? "";
            status = "LOADING";
            progress01 = 0f;
            visible = true;
            enabled = true;
        }

        /// Refresh WAD UI cache without resetting the progress bar (mid-build).
        public void BindTextures(HudTextureCache textures, string mapName = null)
        {
            if (textures != null) this.textures = textures;
            if (!string.IsNullOrEmpty(mapName)) this.mapName = mapName;
        }

        public void SetProgress(float progress01, string status = null)
        {
            this.progress01 = Mathf.Clamp01(progress01);
            if (!string.IsNullOrEmpty(status))
                this.status = status;
        }

        public void Hide()
        {
            visible = false;
            enabled = false;
            progress01 = 0f;
        }

        void OnGUI()
        {
            if (!visible || Event.current.type != EventType.Repaint) return;

            var t = VirtualScreenRenderer.ComputeForScreen();
            DrawBackground(t);
            DrawStatus(t);
            DrawMapName(t);
            DrawProgressBar(t);
        }

        void DrawBackground(in VirtualScreenRenderer.Transform t)
        {
            if (textures != null && textures.TryGet("TITLEPIC", out var title))
            {
                var r = VirtualScreenRenderer.ToScreenSnapped(t, 0, 0, title.Width, title.Height);
                GUI.DrawTexture(r, title.Texture);

                // Dim the title art so LOADING text stays readable.
                Color prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(VirtualScreenRenderer.ToScreen(t, 0, 0, 320, 200), Texture2D.whiteTexture);
                GUI.color = prev;
                return;
            }

            var fill = VirtualScreenRenderer.ToScreen(t, 0, 0, 320, 200);
            Color c = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.1f, 1f);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = c;
        }

        void DrawStatus(in VirtualScreenRenderer.Transform t)
        {
            int dots = ((int)(Time.unscaledTime * 3f)) % 4;
            string label = (status ?? "LOADING") + new string('.', dots);
            DrawHuStringCentered(t, 88f, label);
        }

        void DrawMapName(in VirtualScreenRenderer.Transform t)
        {
            if (CampaignRoute.TryNormalize(mapName, out string canon) &&
                canon.Length == 4 && canon[1] == '1')
            {
                int m = canon[3] - '1';
                if (m >= 0 && m <= 8 &&
                    textures != null &&
                    textures.TryGet($"WILV0{m}", out var e))
                {
                    float x = (320 - e.Width) * 0.5f;
                    var r = VirtualScreenRenderer.ToScreenSnapped(t, x, 110f, e.Width, e.Height);
                    GUI.DrawTexture(r, e.Texture);
                    return;
                }
            }

            if (!string.IsNullOrEmpty(mapName))
                DrawHuStringCentered(t, 110f, mapName.ToUpperInvariant());
        }

        void DrawProgressBar(in VirtualScreenRenderer.Transform t)
        {
            var outer = VirtualScreenRenderer.ToScreenSnapped(t, BarX, BarY, BarW, BarH);
            Color prev = GUI.color;
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            GUI.DrawTexture(outer, Texture2D.whiteTexture);

            float fillW = Mathf.Max(1f, BarW * progress01);
            var inner = VirtualScreenRenderer.ToScreenSnapped(t, BarX, BarY, fillW, BarH);
            GUI.color = new Color(0.85f, 0.05f, 0.05f, 1f);
            GUI.DrawTexture(inner, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void DrawHuStringCentered(in VirtualScreenRenderer.Transform t, float y, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            float width = MeasureHuString(text);
            DrawHuString(t, (320f - width) * 0.5f, y, text);
        }

        float MeasureHuString(string text)
        {
            if (string.IsNullOrEmpty(text) || textures == null) return text.Length * 8f;
            float w = 0f;
            foreach (char ch in text.ToUpperInvariant())
            {
                if (ch == ' ') { w += 4f; continue; }
                string lump = "STCFN" + ((int)ch).ToString("000");
                if (textures.TryGet(lump, out var e)) w += e.Width;
                else w += 4f;
            }
            return w;
        }

        void DrawHuString(in VirtualScreenRenderer.Transform t, float x, float y, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (textures == null)
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(16, (int)(14 * t.Scale)),
                    alignment = TextAnchor.UpperCenter,
                };
                style.normal.textColor = Color.white;
                GUI.Label(VirtualScreenRenderer.ToScreen(t, x, y, 200, 16), text, style);
                return;
            }

            float cx = x;
            bool any = false;
            foreach (char ch in text.ToUpperInvariant())
            {
                if (ch == ' ')
                {
                    cx += 4f;
                    continue;
                }

                string lump = "STCFN" + ((int)ch).ToString("000");
                if (!textures.TryGet(lump, out var e))
                {
                    cx += 4f;
                    continue;
                }

                var r = VirtualScreenRenderer.ToScreenSnapped(t, cx, y, e.Width, e.Height);
                GUI.DrawTexture(r, e.Texture);
                cx += e.Width;
                any = true;
            }

            if (!any)
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(16, (int)(14 * t.Scale)),
                };
                style.normal.textColor = Color.white;
                GUI.Label(VirtualScreenRenderer.ToScreen(t, x, y, 200, 16), text, style);
            }
        }
    }
}
