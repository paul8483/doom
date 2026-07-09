using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doom.Wad;
using Doom.Map;
using Doom.MapBuild;

namespace Doom.Stage3.PlayTests
{
    /// <summary>
    /// Headless render-capture harness (Stage 6b diagnostic tool).
    ///
    /// Loads E1M1 in the preview scene, settles the player on the floor, then:
    ///  1) saves the player's spawn view to Logs/stair-spawn.png (in case the
    ///     computed framing is off — the lead's screenshots were near spawn), and
    ///  2) re-poses the player camera to frame a stair RISER (the vertical face on
    ///     the LOWER sector's side of a small upward floor step) and saves
    ///     Logs/stair-capture.png.
    ///
    /// This does NOT touch any rendering/production code — it only reads MapData to
    /// pick a riser, disables the PlayerController, moves Camera.main, and calls
    /// Camera.Render() into a RenderTexture. Re-running regenerates fresh PNGs so we
    /// can diff before/after when iterating on a rendering fix.
    /// </summary>
    public class StairCaptureTests
    {
        const float WorldScale = 1f / 32f;
        static string LogsDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
        static string SpawnPng => Path.Combine(LogsDir, "stair-spawn.png");
        static string CapturePng => Path.Combine(LogsDir, "stair-capture.png");

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator Capture_E1M1_stair_riser_to_png()
        {
            // Building E1M1's ~182 MeshColliders makes PhysX emit non-fatal
            // "cleaning the mesh failed" logs for a few degenerate sectors —
            // unrelated to capturing, so don't let them fail this tool.
            LogAssert.ignoreFailingMessages = true;

            // Deterministic 1/60s step so the CharacterController actually presses
            // into the floor each frame in headless batchmode (see PlayerLandsOnFloorTests).
            Time.captureDeltaTime = 1f / 60f;

            SceneManager.LoadScene("Stage2_MapPreview", LoadSceneMode.Single);
            yield return null; yield return null;
            // Let MapLoader.Build finish (geometry + Player + camera).
            for (int i = 0; i < 90; i++) yield return null;
            yield return new WaitForFixedUpdate();

            var player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null, "Player GameObject must exist after MapLoader.Build");
            var cc = player.GetComponent<CharacterController>();

            // Settle the player on the floor (spawn is bounds.max.y + 5 above it).
            for (int i = 0; i < 300; i++)
            {
                if (cc != null && cc.isGrounded) break;
                yield return null;
            }

            var cam = Camera.main;
            Assert.That(cam, Is.Not.Null, "Camera.main (player camera) must exist");

            // ── Capture #1: the player's spawn view (before we move anything) ──────
            yield return null; // let the renderer settle one frame at the spawn pose
            CaptureTo(cam, SpawnPng, 800, 600);
            Debug.Log($"[StairCapture] wrote spawn view: {SpawnPng}");

            // ── Disable the PlayerController so it can't fight our camera pose ─────
            var pc = Object.FindAnyObjectByType<PlayerController>();
            if (pc != null) pc.enabled = false;

            // ── Find a STAIRCASE in E1M1 and compute a far-back eye-level pose ─────
            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var map = MapData.Load(wad, "E1M1");

            // A staircase is a chain of ≥3 sectors with monotonically increasing floor
            // heights, each pair connected by a two-sided linedef forming a small
            // upward step (~8..24 DOOM units). FindStaircase returns the chain plus the
            // riser linedefs between consecutive steps.
            var stair = FindStaircase(map, minStep: 8, maxStep: 28, minSteps: 3);
            Assert.That(stair, Is.Not.Null,
                "E1M1 should contain a run of >=3 sectors with increasing floors (a staircase)");

            // The bottom step's riser (first in the chain) anchors our aim. We look UP
            // the staircase from the lower side so the risers are seen FACE-ON.
            int bottomLine = stair.RiserLines[0];
            var ld = map.LineDefs[bottomLine];

            int frontSec = map.SideDefs[ld.FrontSideIdx].SectorIdx;
            int backSec  = map.SideDefs[ld.BackSideIdx].SectorIdx;
            float lowerFloor = Mathf.Min(map.Sectors[frontSec].FloorHeight,
                                         map.Sectors[backSec].FloorHeight);
            float topFloor   = map.Sectors[stair.Sectors[stair.Sectors.Count - 1]].FloorHeight;

            // Aim point: centre of the WHOLE staircase run (midpoint of the bottom riser
            // line, raised to about the middle of the vertical extent of all the steps).
            var v1 = map.Vertexes[ld.V1];
            var v2 = map.Vertexes[ld.V2];
            float bx = (v1.X + v2.X) * 0.5f * WorldScale;
            float bz = (v1.Y + v2.Y) * 0.5f * WorldScale;

