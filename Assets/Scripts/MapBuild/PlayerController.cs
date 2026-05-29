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
        [SerializeField] Transform cameraPivot;

        InputAction moveAction;
        InputAction lookAction;
        InputAction sprintAction;
        InputActionMap playerMap;

        CharacterController cc;
        float pitch;
        float verticalVelocity;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            BuildInputActions();
        }

        void OnEnable()
        {
            playerMap.Enable();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void OnDisable()
        {
            playerMap.Disable();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnDestroy()
        {
            playerMap?.Dispose();
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
        }

        void Update()
        {
            ApplyLook();
            ApplyMovement();
        }

        void ApplyLook()
        {
            if (cameraPivot == null) return;
            Vector2 look = lookAction.ReadValue<Vector2>() * mouseSensitivity;
            transform.Rotate(0f, look.x, 0f);
            pitch = Mathf.Clamp(pitch - look.y, -pitchLimit, pitchLimit);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void ApplyMovement()
        {
            Vector2 move = moveAction.ReadValue<Vector2>();
            bool sprint = sprintAction.IsPressed();
            float speed = sprint ? runSpeed : walkSpeed;

            Vector3 horizontal = (transform.forward * move.y + transform.right * move.x) * speed;

            if (cc.isGrounded && verticalVelocity < 0f)
                verticalVelocity = groundStickSpeed;
            verticalVelocity += gravity * Time.deltaTime;

            cc.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
        }
    }
}
