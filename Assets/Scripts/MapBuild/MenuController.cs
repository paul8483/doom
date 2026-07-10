using UnityEngine;
using UnityEngine.InputSystem;

namespace Doom.MapBuild
{
    public enum MenuKind
    {
        None,
        Main,
        Pause,
    }

    public enum MenuAction
    {
        NewGame,
        LoadGame,
        Options,
        Quit,
        Resume,
        SaveGame,
        QuitToMain,
    }

    /// WAD-driven main/pause menu. Navigation via keyboard; tests call ActivateSelected.
    public sealed class MenuController : MonoBehaviour
    {
        struct Item
        {
            public string Patch;
            public string FallbackLabel;
            public MenuAction Action;
            public float X, Y;
        }

        static readonly Item[] MainItems =
        {
            new Item { Patch = "M_NGAME", FallbackLabel = "New Game", Action = MenuAction.NewGame, X = 97, Y = 72 },
            new Item { Patch = "M_LOADG", FallbackLabel = "Load Game", Action = MenuAction.LoadGame, X = 97, Y = 92 },
            new Item { Patch = "M_OPTION", FallbackLabel = "Options", Action = MenuAction.Options, X = 97, Y = 112 },
            new Item { Patch = "M_QUITG", FallbackLabel = "Quit", Action = MenuAction.Quit, X = 97, Y = 132 },
        };

        static readonly Item[] PauseItems =
        {
            new Item { Patch = null, FallbackLabel = "Resume", Action = MenuAction.Resume, X = 97, Y = 56 },
            new Item { Patch = "M_SAVEG", FallbackLabel = "Save Game", Action = MenuAction.SaveGame, X = 97, Y = 76 },
            new Item { Patch = "M_LOADG", FallbackLabel = "Load Game", Action = MenuAction.LoadGame, X = 97, Y = 96 },
            new Item { Patch = "M_OPTION", FallbackLabel = "Options", Action = MenuAction.Options, X = 97, Y = 116 },
            new Item { Patch = null, FallbackLabel = "Quit to Main", Action = MenuAction.QuitToMain, X = 97, Y = 136 },
        };

        GameFlowController flow;
        HudTextureCache textures;
        Item[] items = System.Array.Empty<Item>();
        int selected;
        int skullTic;
        string statusMessage;

        public MenuKind Kind { get; private set; } = MenuKind.None;
        public bool IsVisible => Kind != MenuKind.None;
        public int SelectedIndex => selected;
        public string StatusMessage => statusMessage;

        public void Init(GameFlowController flow) => this.flow = flow;

        public void ShowMain(HudTextureCache textures)
        {
            this.textures = textures;
            Kind = MenuKind.Main;
            items = MainItems;
            selected = 0;
            statusMessage = null;
            enabled = true;
        }

        public void ShowPause(HudTextureCache textures)
        {
            this.textures = textures;
            Kind = MenuKind.Pause;
            items = PauseItems;
            selected = 0;
            statusMessage = null;
            enabled = true;
        }

        public void Hide()
        {
            Kind = MenuKind.None;
            items = System.Array.Empty<Item>();
            statusMessage = null;
            enabled = false;
        }

        public void MoveSelection(int delta)
        {
            if (!IsVisible || items.Length == 0) return;
            selected = (selected + delta + items.Length) % items.Length;
            statusMessage = null;
        }

        public void ActivateSelected()
        {
            if (!IsVisible || items.Length == 0) return;
            Activate(items[selected].Action);
        }

        /// Test/API hook — activate a specific action without keyboard.
        public void Activate(MenuAction action)
        {
            if (flow == null) flow = GameFlowController.Ensure();

            switch (action)
            {
                case MenuAction.NewGame:
                    flow.StartNewGame();
                    break;
                case MenuAction.Resume:
                    flow.Resume();
                    break;
                case MenuAction.QuitToMain:
                    flow.QuitToMainMenu();
                    break;
                case MenuAction.Quit:
                    flow.QuitApplication();
                    break;
                case MenuAction.LoadGame:
                case MenuAction.SaveGame:
                    statusMessage = "Save/Load — Stage 7d";
                    break;
                case MenuAction.Options:
                    SettingsController.Ensure().OpenOptions();
                    break;
            }
        }

