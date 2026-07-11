using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Doom.MapBuild.Rendering
{
    /// Configures the world (player) camera for Classic vs Enhanced presentation.
    /// Does not touch OnGUI HUD / weapon / menu compositing.
    public sealed class WorldCameraRenderer : MonoBehaviour
    {
        Camera worldCamera;
        UniversalAdditionalCameraData cameraData;
        Volume volume;
        VolumeProfile enhancedProfile;
        bool ownsVolume;
        readonly EnhancedPostController post = new EnhancedPostController();
        GraphicsCapabilityReport capabilities = GraphicsCapabilityReport.Full;

        public Camera WorldCamera => worldCamera;
        public bool PostProcessingEnabled =>
            cameraData != null && cameraData.renderPostProcessing;
        public EnhancedPostController Post => post;
        public bool VolumeEnabled => volume != null && volume.enabled;

        public void Init(Camera camera, VolumeProfile enhancedVolumeProfile)
        {
            worldCamera = camera;
            enhancedProfile = enhancedVolumeProfile;
            cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
                cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();

            cameraData.renderType = CameraRenderType.Base;
            cameraData.renderPostProcessing = false;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            EnsureVolume();
            post.Bind(enhancedProfile);
            ApplyProfile(GraphicsProfile.Classic);
        }

        public void SetCapabilities(GraphicsCapabilityReport report) =>
            capabilities = report;

        void EnsureVolume()
        {
            if (volume != null) return;
            var go = new GameObject("DoomWorldVolume");
            go.transform.SetParent(worldCamera.transform, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;
            volume.profile = enhancedProfile;
            volume.enabled = false;
            ownsVolume = true;
        }

        public void ApplyProfile(GraphicsProfile profile)
        {
            if (worldCamera == null || cameraData == null) return;

            bool enhanced = profile.Mode == Doom.Game.GraphicsMode.Enhanced;
            bool postOn = enhanced && profile.PostProcessing;
            bool hdr = enhanced && profile.Hdr && capabilities.Hdr;

            post.Apply(profile, capabilities);

            cameraData.renderPostProcessing = postOn;
            worldCamera.allowHDR = hdr;
            worldCamera.allowMSAA = enhanced && profile.Msaa && capabilities.Msaa;

            if (volume != null)
            {
                volume.enabled = postOn && enhancedProfile != null;
                volume.profile = enhancedProfile;
            }
        }

        /// Re-apply after resize/fullscreen so Enhanced does not keep stale RT scale.
        public void NotifyDisplayChanged()
        {
            if (GraphicsModeController.Instance == null) return;
            ApplyProfile(GraphicsModeController.Instance.ActiveProfile);
        }

        void OnDestroy()
        {
            if (ownsVolume && volume != null)
                Destroy(volume.gameObject);
        }
    }
}
