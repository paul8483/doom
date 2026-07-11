using UnityEngine;
using UnityEngine.InputSystem;

namespace Doom.MapBuild
{
    public enum MenuKind
    {
        None,
        Main,
        Pause,
        SaveSlots,
        LoadSlots,
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
        Slot0,
        Slot1,
        Slot2,
        Slot3,
        Slot4,
        Slot5,
        Back,
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
            // No IWAD "Resume" patch — Esc closes pause (GameFlowController). Same
            // WAD menu font for every visible row.
            new Item { Patch = "M_SAVEG", FallbackLabel = "Save Game", Action = MenuAction.SaveGame, X = 97, Y = 72 },
            new Item { Patch = "M_LOADG", FallbackLabel = "Load Game", Action = MenuAction.LoadGame, X = 97, Y = 92 },
            new Item { Patch = "M_OPTION", FallbackLabel = "Options", Action = MenuAction.Options, X = 97, Y = 112 },
            new Item { Patch = "M_ENDGAM", FallbackLabel = "End Game", Action = MenuAction.QuitToMain, X = 97, Y = 132 },
        };

        GameFlowController flow;
        HudTextureCache textures;
        Item[] items = System.Array.Empty<Item>();
        int selected;
        int skullTic;
        string statusMessage;
        MenuKind returnKind = MenuKind.None;

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
                    ShowSlotMenu(load: true);
                    break;
                case MenuAction.SaveGame:
                    if (flow.State != GameFlowState.Paused)
                    {
                        statusMessage = "Save only while paused.";
                        break;
                    }
                    ShowSlotMenu(load: false);
                    break;
                case MenuAction.Options:
                    SettingsController.Ensure().OpenOptions();
                    break;
                case MenuAction.Back:
                    ReturnFromSlots();
                    break;
                case MenuAction.Slot0:
                case MenuAction.Slot1:
                case MenuAction.Slot2:
                case MenuAction.Slot3:
                case MenuAction.Slot4:
                case MenuAction.Slot5:
                    HandleSlot(action - MenuAction.Slot0);
                    break;
            }
        }

        void ShowSlotMenu(bool load)
        {
            returnKind = Kind;
            Kind = load ? MenuKind.LoadSlots : MenuKind.SaveSlots;
            items = BuildSlotItems(load);
            selected = 0;
            statusMessage = null;
        }

        Item[] BuildSlotItems(bool load)
        {
            var saves = SaveGameController.Ensure();
            var list = new Item[SaveGameController.SlotCount + 1];
            for (int i = 0; i < SaveGameController.SlotCount; i++)
            {
                string label = saves.SlotExists(i)
                    ? $"Slot {i} (used)"
                    : $"Slot {i} (empty)";
                list[i] = new Item
                {
                    Patch = null,
                    FallbackLabel = label,
                    Action = (MenuAction)((int)MenuAction.Slot0 + i),
                    X = 80,
                    Y = 48 + i * 16,
                };
            }
            list[SaveGameController.SlotCount] = new Item
            {
                Patch = null,
                FallbackLabel = "Back",
                Action = MenuAction.Back,
                X = 80,
                Y = 48 + SaveGameController.SlotCount * 16,
            };
            return list;
        }

        void HandleSlot(int slot)
        {
            var saves = SaveGameController.Ensure();
            bool ok;
            if (Kind == MenuKind.SaveSlots)
            {
                ok = saves.TrySave(slot, confirmOverwrite: false);
                statusMessage = ok ? $"Saved slot {slot}." : saves.LastError;
                if (ok)
                {
                    // Refresh labels; stay on save menu while paused.
                    items = BuildSlotItems(load: false);
                }
            }
            else
            {
                ok = saves.TryLoad(slot);
                statusMessage = ok ? null : saves.LastError;
                // On success the scene reloads; menu is torn down with the scene.
            }
        }

        void ReturnFromSlots()
        {
            statusMessage = null;
            if (returnKind == MenuKind.Main)
                ShowMain(textures);
            else
                ShowPause(textures);
            returnKind = MenuKind.None;
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
            if (kb.escapeKey.wasPressedThisFrame &&
                (Kind == MenuKind.SaveSlots || Kind == MenuKind.LoadSlots))
                Activate(MenuAction.Back);
        }

        void OnGUI()
        {
            if (!IsVisible || Event.current.type != EventType.Repaint) return;

            var t = VirtualScreenRenderer.ComputeForScreen();

            if (Kind == MenuKind.Main)
                DrawMainBackground(t);
            else if (Kind == MenuKind.SaveSlots || Kind == MenuKind.LoadSlots)
                DrawSlotBackground(t);
            else
                DrawPauseDim(t);

            if (Kind == MenuKind.SaveSlots || Kind == MenuKind.LoadSlots)
                DrawSlotTitle(t);

            for (int i = 0; i < items.Length; i++)
                DrawItem(t, items[i], i == selected);

            if (!string.IsNullOrEmpty(statusMessage))
                DrawHuStringCentered(t, 170f, statusMessage.ToUpperInvariant());
        }

        void DrawSlotBackground(in VirtualScreenRenderer.Transform t)
        {
            var bg = VirtualScreenRenderer.ToScreen(t, 0, 0, 320, 200);
            Color prev = GUI.color;
            // Same plate as Options: solid dark-red from main, dim over world from pause.
            if (returnKind == MenuKind.Pause)
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
            else
                GUI.color = new Color(0.55f, 0f, 0f, 1f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void DrawSlotTitle(in VirtualScreenRenderer.Transform t)
        {
            string patch = Kind == MenuKind.SaveSlots ? "M_SGTTL" : "M_LGTTL";
            if (textures != null && textures.TryGet(patch, out var e))
            {
                float x = (320 - e.Width) * 0.5f;
                var r = VirtualScreenRenderer.ToScreenSnapped(t, x, 20f, e.Width, e.Height);
                GUI.DrawTexture(r, e.Texture);
                return;
            }

            string title = Kind == MenuKind.SaveSlots ? "SAVE GAME" : "LOAD GAME";
            DrawHuStringCentered(t, 20f, title);
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
                DrawHuStringCentered(t, 20f, "PAUSE");
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
                DrawHuString(t, item.X, item.Y, item.FallbackLabel);

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

        void DrawHuStringCentered(in VirtualScreenRenderer.Transform t, float y, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            float width = MeasureHuString(text);
            DrawHuString(t, (320f - width) * 0.5f, y, text);
        }

        float MeasureHuString(string text)
        {
            if (string.IsNullOrEmpty(text) || textures == null) return text?.Length * 8f ?? 0f;
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
                    fontSize = Mathf.Max(14, (int)(12 * t.Scale)),
                    alignment = TextAnchor.UpperLeft,
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
                    fontSize = Mathf.Max(14, (int)(12 * t.Scale)),
                };
                style.normal.textColor = Color.white;
                GUI.Label(VirtualScreenRenderer.ToScreen(t, x, y, 200, 16), text, style);
            }
        }
    }
}
