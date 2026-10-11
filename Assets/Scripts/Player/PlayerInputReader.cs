using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MuckClone.Player
{
    /// <summary>
    /// Desacopla o Unity Input System da lógica de física e gameplay.
    /// Inclui resolução robusta de ações com fallback direto para teclado/mouse caso o Input Asset não esteja configurado.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("Input Action References (Opcional - resolvido automaticamente)")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference lookAction;
        [SerializeField] private InputActionReference jumpAction;
        [SerializeField] private InputActionReference sprintAction;
        [SerializeField] private InputActionReference crouchAction;
        [SerializeField] private InputActionReference attackAction;
        [SerializeField] private InputActionReference interactAction;

        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool IsSprintHeld { get; private set; }
        public bool IsCrouchHeld { get; private set; }

        public event Action OnJumpPerformed;
        public event Action OnCrouchStarted;
        public event Action OnCrouchCanceled;
        public event Action OnAttackPerformed;
        public event Action OnInteractPerformed;
        public event Action<int> OnHotbarSlotSelected;
        public event Action<float> OnHotbarScroll;

        private InputAction _resolvedMove;
        private InputAction _resolvedLook;
        private InputAction _resolvedJump;
        private InputAction _resolvedSprint;
        private InputAction _resolvedCrouch;
        private InputAction _resolvedAttack;
        private InputAction _resolvedInteract;

        private void Awake()
        {
            _resolvedMove = ResolveAction(moveAction, "Player/Move");
            _resolvedLook = ResolveAction(lookAction, "Player/Look");
            _resolvedJump = ResolveAction(jumpAction, "Player/Jump");
            _resolvedSprint = ResolveAction(sprintAction, "Player/Sprint");
            _resolvedCrouch = ResolveAction(crouchAction, "Player/Crouch");
            _resolvedAttack = ResolveAction(attackAction, "Player/Attack");
            _resolvedInteract = ResolveAction(interactAction, "Player/Interact");
        }

        private void OnEnable()
        {
            EnableAction(_resolvedMove);
            EnableAction(_resolvedLook);
            EnableAction(_resolvedSprint);
            EnableAction(_resolvedCrouch);
            EnableAction(_resolvedJump);
            EnableAction(_resolvedAttack);
            EnableAction(_resolvedInteract);
        }

        private void OnDisable()
        {
            DisableAction(_resolvedMove);
            DisableAction(_resolvedLook);
            DisableAction(_resolvedSprint);
            DisableAction(_resolvedCrouch);
            DisableAction(_resolvedJump);
            DisableAction(_resolvedAttack);
            DisableAction(_resolvedInteract);

            MoveInput = Vector2.zero;
            LookInput = Vector2.zero;
            IsSprintHeld = false;
            IsCrouchHeld = false;
        }

        private void Update()
        {
            ReadMovementInput();
            ReadLookInput();
            ReadSprintInput();
            ReadCrouchInput();
            ReadJumpInput();
            ReadActionInputs();
        }

        private void ReadMovementInput()
        {
            if (_resolvedMove != null && _resolvedMove.enabled)
            {
                MoveInput = _resolvedMove.ReadValue<Vector2>();
            }
            else if (Keyboard.current != null)
            {
                float x = 0f;
                float y = 0f;
                if (Keyboard.current.wKey.isPressed) y += 1f;
                if (Keyboard.current.sKey.isPressed) y -= 1f;
                if (Keyboard.current.dKey.isPressed) x += 1f;
                if (Keyboard.current.aKey.isPressed) x -= 1f;
                MoveInput = new Vector2(x, y).normalized;
            }
        }

        private void ReadLookInput()
        {
            if (_resolvedLook != null && _resolvedLook.enabled)
            {
                LookInput = _resolvedLook.ReadValue<Vector2>();
            }
            else if (Mouse.current != null)
            {
                LookInput = Mouse.current.delta.ReadValue();
            }
        }

        private void ReadSprintInput()
        {
            bool sprintPressed = false;
            if (_resolvedSprint != null && _resolvedSprint.enabled)
            {
                sprintPressed = _resolvedSprint.IsPressed();
            }

            if (!sprintPressed && Keyboard.current != null)
            {
                sprintPressed = Keyboard.current.leftShiftKey.isPressed;
            }

            IsSprintHeld = sprintPressed;
        }

        private void ReadCrouchInput()
        {
            bool crouchPressed = false;
            if (_resolvedCrouch != null && _resolvedCrouch.enabled)
            {
                crouchPressed = _resolvedCrouch.IsPressed();
            }

            if (!crouchPressed && Keyboard.current != null)
            {
                crouchPressed = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed;
            }

            if (crouchPressed != IsCrouchHeld)
            {
                IsCrouchHeld = crouchPressed;
                if (IsCrouchHeld)
                {
                    OnCrouchStarted?.Invoke();
                }
                else
                {
                    OnCrouchCanceled?.Invoke();
                }
            }
        }

        private void ReadJumpInput()
        {
            bool jumpTriggered = false;
            if (_resolvedJump != null && _resolvedJump.enabled)
            {
                jumpTriggered = _resolvedJump.WasPressedThisFrame();
            }

            if (!jumpTriggered && Keyboard.current != null)
            {
                jumpTriggered = Keyboard.current.spaceKey.wasPressedThisFrame;
            }

            if (jumpTriggered)
            {
                OnJumpPerformed?.Invoke();
            }
        }

        private void ReadActionInputs()
        {
            bool attack = false;
            if (_resolvedAttack != null && _resolvedAttack.enabled) attack = _resolvedAttack.WasPressedThisFrame();
            if (!attack && Mouse.current != null) attack = Mouse.current.leftButton.wasPressedThisFrame;
            if (attack) OnAttackPerformed?.Invoke();

            bool interact = false;
            if (_resolvedInteract != null && _resolvedInteract.enabled) interact = _resolvedInteract.WasPressedThisFrame();
            if (!interact && Keyboard.current != null) interact = Keyboard.current.eKey.wasPressedThisFrame;
            if (interact) OnInteractPerformed?.Invoke();

            // Seleção de Hotbar (Teclas 1 a 5)
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame) OnHotbarSlotSelected?.Invoke(0);
                else if (Keyboard.current.digit2Key.wasPressedThisFrame) OnHotbarSlotSelected?.Invoke(1);
                else if (Keyboard.current.digit3Key.wasPressedThisFrame) OnHotbarSlotSelected?.Invoke(2);
                else if (Keyboard.current.digit4Key.wasPressedThisFrame) OnHotbarSlotSelected?.Invoke(3);
                else if (Keyboard.current.digit5Key.wasPressedThisFrame) OnHotbarSlotSelected?.Invoke(4);
            }

            // Rolagem da roda do mouse para alternar Hotbar
            if (Mouse.current != null)
            {
                float scrollY = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scrollY) > 0.01f)
                {
                    OnHotbarScroll?.Invoke(scrollY);
                }
            }
        }

        private InputAction ResolveAction(InputActionReference reference, string fallbackPath)
        {
            if (reference != null && reference.action != null)
            {
                return reference.action;
            }

            return InputSystem.actions?.FindAction(fallbackPath);
        }

        private void EnableAction(InputAction action)
        {
            if (action != null && !action.enabled) action.Enable();
        }

        private void DisableAction(InputAction action)
        {
            if (action != null && action.enabled) action.Disable();
        }
    }
}
