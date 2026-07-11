using UnityEngine;
using UnityEngine.Rendering;
using Doom.Game;

namespace Doom.MapBuild.Rendering
{
    /// Persistent graphics-mode adapter. Survives scene reload on GameSessionHost;
    /// rebinds to each map's WorldRenderContext.
    public sealed class GraphicsModeController : MonoBehaviour, IGraphicsModeAdapter
    {
        public static GraphicsModeController Instance { get; private set; }

        GraphicsMode current = GraphicsMode.Classic;
        GraphicsProfile activeProfile = GraphicsProfile.Classic;
        WorldRenderContext context;
        DoomMaterialFactory factory;
        GraphicsCapabilityReport capabilities = GraphicsCapabilityReport.Full;
        VolumeProfile enhancedVolumeProfile;
        string lastError;

        public GraphicsMode Current => current;
        public GraphicsProfile ActiveProfile => activeProfile;
        public WorldRenderContext Context => context;
        public DoomMaterialFactory Factory => factory;
        public string LastError => lastError;

        public static GraphicsModeController Ensure()
        {
            if (Instance != null) return Instance;
            var host = GameSessionHost.Ensure();
            var ctrl = host.GetComponent<GraphicsModeController>();
            if (ctrl == null) ctrl = host.gameObject.AddComponent<GraphicsModeController>();
            return ctrl;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            factory = new DoomMaterialFactory();
            enhancedVolumeProfile = LoadVolumeProfile();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        static VolumeProfile LoadVolumeProfile()
        {
#if UNITY_EDITOR
            var fromSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                "Assets/Settings/Rendering/DoomEnhancedVolume.asset");
            if (fromSettings != null) return fromSettings;
#endif
            // Empty profile until Task 10 fills post overrides; still toggles cleanly.
            return ScriptableObject.CreateInstance<VolumeProfile>();
        }

        public void SetCapabilities(GraphicsCapabilityReport report) =>
            capabilities = report;

        public void SetEnhancedVolumeProfile(VolumeProfile profile) =>
            enhancedVolumeProfile = profile;

        public VolumeProfile EnhancedVolumeProfile => enhancedVolumeProfile;

        /// Called by MapLoader after creating the world camera / materials.
        public void RegisterContext(WorldRenderContext worldContext)
        {
            context?.Dispose();
            context = worldContext;
            if (context == null) return;

            if (factory == null) factory = new DoomMaterialFactory();
            context.BindFactory(factory);

            // Re-apply the persisted mode to the new scene context.
            ApplyInternal(current, force: true);
        }

        public void ClearContext()
        {
            context?.Dispose();
            context = null;
        }

        public void Apply(GraphicsMode mode)
        {
            ApplyInternal(mode, force: false);
        }

        void ApplyInternal(GraphicsMode mode, bool force)
        {
            lastError = null;
            if (!GameSettingsData.IsDefinedGraphicsMode(mode))
                mode = GraphicsMode.Classic;

            if (!force && mode == current && context != null &&
                context.Materials != null &&
                context.Materials.ActiveMode == mode)
            {
                current = mode;
                return;
            }

            var previous = current;
            var previousProfile = activeProfile;

            try
            {
                var requested = GraphicsProfile.ForMode(mode);
                var effective = GraphicsCapabilityPolicy.Apply(requested, capabilities);

                if (factory == null) factory = new DoomMaterialFactory();
                factory.SetActiveProfile(effective);

                context?.ApplyProfile(effective, factory);

                current = mode;
                activeProfile = effective;
            }
            catch (System.Exception ex)
            {
                lastError = ex.Message;
                Debug.LogError($"GraphicsModeController: failed to apply {mode}: {ex.Message}");
                // Roll back requested mode bookkeeping; leave materials as-is if mid-apply.
                current = previous;
                activeProfile = previousProfile;
                try
                {
                    factory?.SetActiveProfile(previousProfile);
                    context?.ApplyProfile(previousProfile, factory);
                }
                catch (System.Exception rollbackEx)
                {
                    Debug.LogError($"GraphicsModeController: rollback failed: {rollbackEx.Message}");
                }
            }
        }
    }
}
