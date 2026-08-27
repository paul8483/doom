using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Doom.Game;
using Doom.MapBuild.Rendering;

namespace Doom.MapBuild
{
    /// Applies and persists runtime settings; owns the Options submenu UI.
    /// Drawn with the same WAD patches / TITLEPIC / skull cursor as MenuController.
    public sealed class SettingsController : MonoBehaviour
    {
        public static SettingsController Instance { get; private set; }

        /// Fired after a committed settings change is applied (hot-switch consumers).
        public static event System.Action<GameSettingsData> SettingsApplied;

        const int ItemX = 60;
        const int SkullX = 28;
        const int ThermoWidth = 16;
        const float ThermoCell = 8f;

        enum OptionRow
        {
            SfxVolume,
            MusicVolume,
            MouseSens,
            InvertY,
            Fullscreen,
            GraphicsMode,
        }

        SettingsStore store;
        IDisplayAdapter display;
        IGraphicsModeAdapter graphics;
        GameSettingsData current;
        HudTextureCache textures;
        bool editing;
        int selected;
        int skullTic;
        MenuKind returnMenuKind;
        readonly List<OptionRow> visibleRows = new List<OptionRow>(8);

        public GameSettingsData Current => current ?? GameSettingsData.Defaults;
        public bool IsEditing => editing;
        public int SelectedIndex => selected;
        public IDisplayAdapter Display => display;
        public IGraphicsModeAdapter Graphics => graphics;

        /// Test seam: how many Options rows are currently listed.
        public int VisibleOptionCount
        {
            get
            {
                RebuildVisibleRows();
                return visibleRows.Count;
            }
        }

