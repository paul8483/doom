using UnityEngine;
using UnityEngine.InputSystem;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Applies and persists runtime settings; owns the Options submenu UI.
    public sealed class SettingsController : MonoBehaviour
    {
        public static SettingsController Instance { get; private set; }

        SettingsStore store;
        IDisplayAdapter display;
        GameSettingsData current;
        GameSettingsData editSnapshot;
        bool editing;
        int selected;
        MenuKind returnMenuKind;

        static readonly string[] Labels =
        {
            "SFX Volume",
            "Music Volume",
            "Mouse Sens",
            "Invert Y",
            "Fullscreen",
            "Apply",
            "Cancel",
        };

        public GameSettingsData Current => current ?? GameSettingsData.Defaults;
        public bool IsEditing => editing;
        public int SelectedIndex => selected;
        public IDisplayAdapter Display => display;

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
            current = store.Load();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// Test hook: inject store/display before Apply.
        public void ConfigureForTests(SettingsStore store, IDisplayAdapter display)
        {
            this.store = store ?? this.store;
            this.display = display ?? this.display;
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
            editSnapshot = Current;
            current = editSnapshot;
            editing = true;
            selected = 0;
            flow.Menu?.Hide();
            enabled = true;
            ApplyRuntime(current);
        }

        public void Cancel()
        {
            if (!editing) return;
            current = editSnapshot;
            ApplyRuntime(current);
            CloseOptions();
        }

        public void ApplyAndSave()
        {
            if (!editing) return;
            store.Save(current);
            editSnapshot = current;
            ApplyRuntime(current);
            CloseOptions();
        }

        public void SetSfxVolume(float v)
        {
            if (!GameSettingsData.TryCreate(v, Current.MusicVolume, Current.MouseSensitivity,
                    Current.InvertY, Current.Fullscreen, Current.ResolutionWidth,
                    Current.ResolutionHeight, out var next, out _))
                return;
            current = next;
            ApplyRuntime(current);
        }

        public void SetMusicVolume(float v)
        {
            if (!GameSettingsData.TryCreate(Current.SfxVolume, v, Current.MouseSensitivity,
                    Current.InvertY, Current.Fullscreen, Current.ResolutionWidth,
                    Current.ResolutionHeight, out var next, out _))
                return;
            current = next;
            ApplyRuntime(current);
        }

        public void SetMouseSensitivity(float v)
        {
            if (!GameSettingsData.TryCreate(Current.SfxVolume, Current.MusicVolume, v,
                    Current.InvertY, Current.Fullscreen, Current.ResolutionWidth,
                    Current.ResolutionHeight, out var next, out _))
                return;
            current = next;
            ApplyRuntime(current);
        }

        public void SetInvertY(bool v)
        {
            current = Current.WithInvertY(v);
            ApplyRuntime(current);
        }

        public void SetFullscreen(bool v)
        {
            current = Current.WithFullscreen(v);
            ApplyRuntime(current);
        }

        void CloseOptions()
        {
            editing = false;
            enabled = false;

            var flow = GameFlowController.Instance;
            if (flow == null) return;

            if (returnMenuKind == MenuKind.Main)
                flow.Menu.ShowMain(ResolveTextures());
            else if (returnMenuKind == MenuKind.Pause)
                flow.Menu.ShowPause(ResolveTextures());
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
        }

        static HudTextureCache ResolveTextures()
        {
            var loader = UnityEngine.Object.FindAnyObjectByType<MapLoader>();
            return loader != null ? loader.HudTextures : null;
        }

        void Update()
        {
            if (!editing) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
                selected = (selected + Labels.Length - 1) % Labels.Length;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)
                selected = (selected + 1) % Labels.Length;

            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
                Nudge(-1);
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                Nudge(1);

            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                ActivateSelected();
            if (kb.escapeKey.wasPressedThisFrame)
                Cancel();
        }

        void Nudge(int dir)
        {
            switch (selected)
            {
                case 0: SetSfxVolume(Current.SfxVolume + dir * 0.05f); break;
                case 1: SetMusicVolume(Current.MusicVolume + dir * 0.05f); break;
                case 2: SetMouseSensitivity(Current.MouseSensitivity + dir * 0.02f); break;
                case 3: SetInvertY(!Current.InvertY); break;
                case 4: SetFullscreen(!Current.Fullscreen); break;
            }
        }

        void ActivateSelected()
        {
            switch (selected)
            {
                case 3: SetInvertY(!Current.InvertY); break;
                case 4: SetFullscreen(!Current.Fullscreen); break;
                case 5: ApplyAndSave(); break;
                case 6: Cancel(); break;
            }
        }

        void OnGUI()
        {
            if (!editing || Event.current.type != EventType.Repaint) return;

            var t = VirtualScreenRenderer.ComputeForScreen();
            var bg = VirtualScreenRenderer.ToScreen(t, 0, 0, 320, 200);
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = prev;

            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(16, (int)(14 * t.Scale)),
                alignment = TextAnchor.UpperCenter,
            };
            title.normal.textColor = Color.white;
            GUI.Label(VirtualScreenRenderer.ToScreen(t, 0, 20, 320, 24), "Options", title);

            var row = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(12, (int)(11 * t.Scale)),
                alignment = TextAnchor.UpperLeft,
            };

            for (int i = 0; i < Labels.Length; i++)
            {
                row.normal.textColor = i == selected ? Color.yellow : Color.white;
                string value = ValueLabel(i);
                string text = string.IsNullOrEmpty(value)
                    ? Labels[i]
                    : $"{Labels[i]}: {value}";
                GUI.Label(VirtualScreenRenderer.ToScreen(t, 60, 50 + i * 16, 220, 16), text, row);
            }
        }

        string ValueLabel(int index)
        {
            switch (index)
            {
                case 0: return Current.SfxVolume.ToString("0.00");
                case 1: return Current.MusicVolume.ToString("0.00");
                case 2: return Current.MouseSensitivity.ToString("0.00");
                case 3: return Current.InvertY ? "On" : "Off";
                case 4: return Current.Fullscreen ? "On" : "Off";
                default: return null;
            }
        }
    }
}
