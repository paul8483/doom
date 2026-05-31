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
        const string SpawnPng   = "D:/Development/doom/Logs/stair-spawn.png";
        const string CapturePng = "D:/Development/doom/Logs/stair-capture.png";

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

            // ── Pick a riser linedef from E1M1 and compute a framing pose ─────────
            string path = Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");
            using var wad = WadFile.Open(path);
            var map = MapData.Load(wad, "E1M1");

            // A "riser" is a two-sided line whose two sectors have different floor
            // heights by a small upward step (8..32 DOOM units). The riser face is on
            // the LOWER sector's side; we aim the camera from there.
            //
            // Prefer line 7 (ASHWALL, step 8): it sits in the open starting room, which
            // renders cleanly. Line 13 (MC17, step 24) was tried first but its lower
            // sector (50) is a degenerate sector whose floor mesh does not render — the
            // capture showed the riser face with a camera-background void where the
            // floor should be. The starting-room ASHWALL riser frames reliably.
            int riserLine = PickRiserLine(map, preferred: 7, minStep: 8, maxStep: 32);
            Assert.That(riserLine, Is.GreaterThanOrEqualTo(0),
                "E1M1 should contain a two-sided upward floor step (riser)");

            var ld = map.LineDefs[riserLine];
            int frontSec = map.SideDefs[ld.FrontSideIdx].SectorIdx;
            int backSec  = map.SideDefs[ld.BackSideIdx].SectorIdx;
            float frontFloor = map.Sectors[frontSec].FloorHeight;
            float backFloor  = map.Sectors[backSec].FloorHeight;

            // Lower sector = the one with the smaller floor height. The riser is
            // visible from inside the lower sector.
            int lowerSec  = frontFloor <= backFloor ? frontSec : backSec;
            float lowerFloor  = Mathf.Min(frontFloor, backFloor);
            float upperFloor  = Mathf.Max(frontFloor, backFloor);

            // Vertices of the line (DOOM space).
            var v1 = map.Vertexes[ld.V1];
            var v2 = map.Vertexes[ld.V2];

            // Midpoint of the line in Unity world XZ.
            float mx = (v1.X + v2.X) * 0.5f * WorldScale;
            float mz = (v1.Y + v2.Y) * 0.5f * WorldScale;
            // Riser mid height = halfway up the step, in Unity Y.
            float midY = (lowerFloor + upperFloor) * 0.5f * WorldScale;
            var riserMid = new Vector3(mx, midY, mz);

            // 2D outward normal of the line (in Unity XZ). Line direction d = (dx,dz);
            // a perpendicular is (dz, -dx). Choose the sign that points toward the
            // LOWER sector (so the camera sits in front of the visible riser face).
            float dx = (v2.X - v1.X) * WorldScale;
            float dz = (v2.Y - v1.Y) * WorldScale;
            var nA = new Vector3(dz, 0f, -dx).normalized;

            // Decide which normal direction points into the lower sector by sampling a
            // probe point just off the midpoint and finding which sector it lands in.
            // Robust-enough heuristic: the lower-sector side is the one whose probe is
            // NOT inside the upper step. We test by nudging along +/-nA a small amount
            // and checking the floor height under that point via a downward raycast.
            Vector3 normalToLower = ChooseLowerSideNormal(
                new Vector3(mx, 0f, mz), nA, lowerFloor * WorldScale, upperFloor * WorldScale);

            // Camera pose: eye-level, head-on from the lower side. The riser face here
            // is small (8 DOOM units ≈ 0.25 m) inside a ~3.75 m-tall room, so we back
            // off and look horizontally so the lower floor + step + upper floor + wall
            // read in context.
            //
            // SELF-CORRECTION: the lower sector's floor must actually be rendered under
            // the camera, else the frame fills with camera-background void (some E1M1
            // sectors are degenerate and don't render — line 13's lower sector did this).
            // We pick the back-off distance that has the MOST solid floor under the
            // camera (raycast down), trying both normal signs, and shorten the distance
            // if a closer stance keeps the camera over real floor.
            float eye = 48f * WorldScale;                   // eye ≈ 1.5 m above lower floor
            float camY = lowerFloor * WorldScale + eye;

            // Prefer the FARTHEST stance (up to 3.5 m) that still has solid lower floor
            // under the camera, so the lower floor → riser → upper floor all sit in the
            // frame rather than a steep close-up.
            Vector3 bestPos = new Vector3(mx, camY, mz) + normalToLower * 3.5f;
            bool foundFloor = false;
            foreach (Vector3 nrm in new[] { normalToLower, -normalToLower })
            {
                Vector3 sideBest = Vector3.zero; bool sideOk = false;
                for (float d = 3.5f; d >= 1.0f; d -= 0.25f)
                {
                    Vector3 p = new Vector3(mx, camY, mz) + nrm * d;
                    if (Physics.Raycast(new Vector3(p.x, camY + 1f, p.z), Vector3.down,
                                        out var hit, 30f))
                    {
                        float floorErr = Mathf.Abs(hit.point.y - lowerFloor * WorldScale);
                        if (floorErr < 0.6f) { sideBest = p; sideOk = true; break; } // farthest valid
                    }
                }
                if (sideOk) { bestPos = sideBest; normalToLower = nrm; foundFloor = true; break; }
            }

            cam.transform.position = bestPos;
            // Aim at the riser midpoint so the step face is centred, with the lower floor
            // below and the upper floor/wall above.
            cam.transform.LookAt(riserMid);
            Debug.Log($"[StairCapture] poseFoundFloor={foundFloor} bestPos={bestPos}");
            Vector3 camPos = bestPos;

            Debug.Log($"[StairCapture] riserLine={riserLine} frontSec={frontSec}(f{frontFloor},c{map.Sectors[frontSec].CeilingHeight}) " +
                      $"backSec={backSec}(f{backFloor},c{map.Sectors[backSec].CeilingHeight}) lowerSec={lowerSec} step={(upperFloor - lowerFloor)} " +
                      $"camPos={camPos} riserMid={riserMid} lowerTex={map.SideDefs[LowerSideOf(ld, map, lowerSec)].LowerTexture}");

            yield return null; // let the new pose register before rendering
            CaptureTo(cam, CapturePng, 800, 600);
            Debug.Log($"[StairCapture] wrote stair capture: {CapturePng}");

            // ── Assertions (lenient — this is a capture tool, not a content test) ──
            AssertNonTrivialPng(SpawnPng);
            AssertNonTrivialPng(CapturePng);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// Pick a two-sided linedef forming a small upward floor step. Returns the
        /// `preferred` line if it qualifies, else the qualifying line with the
        /// largest step (frames best), else -1.
        static int PickRiserLine(MapData map, int preferred, int minStep, int maxStep)
        {
            bool Qualifies(int i, out int step)
            {
                step = 0;
                if (i < 0 || i >= map.LineDefs.Length) return false;
                var ld = map.LineDefs[i];
                if (!ld.IsTwoSided) return false;
                if (ld.FrontSideIdx < 0 || ld.BackSideIdx < 0) return false;
                int fs = map.SideDefs[ld.FrontSideIdx].SectorIdx;
                int bs = map.SideDefs[ld.BackSideIdx].SectorIdx;
                if (fs < 0 || bs < 0) return false;
                int diff = Mathf.Abs(map.Sectors[fs].FloorHeight - map.Sectors[bs].FloorHeight);
                step = diff;
                return diff >= minStep && diff <= maxStep;
            }

            if (Qualifies(preferred, out _)) return preferred;

            int best = -1, bestStep = -1;
            for (int i = 0; i < map.LineDefs.Length; i++)
            {
                if (Qualifies(i, out int step) && step > bestStep)
                {
                    best = i; bestStep = step;
                }
            }
            return best;
        }

        /// Index of the sidedef belonging to `lowerSec` for this linedef (the side
        /// whose LowerTexture is the riser face).
        static int LowerSideOf(LineDef ld, MapData map, int lowerSec)
        {
            if (ld.FrontSideIdx >= 0 && map.SideDefs[ld.FrontSideIdx].SectorIdx == lowerSec)
                return ld.FrontSideIdx;
            return ld.BackSideIdx;
        }

        /// Choose the normal direction (±n) that points into the LOWER sector. We
        /// raycast straight down from a probe just off the line on each side; the
        /// side whose floor is at the LOWER height is the lower sector's side.
        static Vector3 ChooseLowerSideNormal(Vector3 lineMidXZ, Vector3 n,
                                             float lowerY, float upperY)
        {
            const float probe = 0.6f;     // meters off the line
            const float castFromY = 50f;  // high above the map
            const float castDist  = 200f;

            float FloorYAt(Vector3 p)
            {
                var origin = new Vector3(p.x, castFromY, p.z);
                if (Physics.Raycast(origin, Vector3.down, out var hit, castDist))
                    return hit.point.y;
                return float.NaN;
            }

            Vector3 plus  = lineMidXZ + n * probe;
            Vector3 minus = lineMidXZ - n * probe;
            float yPlus  = FloorYAt(plus);
            float yMinus = FloorYAt(minus);

            // Pick whichever side's floor is closer to the LOWER height.
            float dPlus  = float.IsNaN(yPlus)  ? float.MaxValue : Mathf.Abs(yPlus  - lowerY);
            float dMinus = float.IsNaN(yMinus) ? float.MaxValue : Mathf.Abs(yMinus - lowerY);

            // If both raycasts failed, fall back to +n (still a valid framing attempt).
            if (dPlus == float.MaxValue && dMinus == float.MaxValue) return n;
            return dPlus <= dMinus ? n : -n;
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
