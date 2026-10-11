using UnityEngine;
using UnityEngine.InputSystem;

namespace MuckClone.Player
{
    /// <summary>
    /// Controla a rotação da câmera (Pitch vertical com clamp) e a rotação do corpo do jogador (Yaw horizontal).
    /// Desacoplado do PlayerMovement para manter responsabilidade única.
    /// </summary>
    public class PlayerLook : MonoBehaviour
    {
        [Header("Referências")]
        [Tooltip("Transform do GameObject que contém a Câmera (filho do Player)")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private PlayerInputReader inputReader;

        [Header("Sensibilidade e Inversão")]
        [Range(0.05f, 2f)]
        [SerializeField] private float mouseSensitivity = 0.2f;
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;
        [Tooltip("Inverte eixo Y (cima/baixo)")]
        [SerializeField] private bool invertY = false;
        [Tooltip("Inverte eixo X (esquerda/direita)")]
        [SerializeField] private bool invertX = false;

        [Header("Cursor")]
        [SerializeField] private bool lockCursorOnStart = true;

        private float _currentPitch = 0f;

        private void Awake()
        {
            if (inputReader == null)
            {
                inputReader = GetComponentInParent<PlayerInputReader>();
                if (inputReader == null)
                {
                    inputReader = GetComponent<PlayerInputReader>();
                }
            }

            if (cameraTransform == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraTransform = cam.transform;
            }
        }

        private void Start()
        {
            if (lockCursorOnStart)
            {
                SetCursorLock(true);
            }
        }

        private void LateUpdate()
        {
            HandleCursorUnlockToggle();

            if (Cursor.lockState != CursorLockMode.Locked || inputReader == null) return;

            Vector2 lookInput = inputReader.LookInput;
            float mouseX = lookInput.x * mouseSensitivity * (invertX ? -1f : 1f);
            float mouseY = lookInput.y * mouseSensitivity * (invertY ? -1f : 1f);

            // Rotação horizontal (Yaw) gira o corpo do jogador
            transform.Rotate(Vector3.up * mouseX);

            // Rotação vertical (Pitch) gira apenas a câmera: mouse para cima diminui ângulo X (olhar para o céu)
            _currentPitch -= mouseY;
            _currentPitch = Mathf.Clamp(_currentPitch, minPitch, maxPitch);

            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.Euler(_currentPitch, 0f, 0f);
            }
        }

        private void HandleCursorUnlockToggle()
        {
            // Pressionar ESC libera o mouse para debugar no Unity Editor
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                bool isLocked = Cursor.lockState == CursorLockMode.Locked;
                SetCursorLock(!isLocked);
            }

            // Clique do mouse bloqueia novamente
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                SetCursorLock(true);
            }
        }

        public void SetCursorLock(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
