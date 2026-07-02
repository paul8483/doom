using UnityEngine;

namespace Doom.MapBuild
{
    /// Minimal debug readout of health/armor in the top-left. This is a temporary
    /// placeholder — a real HUD (face, ammo, weapon) lands in Stage 7.
    public sealed class PlayerHud : MonoBehaviour
    {
        PlayerHealth health;
        PlayerWeapons weapons;

        public void Init(PlayerHealth health) => this.health = health;
        public void SetWeapons(PlayerWeapons w) => weapons = w;

        void OnGUI()
        {
            if (health == null) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 20 };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(12f, 8f, 300f, 28f), $"HEALTH {health.Health}", style);
            GUI.Label(new Rect(12f, 32f, 300f, 28f), $"ARMOR {health.Armor}", style);
            if (weapons != null)
            {
                var def = Doom.Game.WeaponTable.Get(weapons.Loadout.Current);
                string ammo = def.Ammo == Doom.Game.AmmoType.None
                    ? "-" : weapons.Ammo.Get(def.Ammo).ToString();
                GUI.Label(new Rect(12f, 56f, 300f, 28f), $"AMMO {ammo}", style);
            }
        }
    }
}
