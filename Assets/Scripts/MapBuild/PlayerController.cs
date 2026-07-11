using UnityEngine;
using UnityEngine.InputSystem;

namespace Doom.MapBuild
{
    /// FPS controller: WASD walk/strafe, mouse mouselook (yaw on root,
    /// pitch on cameraPivot, clamp ±85°), Shift run (hold), no jump, no crouch.
    /// Input Actions are built in code — no asset file.
    [AddComponentMenu("Doom/Player Controller")]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Movement (m/s)")]
        [SerializeField] float walkSpeed = 6.25f;   // 200 DOOM units/s × (1/32)
        [SerializeField] float runSpeed  = 13.75f;  // 440 DOOM units/s × (1/32)
        [SerializeField] float gravity   = -9.81f;
        [SerializeField] float groundStickSpeed = -2f;

        [Header("Look")]
        [SerializeField] float mouseSensitivity = 0.1f;  // degrees per pixel
        [SerializeField] float pitchLimit = 85f;
        [SerializeField] bool invertY;
        [Tooltip("Child transform at eye height; pitch is applied here. Wired by MapLoader.")]
        [SerializeField] Transform cameraPivot;

        InputAction moveAction;
        InputAction lookAction;
        InputAction sprintAction;
        InputAction useAction;
        InputAction locationDumpAction;
        InputActionMap playerMap;

        LineActivator activator;
        CharacterController cc;
        float pitch;
        float verticalVelocity;
        float maxStepOffset;

        public float MouseSensitivity => mouseSensitivity;
        public bool InvertY => invertY;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            // Unity CC treats stepOffset as extra headroom during Move (issue 576605):
            // openings shorter than height+stepOffset block even when the capsule fits.
            // Keep the authored DOOM 24-unit max and clamp per-frame under lintels.
            maxStepOffset = cc.stepOffset;
            BuildInputActions();
        }

        void OnEnable()
        {
            playerMap.Enable();
            // Cursor lock/unlock is owned by GameFlowController (Stage 7c).
        }

        void OnDisable()
        {
            playerMap.Disable();
        }

        void OnDestroy()
        {
            if (useAction != null) useAction.performed -= OnUse;
            if (locationDumpAction != null) locationDumpAction.performed -= OnLocationDump;
            playerMap?.Dispose();
        }

        public void SetCameraPivot(Transform pivot) => cameraPivot = pivot;

        public float PitchDegrees => pitch;

        /// Teleport + view restore for save load (disables CharacterController briefly).
        public void SetView(float yawDegrees, float pitchDegrees)
        {
            pitch = Mathf.Clamp(pitchDegrees, -pitchLimit, pitchLimit);
            transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        public void ApplyLookSettings(float sensitivity, bool invertYAxis)
        {
            mouseSensitivity = Mathf.Clamp(sensitivity, 0.01f, 2f);
            invertY = invertYAxis;
        }

        void BuildInputActions()
        {
            playerMap = new InputActionMap("Player");

            moveAction = playerMap.AddAction("Move",
                InputActionType.Value, expectedControlLayout: "Vector2");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up",    "<Keyboard>/w")
                .With("Down",  "<Keyboard>/s")
                .With("Left",  "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            lookAction = playerMap.AddAction("Look",
                InputActionType.Value, "<Mouse>/delta", expectedControlLayout: "Vector2");

            sprintAction = playerMap.AddAction("Sprint",
                InputActionType.Button, "<Keyboard>/leftShift");

            useAction = playerMap.AddAction("Use",
                InputActionType.Button, "<Keyboard>/e");
            useAction.AddBinding("<Gamepad>/buttonWest");
            useAction.performed += OnUse;

            locationDumpAction = playerMap.AddAction("LocationDump",
                InputActionType.Button, "<Keyboard>/t");
            locationDumpAction.performed += OnLocationDump;
        }

        // LineActivator is added to the Player by MapLoader AFTER this controller's
        // Awake/BuildInputActions, so resolve it lazily on first Use.
        void OnUse(InputAction.CallbackContext _)
        {
            if (activator == null) activator = GetComponent<LineActivator>();
            if (activator != null) activator.TryUse();
        }

        void OnLocationDump(InputAction.CallbackContext _)
        {
            if (activator == null) activator = GetComponent<LineActivator>();
            if (activator != null) activator.DumpLocation();
        }

        void Update()
        {
            ApplyLook();
            ApplyMovement();
        }

        void ApplyLook()
        {
            Vector2 look = lookAction.ReadValue<Vector2>() * mouseSensitivity;
            transform.Rotate(0f, look.x, 0f);             // yaw always applies
            if (cameraPivot == null) return;              // pitch needs the pivot
            float pitchDelta = invertY ? look.y : -look.y;
            pitch = Mathf.Clamp(pitch + pitchDelta, -pitchLimit, pitchLimit);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void ApplyMovement()
        {
            Vector2 move = moveAction.ReadValue<Vector2>();
            bool sprint = sprintAction.IsPressed();
            float speed = sprint ? runSpeed : walkSpeed;

            // Diagonal movement intentionally un-normalized (matches original DOOM).
            Vector3 horizontal = (transform.forward * move.y + transform.right * move.x) * speed;

            if (cc.isGrounded && verticalVelocity < 0f)
                verticalVelocity = groundStickSpeed;
            verticalVelocity += gravity * Time.deltaTime;

            AdaptStepOffset(horizontal);
            cc.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        /// Clamps stepOffset so height+step fits under any lintel ahead of the move.
        /// Without this, E1M2's 64-unit doorway (56 height + 24 step) is impassable.
        void AdaptStepOffset(Vector3 horizontalVelocity)
        {
            Vector3 dir = horizontalVelocity;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f || maxStepOffset <= 0f)
            {
                cc.stepOffset = maxStepOffset;
                return;
            }
            dir.Normalize();

            Vector3 head = transform.position + Vector3.up * cc.height;
            float probeForward = cc.radius + 0.35f;
            float clearance = maxStepOffset;
            const float step = 0.05f;
            for (float up = step; up <= maxStepOffset + step; up += step)
            {
                if (Physics.Raycast(head + Vector3.up * up, dir, probeForward, ~0,
                                    QueryTriggerInteraction.Ignore))
                {
                    clearance = up - step;
                    break;
                }
            }

            cc.stepOffset = Mathf.Clamp(clearance, 0f, maxStepOffset);
        }
    }
}
