using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;

namespace Doom.MapBuild
{
    /// <summary>
    /// Development-build profiler capture toggled with P.
    /// Binary captures are written beside the standalone build.
    /// </summary>
    public sealed class RuntimeProfilerCapture : MonoBehaviour
    {
        static RuntimeProfilerCapture instance;

        bool recording;
        string capturePath;
        string status;
        float statusUntil;
        GUIStyle statusStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoBootstrap()
        {
            if (!Debug.isDebugBuild && !Application.isEditor) return;
            if (instance != null) return;

            var go = new GameObject(nameof(RuntimeProfilerCapture));
            instance = go.AddComponent<RuntimeProfilerCapture>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.pKey.wasPressedThisFrame)
            {
                if (recording)
                    StopCapture();
                else
                    StartCapture();
            }
        }

        void StartCapture()
        {
            string directory = GetCaptureDirectory();
            Directory.CreateDirectory(directory);
            capturePath = Path.Combine(
                directory,
                $"doom-{DateTime.Now:yyyyMMdd-HHmmss}.raw");

            Profiler.logFile = capturePath;
            Profiler.enableBinaryLog = true;
            Profiler.enabled = true;
            recording = true;

            ShowStatus($"PROFILER RECORDING\nP: stop\n{capturePath}", 8f);
            Debug.Log($"[ProfilerCapture] Started: {capturePath}");
        }

        void StopCapture()
        {
            Profiler.enabled = false;
            Profiler.enableBinaryLog = false;
            Profiler.logFile = string.Empty;
            recording = false;

            ShowStatus($"PROFILER SAVED\n{capturePath}", 8f);
            Debug.Log($"[ProfilerCapture] Saved: {capturePath}");
        }

        void OnApplicationQuit()
        {
            if (recording)
                StopCapture();
        }

        void OnGUI()
        {
            if (!recording && Time.realtimeSinceStartup > statusUntil) return;

            statusStyle ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 16,
                wordWrap = true,
            };
            statusStyle.normal.textColor = recording ? Color.red : Color.green;

            GUI.Box(new Rect(12f, 12f, Mathf.Min(Screen.width - 24f, 720f), 76f),
                status, statusStyle);
        }

        void ShowStatus(string message, float seconds)
        {
            status = message;
            statusUntil = Time.realtimeSinceStartup + seconds;
        }

        static string GetCaptureDirectory()
        {
            if (!Application.isEditor)
            {
                string buildDirectory = Path.GetFullPath(
                    Path.Combine(Application.dataPath, ".."));
                return Path.Combine(buildDirectory, "ProfilerCaptures");
            }

            return Path.Combine(Application.persistentDataPath, "ProfilerCaptures");
        }
    }
}