            // The look target sits up the run so several step tops + risers are in
            // frame. Put it ~halfway up the total rise above the bottom floor, and
            // pushed INTO the staircase along the climb direction so the run fills the
            // centre of the frame rather than its lower edge.
            float aimY = (lowerFloor + (topFloor - lowerFloor) * 0.42f) * WorldScale;

            // Climb direction (unit, Unity XZ): from the bottom riser midpoint toward the
            // top riser midpoint. The camera backs off OPPOSITE this (down the stairs).
            int topLine = stair.RiserLines[stair.RiserLines.Count - 1];
            var tld = map.LineDefs[topLine];
            var tv1 = map.Vertexes[tld.V1];
            var tv2 = map.Vertexes[tld.V2];
            float tx = (tv1.X + tv2.X) * 0.5f * WorldScale;
            float tz = (tv1.Y + tv2.Y) * 0.5f * WorldScale;

            Vector3 climbDir = new Vector3(tx - bx, 0f, tz - bz);
            if (climbDir.sqrMagnitude < 1e-4f)
            {
                // Degenerate (all risers near-collinear midpoints): use the bottom line's
                // outward normal toward the lower sector instead.
                float dx = (v2.X - v1.X) * WorldScale;
                float dz = (v2.Y - v1.Y) * WorldScale;
                climbDir = new Vector3(dz, 0f, -dx);
            }
            climbDir.Normalize();

            // Aim target nudged up the run from the bottom riser midpoint so the run is
            // centred in the frame (the camera is well back, so a couple metres in).
            var aimPoint = new Vector3(bx, aimY, bz) + climbDir * 1.5f;

            // Eye level: ~1.7 m above the bottom (lower) floor.
            float eye  = 54f * WorldScale;          // ~1.69 m
            float camY = lowerFloor * WorldScale + eye;

            // Back the camera OFF the bottom step, opposite the climb direction, far
            // enough to see the risers face-on. Pick the farthest stance (up to ~9 m)
            // that still stands on solid lower floor (raycast down), so we aren't poking
            // the camera through a wall or into a void sector.
            Vector3 backDir = -climbDir;
            Vector3 bottomMidXZ = new Vector3(bx, camY, bz);
            Vector3 bestPos = bottomMidXZ + backDir * 9.0f;
            bool foundFloor = false;
            for (float d = 9.0f; d >= 2.0f; d -= 0.25f)
            {
                Vector3 p = bottomMidXZ + backDir * d;
                if (Physics.Raycast(new Vector3(p.x, camY + 1.0f, p.z), Vector3.down,
                                    out var hit, 30f))
                {
                    float floorErr = Mathf.Abs(hit.point.y - lowerFloor * WorldScale);
                    if (floorErr < 0.8f) { bestPos = p; foundFloor = true; break; } // farthest valid
                }
            }

            cam.fieldOfView = 60f;
            cam.transform.position = bestPos;
            // Look toward the staircase. LookAt yields the natural slight-downward pitch
            // because the aim point sits low on the run while the eye is ~1.6 m up.
            cam.transform.LookAt(aimPoint);

            float pitch = cam.transform.eulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            Vector3 camPos = bestPos;

            Debug.Log($"[StairCapture] staircase sectors=[{string.Join(",", stair.Sectors)}] " +
                      $"riserLines=[{string.Join(",", stair.RiserLines)}] " +
                      $"floors=[{string.Join(",", stair.FloorHeights)}] " +
                      $"bottomLine={bottomLine} lowerFloor={lowerFloor} topFloor={topFloor} " +
                      $"foundFloor={foundFloor} camPos={camPos} aim={aimPoint} pitchDeg={pitch:F1} " +
                      $"backDist={(bestPos - bottomMidXZ).magnitude:F2}");

            yield return null; // let the new pose register before rendering
            CaptureTo(cam, CapturePng, 800, 600);
            Debug.Log($"[StairCapture] wrote stair capture: {CapturePng}");

            // ── Assertions (lenient — this is a capture tool, not a content test) ──
            AssertNonTrivialPng(SpawnPng);
            AssertNonTrivialPng(CapturePng);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// A discovered staircase: an ordered run of sector indices with strictly
        /// increasing floor heights, plus the two-sided riser linedef between each
        /// consecutive pair, and the floor heights for logging.
        class Staircase
        {
            public System.Collections.Generic.List<int> Sectors = new();
            public System.Collections.Generic.List<int> RiserLines = new();
            public System.Collections.Generic.List<int> FloorHeights = new();
        }

