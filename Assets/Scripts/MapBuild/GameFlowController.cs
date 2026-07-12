using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Doom.Game;
using Doom.Wad;

namespace Doom.MapBuild
{
    /// Single owner of freeze / cursor / timescale and high-level UI state.
    /// Death, intermission, and pause are mutually exclusive.
    public sealed class GameFlowController : MonoBehaviour
    {
        public const string PreviewSceneName = LevelTransitionController.PreviewSceneName;

        /// When true, MapLoader builds the level and enters Playing without the
        /// main menu. Editor/PlayMode default is true (tests + hit-Play convenience).
        /// Standalone player boots with this false → main menu (Stage 7e acceptance).
        public static bool AutoStartPlaying = true;

        /// One-shot: next MapLoader.Start skips geometry and opens the main menu.
        public static bool ForceMainMenuOnNextLoad;

        public static GameFlowController Instance { get; private set; }

        public GameFlowState State { get; private set; } = GameFlowState.Boot;
        public MenuController Menu { get; private set; }
        public LoadingView Loading { get; private set; }

        float savedTimeScale = 1f;
        bool timeScaleSaved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyPlayerBootDefaults()
        {
            // Fresh Windows build must open main menu, not E1M1. Leave Editor/PlayMode
            // on AutoStartPlaying=true so existing tests keep auto-building the map.
            if (!Application.isEditor)
                AutoStartPlaying = false;
        }

        public static GameFlowController Ensure()
        {
            if (Instance != null) return Instance;
            var host = GameSessionHost.Ensure();
            var flow = host.GetComponent<GameFlowController>();
            if (flow == null) flow = host.gameObject.AddComponent<GameFlowController>();
            return flow;
        }

        public static void ResetForTests()
        {
            AutoStartPlaying = true;
            ForceMainMenuOnNextLoad = false;
            Time.timeScale = 1f;
            // Host reset destroys this component with the DDOL object.
            GameSessionHost.ResetForTests();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            Menu = GetComponent<MenuController>() ?? gameObject.AddComponent<MenuController>();
            Menu.Init(this);
            Loading = GetComponent<LoadingView>() ?? gameObject.AddComponent<LoadingView>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            RestoreTimeScaleIfNeeded();
        }

        void Update()
        {
            if (SettingsController.Instance != null && SettingsController.Instance.IsEditing)
                return;

            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

            // Slot submenus handle Escape themselves (Back).
            if (Menu != null &&
                (Menu.Kind == MenuKind.SaveSlots || Menu.Kind == MenuKind.LoadSlots))
                return;

            if (State == GameFlowState.Playing)
                RequestPause();
            else if (State == GameFlowState.Paused)
                Resume();
        }

        /// Map finished building and player spawned (or UI-only boot decided to play).
        public void NotifyLevelReady()
        {
            if (State == GameFlowState.Intermission) return;
            EnterPlaying();
        }

        public void EnterMainMenu()
        {
            var host = GameSessionHost.Ensure();
            host.ClearPendingRestore();
            host.Session.Clear();
            HideLoading();
            SetState(GameFlowState.MainMenu);
            FreezeGameplay();
            UnlockCursor();
            RestoreTimeScaleIfNeeded();
            Menu.ShowMain(ResolveHudTextures());
        }

        public void EnterLoading()
        {
            if (Menu != null) Menu.Hide();
            SetState(GameFlowState.Loading);
            FreezeGameplay();
            UnlockCursor();
            ShowLoading();
        }

        public void EnterPlaying()
        {
            if (Menu != null) Menu.Hide();
            HideLoading();
            SetState(GameFlowState.Playing);
            RestoreTimeScaleIfNeeded();
            UnfreezeGameplay();
            LockCursor();
        }

        public void RequestPause()
        {
            if (State != GameFlowState.Playing) return;
            SetState(GameFlowState.Paused);
            SaveAndZeroTimeScale();
            FreezeGameplay();
            UnlockCursor();
            PauseMusic();
            Menu.ShowPause(ResolveHudTextures());
        }

        public void Resume()
        {
            if (State != GameFlowState.Paused) return;
            ResumeMusic();
            EnterPlaying();
        }

        public void EnterDead()
        {
            if (State == GameFlowState.Dead) return;
            if (State != GameFlowState.Playing && State != GameFlowState.Paused) return;

            if (State == GameFlowState.Paused)
                RestoreTimeScaleIfNeeded();

            Menu.Hide();
            SetState(GameFlowState.Dead);
            FreezeGameplay();
            UnlockCursor();
        }

        public void LeaveDeadToPlaying()
        {
            if (State != GameFlowState.Dead) return;
            EnterPlaying();
        }

        public void EnterIntermission()
        {
            if (State == GameFlowState.Intermission) return;
            if (State == GameFlowState.Paused)
                RestoreTimeScaleIfNeeded();

            Menu.Hide();
            SetState(GameFlowState.Intermission);
            FreezeGameplay();
            UnlockCursor();
        }

        /// New Game from main/pause menu: clear session, start E1M1, reload scene.
        public void StartNewGame()
        {
            var host = GameSessionHost.Ensure();
            host.ClearPendingRestore();
            host.Session.Clear();
            host.SetNextSpawnId(0);
            host.Session.BeginNewGame("E1M1", CollectAvailableMaps());
            EnterLoading();
            SceneManager.LoadScene(PreviewSceneName, LoadSceneMode.Single);
        }

