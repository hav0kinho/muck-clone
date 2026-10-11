using MuckClone.PowerUps;
using UnityEngine;

namespace MuckClone.Player
{
    /// <summary>
    /// Controlador de física e movimentação baseado em Rigidbody.
    /// Resposta imediata de atrito (sem deslizar como gelo), Air Strafe estilo Quake/Muck,
    /// pulo consistente com Coyote Time + Jump Buffer, e mecânica de Slide com impulso.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private Transform cameraTransform;

        [Header("Velocidades no Solo")]
        [Tooltip("Velocidade normal de caminhada")]
        [SerializeField] private float walkSpeed = 8f;
        [Tooltip("Velocidade de corrida (Shift)")]
        [SerializeField] private float sprintSpeed = 14f;
        [Tooltip("Velocidade ao andar agachado")]
        [SerializeField] private float crouchSpeed = 4.5f;
        [Tooltip("Aceleração no chão (quanto maior, mais responsivo ao arrancar)")]
        [SerializeField] private float groundAcceleration = 55f;
        [Tooltip("Atrito de frenagem no chão (quanto maior, mais rápido para ao soltar as teclas)")]
        [SerializeField] private float groundFriction = 65f;

        [Header("Pulo & Bunny Hopping (Air Strafe)")]
        [Tooltip("Força vertical do pulo")]
        [SerializeField] private float jumpForce = 9f;
        [Tooltip("Limite máximo de ganho por strafe no ar")]
        [SerializeField] private float airMaxSpeed = 9f;
        [Tooltip("Taxa de aceleração ao fazer strafe no ar")]
        [SerializeField] private float airAcceleration = 30f;
        [Tooltip("Atrito aerodinâmico suave quando não há input de strafe no ar")]
        [SerializeField] private float airDrag = 1.5f;
        [Tooltip("Gravidade extra aplicada na queda")]
        [SerializeField] private float fallGravityMultiplier = 1.6f;
        [Tooltip("Janela de tolerância após sair de uma borda (Coyote Time)")]
        [SerializeField] private float coyoteTimeDuration = 0.15f;
        [Tooltip("Buffer de input de pulo antes de tocar o chão")]
        [SerializeField] private float jumpBufferDuration = 0.15f;

        [Header("Mecânica de Slide (Deslize)")]
        [Tooltip("Velocidade mínima horizontal necessária para entrar em slide")]
        [SerializeField] private float minSpeedToSlide = 5.5f;
        [Tooltip("Impulso inicial aplicado ao iniciar o slide")]
        [SerializeField] private float slideImpulse = 9f;
        [Tooltip("Atrito durante o slide em terreno plano")]
        [SerializeField] private float slideFriction = 3.5f;
        [Tooltip("Aceleração extra para baixo em declives durante o slide")]
        [SerializeField] private float slopeSlideForce = 25f;

        [Header("Detecção de Solo e Declives")]
        [SerializeField] private LayerMask groundLayer = ~0;
        [SerializeField] private float groundCheckDistance = 0.4f;
        [SerializeField] private float maxSlopeAngle = 50f;

        [Header("Alturas do Colisor e Câmera")]
        [SerializeField] private float standingHeight = 2.0f;
        [SerializeField] private float crouchingHeight = 1.0f;
        [SerializeField] private float cameraStandingY = 0.75f;
        [SerializeField] private float cameraCrouchingY = 0.1f;
        [SerializeField] private float heightChangeSpeed = 14f;

        [Header("Telemetria e Debug (Inspector e Tela)")]
        [Tooltip("Exibe painel de telemetria no canto superior esquerdo da tela durante o Play")]
        [SerializeField] private bool showOnScreenHUD = true;
        [Tooltip("Emite logs no Console do Unity ao trocar de estado (Ex: Começou a correr, pulou, deslizou)")]
        [SerializeField] private bool logStateChanges = false;
        [Space(5)]
        [SerializeField] private string debugState = "IDLE";
        [SerializeField] private float debugSpeed = 0f;
        [SerializeField] private bool debugGrounded = false;
        [SerializeField] private float debugSlope = 0f;
        [SerializeField] private Vector3 debugLinearVelocity = Vector3.zero;