        public static SettingsController Ensure()
        {
            if (Instance != null) return Instance;
            var host = GameSessionHost.Ensure();
            var sc = host.GetComponent<SettingsController>();
            if (sc == null) sc = host.gameObject.AddComponent<SettingsController>();
            return sc;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            store = new SettingsStore();
            display = new UnityDisplayAdapter();
            graphics = GraphicsModeController.Ensure();
            current = store.Load();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// Test hook: inject store/display/graphics before Apply.
        public void ConfigureForTests(
            SettingsStore store,
            IDisplayAdapter display,
            IGraphicsModeAdapter graphics = null)
        {
            this.store = store ?? this.store;
            this.display = display ?? this.display;
            this.graphics = graphics ?? this.graphics ?? (IGraphicsModeAdapter)GraphicsModeController.Ensure();
            current = this.store.Load();
            ApplyRuntime(current);
        }

        public void ApplyLoadedSettings()
        {
            if (current == null) current = store.Load();
            ApplyRuntime(current);
        }

        public void OpenOptions()
        {
            var flow = GameFlowController.Ensure();
            returnMenuKind = flow.Menu != null ? flow.Menu.Kind : MenuKind.None;
            textures = ResolveTextures();
            editing = true;
            selected = 0;
            skullTic = 0;
            RebuildVisibleRows();
            flow.Menu?.Hide();
            enabled = true;
            ApplyRuntime(current);
        }

        public void SetSfxVolume(float v)
        {
            if (!GameSettingsData.TryCreate(v, Current.MusicVolume, Current.MouseSensitivity,
                    Current.InvertY, Current.Fullscreen, Current.ResolutionWidth,
                    Current.ResolutionHeight, Current.GraphicsMode,
                    out var next, out _))
                return;
            Commit(next);
        }

        public void SetMusicVolume(float v)
        {
            if (!GameSettingsData.TryCreate(Current.SfxVolume, v, Current.MouseSensitivity,
                    Current.InvertY, Current.Fullscreen, Current.ResolutionWidth,
                    Current.ResolutionHeight, Current.GraphicsMode,
                    out var next, out _))
                return;
            Commit(next);
        }

        public void SetMouseSensitivity(float v)
        {
            if (!GameSettingsData.TryCreate(Current.SfxVolume, Current.MusicVolume, v,
                    Current.InvertY, Current.Fullscreen, Current.ResolutionWidth,
                    Current.ResolutionHeight, Current.GraphicsMode,
                    out var next, out _))
                return;
            Commit(next);
        }

        public void SetInvertY(bool v)
        {
            Commit(Current.WithInvertY(v));
        }

        public void SetFullscreen(bool v)
        {
            Commit(Current.WithFullscreen(v));
        }

        public void SetGraphicsMode(GraphicsMode mode)
        {
            if (!GameSettingsData.IsDefinedGraphicsMode(mode)) return;
            Commit(Current.WithGraphicsMode(mode));
            RebuildVisibleRows();
            if (selected >= visibleRows.Count)
                selected = visibleRows.Count - 1;
        }

        public void CycleGraphicsMode(int dir)
        {
            var next = Current.GraphicsMode == GraphicsMode.Classic
                ? GraphicsMode.Enhanced
                : GraphicsMode.Classic;
            // dir ignored for two-value toggle; kept for left/right symmetry.
            if (dir == 0) return;
            SetGraphicsMode(next);
        }

        public void CloseOptions()
        {
            if (!editing) return;
            editing = false;
            enabled = false;
            textures = null;

            var flow = GameFlowController.Instance;
            if (flow == null) return;

            if (returnMenuKind == MenuKind.Main)
                flow.Menu.ShowMain(ResolveTextures());
            else if (returnMenuKind == MenuKind.Pause)
                flow.Menu.ShowPause(ResolveTextures());
        }

        void Commit(GameSettingsData next)
        {
            current = next;
            ApplyRuntime(current);
            store.Save(current);
        }

        void ApplyRuntime(GameSettingsData data)
        {
            if (data == null) return;

            var loader = UnityEngine.Object.FindAnyObjectByType<MapLoader>();
            if (loader != null)
            {
                if (loader.Sound != null) loader.Sound.SetVolume(data.SfxVolume);
                if (loader.Music != null) loader.Music.SetVolume(data.MusicVolume);
            }

            var player = GameObject.Find("Player");
            if (player != null)
            {
                var pc = player.GetComponent<PlayerController>();
                if (pc != null) pc.ApplyLookSettings(data.MouseSensitivity, data.InvertY);
            }

            display?.Apply(data.Fullscreen, data.ResolutionWidth, data.ResolutionHeight);
            graphics?.Apply(data.GraphicsMode);
            SettingsApplied?.Invoke(data);
        }

        static HudTextureCache ResolveTextures()
        {
            var loader = UnityEngine.Object.FindAnyObjectByType<MapLoader>();
            return loader != null ? loader.HudTextures : null;
        }

        void RebuildVisibleRows()
        {
            visibleRows.Clear();
            visibleRows.Add(OptionRow.SfxVolume);
            visibleRows.Add(OptionRow.MusicVolume);
            visibleRows.Add(OptionRow.MouseSens);
            visibleRows.Add(OptionRow.InvertY);
            visibleRows.Add(OptionRow.Fullscreen);
            visibleRows.Add(OptionRow.GraphicsMode);
        }

        /// The classic six-row Options layout (the Enhanced 2D mode's
        /// 3D Objects row was removed 2026-08-28 — Enhanced IS the 3D mode).
        static float RowYForIndex(int index) => index switch
        {
            0 => 36f,  // SFX (+ thermo)
            1 => 64f,  // Music (+ thermo)
            2 => 92f,  // Mouse (+ thermo)
            3 => 120f, // Invert Y
            4 => 136f, // Fullscreen
            5 => 152f, // Graphics Mode
            _ => 36f + index * 16f,
        };

        void Update()
        {
            if (!editing) return;
            skullTic++;
            RebuildVisibleRows();
            if (selected >= visibleRows.Count)
                selected = System.Math.Max(0, visibleRows.Count - 1);

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
                selected = (selected + visibleRows.Count - 1) % visibleRows.Count;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)
                selected = (selected + 1) % visibleRows.Count;

            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
                Nudge(-1);
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                Nudge(1);

            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                ActivateSelected();
            if (kb.escapeKey.wasPressedThisFrame)
                CloseOptions();
        }

