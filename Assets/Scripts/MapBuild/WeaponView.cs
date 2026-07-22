using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// First-person weapon sprite: OnGUI draw onto a virtual 320x200 screen,
    /// fire frames + muzzle flash driven by the Fired event, DOOM-style bob
    /// while moving.
    ///
    /// Placement formula -- R_DrawPSprite: the patch is drawn in 320x200 space
    /// so that left edge = sx - leftOffset, top edge = sy - topOffset, where at
    /// rest sx=1, sy=WEAPONTOP=32. Weapon patches are authored with offsets
    /// that assume these coordinates. Drawing is clipped to the view window
    /// above the status bar (DOOM viewheight = 200−32) so tall weapons like
    /// the chainsaw never paint over STBAR — vanilla overwrites with ST_Drawer;
    /// we clip because OnGUI order vs DoomHud is not guaranteed.
    /// Verified visually in the editor (Stage 6c final check); note
    /// StairCaptureTests cannot capture this — it renders via Camera.Render(),
    /// which excludes OnGUI/IMGUI output.
    public sealed class WeaponView : MonoBehaviour
    {
        const float WeaponTopPx = 32f;
        const float WeaponBottomPx = 128f;
        const float LowerPixelsPerTic = 6f;
        const float MaxBobPx = 16f;
        const int ViewHeightPx =
            VirtualScreenRenderer.Height - VirtualScreenRenderer.StatusBarHeight;

        PlayerWeapons weapons;
        SpriteCache cache;
        float worldScale;
        CharacterController cc;

        WeaponDef anim;      // active fire sequence (null = idle)
        int animIdx;
        float animLeft;
        float flashLeft;
        int flashIdx;
        float flashDelayLeft;
        bool flashRandomHold;
        PlayerHealth health;
        PlayerDeathHandler deathHandler;
        bool lowering;
        float lowerY;

        public void Init(PlayerWeapons weapons, SpriteCache cache, float worldScale,
                         CharacterController controller)
        {
            this.weapons = weapons;
            this.cache = cache;
            this.worldScale = worldScale;
            cc = controller;
            weapons.Fired += OnFired;
            health = weapons.GetComponent<PlayerHealth>();
            deathHandler = weapons.GetComponent<PlayerDeathHandler>();
            if (health != null) health.Died += OnPlayerDied;
            if (deathHandler != null) deathHandler.Respawned += OnRespawned;
        }

        void OnDestroy()
        {
            if (weapons != null) weapons.Fired -= OnFired;
            if (health != null) health.Died -= OnPlayerDied;
            if (deathHandler != null) deathHandler.Respawned -= OnRespawned;
        }

        void OnPlayerDied()
        {
            anim = null;
            flashLeft = 0f;
            flashDelayLeft = 0f;
            lowering = true;
            lowerY = WeaponTopPx;
        }

        void OnRespawned()
        {
            lowering = false;
            lowerY = WeaponTopPx;
        }

        void OnFired(WeaponDef def)
        {
            anim = def;
            animIdx = 0;
            animLeft = def.FireTics[0] / 35f;
            flashRandomHold = def.RandomFlash;
            flashDelayLeft = def.FlashDelayTic / 35f;
            if (def.FlashSprite != null && def.FlashTics.Length > 0 && def.FlashDelayTic <= 0)
                BeginFlash(def);
            else
                flashLeft = 0f;
        }

        void BeginFlash(WeaponDef def)
        {
            if (def.RandomFlash && def.FlashFrames.Length > 0)
                flashIdx = weapons.Rng.Next() & 1;
            else
                flashIdx = 0;
            if (flashIdx >= def.FlashFrames.Length) flashIdx = 0;
            flashLeft = def.FlashTics[Mathf.Min(flashIdx, def.FlashTics.Length - 1)] / 35f;
            flashDelayLeft = 0f;
        }

        void Update()
        {
            if (lowering)
                lowerY = Mathf.Min(WeaponBottomPx,
                    lowerY + LowerPixelsPerTic * 35f * Time.deltaTime);

            if (anim != null)
            {
                animLeft -= Time.deltaTime;
                if (animLeft <= 0f)
                {
                    animIdx++;
                    if (animIdx >= anim.FireFrames.Length) anim = null;
                    else animLeft = anim.FireTics[animIdx] / 35f;
                }
            }
            if (flashDelayLeft > 0f && anim != null)
            {
                flashDelayLeft -= Time.deltaTime;
                if (flashDelayLeft <= 0f)
                    BeginFlash(anim);
            }
            if (flashLeft > 0f)
            {
                flashLeft -= Time.deltaTime;
                if (flashLeft <= 0f && anim != null && !flashRandomHold
                    && flashIdx + 1 < anim.FlashTics.Length)
                {
                    flashIdx++;
                    flashLeft = anim.FlashTics[flashIdx] / 35f;
                }
            }
        }

        void OnGUI()
        {
            if (weapons == null || Event.current.type != EventType.Repaint) return;
            if (!GameFlowController.ShouldDrawWeaponView() && !lowering) return;
            if (lowering && lowerY >= WeaponBottomPx) return;
            var def = WeaponTable.Get(weapons.Loadout.Current);

            // Bob (A_WeaponReady): angle advances 128 units/tic out of 8192 per circle.
            float bob = BobAmplitudePx();
            float phase = Time.time * 35f * 128f / 8192f * 2f * Mathf.PI;
            float sx = 1f + bob * Mathf.Cos(phase);
            // DOOM folds the angle into a half-circle: sy follows |sin| at the same rate.
            float sy = WeaponTopPx + bob * Mathf.Abs(Mathf.Sin(phase));
            if (anim != null) { sx = 1f; sy = WeaponTopPx; } // no bob while firing
            if (lowering) { sx = 1f; sy = lowerY; }

            var t = VirtualScreenRenderer.ComputeForScreen();
            // Clip to view window above STBAR (BeginClip makes draw coords relative).
            var clip = VirtualScreenRenderer.ToScreen(
                t, 0, 0, VirtualScreenRenderer.Width, ViewHeightPx);
            GUI.BeginClip(clip);

            int frame = anim != null ? anim.FireFrames[animIdx] : def.IdleFrame;
            DrawPatch(def.Sprite, frame, sx, sy, t, clip);
            if (anim != null && flashLeft > 0f && anim.FlashSprite != null)
                DrawPatch(anim.FlashSprite, anim.FlashFrames[flashIdx], sx, sy, t, clip);

            GUI.EndClip();
        }

        float BobAmplitudePx()
        {
            if (cc == null) return 0f;
            var v = cc.velocity; v.y = 0f;
            float unitsPerTic = v.magnitude / worldScale / 35f;   // DOOM momx/momy
            return Mathf.Min(MaxBobPx, unitsPerTic * unitsPerTic * 0.25f);
        }

        public bool IsLoweringForTest => lowering;
        public float LowerYForTest => lowerY;
        public void AdvanceLowerForTest(float seconds)
        {
            if (lowering)
                lowerY = Mathf.Min(WeaponBottomPx,
                    lowerY + LowerPixelsPerTic * 35f * seconds);
        }

        void DrawPatch(string sprite, int frame, float sx, float sy,
                       in VirtualScreenRenderer.Transform t, Rect clip)
        {
            // SpriteCache serves Enhanced4X when SpritesUpscale4X is active;
            // placement always uses PatchHeader dims/offsets (not texture size).
            var sm = cache.Get(sprite, frame, 0);
            if (!sm.IsValid) return;
            var tex = sm.Material != null ? sm.Material.mainTexture : null;
            if (tex == null) return;

            var r = PlacementRect(t, sx, sy, sm);
            r.x -= clip.x;
            r.y -= clip.y;
            GUI.DrawTexture(r, tex);
        }

        /// R_DrawPSprite screen rect from header dims — identical for native and 4×.
        public static Rect PlacementRect(
            in VirtualScreenRenderer.Transform t, float sx, float sy, in SpriteMaterial sm) =>
            VirtualScreenRenderer.WeaponPatch(
                t, sx, sy, sm.LeftOffset, sm.TopOffset, sm.Width, sm.Height);
    }
}

