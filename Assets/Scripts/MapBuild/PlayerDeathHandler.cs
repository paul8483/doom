using UnityEngine;
using UnityEngine.InputSystem;

namespace Doom.MapBuild
{
    /// On player death: notify GameFlow, show overlay, respawn on R.
    /// Freeze/cursor ownership lives in <see cref="GameFlowController"/>.
    public sealed class PlayerDeathHandler : MonoBehaviour
    {
        PlayerHealth health;
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
            // controller/activator/damage kept in signature for MapLoader compatibility;
            // freeze is centralized in GameFlowController (Stage 7c).
            this.health = health;
            this.cc = cc;
            this.startPos = startPos;
            this.startRot = startRot;
            health.Died += OnDied;
        }

        /// Kept for MapLoader wiring compatibility; freeze is owned by GameFlow.
        public void SetWeapons(PlayerWeapons weapons) { }

        void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        void OnDied()
        {
            dead = true;
            GameFlowController.Ensure().EnterDead();
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
            dead = false;
            GameFlowController.Ensure().LeaveDeadToPlaying();
            Respawned?.Invoke();
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
