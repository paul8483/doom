using System;
using Doom.Game;
using UnityEngine;

namespace Doom.MapBuild
{
    /// Abstraction over preference persistence (PlayerPrefs or in-memory for tests).
    public interface ISettingsStorage
    {
        int GetInt(string key, int defaultValue);
        float GetFloat(string key, float defaultValue);
        void SetInt(string key, int value);
        void SetFloat(string key, float value);
        void Save();
    }

    public sealed class PlayerPrefsSettingsStorage : ISettingsStorage
    {
        public int GetInt(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue);
        public float GetFloat(string key, float defaultValue) => PlayerPrefs.GetFloat(key, defaultValue);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
        public void Save() => PlayerPrefs.Save();
    }

    public sealed class MemorySettingsStorage : ISettingsStorage
    {
        readonly System.Collections.Generic.Dictionary<string, float> floats =
            new System.Collections.Generic.Dictionary<string, float>(StringComparer.Ordinal);
        readonly System.Collections.Generic.Dictionary<string, int> ints =
            new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);

        public int GetInt(string key, int defaultValue) =>
            ints.TryGetValue(key, out int v) ? v : defaultValue;

        public float GetFloat(string key, float defaultValue) =>
            floats.TryGetValue(key, out float v) ? v : defaultValue;

        public void SetInt(string key, int value) => ints[key] = value;
        public void SetFloat(string key, float value) => floats[key] = value;
        public void Save() { }
    }

    /// Versioned load/save for <see cref="GameSettingsData"/>. Separate from save slots.
    /// Field keys keep the historical <c>v1</c> prefix so existing PlayerPrefs migrate:
    /// schema 2 adds GraphicsMode (default Classic); schema 3 adds Enhanced3DObjects
    /// (default On when the key is absent).
    public sealed class SettingsStore
    {
        const string Prefix = "Doom.Settings.v1.";
        const string KeyVersion = Prefix + "Version";
        const string KeySfx = Prefix + "SfxVolume";
        const string KeyMusic = Prefix + "MusicVolume";
        const string KeySens = Prefix + "MouseSensitivity";
        const string KeyInvert = Prefix + "InvertY";
        const string KeyFullscreen = Prefix + "Fullscreen";
        const string KeyResW = Prefix + "ResW";
        const string KeyResH = Prefix + "ResH";
        const string KeyGraphicsMode = Prefix + "GraphicsMode";
        const string KeyEnhanced3DObjects = Prefix + "Enhanced3DObjects";

        readonly ISettingsStorage storage;

        public SettingsStore(ISettingsStorage storage = null)
        {
            this.storage = storage ?? new PlayerPrefsSettingsStorage();
        }

        public GameSettingsData Load()
        {
            int version = storage.GetInt(KeyVersion, 0);
            if (version < GameSettingsData.FirstSupportedSchemaVersion ||
                version > GameSettingsData.SchemaVersion)
                return GameSettingsData.Defaults;

            float sfx = storage.GetFloat(KeySfx, GameSettingsData.DefaultSfxVolume);
            float music = storage.GetFloat(KeyMusic, GameSettingsData.DefaultMusicVolume);
            float sens = storage.GetFloat(KeySens, GameSettingsData.DefaultMouseSensitivity);
            bool invert = storage.GetInt(KeyInvert, 0) != 0;
            bool fullscreen = storage.GetInt(KeyFullscreen, 1) != 0;
            int rw = storage.GetInt(KeyResW, 0);
            int rh = storage.GetInt(KeyResH, 0);

            // v1 had no GraphicsMode key — default Classic. Unknown ints → Classic.
            GraphicsMode gfx = version >= 2
                ? GameSettingsData.NormalizeGraphicsMode(
                    storage.GetInt(KeyGraphicsMode, (int)GraphicsMode.Classic))
                : GraphicsMode.Classic;

            // v1/v2 had no Enhanced3DObjects key — default On.
            bool enhanced3D = version >= 3
                ? storage.GetInt(KeyEnhanced3DObjects,
                    GameSettingsData.DefaultEnhanced3DObjects ? 1 : 0) != 0
                : GameSettingsData.DefaultEnhanced3DObjects;

            if (!GameSettingsData.TryCreate(sfx, music, sens, invert, fullscreen, rw, rh, gfx,
                    enhanced3D, out var data, out _))
                return GameSettingsData.Defaults;

            return data;
        }

        public void Save(GameSettingsData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            storage.SetInt(KeyVersion, GameSettingsData.SchemaVersion);
            storage.SetFloat(KeySfx, data.SfxVolume);
            storage.SetFloat(KeyMusic, data.MusicVolume);
            storage.SetFloat(KeySens, data.MouseSensitivity);
            storage.SetInt(KeyInvert, data.InvertY ? 1 : 0);
            storage.SetInt(KeyFullscreen, data.Fullscreen ? 1 : 0);
            storage.SetInt(KeyResW, data.ResolutionWidth);
            storage.SetInt(KeyResH, data.ResolutionHeight);
            storage.SetInt(KeyGraphicsMode, (int)data.GraphicsMode);
            storage.SetInt(KeyEnhanced3DObjects, data.Enhanced3DObjects ? 1 : 0);
            storage.Save();
        }
    }

    /// Display mode adapter — production uses Screen; tests use a fake.
    public interface IDisplayAdapter
    {
        bool Fullscreen { get; }
        int Width { get; }
        int Height { get; }
        void Apply(bool fullscreen, int width, int height);
    }

    public sealed class UnityDisplayAdapter : IDisplayAdapter
    {
        public bool Fullscreen => Screen.fullScreen;
        public int Width => Screen.width;
        public int Height => Screen.height;

        public void Apply(bool fullscreen, int width, int height)
        {
            if (width > 0 && height > 0)
                Screen.SetResolution(width, height, fullscreen);
            else
                Screen.fullScreen = fullscreen;
        }
    }

    public sealed class FakeDisplayAdapter : IDisplayAdapter
    {
        public bool Fullscreen { get; private set; } = true;
        public int Width { get; private set; }
        public int Height { get; private set; }

        public void Apply(bool fullscreen, int width, int height)
        {
            Fullscreen = fullscreen;
            if (width > 0 && height > 0)
            {
                Width = width;
                Height = height;
            }
        }
    }

    /// Applies GraphicsMode to the render stack. Task 2: no-op until URP (Task 4+).
    public interface IGraphicsModeAdapter
    {
        GraphicsMode Current { get; }
        void Apply(GraphicsMode mode);
    }

    public sealed class NoOpGraphicsModeAdapter : IGraphicsModeAdapter
    {
        public GraphicsMode Current { get; private set; } = GraphicsMode.Classic;

        public void Apply(GraphicsMode mode)
        {
            if (!GameSettingsData.IsDefinedGraphicsMode(mode))
                mode = GraphicsMode.Classic;
            Current = mode;
        }
    }
}