        /// Scan E1M1 for a staircase: a chain of >=minSteps sectors whose floor heights
        /// increase monotonically in small steps (minStep..maxStep DOOM units), each
        /// pair joined by a two-sided linedef. We build an adjacency of "upward step"
        /// edges (lower sector -> upper sector via a riser line) and greedily extend the
        /// longest chain. To favour a real visible staircase, we prefer chains whose
        /// bottom sector has the LOWEST floor (most likely standing on open floor).
        static Staircase FindStaircase(MapData map, int minStep, int maxStep, int minSteps)
        {
            int sectorCount = map.Sectors.Length;

            // For each sector, list of (neighbourSector, riserLine) where neighbour is
            // exactly one small step UP from this sector.
            var upEdges = new System.Collections.Generic.List<(int up, int line)>[sectorCount];
            for (int s = 0; s < sectorCount; s++)
                upEdges[s] = new System.Collections.Generic.List<(int, int)>();

            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                var ld = map.LineDefs[i];
                if (!ld.IsTwoSided) continue;
                if (ld.FrontSideIdx < 0 || ld.BackSideIdx < 0) continue;
                int fs = map.SideDefs[ld.FrontSideIdx].SectorIdx;
                int bs = map.SideDefs[ld.BackSideIdx].SectorIdx;
                if (fs < 0 || bs < 0 || fs >= sectorCount || bs >= sectorCount) continue;

                int ff = map.Sectors[fs].FloorHeight;
                int bf = map.Sectors[bs].FloorHeight;
                int diff = Mathf.Abs(ff - bf);
                if (diff < minStep || diff > maxStep) continue;

                int lower = ff <= bf ? fs : bs;
                int upper = ff <= bf ? bs : fs;
                upEdges[lower].Add((upper, i));
            }

            // DFS the longest increasing chain from each sector. Floors strictly
            // increase along an "up" edge by construction, so no revisits are needed.
            Staircase best = null;

            Staircase Extend(int sector, System.Collections.Generic.HashSet<int> visited)
            {
                Staircase localBest = new Staircase();
                localBest.Sectors.Add(sector);
                localBest.FloorHeights.Add(map.Sectors[sector].FloorHeight);

                Staircase deepest = null;
                int deepestEdgeLine = -1;
                foreach (var (up, line) in upEdges[sector])
                {
                    if (visited.Contains(up)) continue;
                    visited.Add(up);
                    var sub = Extend(up, visited);
                    visited.Remove(up);
                    if (deepest == null || sub.Sectors.Count > deepest.Sectors.Count)
                    {
                        deepest = sub;
                        deepestEdgeLine = line;
                    }
                }

                if (deepest != null)
                {
                    localBest.RiserLines.Add(deepestEdgeLine);
                    localBest.RiserLines.AddRange(deepest.RiserLines);
                    localBest.Sectors.AddRange(deepest.Sectors);
                    localBest.FloorHeights.AddRange(deepest.FloorHeights);
                }
                return localBest;
            }

            for (int s = 0; s < sectorCount; s++)
            {
                if (upEdges[s].Count == 0) continue;
                var visited = new System.Collections.Generic.HashSet<int> { s };
                var chain = Extend(s, visited);
                if (chain.Sectors.Count < minSteps) continue;

                // Score: prefer the LONGEST chain; tie-break toward the LOWEST bottom
                // floor (so the camera stands on open low ground in front of the run).
                if (best == null
                    || chain.Sectors.Count > best.Sectors.Count
                    || (chain.Sectors.Count == best.Sectors.Count
                        && chain.FloorHeights[0] < best.FloorHeights[0]))
                {
                    best = chain;
                }
            }

            return best;
        }

        /// Render `cam` into a fresh RenderTexture and save the PNG to `pngPath`.
        static void CaptureTo(Camera cam, string pngPath, int w, int h)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(pngPath));

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();

                File.WriteAllBytes(pngPath, tex.EncodeToPNG());
                Object.Destroy(tex);
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                rt.Release();
                Object.Destroy(rt);
            }
        }

        /// Assert the PNG exists, is more than a few KB, and is not a single flat
        /// color (so we know the camera actually rendered scene geometry).
        static void AssertNonTrivialPng(string pngPath)
        {
            Assert.That(File.Exists(pngPath), Is.True, $"PNG should exist at {pngPath}");
            var bytes = File.ReadAllBytes(pngPath);
            Assert.That(bytes.Length, Is.GreaterThan(3000),
                $"PNG {pngPath} should be non-trivial in size (got {bytes.Length} bytes)");

            // Decode and check the frame isn't uniformly one color.
            var tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            var px = tex.GetPixels32();
            Object.Destroy(tex);
            bool varied = false;
            var first = px[0];
            for (int i = 1; i < px.Length; i++)
            {
                if (px[i].r != first.r || px[i].g != first.g || px[i].b != first.b)
                {
                    varied = true; break;
                }
            }
            Assert.That(varied, Is.True,
                $"PNG {pngPath} is a single flat color — camera likely rendered a blank frame");
        }
    }
}
