using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Doom.Game;
using Doom.Wad;

namespace Doom.MapBuild
{
    /// Freezes gameplay, advances the campaign session, and reloads the preview
    /// scene. Intermission UI is added in Task 7; until then transitions confirm
    /// immediately (or via <see cref="ConfirmIntermission"/>).
    public sealed class LevelTransitionController : MonoBehaviour
    {
        public const string PreviewSceneName = "Stage2_MapPreview";

        public static LevelTransitionController Instance { get; private set; }

        /// When true, skip waiting for intermission confirm (PlayMode default path).
        public static bool ImmediateConfirmForTests = true;

        public bool IsTransitioning { get; private set; }
        public LevelExitRequest? LastRequest { get; private set; }
        public string LastLoadedMap { get; private set; }
        public LevelStatsSnapshot? LastStats { get; private set; }
        public IntermissionView Intermission { get; private set; }

        /// Raised once when an exit request is accepted (before scene reload).
        public event Action<LevelExitRequest> ExitAccepted;

        /// Raised when intermission becomes visible (stats frozen).
        public event Action<LevelStatsSnapshot> IntermissionShown;

        public static LevelTransitionController Ensure()
        {
            if (Instance != null) return Instance;
            var host = GameSessionHost.Ensure();
            var ctrl = host.GetComponent<LevelTransitionController>();
            if (ctrl == null) ctrl = host.gameObject.AddComponent<LevelTransitionController>();
            return ctrl;
        }

        public static void ResetForTests()
        {
            ImmediateConfirmForTests = true;
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
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// Returns false if a transition is already in progress.
        public bool TryRequestExit(LevelExitRequest request)
        {
            if (IsTransitioning) return false;
            IsTransitioning = true;
            LastRequest = request;
            ExitAccepted?.Invoke(request);
            StartCoroutine(RunTransition(request));
            return true;
        }

        /// Task 7 intermission confirm hook. No-op when ImmediateConfirmForTests.
        public void ConfirmIntermission() => _confirm = true;

        bool _confirm;

        IEnumerator RunTransition(LevelExitRequest request)
        {
            FreezeGameplay();

            var host = GameSessionHost.Ensure();
            EnsureSessionActive(host);

            string finishedMap = host.Session.CurrentMap;
            var stats = LevelStatsTracker.Instance != null
                ? LevelStatsTracker.Instance.Stats.Snapshot()
                : default;
            LastStats = stats;

            var carry = CaptureCarry();
            var result = host.Session.Advance(request.Kind, carry ?? PlayerCarryState.FreshStart());

            ShowIntermission(stats, finishedMap, result.NextMap);
            IntermissionShown?.Invoke(stats);

            if (!ImmediateConfirmForTests)
            {
                _confirm = false;
                while (!_confirm) yield return null;
            }

            HideIntermission();

            if (result.Outcome == CampaignOutcome.EpisodeComplete)
            {
                // Task 8 adds main-menu return; for now stay frozen on current scene.
                Debug.Log("[7a] Episode complete — main menu deferred to Task 8");
                IsTransitioning = false;
                yield break;
            }

            LastLoadedMap = result.NextMap;
            Debug.Log($"[7a] Level exit {request.Kind} → {result.NextMap}" +
                      (result.UsedSecretFallback ? " (secret fallback)" : ""));

            // Scene reload tears down the old map; session host survives.
            IsTransitioning = false;
            SceneManager.LoadScene(PreviewSceneName, LoadSceneMode.Single);
        }

        void ShowIntermission(LevelStatsSnapshot stats, string finished, string next)
        {
            if (Intermission == null)
                Intermission = gameObject.GetComponent<IntermissionView>()
                    ?? gameObject.AddComponent<IntermissionView>();

            var loader = UnityEngine.Object.FindAnyObjectByType<MapLoader>();
            var textures = loader != null ? loader.HudTextures : null;
            Intermission.Show(textures, stats, finished, next);
        }

        void HideIntermission()
        {
            if (Intermission != null)
                Intermission.Hide();
        }

        void FreezeGameplay()
        {
            var player = GameObject.Find("Player");
            if (player == null) return;
            var pc = player.GetComponent<PlayerController>();
            if (pc != null) pc.enabled = false;
            var act = player.GetComponent<LineActivator>();
            if (act != null) act.enabled = false;
            var weap = player.GetComponent<PlayerWeapons>();
            if (weap != null) weap.enabled = false;
            var floor = player.GetComponent<FloorDamageSystem>();
            if (floor != null) floor.enabled = false;
        }

        static PlayerCarryState CaptureCarry()
        {
            var player = GameObject.Find("Player");
            if (player == null) return PlayerCarryState.FreshStart();
            var health = player.GetComponent<PlayerHealth>();
            var weapons = player.GetComponent<PlayerWeapons>();
            if (health == null || weapons == null) return PlayerCarryState.FreshStart();
            return PlayerCarryState.Capture(health.Model, weapons.Ammo, weapons.Loadout);
        }

        static void EnsureSessionActive(GameSessionHost host)
        {
            if (host.Session.IsActive) return;

            var loader = UnityEngine.Object.FindAnyObjectByType<MapLoader>();
            string current = loader != null && !string.IsNullOrEmpty(loader.LoadedMapName)
                ? loader.LoadedMapName
                : "E1M1";

            host.Session.BeginNewGame(current, CollectAvailableMaps());
        }

        static List<string> CollectAvailableMaps()
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

        /// Map linedef special number → exit kind.
        public static ExitKind KindFromLinedefSpecial(int specialType) =>
            specialType == 51 || specialType == 124 ? ExitKind.Secret : ExitKind.Normal;
    }
}