        void Nudge(int dir)
        {
            if (selected < 0 || selected >= visibleRows.Count) return;
            switch (visibleRows[selected])
            {
                case OptionRow.SfxVolume: SetSfxVolume(Current.SfxVolume + dir * 0.05f); break;
                case OptionRow.MusicVolume: SetMusicVolume(Current.MusicVolume + dir * 0.05f); break;
                case OptionRow.MouseSens: SetMouseSensitivity(Current.MouseSensitivity + dir * 0.02f); break;
                case OptionRow.InvertY: SetInvertY(!Current.InvertY); break;
                case OptionRow.Fullscreen: SetFullscreen(!Current.Fullscreen); break;
                case OptionRow.GraphicsMode: CycleGraphicsMode(dir); break;
            }
        }

        void ActivateSelected()
        {
            if (selected < 0 || selected >= visibleRows.Count) return;
            switch (visibleRows[selected])
            {
                case OptionRow.InvertY: SetInvertY(!Current.InvertY); break;
                case OptionRow.Fullscreen: SetFullscreen(!Current.Fullscreen); break;
                case OptionRow.GraphicsMode: CycleGraphicsMode(1); break;
            }
        }

        void OnGUI()
        {
            if (!editing || Event.current.type != EventType.Repaint) return;

            var t = VirtualScreenRenderer.ComputeForScreen();
            DrawBackground(t);

            if (textures != null && textures.TryGet("M_OPTTTL", out var titlePatch))
            {
                float tx = (320 - titlePatch.Width) * 0.5f;
                var tr = VirtualScreenRenderer.ToScreenSnapped(
                    t, tx, 12f, titlePatch.Width, titlePatch.Height);
                GUI.DrawTexture(tr, titlePatch.Texture);
            }
            else
                DrawFallbackText(t, 0, 12, 320, "Options", centered: true);

            RebuildVisibleRows();
            for (int i = 0; i < visibleRows.Count; i++)
                DrawRow(t, i, visibleRows[i]);

            if (selected >= 0 && selected < visibleRows.Count)
                DrawSkull(t, SkullX, RowYForIndex(selected) - 2f);
        }

