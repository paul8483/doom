using System;
using System.IO;
using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Persistent campaign session host. Survives scene reloads; never holds
    /// references to MapLoader, player, meshes, or audio after unload.
    public sealed class GameSessionHost : MonoBehaviour
    {
        const int WadIdentitySampleBytes = 4096;

        public static GameSessionHost Instance { get; private set; }

        public SessionState Session { get; private set; }

        /// Monotonic SpawnId allocator shared across scene reloads within a session.
        public int NextSpawnId { get; private set; }

        /// Stable WAD identity (length + content sample hash). Computed once per session.
        public string WadIdentity { get; private set; }

        /// Full-world save waiting to be applied after the next map Build.
        public SaveGame PendingRestore { get; private set; }

        /// Idempotent bootstrap. Safe to call from menus, transitions, and tests.
        public static GameSessionHost Ensure()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("GameSessionHost");
            var host = go.AddComponent<GameSessionHost>();
            return host;
        }

        public int AllocateSpawnId() => NextSpawnId++;

        public void SetNextSpawnId(int value) =>
            NextSpawnId = value < 0 ? 0 : value;

        /// Sync host counter from a live registry after capture / restore.
        public void SyncSpawnIdFrom(WorldStateRegistry registry)
        {
            if (registry == null) return;
            if (registry.NextSpawnId > NextSpawnId)
                NextSpawnId = registry.NextSpawnId;
        }

        public void SetPendingRestore(SaveGame save) =>
            PendingRestore = save ?? throw new ArgumentNullException(nameof(save));

        public void ClearPendingRestore() => PendingRestore = null;

        /// Returns true and clears the pending save when it targets <paramref name="mapName"/>.
        public bool TryConsumePendingRestore(string mapName, out SaveGame save)
        {
            save = null;
            if (PendingRestore == null) return false;
            if (!string.Equals(PendingRestore.MapName, mapName, StringComparison.OrdinalIgnoreCase))
                return false;
            save = PendingRestore;
            PendingRestore = null;
            return true;
        }

        /// Computes and stores WAD identity if missing. Safe to call every Build.
        public void EnsureWadIdentity(string wadPath)
        {
            if (!string.IsNullOrEmpty(WadIdentity)) return;
            WadIdentity = ComputeWadIdentity(wadPath);
        }

        public void SetWadIdentityForTests(string identity) => WadIdentity = identity;

        /// Length + FNV-1a over the first/last sample of the WAD file.
        public static string ComputeWadIdentity(string wadPath)
        {
            if (string.IsNullOrEmpty(wadPath) || !File.Exists(wadPath))
                return "missing";

            var info = new FileInfo(wadPath);
            long length = info.Length;
            uint hash = 2166136261u;

            using (var fs = new FileStream(wadPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int headLen = (int)Math.Min(WadIdentitySampleBytes, length);
                var buf = new byte[Math.Max(headLen, 1)];
                int read = fs.Read(buf, 0, headLen);
                hash = Fnv1a(hash, buf, read);

                if (length > WadIdentitySampleBytes)
                {
                    long tailStart = Math.Max(0, length - WadIdentitySampleBytes);
                    fs.Seek(tailStart, SeekOrigin.Begin);
                    int tailLen = (int)(length - tailStart);
                    if (tailLen > buf.Length) buf = new byte[tailLen];
                    read = fs.Read(buf, 0, tailLen);
                    hash = Fnv1a(hash, buf, read);
                }
            }

            return $"len={length};h={hash:x8}";
        }

        static uint Fnv1a(uint hash, byte[] data, int length)
        {
            for (int i = 0; i < length; i++)
            {
                hash ^= data[i];
                hash *= 16777619u;
            }
            return hash;
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
                if (Application.isPlaying) UnityEngine.Object.Destroy(go);
                else UnityEngine.Object.DestroyImmediate(go);
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
                Session = new SessionState();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
