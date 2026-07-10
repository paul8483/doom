using UnityEngine;
using Doom.Game;

namespace Doom.MapBuild
{
    /// Minimal debug readout of health/armor/keys/powers. Real HUD lands in Stage 7.
    public sealed class PlayerHud : MonoBehaviour
    {
        PlayerHealth health;
        PlayerWeapons weapons;
        PlayerInventory inventory;

        public void Init(PlayerHealth health) => this.health = health;
        public void SetWeapons(PlayerWeapons w) => weapons = w;
        public void SetInventory(PlayerInventory inv) => inventory = inv;

        void OnGUI()
        {
            if (health == null) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 20 };
            style.normal.textColor = Color.white;
            float y = 8f;
            GUI.Label(new Rect(12f, y, 300f, 28f), $"HEALTH {health.Health}", style); y += 24f;
            GUI.Label(new Rect(12f, y, 300f, 28f), $"ARMOR {health.Armor}", style); y += 24f;
            if (weapons != null)
            {
                var def = WeaponTable.Get(weapons.Loadout.Current);
                string ammo = def.Ammo == AmmoType.None
                    ? "-" : weapons.Ammo.Get(def.Ammo).ToString();
                GUI.Label(new Rect(12f, y, 300f, 28f), $"AMMO {ammo}", style); y += 24f;
            }
            if (inventory != null)
            {
                GUI.Label(new Rect(12f, y, 400f, 28f), $"KEYS {FormatKeys(inventory.Keys)}", style);
                y += 24f;
                if (inventory.Powers.Berserk)
                {
                    GUI.Label(new Rect(12f, y, 300f, 28f), "BERSERK", style);
                    y += 24f;
                }
                if (inventory.Powers.IronFeetTics > 0)
                {
                    GUI.Label(new Rect(12f, y, 300f, 28f),
                        $"SUIT {inventory.Powers.IronFeetTics}", style);
                }
            }
        }

        static string FormatKeys(KeyInventory keys)
        {
            var parts = new System.Collections.Generic.List<string>(6);
            if (keys.Has(PlayerKey.BlueCard)) parts.Add("BC");
            if (keys.Has(PlayerKey.BlueSkull)) parts.Add("BS");
            if (keys.Has(PlayerKey.YellowCard)) parts.Add("YC");
            if (keys.Has(PlayerKey.YellowSkull)) parts.Add("YS");
            if (keys.Has(PlayerKey.RedCard)) parts.Add("RC");
            if (keys.Has(PlayerKey.RedSkull)) parts.Add("RS");
            return parts.Count == 0 ? "-" : string.Join(" ", parts);
        }
    }
}
