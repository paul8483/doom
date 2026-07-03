using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// First-person weapon sprite: OnGUI draw onto a virtual 320x200 screen,
    /// fire frames + muzzle flash driven by the Fired event, DOOM-style bob
    /// while moving.
    ///
    /// Placement formula -- R_DrawPSprite: the patch is drawn in 320x200 space
    /// so that left edge = sx - leftOffset (measured from the 160 center),
    /// top edge = sy - topOffset, where at rest sx=1, sy=WEAPONTOP=32. Weapon
    /// patches are authored with offsets that assume these coordinates.
    /// NOTE: not visually verified by the capture harness — StairCaptureTests
    /// renders via Camera.Render(), which excludes OnGUI/IMGUI output, so the
    /// viewmodel/HUD never appear in its PNGs. The formula follows R_DrawPSprite.
    public sealed class WeaponView : MonoBehaviour
    {
        const float WeaponTopPx = 32f;
        const float MaxBobPx = 16f;

        PlayerWeapons weapons;
        SpriteCache cache;
        float worldScale;
        CharacterController cc;

        WeaponDef anim;      // active fire sequence (null = idle)
        int animIdx;
        float animLeft;
        float flashLeft;
        int flashIdx;

        public void Init(PlayerWeapons weapons, SpriteCache cache, float worldScale,
                         CharacterController controller)
        {
            this.weapons = weapons;
            this.cache = cache;
            this.worldScale = worldScale;
            cc = controller;
            weapons.Fired += OnFired;
        }

        void OnDestroy() { if (weapons != null) weapons.Fired -= OnFired; }

        void OnFired(WeaponDef def)
        {
            anim = def;
            animIdx = 0;
            animLeft = def.FireTics[0] / 35f;
            if (def.FlashSprite != null && def.FlashTics.Length > 0)
            {
                flashIdx = 0;
                flashLeft = def.FlashTics[0] / 35f;
            }
            else
            {
                flashLeft = 0f;
            }
        }

        void Update()
        {
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
            if (flashLeft > 0f)
            {
                flashLeft -= Time.deltaTime;
                if (flashLeft <= 0f && anim != null && flashIdx + 1 < anim.FlashTics.Length)
                {
                    flashIdx++;
                    flashLeft = anim.FlashTics[flashIdx] / 35f;
                }
            }
        }

        void OnGUI()
        {
            if (weapons == null || Event.current.type != EventType.Repaint) return;
            var def = WeaponTable.Get(weapons.Loadout.Current);

            // Bob (A_WeaponReady): angle advances 128 units/tic out of 8192 per circle.
            float bob = BobAmplitudePx();
            float phase = Time.time * 35f * 128f / 8192f * 2f * Mathf.PI;
            float sx = 1f + bob * Mathf.Cos(phase);
            // DOOM folds the angle into a half-circle: sy follows |sin| at the same rate.
            float sy = WeaponTopPx + bob * Mathf.Abs(Mathf.Sin(phase));
            if (anim != null) { sx = 1f; sy = WeaponTopPx; } // no bob while firing

            int frame = anim != null ? anim.FireFrames[animIdx] : def.IdleFrame;
            DrawPatch(def.Sprite, frame, sx, sy);
            if (anim != null && flashLeft > 0f && anim.FlashSprite != null)
                DrawPatch(anim.FlashSprite, anim.FlashFrames[flashIdx], sx, sy);
        }

        float BobAmplitudePx()
        {
            if (cc == null) return 0f;
            var v = cc.velocity; v.y = 0f;
            float unitsPerTic = v.magnitude / worldScale / 35f;   // DOOM momx/momy
            return Mathf.Min(MaxBobPx, unitsPerTic * unitsPerTic * 0.25f);
        }

        void DrawPatch(string sprite, int frame, float sx, float sy)
        {
            var sm = cache.Get(sprite, frame, 0);
            if (!sm.IsValid) return;
            var tex = sm.Material != null ? sm.Material.mainTexture : null;
            if (tex == null) return;

            float scale = Screen.height / 200f;
            float left = 160f + sx - sm.LeftOffset;               // virtual px
            float top = sy - sm.TopOffset;
            var r = new Rect(
                Screen.width * 0.5f + (left - 160f) * scale,
                top * scale,
                sm.Width * scale,
                sm.Height * scale);
            GUI.DrawTexture(r, tex);
        }
    }
}