        void Update()
        {
            if (!IsVisible) return;

            // Unscaled so pause (timeScale 0) still navigates.
            skullTic++;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
                MoveSelection(-1);
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)
                MoveSelection(1);
            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                ActivateSelected();
        }

        void OnGUI()
        {
            if (!IsVisible || Event.current.type != EventType.Repaint) return;

            var t = VirtualScreenRenderer.ComputeForScreen();

            if (Kind == MenuKind.Main)
                DrawMainBackground(t);
            else
                DrawPauseDim(t);

            for (int i = 0; i < items.Length; i++)
                DrawItem(t, items[i], i == selected);

            if (!string.IsNullOrEmpty(statusMessage))
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(12, (int)(10 * t.Scale)),
                    alignment = TextAnchor.UpperCenter,
                };
                style.normal.textColor = Color.yellow;
                var r = VirtualScreenRenderer.ToScreen(t, 0, 170, 320, 20);
                GUI.Label(r, statusMessage, style);
            }
        }

        void DrawMainBackground(in VirtualScreenRenderer.Transform t)
        {
            if (textures != null && textures.TryGet("TITLEPIC", out var title))
            {
                var r = VirtualScreenRenderer.ToScreenSnapped(t, 0, 0, title.Width, title.Height);
                GUI.DrawTexture(r, title.Texture);
            }
            else
            {
                var r = VirtualScreenRenderer.ToScreen(t, 0, 0, 320, 200);
                GUI.DrawTexture(r, Texture2D.blackTexture);
            }

            if (textures != null && textures.TryGet("M_DOOM", out var logo))
            {
                float x = (320 - logo.Width) * 0.5f;
                var r = VirtualScreenRenderer.ToScreenSnapped(t, x, 12, logo.Width, logo.Height);
                GUI.DrawTexture(r, logo.Texture);
            }
        }

        void DrawPauseDim(in VirtualScreenRenderer.Transform t)
        {
            var r = VirtualScreenRenderer.ToScreen(t, 0, 0, 320, 200);
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;

            if (textures != null && textures.TryGet("M_PAUSE", out var pause))
            {
                float x = (320 - pause.Width) * 0.5f;
                var pr = VirtualScreenRenderer.ToScreenSnapped(t, x, 20, pause.Width, pause.Height);
                GUI.DrawTexture(pr, pause.Texture);
            }
            else
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(16, (int)(14 * t.Scale)),
                    alignment = TextAnchor.UpperCenter,
                };
                style.normal.textColor = Color.white;
                GUI.Label(VirtualScreenRenderer.ToScreen(t, 0, 20, 320, 24), "Pause", style);
            }
        }

        void DrawItem(in VirtualScreenRenderer.Transform t, Item item, bool isSelected)
        {
            bool drew = false;
            if (!string.IsNullOrEmpty(item.Patch) && textures != null &&
                textures.TryGet(item.Patch, out var e))
            {
                var r = VirtualScreenRenderer.ToScreenSnapped(t, item.X, item.Y, e.Width, e.Height);
                GUI.DrawTexture(r, e.Texture);
                drew = true;
            }

            if (!drew)
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(14, (int)(12 * t.Scale)),
                    alignment = TextAnchor.UpperLeft,
                };
                style.normal.textColor = isSelected ? Color.yellow : Color.white;
                var r = VirtualScreenRenderer.ToScreen(t, item.X, item.Y, 160, 16);
                GUI.Label(r, item.FallbackLabel, style);
            }

            if (isSelected)
                DrawSkull(t, item.X - 32, item.Y - 2);
        }

        void DrawSkull(in VirtualScreenRenderer.Transform t, float x, float y)
        {
            string skull = (skullTic / 8) % 2 == 0 ? "M_SKULL1" : "M_SKULL2";
            if (textures != null && textures.TryGet(skull, out var e))
            {
                var r = VirtualScreenRenderer.ToScreenSnapped(t, x, y, e.Width, e.Height);
                GUI.DrawTexture(r, e.Texture);
                return;
            }

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(14, (int)(12 * t.Scale)),
            };
            style.normal.textColor = Color.red;
            GUI.Label(VirtualScreenRenderer.ToScreen(t, x, y, 24, 16), ">", style);
        }
    }
}
