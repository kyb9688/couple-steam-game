using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TrashCleaner3D
{
    /// <summary>
    /// 원룸 1칸 톤 테스트용 초간단 1인칭 이동 + 손전등 토글(F) 스크립트
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class SimpleFpsWalk : MonoBehaviour
    {
        public float moveSpeed = 3.2f;
        public float mouseSensitivity = 0.15f;
        public Transform cameraTransform;
        public Light flashlight;

        private CharacterController _controller;
        private float _pitch;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            float moveX = 0f;
            float moveZ = 0f;
            float lookX = 0f;
            float lookY = 0f;
            bool toggleLight = false;
            bool unlockCursor = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed) moveZ += 1f;
                if (kb.sKey.isPressed) moveZ -= 1f;
                if (kb.dKey.isPressed) moveX += 1f;
                if (kb.aKey.isPressed) moveX -= 1f;
                if (kb.fKey.wasPressedThisFrame) toggleLight = true;
                if (kb.escapeKey.wasPressedThisFrame) unlockCursor = true;
            }
            if (mouse != null)
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    lookX = delta.x * mouseSensitivity;
                    lookY = delta.y * mouseSensitivity;
                }
                else if (mouse.leftButton.wasPressedThisFrame)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
#else
            moveX = Input.GetAxis("Horizontal");
            moveZ = Input.GetAxis("Vertical");
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                lookX = Input.GetAxis("Mouse X") * 2.0f;
                lookY = Input.GetAxis("Mouse Y") * 2.0f;
            }
            if (Input.GetKeyDown(KeyCode.F)) toggleLight = true;
            if (Input.GetKeyDown(KeyCode.Escape)) unlockCursor = true;
#endif

            if (unlockCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (toggleLight && flashlight != null)
            {
                flashlight.enabled = !flashlight.enabled;
            }

            // Look rotation
            transform.Rotate(Vector3.up * lookX);
            if (cameraTransform != null)
            {
                _pitch = Mathf.Clamp(_pitch - lookY, -80f, 80f);
                cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }

            // Movement + simple gravity
            Vector3 move = (transform.right * moveX + transform.forward * moveZ).normalized * moveSpeed;
            move.y = _controller.isGrounded ? -1f : -9.81f;
            _controller.Move(move * Time.deltaTime);
        }
    }
}
