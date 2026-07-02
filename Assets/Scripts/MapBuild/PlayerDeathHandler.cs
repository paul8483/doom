using UnityEngine;
using UnityEngine.InputSystem;

namespace Doom.MapBuild
{
    /// On player death: freeze movement/use/floor-damage, show a "You died" overlay,
    /// and respawn at the start when R is pressed.
    public sealed class PlayerDeathHandler : MonoBehaviour
    {
        PlayerHealth health;
        PlayerController controller;
        LineActivator activator;
        FloorDamageSystem damage;
        PlayerWeapons weapons;
        CharacterController cc;
        Vector3 startPos;
        Quaternion startRot;
        bool dead;

        /// Fired after a full respawn (health reset, position/rotation restored,
        /// components re-enabled) so other systems (e.g. PlayerWeapons) can reset
        /// their own state. Stage 6c.
        public event System.Action Respawned;

        public void Init(PlayerHealth health, PlayerController controller, LineActivator activator,
                         FloorDamageSystem damage, CharacterController cc,
                         Vector3 startPos, Quaternion startRot)
        {
            this.health = health;
            this.controller = controller;
            this.activator = activator;
            this.damage = damage;
            this.cc = cc;
            this.startPos = startPos;
            this.startRot = startRot;
            health.Died += OnDied;
        }

        /// Wire the weapons component into the death/respawn freeze path so a dead
        /// player cannot fire or switch weapons. Set separately from Init because
        /// MapLoader creates PlayerWeapons after PlayerDeathHandler (Stage 6c).
        public void SetWeapons(PlayerWeapons weapons) => this.weapons = weapons;

        void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        void OnDied()
        {
            dead = true;
            SetActive(false);
        }

        void Update()
        {
            if (!dead) return;
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) Respawn();
        }

        /// Respawn at the start with full health. Public so tests can drive it.
        public void Respawn()
        {
            if (!dead) return;
            health.ResetHealth();
            // CharacterController must be disabled to teleport, else it eats the move.
            if (cc != null) cc.enabled = false;
            transform.position = startPos;
            transform.rotation = startRot;
            if (cc != null) cc.enabled = true;
            SetActive(true);
            dead = false;
            Respawned?.Invoke();
        }

        void SetActive(bool on)
        {
            if (controller != null) controller.enabled = on;
            if (activator != null) activator.enabled = on;
            if (damage != null) damage.enabled = on;
            if (weapons != null) weapons.enabled = on;
        }

        void OnGUI()
        {
            if (!dead) return;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter,
            };
            style.normal.textColor = Color.red;
            GUI.Label(new Rect(0, Screen.height / 2f - 40f, Screen.width, 80f),
                      "You died — press R", style);
        }
    }
}