        /// Quit to main menu: clear session and reload without map build intent.
        public void QuitToMainMenu()
        {
            var host = GameSessionHost.Ensure();
            host.ClearPendingRestore();
            host.Session.Clear();
            host.SetNextSpawnId(0);
            EnterLoading();
            ForceMainMenuOnNextLoad = true;
            SceneManager.LoadScene(PreviewSceneName, LoadSceneMode.Single);
        }

        /// Episode complete → main menu (session cleared, scene reloaded).
        public void ReturnToMainMenuAfterEpisode()
        {
            QuitToMainMenu();
        }

        public void QuitApplication()
        {
            // Editor: Application.Quit is a no-op; stop Play mode via reflection so
            // Doom.MapBuild does not take an Editor asmdef reference.
            var editorApp = Type.GetType("UnityEditor.EditorApplication,UnityEditor");
            if (editorApp != null)
            {
                var prop = editorApp.GetProperty("isPlaying",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                prop?.SetValue(null, false);
                return;
            }

            Application.Quit();
        }

        /// Whether MapLoader should build geometry this Start().
        public static bool ShouldBuildMap()
        {
            if (ForceMainMenuOnNextLoad)
            {
                ForceMainMenuOnNextLoad = false;
                return false;
            }

            if (AutoStartPlaying) return true;
            if (!string.IsNullOrEmpty(MapLoader.MapNameOverride)) return true;

            var host = GameSessionHost.Instance;
            if (host != null && host.Session != null && host.Session.IsActive &&
                !string.IsNullOrEmpty(host.Session.CurrentMap))
                return true;

            return false;
        }

        void SetState(GameFlowState next) => State = next;

        /// STBAR / face: only while playing or dead (death overlay still shows status).
        public static bool ShouldDrawStatusHud()
        {
            var flow = Instance;
            if (flow == null) return true;
            return flow.State == GameFlowState.Playing || flow.State == GameFlowState.Dead;
        }

        /// Viewmodel weapon: only while actively playing — hide under pause/menus.
        public static bool ShouldDrawWeaponView()
        {
            var flow = Instance;
            if (flow == null) return true;
            return flow.State == GameFlowState.Playing;
        }

        void FreezeGameplay()
        {
            SetPlayerGameplayEnabled(false);
        }

        void UnfreezeGameplay()
        {
            SetPlayerGameplayEnabled(true);
        }

        static void SetPlayerGameplayEnabled(bool on)
        {
            var player = GameObject.Find("Player");
            if (player == null) return;

            var pc = player.GetComponent<PlayerController>();
            if (pc != null) pc.enabled = on;

            var act = player.GetComponent<LineActivator>();
            if (act != null) act.enabled = on;

            var weap = player.GetComponent<PlayerWeapons>();
            if (weap != null) weap.enabled = on;

            var floor = player.GetComponent<FloorDamageSystem>();
            if (floor != null) floor.enabled = on;
        }

        void SaveAndZeroTimeScale()
        {
            if (!timeScaleSaved)
            {
                savedTimeScale = Time.timeScale;
                if (savedTimeScale <= 0f) savedTimeScale = 1f;
                timeScaleSaved = true;
            }

            Time.timeScale = 0f;
        }

        void RestoreTimeScaleIfNeeded()
        {
            if (!timeScaleSaved) return;
            Time.timeScale = savedTimeScale;
            timeScaleSaved = false;
        }

        static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        static void PauseMusic()
        {
            var loader = UnityEngine.Object.FindAnyObjectByType<MapLoader>();
            loader?.Music?.Pause();
        }

        static void ResumeMusic()
        {
            var loader = UnityEngine.Object.FindAnyObjectByType<MapLoader>();
            loader?.Music?.Resume();
        }

        void ShowLoading()
        {
            if (Loading == null)
                Loading = LoadingView.Ensure();

            var host = GameSessionHost.Instance;
            string map = host != null && host.Session != null && host.Session.IsActive
                ? host.Session.CurrentMap
                : "";
            Loading.Show(ResolveHudTextures(), map);
        }

        void HideLoading()
        {
            if (Loading != null) Loading.Hide();
            else
            {
                var view = GetComponent<LoadingView>();
                view?.Hide();
            }
        }

        /// MapLoader calls this so AutoStartPlaying / editor Play also get a plate.
        public void EnsureLoadingShown(string mapName)
        {
            if (Loading == null)
                Loading = LoadingView.Ensure();
            if (!Loading.IsVisible)
                Loading.Show(ResolveHudTextures(), mapName);
            if (State == GameFlowState.Boot)
            {
                SetState(GameFlowState.Loading);
                FreezeGameplay();
            }
        }

        public void ReportLoadProgress(float progress01, string status = null)
        {
            if (Loading == null) Loading = GetComponent<LoadingView>();
            if (Loading != null && Loading.IsVisible)
                Loading.SetProgress(progress01, status);
        }

        static HudTextureCache ResolveHudTextures()
        {
            var loader = UnityEngine.Object.FindAnyObjectByType<MapLoader>();
            return loader != null ? loader.HudTextures : null;
        }

        internal static List<string> CollectAvailableMaps()
        {
            var list = new List<string>();
            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            if (!File.Exists(path))
            {
                for (int m = 1; m <= 9; m++) list.Add($"E1M{m}");
                return list;
            }

            using var wad = WadFile.Open(path);
            foreach (var lump in wad.Directory)
            {
                if (WadMapNames.IsMapMarker(lump.Name) &&
                    CampaignRoute.TryNormalize(lump.Name, out string canonical))
                    list.Add(canonical);
            }

            return list;
        }
    }
}
