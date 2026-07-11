using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Game;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    public class SettingsPlayTests
    {
        MemorySettingsStorage memory;
        FakeDisplayAdapter display;

        [SetUp]
        public void SetUp()
        {
            MapLoader.MapNameOverride = null;
            GameFlowController.ResetForTests();
            GameFlowController.AutoStartPlaying = true;
            memory = new MemorySettingsStorage();
            display = new FakeDisplayAdapter();
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            MapLoader.MapNameOverride = null;
            LevelTransitionController.ImmediateConfirmForTests = true;
            GameFlowController.ResetForTests();
        }

        [UnityTest]
        public IEnumerator Settings_apply_to_sound_music_and_look()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForPlaying();

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display);

            settings.OpenOptions();
            Assert.That(settings.IsEditing, Is.True);

            settings.SetSfxVolume(0.25f);
            settings.SetMusicVolume(0.35f);
            settings.SetMouseSensitivity(0.22f);
            settings.SetInvertY(true);
            settings.SetFullscreen(false);

            var loader = Object.FindAnyObjectByType<MapLoader>();
            Assert.That(loader.Sound.Volume, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(loader.Music.Volume, Is.EqualTo(0.35f).Within(0.001f));

            var pc = GameObject.Find("Player").GetComponent<PlayerController>();
            Assert.That(pc.MouseSensitivity, Is.EqualTo(0.22f).Within(0.001f));
            Assert.That(pc.InvertY, Is.True);
            Assert.That(display.Fullscreen, Is.False);

            settings.ApplyAndSave();
            Assert.That(settings.IsEditing, Is.False);

            // Reload preferences from the same memory store.
            var reloaded = new SettingsStore(memory).Load();
            Assert.That(reloaded.SfxVolume, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(reloaded.MusicVolume, Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(reloaded.MouseSensitivity, Is.EqualTo(0.22f).Within(0.001f));
            Assert.That(reloaded.InvertY, Is.True);
            Assert.That(reloaded.Fullscreen, Is.False);
            Assert.That(reloaded.GraphicsMode, Is.EqualTo(GraphicsMode.Classic));
        }

        [UnityTest]
        public IEnumerator Options_graphics_mode_apply_persist_and_cancel()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForPlaying();

            var gfx = new NoOpGraphicsModeAdapter();
            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display, gfx);

            settings.OpenOptions();
            Assert.That(settings.Current.GraphicsMode, Is.EqualTo(GraphicsMode.Classic));

            settings.SetGraphicsMode(GraphicsMode.Enhanced);
            Assert.That(settings.Current.GraphicsMode, Is.EqualTo(GraphicsMode.Enhanced));
            Assert.That(gfx.Current, Is.EqualTo(GraphicsMode.Enhanced));

            settings.ApplyAndSave();
            Assert.That(settings.IsEditing, Is.False);

            var reloaded = new SettingsStore(memory).Load();
            Assert.That(reloaded.GraphicsMode, Is.EqualTo(GraphicsMode.Enhanced));

            // Cancel restores snapshot including graphics mode.
            settings.ConfigureForTests(new SettingsStore(memory), display, gfx);
            settings.OpenOptions();
            settings.SetGraphicsMode(GraphicsMode.Classic);
            Assert.That(gfx.Current, Is.EqualTo(GraphicsMode.Classic));
            settings.Cancel();
            Assert.That(settings.Current.GraphicsMode, Is.EqualTo(GraphicsMode.Enhanced));
            Assert.That(gfx.Current, Is.EqualTo(GraphicsMode.Enhanced));
        }

        [UnityTest]
        public IEnumerator Options_cancel_restores_snapshot()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForPlaying();

            var settings = SettingsController.Ensure();
            settings.ConfigureForTests(new SettingsStore(memory), display);
            settings.SetSfxVolume(0.8f);
            // Persist baseline without opening options.
            new SettingsStore(memory).Save(settings.Current);
            settings.ConfigureForTests(new SettingsStore(memory), display);

            settings.OpenOptions();
            settings.SetSfxVolume(0.1f);
            Assert.That(Object.FindAnyObjectByType<MapLoader>().Sound.Volume,
                Is.EqualTo(0.1f).Within(0.001f));

            settings.Cancel();
            Assert.That(settings.IsEditing, Is.False);
            Assert.That(Object.FindAnyObjectByType<MapLoader>().Sound.Volume,
                Is.EqualTo(0.8f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator Pause_pauses_music_and_resume_restores()
        {
            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return WaitForPlaying();

            var music = Object.FindAnyObjectByType<MapLoader>().Music;
            Assert.That(music, Is.Not.Null);
            Assert.That(music.IsActive, Is.True);

            var flow = GameFlowController.Ensure();
            flow.RequestPause();
            yield return null;
            Assert.That(music.IsPaused, Is.True);

            flow.Resume();
            yield return null;
            Assert.That(music.IsPaused, Is.False);
            Assert.That(music.IsActive, Is.True);
        }

        static IEnumerator WaitForPlaying()
        {
            for (int i = 0; i < 300; i++)
            {
                var flow = GameFlowController.Instance;
                if (flow != null && flow.State == GameFlowState.Playing &&
                    GameObject.Find("Player") != null)
                    yield break;
                yield return null;
            }

            Assert.Fail("Timed out waiting for Playing");
        }
    }
}