        // Propriedades Públicas
        public bool IsGrounded { get; private set; }
        public bool IsSliding { get; private set; }
        public bool IsCrouching { get; private set; }
        public Vector3 CurrentVelocity => _rb != null ? _rb.linearVelocity : Vector3.zero;
        public float CurrentHorizontalSpeed => new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z).magnitude;

        private Rigidbody _rb;
        private CapsuleCollider _col;
        private PlayerStats _stats;
        private PlayerStats Stats
        {
            get
            {
                if (_stats == null) _stats = GetComponent<PlayerStats>() ?? GetComponentInParent<PlayerStats>();
                return _stats;
            }
        }
        private Vector3 _groundNormal = Vector3.up;
        private float _currentSlopeAngle = 0f;

        private float _coyoteTimer = 0f;
        private float _jumpBufferTimer = 0f;
        private Vector3 _slideDirection = Vector3.forward;

        private readonly RaycastHit[] _sphereCastHits = new RaycastHit[8];
        private bool _collisionGroundContact = false;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _col = GetComponent<CapsuleCollider>();

            if (inputReader == null)
            {
                inputReader = GetComponent<PlayerInputReader>();
            }

            // Física otimizada para arcade FPS
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;

            _col.height = standingHeight;
            _col.center = Vector3.zero;

            _stats = GetComponent<PlayerStats>() ?? GetComponentInParent<PlayerStats>();

            // Garante PhysicsMaterial sem atrito para evitar atrito estático do PhysX em rampas
            PhysicsMaterial frictionlessMat = new PhysicsMaterial("CharacterFrictionless")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0f
            };
            _col.material = frictionlessMat;
        }

        private void OnEnable()
        {
            if (inputReader != null)
            {
                inputReader.OnJumpPerformed += RequestJump;
                inputReader.OnCrouchStarted += RequestCrouch;
                inputReader.OnCrouchCanceled += RequestUncrouch;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.OnJumpPerformed -= RequestJump;
                inputReader.OnCrouchStarted -= RequestCrouch;
                inputReader.OnCrouchCanceled -= RequestUncrouch;
            }
        }

        private string _lastLoggedState = "";

        private void Update()
        {
            // Atualizar timers de pulo
            if (IsGrounded)
            {
                _coyoteTimer = coyoteTimeDuration;
            }
            else
            {
                _coyoteTimer -= Time.deltaTime;
            }

            if (_jumpBufferTimer > 0f)
            {
                _jumpBufferTimer -= Time.deltaTime;
            }

            AdjustColliderAndCameraHeight();
            UpdateTelemetry();
        }

        private void UpdateTelemetry()
        {
            debugSpeed = CurrentHorizontalSpeed;
            debugGrounded = IsGrounded;
            debugSlope = _currentSlopeAngle;
            debugLinearVelocity = _rb != null ? _rb.linearVelocity : Vector3.zero;

            if (IsSliding) debugState = "DESLIZANDO (SLIDE)";
            else if (!IsGrounded) debugState = "NO AR (AIR/JUMP)";
            else if (IsCrouching) debugState = "AGACHADO (CROUCH)";
            else if (inputReader != null && inputReader.IsSprintHeld && debugSpeed > walkSpeed * 1.05f) debugState = "CORRENDO (SPRINT)";
            else if (debugSpeed > 0.2f) debugState = "ANDANDO (WALK)";
            else debugState = "PARADO (IDLE)";

            if (logStateChanges && debugState != _lastLoggedState)
            {
                Debug.Log($"[Player State] {_lastLoggedState} -> {debugState} | Vel: {debugSpeed:F1} m/s | Chão: {IsGrounded}");
                _lastLoggedState = debugState;
            }
        }

        private void FixedUpdate()
        {
            PerformGroundCheck();

            if (IsSliding)
            {
                ApplySlidePhysics();
            }
            else if (IsGrounded)
            {
                ApplyGroundMovement();
            }
            else
            {
                ApplyAirMovement();
            }

            ApplyCustomGravity();

            // Consumir buffer de pulo se puder pular
            if (_jumpBufferTimer > 0f && (_coyoteTimer > 0f || IsGrounded))
            {
                ExecuteJump();
            }

            _collisionGroundContact = false;
        }

        private void PerformGroundCheck()
        {
            // Calcula a base esférica inferior do CapsuleCollider
            float radius = _col.radius * 0.9f;
            float bottomY = _col.center.y - (_col.height * 0.5f - _col.radius);
            Vector3 sphereOrigin = transform.TransformPoint(new Vector3(_col.center.x, bottomY + 0.1f, _col.center.z));

            int hitCount = Physics.SphereCastNonAlloc(
                sphereOrigin,
                radius,
                Vector3.down,
                _sphereCastHits,
                groundCheckDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );

            bool foundValidGround = false;
            RaycastHit closestHit = default;
            float minDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _sphereCastHits[i];
                if (hit.collider == null || hit.collider == _col || hit.collider.transform.root == transform.root)
                {
                    continue;
                }

                if (hit.distance < minDistance)
                {
                    minDistance = hit.distance;
                    closestHit = hit;
                    foundValidGround = true;
                }
            }

            if (foundValidGround)
            {
                _groundNormal = closestHit.normal;
                _currentSlopeAngle = Vector3.Angle(Vector3.up, _groundNormal);
                IsGrounded = _currentSlopeAngle <= maxSlopeAngle;
            }
            else
            {
                // Fallback para contato de colisão direta caso o raycast não alcance
                if (_collisionGroundContact)
                {
                    IsGrounded = true;
                }
                else
                {
                    _groundNormal = Vector3.up;
                    _currentSlopeAngle = 0f;
                    IsGrounded = false;
                }
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                float angle = Vector3.Angle(Vector3.up, contact.normal);
                if (angle <= maxSlopeAngle && contact.point.y < (transform.position.y + 0.2f))
                {
                    _collisionGroundContact = true;
                    _groundNormal = contact.normal;
                    _currentSlopeAngle = angle;
                    break;
                }
            }
        }

        private void ApplyGroundMovement()
        {
            Vector2 input = inputReader != null ? inputReader.MoveInput : Vector2.zero;
            Vector3 moveWish = (transform.forward * input.y + transform.right * input.x).normalized;

            // Determina a velocidade desejada
            float targetSpeed = walkSpeed;
            if (IsCrouching)
            {
                targetSpeed = crouchSpeed;
            }
            else if (inputReader != null && inputReader.IsSprintHeld && input.y > 0.05f)
            {
                targetSpeed = sprintSpeed;
            }

            // Aplica bônus do Roguelike PlayerStats se ativo
            if (Stats != null)
            {
                targetSpeed = Stats.GetModifiedSpeed(targetSpeed);
            }

            Vector3 currentVel = _rb.linearVelocity;

            if (moveWish.sqrMagnitude > 0.001f)
            {
                // Alinha o vetor de movimento 3D tangencialmente à rampa (subida e descida fluidas)
                Vector3 slopeMoveDir = Vector3.ProjectOnPlane(moveWish, _groundNormal).normalized;
                Vector3 targetVelocity = slopeMoveDir * targetSpeed;

                _rb.linearVelocity = Vector3.MoveTowards(currentVel, targetVelocity, groundAcceleration * Time.fixedDeltaTime);
            }
            else
            {
                // Parando: desacelera a velocidade horizontal até zero rapidamente (elimina gelo)
                Vector3 currentHorizontal = new Vector3(currentVel.x, 0f, currentVel.z);
                Vector3 stoppedHorizontal = Vector3.MoveTowards(currentHorizontal, Vector3.zero, groundFriction * Time.fixedDeltaTime);

                // Em rampa parado, anula o componente Y para não deslizar sozinho
                float yVel = (_currentSlopeAngle > 1f && IsGrounded) ? 0f : currentVel.y;
                _rb.linearVelocity = new Vector3(stoppedHorizontal.x, yVel, stoppedHorizontal.z);
            }
        }

        /// <summary>
        /// Algoritmo clássico de Air Strafing (Quake / Source / Muck).
        /// Preserva momentum linear e permite ganhar velocidade virando a câmera e movimentando lateralmente.
        /// </summary>
        private void ApplyAirMovement()
        {
            Vector2 input = inputReader != null ? inputReader.MoveInput : Vector2.zero;
            Vector3 wishDir = (transform.forward * input.y + transform.right * input.x).normalized;

            Vector3 currentVel = _rb.linearVelocity;
            Vector3 currentHorizontal = new Vector3(currentVel.x, 0f, currentVel.z);

            if (wishDir.sqrMagnitude > 0.001f)
            {
                float currentSpeedInWishDir = Vector3.Dot(currentHorizontal, wishDir);
                float maxAir = Stats != null ? Stats.GetModifiedSpeed(airMaxSpeed) : airMaxSpeed;
                float addSpeed = maxAir - currentSpeedInWishDir;

                if (addSpeed > 0f)
                {
                    float accelSpeed = airAcceleration * Time.fixedDeltaTime;
                    accelSpeed = Mathf.Min(accelSpeed, addSpeed);
                    _rb.AddForce(wishDir * accelSpeed, ForceMode.VelocityChange);
                }
            }
            else
            {
                // Leve arrasto horizontal para não flutuar infinitamente sem input
                Vector3 dragVelocity = Vector3.MoveTowards(currentHorizontal, Vector3.zero, airDrag * Time.fixedDeltaTime);
                _rb.linearVelocity = new Vector3(dragVelocity.x, currentVel.y, dragVelocity.z);
            }
        }

        private void ApplySlidePhysics()
        {
            Vector3 currentVel = _rb.linearVelocity;
            Vector3 currentHorizontal = new Vector3(currentVel.x, 0f, currentVel.z);
            float currentSpeed = currentHorizontal.magnitude;

            // Se estiver em declive voltado para baixo, ganha aceleração tangencial
            if (_currentSlopeAngle > 4f)
            {
                Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, _groundNormal).normalized;
                _rb.AddForce(downhill * slopeSlideForce, ForceMode.Acceleration);
            }
            else
            {
                // No chão plano, o atrito suave reduz a velocidade gradualmente
                Vector3 slowDown = Vector3.MoveTowards(currentHorizontal, Vector3.zero, slideFriction * Time.fixedDeltaTime);
                _rb.linearVelocity = new Vector3(slowDown.x, currentVel.y, slowDown.z);
            }

            // Encerra slide se parar ou perder o chão
            if (currentSpeed < (crouchSpeed * 0.85f) || !IsGrounded)
            {
                StopSlide();
            }
        }

        private void ApplyCustomGravity()
        {
            if (!IsGrounded && _rb.linearVelocity.y < 0f)
            {
                _rb.AddForce(Physics.gravity * (fallGravityMultiplier - 1f), ForceMode.Acceleration);
            }
        }

        private void RequestJump()
        {
            _jumpBufferTimer = jumpBufferDuration;
        }

        private void ExecuteJump()
        {
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            IsGrounded = false;

            if (IsSliding)
            {
                StopSlide();
            }

            // Pulo vertical consistente com bônus Roguelike
            Vector3 vel = _rb.linearVelocity;
            float actualJump = Stats != null ? Stats.GetModifiedJump(jumpForce) : jumpForce;
            vel.y = actualJump;
            _rb.linearVelocity = vel;
        }

        private void RequestCrouch()
        {
            float speed = CurrentHorizontalSpeed;
            bool wantsToSprint = inputReader != null && inputReader.IsSprintHeld;

            // Inicia slide se estiver no chão e em velocidade de caminhada rápida ou corrida
            if (IsGrounded && (speed >= minSpeedToSlide || wantsToSprint) && !IsSliding)
            {
                StartSlide();
            }
            else
            {
                IsCrouching = true;
            }
        }

        private void RequestUncrouch()
        {
            if (IsSliding)
            {
                StopSlide();
            }

            if (CanStandUp())
            {
                IsCrouching = false;
            }
        }

        private void StartSlide()
        {
            IsSliding = true;
            IsCrouching = true;

            Vector3 currentHorizontal = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            _slideDirection = currentHorizontal.sqrMagnitude > 1f ? currentHorizontal.normalized : transform.forward;

            // Impulso inicial do slide
            _rb.AddForce(_slideDirection * slideImpulse, ForceMode.Impulse);
        }

        private void StopSlide()
        {
            IsSliding = false;

            if (inputReader != null && !inputReader.IsCrouchHeld && CanStandUp())
            {
                IsCrouching = false;
            }
        }

        private bool CanStandUp()
        {
            Vector3 castOrigin = transform.position;
            float checkDistance = standingHeight - crouchingHeight;
            return !Physics.SphereCast(castOrigin, _col.radius * 0.85f, Vector3.up, out _, checkDistance, groundLayer, QueryTriggerInteraction.Ignore);
        }

        private void AdjustColliderAndCameraHeight()
        {
            float targetHeight = (IsCrouching || IsSliding) ? crouchingHeight : standingHeight;
            float targetCenterY = (targetHeight - standingHeight) * 0.5f;

            _col.height = Mathf.MoveTowards(_col.height, targetHeight, heightChangeSpeed * Time.deltaTime);
            _col.center = new Vector3(0f, targetCenterY, 0f);

            if (cameraTransform != null)
            {
                float targetCamY = (IsCrouching || IsSliding) ? cameraCrouchingY : cameraStandingY;
                Vector3 camPos = cameraTransform.localPosition;
                camPos.y = Mathf.MoveTowards(camPos.y, targetCamY, heightChangeSpeed * Time.deltaTime);
                cameraTransform.localPosition = camPos;
            }
        }

        private void OnGUI()
        {
            if (!showOnScreenHUD) return;

            GUILayout.BeginArea(new Rect(15, 15, 275, 195), GUI.skin.box);
            GUILayout.Label($"<b>TELEMETRIA DO PLAYER</b>");
            GUILayout.Label($"Estado: <b>{debugState}</b>");
            GUILayout.Label($"Vel. Horizontal: <b>{debugSpeed:F1} m/s</b>");
            GUILayout.Label($"No Chão: <b>{(debugGrounded ? "<color=green>SIM</color>" : "<color=red>NÃO</color>")}</b>");
            GUILayout.Label($"Inclinação: <b>{debugSlope:F1}°</b>");
            GUILayout.Label($"Velocidade Y: <b>{debugLinearVelocity.y:F1} m/s</b>");
            GUILayout.Label($"Shift: {(inputReader != null && inputReader.IsSprintHeld ? "SIM" : "NÃO")} | Crouch: {(inputReader != null && inputReader.IsCrouchHeld ? "SIM" : "NÃO")}");

            if (Stats != null)
            {
                float speedBonus = (Stats.GetModifiedSpeed(1f) - 1f) * 100f;
                float jumpBonus = (Stats.GetModifiedJump(1f) - 1f) * 100f;
                if (speedBonus > 0.1f || jumpBonus > 0.1f)
                {
                    GUILayout.Label($"<color=cyan><b>ROGUELIKE:</b></color> +{speedBonus:F0}% Vel | +{jumpBonus:F0}% Pulo");
                }
            }
            GUILayout.EndArea();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            if (_col != null)
            {
                float radius = _col.radius * 0.9f;
                float bottomY = _col.center.y - (_col.height * 0.5f - _col.radius);
                Vector3 sphereOrigin = transform.TransformPoint(new Vector3(_col.center.x, bottomY + 0.1f, _col.center.z));
                Gizmos.DrawWireSphere(sphereOrigin + Vector3.down * groundCheckDistance, radius);
            }
        }
    }
}
