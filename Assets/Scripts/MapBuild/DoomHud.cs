using System;
using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// WAD-driven DOOM status bar in virtual 320×200 space.
    /// Layout constants from linuxdoom-1.10/st_stuff.c.
    public sealed class DoomHud : MonoBehaviour
    {
        const int BarY = 168;
        const int AmmoX = 44, AmmoY = 171;
        const int HealthX = 90, HealthY = 171;
        const int ArmsBgX = 104, ArmsBgY = 168;
        const int ArmsX = 111, ArmsY = 172;
        const int ArmsXSpace = 12, ArmsYSpace = 10;
        const int FacesX = 143, FacesY = 168;
        const int ArmorX = 221, ArmorY = 171;
        const int Key0X = 239, Key0Y = 171;
        const int Key1Y = 181, Key2Y = 191;
        const int Ammo0X = 288, Ammo0Y = 173;
        const int Ammo1Y = 179, Ammo2Y = 191, Ammo3Y = 185;
        const int MaxAmmo0X = 314;
        const float TicSeconds = 1f / 35f;

        PlayerHealth health;
        PlayerWeapons weapons;
        PlayerInventory inventory;
        HudTextureCache textures;
        FaceState face;
        float ticAccum;
        HudModel model;
        bool wired;

        /// Latest projected model (PlayMode tests read this).
        public HudModel Model => model;
        public FaceState Face => face;
        public bool IsReady => wired && textures != null;

        public void Init(
            PlayerHealth health,
            PlayerWeapons weapons,
            PlayerInventory inventory,
            HudTextureCache textures)
        {
            TearDown();
            this.health = health;
            this.weapons = weapons;
            this.inventory = inventory;
            this.textures = textures;
            face = new FaceState();
            face.Reset(health != null ? health.Health : HealthModel.MaxHealth);
            wired = true;

            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Died += OnDied;
            }

            if (inventory != null)
                inventory.PickedUp += OnPickedUp;

            RefreshModel();
        }

        void OnDestroy() => TearDown();

        void TearDown()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }

            if (inventory != null)
                inventory.PickedUp -= OnPickedUp;

            health = null;
            weapons = null;
            inventory = null;
            textures = null;
            wired = false;
        }

        public void OnRespawn()
        {
            if (face == null || health == null) return;
            face.Reset(health.Health);
            RefreshModel();
        }

        void OnDamaged(int hpLost, FaceAttackerSide side)
        {
            if (face == null || health == null) return;
            face.OnDamage(health.Health, hpLost, side);
            RefreshModel();
        }

        void OnDied()
        {
            face?.OnDeath();
            RefreshModel();
        }

        void OnPickedUp(int doomedNum, PickupSoundKind _)
        {
            if (face == null || health == null) return;
            if (IsWeaponPickup(doomedNum))
                face.OnWeaponPickup(health.Health);
            RefreshModel();
        }

        void Update()
        {
            if (!wired || health == null) return;

            ticAccum += Time.deltaTime;
            int steps = 0;
            while (ticAccum >= TicSeconds)
            {
                ticAccum -= TicSeconds;
                steps++;
            }

            if (steps > 0)
                face.Advance(steps, health.Health);

            RefreshModel();
        }

        void RefreshModel()
        {
            if (health == null || weapons == null || inventory == null || face == null)
                return;
            model = HudModel.From(
                health.Model, weapons.Ammo, weapons.Loadout,
                inventory.Keys, inventory.Powers, face);
        }

        void OnGUI()
        {
            if (!wired || textures == null || Event.current.type != EventType.Repaint)
                return;

            var t = VirtualScreenRenderer.ComputeForScreen();
            DrawPatch(t, "STBAR", 0, BarY);
            DrawPatch(t, "STARMS", ArmsBgX, ArmsBgY);

            if (model.ReadyAmmoVisible)
                DrawTallNumber(t, model.ReadyAmmo, AmmoX, AmmoY, 3);

            DrawTallPercent(t, model.Health, HealthX, HealthY);
            DrawTallPercent(t, model.Armor, ArmorX, ArmorY);
            DrawArms(t);
            DrawKeys(t);
            DrawAmmoCounters(t);
            DrawPatch(t, model.FacePatch, FacesX, FacesY);
        }

        void DrawArms(in VirtualScreenRenderer.Transform t)
        {
            // arms[i] → weaponowned[i+1]: pistol, shotgun, chaingun, rocket, plasma, bfg
            DrawArmDigit(t, 0, model.OwnsPistol, 2);
            DrawArmDigit(t, 1, model.OwnsShotgun, 3);
            DrawArmDigit(t, 2, model.OwnsChaingun, 4);
            DrawArmDigit(t, 3, false, 5);
            DrawArmDigit(t, 4, false, 6);
            DrawArmDigit(t, 5, false, 7);
        }

        void DrawArmDigit(in VirtualScreenRenderer.Transform t, int slot, bool owned, int digit)
        {
            int col = slot % 3;
            int row = slot / 3;
            float x = ArmsX + col * ArmsXSpace;
            float y = ArmsY + row * ArmsYSpace;
            string name = (owned ? "STYSNUM" : "STGNUM") + digit;
            DrawPatch(t, name, x, y);
        }

        void DrawKeys(in VirtualScreenRenderer.Transform t)
        {
            DrawKey(t, model.BlueCard, model.BlueSkull, 0, 3, 6, Key0X, Key0Y);
            DrawKey(t, model.YellowCard, model.YellowSkull, 1, 4, 7, Key0X, Key1Y);
            DrawKey(t, model.RedCard, model.RedSkull, 2, 5, 8, Key0X, Key2Y);
        }

        void DrawKey(
            in VirtualScreenRenderer.Transform t,
            bool card, bool skull,
            int cardIdx, int skullIdx, int bothIdx,
            float x, float y)
        {
            int idx = -1;
            if (card && skull) idx = bothIdx;
            else if (skull) idx = skullIdx;
            else if (card) idx = cardIdx;
            if (idx < 0) return;
            DrawPatch(t, "STKEYS" + idx, x, y);
        }

        void DrawAmmoCounters(in VirtualScreenRenderer.Transform t)
        {
            DrawSmallNumber(t, model.Bullets, Ammo0X, Ammo0Y, 3);
            DrawSmallNumber(t, model.Shells, Ammo0X, Ammo1Y, 3);
            DrawSmallNumber(t, model.Rockets, Ammo0X, Ammo3Y, 3);
            DrawSmallNumber(t, model.Cells, Ammo0X, Ammo2Y, 3);

            DrawSmallNumber(t, model.MaxBullets, MaxAmmo0X, Ammo0Y, 3);
            DrawSmallNumber(t, model.MaxShells, MaxAmmo0X, Ammo1Y, 3);
            DrawSmallNumber(t, model.MaxRockets, MaxAmmo0X, Ammo3Y, 3);
            DrawSmallNumber(t, model.MaxCells, MaxAmmo0X, Ammo2Y, 3);
        }

        void DrawTallPercent(in VirtualScreenRenderer.Transform t, int value, float rightX, float y)
        {
            // st_lib.c: percent patch at the anchor X; tall digits grow left from it.
            DrawPatch(t, "STTPRCNT", rightX, y);
            DrawTallNumber(t, value, rightX, y, 3);
        }

        /// Right-aligned tall digits. Returns the left X after drawing (for %).
        float DrawTallNumber(
            in VirtualScreenRenderer.Transform t, int value, float rightX, float y, int digits)
        {
            if (value < 0) value = 0;
            var s = value.ToString();
            if (s.Length > digits) s = s.Substring(s.Length - digits);

            float x = rightX;
            for (int i = s.Length - 1; i >= 0; i--)
            {
                string name = "STTNUM" + s[i];
                if (!textures.TryGet(name, out var e)) break;
                x -= e.Width;
                DrawEntry(t, e, x, y);
            }

            return x;
        }

        void DrawSmallNumber(
            in VirtualScreenRenderer.Transform t, int value, float rightX, float y, int digits)
        {
            if (value < 0) value = 0;
            var s = value.ToString();
            if (s.Length > digits) s = s.Substring(s.Length - digits);

            float x = rightX;
            for (int i = s.Length - 1; i >= 0; i--)
            {
                string name = "STYSNUM" + s[i];
                if (!textures.TryGet(name, out var e)) break;
                x -= e.Width;
                DrawEntry(t, e, x, y);
            }
        }

        void DrawPatch(in VirtualScreenRenderer.Transform t, string name, float vx, float vy)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (!textures.TryGet(name, out var e)) return;
            DrawEntry(t, e, vx, vy);
        }

        static void DrawEntry(
            in VirtualScreenRenderer.Transform t, HudTextureCache.Entry e, float vx, float vy)
        {
            var r = VirtualScreenRenderer.ToScreenSnapped(t, vx, vy, e.Width, e.Height);
            GUI.DrawTexture(r, e.Texture);
        }

        static bool IsWeaponPickup(int doomedNum) =>
            doomedNum == 2001 || doomedNum == 2002 || doomedNum == 2003
            || doomedNum == 2004 || doomedNum == 2005 || doomedNum == 2006;
    }
}