        void DrawBackground(in VirtualScreenRenderer.Transform t)
        {
            var bg = VirtualScreenRenderer.ToScreen(t, 0, 0, 320, 200);
            Color prev = GUI.color;
            // Freedoom TITLEPIC is hellish red — same hue as M_* / STCFN patches,
            // so Options on the title art is unreadable. Use a flat dark-red plate
            // (main) or a dim over the paused world (pause), never the picture.
            if (returnMenuKind == MenuKind.Pause)
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
            else
                GUI.color = new Color(0.55f, 0f, 0f, 1f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void DrawRow(in VirtualScreenRenderer.Transform t, int index, OptionRow row)
        {
            float y = RowYForIndex(index);
            switch (row)
            {
                case OptionRow.SfxVolume:
                    if (!DrawPatch(t, "M_SFXVOL", ItemX, y))
                        DrawFallbackText(t, ItemX, y, 200, "SFX Volume", centered: false);
                    DrawThermo(t, ItemX, y + 13f, ThermoDot01(Current.SfxVolume));
                    break;
                case OptionRow.MusicVolume:
                    if (!DrawPatch(t, "M_MUSVOL", ItemX, y))
                        DrawFallbackText(t, ItemX, y, 200, "Music Volume", centered: false);
                    DrawThermo(t, ItemX, y + 13f, ThermoDot01(Current.MusicVolume));
                    break;
                case OptionRow.MouseSens:
                    if (!DrawPatch(t, "M_MSENS", ItemX, y))
                        DrawFallbackText(t, ItemX, y, 200, "Mouse Sens", centered: false);
                    DrawThermo(t, ItemX, y + 13f, ThermoDotSensitivity(Current.MouseSensitivity));
                    break;
                case OptionRow.InvertY:
                    DrawHuString(t, ItemX, y, "INVERT Y");
                    DrawOnOff(t, ItemX + 120f, y, Current.InvertY);
                    break;
                case OptionRow.Fullscreen:
                    DrawHuString(t, ItemX, y, "FULLSCREEN");
                    DrawOnOff(t, ItemX + 120f, y, Current.Fullscreen);
                    break;
                case OptionRow.GraphicsMode:
                    DrawHuString(t, ItemX, y, "GRAPHICS MODE");
                    DrawHuString(t, ItemX + 140f, y,
                        Current.GraphicsMode == GraphicsMode.Enhanced ? "ENHANCED" : "CLASSIC");
                    break;
            }
        }

        void DrawOnOff(in VirtualScreenRenderer.Transform t, float x, float y, bool on)
        {
            string patch = on ? "M_MSGON" : "M_MSGOFF";
            if (!DrawPatch(t, patch, x, y))
                DrawFallbackText(t, x, y, 40, on ? "On" : "Off", centered: false);
        }

        void DrawThermo(in VirtualScreenRenderer.Transform t, float x, float y, int dot)
        {
            if (textures == null) return;
            float xx = x;
            if (!DrawPatch(t, "M_THERML", xx, y)) return;
            xx += ThermoCell;
            for (int i = 0; i < ThermoWidth; i++)
            {
                DrawPatch(t, "M_THERMM", xx, y);
                xx += ThermoCell;
            }
            DrawPatch(t, "M_THERMR", xx, y);

            int d = Mathf.Clamp(dot, 0, ThermoWidth);
            DrawPatch(t, "M_THERMO", x + ThermoCell + d * ThermoCell, y);
        }

        static int ThermoDot01(float v01) =>
            Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(v01) * ThermoWidth), 0, ThermoWidth);

        static int ThermoDotSensitivity(float sens)
        {
            // Map [0.01, 2] onto the same 0..16 thermo used for volumes.
            float t = (sens - 0.01f) / (2f - 0.01f);
            return ThermoDot01(t);
        }

        void DrawSkull(in VirtualScreenRenderer.Transform t, float x, float y)
        {
            string skull = (skullTic / 8) % 2 == 0 ? "M_SKULL1" : "M_SKULL2";
            if (DrawPatch(t, skull, x, y)) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(14, (int)(12 * t.Scale)),
            };
            style.normal.textColor = Color.red;
            GUI.Label(VirtualScreenRenderer.ToScreen(t, x, y, 24, 16), ">", style);
        }

        bool DrawPatch(in VirtualScreenRenderer.Transform t, string name, float x, float y)
        {
            if (textures == null || !textures.TryGet(name, out var e))
                return false;
            var r = VirtualScreenRenderer.ToScreenSnapped(t, x, y, e.Width, e.Height);
            GUI.DrawTexture(r, e.Texture);
            return true;
        }

        void DrawHuString(in VirtualScreenRenderer.Transform t, float x, float y, string text)
        {
            if (textures == null || string.IsNullOrEmpty(text))
            {
                DrawFallbackText(t, x, y, 200, text ?? "", centered: false);
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
                DrawFallbackText(t, x, y, 200, text, centered: false);
        }

        static void DrawFallbackText(
            in VirtualScreenRenderer.Transform t, float x, float y, float w, string text, bool centered)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(12, (int)(11 * t.Scale)),
                alignment = centered ? TextAnchor.UpperCenter : TextAnchor.UpperLeft,
            };
            style.normal.textColor = Color.white;
            GUI.Label(VirtualScreenRenderer.ToScreen(t, x, y, w, 16), text, style);
        }
    }
}
