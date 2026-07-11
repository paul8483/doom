using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Doom.MapBuild.Editor
{
    /// Stage 8 Task 3: create URP assets, assign pipeline, Linear color space,
    /// strip scene Directional Light. Idempotent — safe to re-run.
    public static class ConfigureUrpPipeline
    {
        const string SettingsDir = "Assets/Settings/Rendering";
        const string PipelinePath = SettingsDir + "/DoomUniversalRenderPipeline.asset";
        const string RendererPath = SettingsDir + "/DoomUniversalRenderer.asset";
        const string VolumePath = SettingsDir + "/DoomEnhancedVolume.asset";
        const string ScenePath = "Assets/Scenes/Stage2_MapPreview.unity";
        const string ClassicOpaquePath = "Assets/Shaders/DoomClassicOpaque.shader";
        const string ClassicCutoutPath = "Assets/Shaders/DoomClassicCutout.shader";
        const string EnhancedOpaquePath = "Assets/Shaders/DoomEnhancedWorld.shader";
        const string EnhancedCutoutPath = "Assets/Shaders/DoomEnhancedCutout.shader";
        const string EnhancedSpritePath = "Assets/Shaders/DoomEnhancedSprite.shader";
        const string SpectrePath = "Assets/Shaders/DoomSpectre.shader";
        const string SkyPath = "Assets/Shaders/DoomSky.shader";
        const string FluidPath = "Assets/Shaders/DoomFluid.shader";

        [MenuItem("Tools/Doom/Configure URP Pipeline (Stage 8)")]
        public static void ConfigureFromMenu() => Configure();

        /// Batchmode entry: -executeMethod Doom.MapBuild.Editor.ConfigureUrpPipeline.Configure
        public static void Configure()
        {
            Directory.CreateDirectory(SettingsDir.Replace('/', Path.DirectorySeparatorChar));

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }

            // Depth/opaque available for Enhanced; SSAO feature added below.
            renderer.depthPrimingMode = DepthPrimingMode.Disabled;

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            else
            {
                var so = new SerializedObject(pipeline);
                var list = so.FindProperty("m_RendererDataList");
                if (list != null && list.arraySize > 0)
                {
                    list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 1; // Classic default; Enhanced raises via EnhancedPostController
            pipeline.useSRPBatcher = true;
            pipeline.supportsCameraDepthTexture = true;
            pipeline.supportsCameraOpaqueTexture = true;

            // Task 9: additional lights + shadows (no directional sun at runtime).
            var pipeSo = new SerializedObject(pipeline);
            var addShadows = pipeSo.FindProperty("m_AdditionalLightShadowsSupported");
            if (addShadows != null) addShadows.boolValue = true;
            var perObject = pipeSo.FindProperty("m_AdditionalLightsPerObjectLimit");
            if (perObject != null) perObject.intValue = 8;
            pipeSo.ApplyModifiedPropertiesWithoutUndo();

            EnsureSsaoFeature(renderer);

            EditorUtility.SetDirty(pipeline);
            EditorUtility.SetDirty(renderer);

            var volume = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (volume == null)
            {
                volume = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(volume, VolumePath);
            }

            EnsureVolumeOverrides();

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            GraphicsSettings.lightsUseLinearIntensity = true;

            IncludeShader(ClassicOpaquePath);
            IncludeShader(ClassicCutoutPath);
            IncludeShader(EnhancedOpaquePath);
            IncludeShader(EnhancedCutoutPath);
            IncludeShader(EnhancedSpritePath);
            IncludeShader(SpectrePath);
            IncludeShader(SkyPath);
            IncludeShader(FluidPath);
            // Keep legacy Built-in names included until runtime fully migrated.
            IncludeShader("Assets/Shaders/DoomUnlit.shader");
            IncludeShader("Assets/Shaders/DoomUnlitCutout.shader");

            StripSceneDirectionalLight();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[Stage8] URP configured: package Universal RP, Linear color space, " +
                $"pipeline={PipelinePath}, renderer={RendererPath}, volume={VolumePath}");
        }

        static void EnsureSsaoFeature(UniversalRendererData renderer)
        {
            if (renderer == null) return;
            try
            {
                foreach (var feature in renderer.rendererFeatures)
                {
                    if (feature is ScreenSpaceAmbientOcclusion)
                    {
                        feature.SetActive(true);
                        EditorUtility.SetDirty(renderer);
                        return;
                    }
                }

                var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "ScreenSpaceAmbientOcclusion";
                ssao.SetActive(true);
                AssetDatabase.AddObjectToAsset(ssao, renderer);
                renderer.rendererFeatures.Add(ssao);
                EditorUtility.SetDirty(renderer);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Stage8] Could not add SSAO renderer feature: {ex.Message}");
            }
        }

        static void EnsureVolumeOverrides()
        {
            var volume = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (volume == null) return;
            var post = new Doom.MapBuild.Rendering.EnhancedPostController();
            post.Bind(volume);
            EditorUtility.SetDirty(volume);
        }

        static void IncludeShader(string assetPath)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
            if (shader == null)
            {
                Debug.LogWarning($"[Stage8] Always-included shader missing: {assetPath}");
                return;
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[Stage8] Could not open GraphicsSettings.asset for Always Included Shaders");
                return;
            }

            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("m_AlwaysIncludedShaders");
            if (prop == null || !prop.isArray) return;

            for (int i = 0; i < prop.arraySize; i++)
            {
                if (prop.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    return;
            }

            prop.InsertArrayElementAtIndex(prop.arraySize);
            prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = shader;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(assets[0]);
        }

        static void StripSceneDirectionalLight()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogWarning($"[Stage8] Scene not found: {ScenePath}");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            int removed = 0;
            foreach (var light in lights)
            {
                if (light == null) continue;
                if (light.type != LightType.Directional) continue;
                Object.DestroyImmediate(light.gameObject);
                removed++;
            }

            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[Stage8] Removed {removed} Directional Light(s) from {ScenePath}");
            }
        }
    }
}
