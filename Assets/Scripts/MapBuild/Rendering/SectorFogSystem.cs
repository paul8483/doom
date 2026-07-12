using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild.Rendering
{
    /// Soft distance fog for Enhanced. Uses shader globals consumed by Enhanced/
    /// Fluid shaders; clamps so switches/doors stay readable at play distance.
    public sealed class SectorFogSystem : MonoBehaviour
    {
        public static SectorFogSystem Instance { get; private set; }

        public const float DefaultDensity = 0.55f;
        public const float DefaultStart = 4f;
        public const float DefaultEnd = 45f;

        static readonly int FogColorId = Shader.PropertyToID("_DoomFogColor");
        static readonly int FogParamsId = Shader.PropertyToID("_DoomFogParams");

        bool enabledForProfile;
        Color fogColor = new Color(0.08f, 0.08f, 0.1f, 1f);

        public bool IsProfileEnabled => enabledForProfile;
        public bool FogGlobalsActive { get; private set; }

        public void Init()
        {
            Instance = this;
            var profile = GraphicsModeController.Instance != null
                ? GraphicsModeController.Instance.ActiveProfile
                : GraphicsProfile.Classic;
            ApplyProfile(profile, GraphicsCapabilityReport.Full);
        }

        public void ApplyProfile(GraphicsProfile profile, GraphicsCapabilityReport caps)
        {
            _ = caps;
            enabledForProfile = profile.Mode == GraphicsMode.Enhanced && profile.Fog;
            PushGlobals();
        }

        public void SetCapabilities(GraphicsCapabilityReport caps)
        {
            // No-op for enablement — see ApplyProfile. Kept for call-site stability.
            _ = caps;
        }

        void PushGlobals()
        {
            if (enabledForProfile)
            {
                Shader.SetGlobalColor(FogColorId, fogColor);
                // x=density, y=start, z=end, w=enabled
                Shader.SetGlobalVector(FogParamsId,
                    new Vector4(DefaultDensity, DefaultStart, DefaultEnd, 1f));
                FogGlobalsActive = true;
                // Keep Unity fog off — custom shaders own the look; avoids double fog.
                RenderSettings.fog = false;
            }
            else
            {
                Shader.SetGlobalColor(FogColorId, Color.clear);
                Shader.SetGlobalVector(FogParamsId, Vector4.zero);
                FogGlobalsActive = false;
                RenderSettings.fog = false;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            enabledForProfile = false;
            PushGlobals();
        }
    }
}
