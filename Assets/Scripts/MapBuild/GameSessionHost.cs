using UnityEngine;

namespace Doom.MapBuild
{
    /// Persistent campaign session host. Survives scene reloads; never holds
    /// references to MapLoader, player, meshes, or audio after unload.
    public sealed class GameSessionHost : MonoBehaviour
    {
        public static GameSessionHost Instance { get; private set; }

        public Doom.Game.SessionState Session { get; private set; }

        /// Idempotent bootstrap. Safe to call from menus, transitions, and tests.
        public static GameSessionHost Ensure()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("GameSessionHost");
            var host = go.AddComponent<GameSessionHost>();
            return host;
        }

        /// Destroys the host and clears test overrides. PlayMode teardown must call this.
        public static void ResetForTests()
        {
            MapLoader.MapNameOverride = null;
            GameFlowController.AutoStartPlaying = true;
            GameFlowController.ForceMainMenuOnNextLoad = false;
            Time.timeScale = 1f;
            if (Instance != null)
            {
                var go = Instance.gameObject;
                Instance = null;
                if (Application.isPlaying) Object.Destroy(go);
                else Object.DestroyImmediate(go);
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (Session == null)
                Session = new Doom.Game.SessionState();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
