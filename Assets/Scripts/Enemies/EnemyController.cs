using System.Collections;
using MuckClone.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace MuckClone.Enemies
{
    public enum EnemyState
    {
        Idle,
        Chase,
        Attack,
        Knockback
    }

    /// <summary>
    /// IA de navegação e combate dos inimigos (NavMesh).
    /// Gerencia estados de patrulha, perseguição ao Player, ataques corpo a corpo com knockback
    /// e recuperação física ao receber impactos.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyController : MonoBehaviour
    {
        [Header("Configuração de IA")]
        [SerializeField] private EnemyData enemyData;
        [SerializeField] private EnemyState currentState = EnemyState.Idle;

        private NavMeshAgent _agent;
        private EnemyHealth _health;
        private Transform _targetPlayer;
        private PlayerHealth _playerHealth;

        private float _nextAttackTime;
        private float _destinationUpdateTime;
        private Coroutine _knockbackRoutine;
        private Vector3 _spawnPosition;

        public EnemyState State => currentState;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<EnemyHealth>();
            _spawnPosition = transform.position;
        }

        private void Start()
        {
            ApplyEnemyStats();
            FindPlayerReference();

            // Gera corpo visual estilizado se não houver malha
            if (GetComponentInChildren<MeshFilter>() == null)
            {
                CreateFallbackVisual();
            }
        }

        private void ApplyEnemyStats()
        {
            if (enemyData == null) return;

            _agent.speed = enemyData.moveSpeed;
            _agent.acceleration = enemyData.acceleration;
            _agent.stoppingDistance = enemyData.stoppingDistance;
        }

        private void FindPlayerReference()
        {
            if (_playerHealth == null)
            {
                _playerHealth = FindFirstObjectByType<PlayerHealth>();
                if (_playerHealth != null) _targetPlayer = _playerHealth.transform;
            }
        }

        private void Update()
        {
            if (_health.IsDead || currentState == EnemyState.Knockback) return;

            FindPlayerReference();

            if (_targetPlayer == null || _playerHealth.IsDead)
            {
                currentState = EnemyState.Idle;
                if (_agent.isOnNavMesh) _agent.isStopped = true;
                return;
            }

            float distToPlayer = Vector3.Distance(transform.position, _targetPlayer.position);
            float aggroRange = enemyData != null ? enemyData.aggroRadius : 20f;
            float attackRange = enemyData != null ? enemyData.attackRange : 2.0f;

            switch (currentState)
            {
                case EnemyState.Idle:
                    if (distToPlayer <= aggroRange)
                    {
                        currentState = EnemyState.Chase;
                    }
                    break;

                case EnemyState.Chase:
                    if (distToPlayer > (enemyData != null ? enemyData.loseAggroRadius : 35f))
                    {
                        currentState = EnemyState.Idle;
                        if (_agent.isOnNavMesh) _agent.isStopped = true;
                        break;
                    }

                    if (distToPlayer <= attackRange)
                    {
                        currentState = EnemyState.Attack;
                        if (_agent.isOnNavMesh) _agent.isStopped = true;
                    }
                    else
                    {
                        if (_agent.isOnNavMesh)
                        {
                            _agent.isStopped = false;
                            if (Time.time >= _destinationUpdateTime)
                            {
                                _destinationUpdateTime = Time.time + 0.2f;
                                _agent.SetDestination(_targetPlayer.position);
                            }
                        }
                    }
                    break;

                case EnemyState.Attack:
                    // Rotação em direção ao jogador durante o combate
                    Vector3 lookDir = (_targetPlayer.position - transform.position);
                    lookDir.y = 0;
                    if (lookDir.sqrMagnitude > 0.01f)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 8f);
                    }

                    if (distToPlayer > attackRange * 1.3f)
                    {
                        currentState = EnemyState.Chase;
                        break;
                    }

                    if (Time.time >= _nextAttackTime)
                    {
                        StartCoroutine(PerformAttackRoutine());
                    }
                    break;
            }
        }

        private IEnumerator PerformAttackRoutine()
        {
            float cooldown = enemyData != null ? enemyData.attackCooldown : 1.0f;
            _nextAttackTime = Time.time + cooldown;

            // 1. Windup (antecipação do golpe com leve recuo visual)
            Vector3 startPos = transform.position;
            Vector3 forwardStep = transform.forward * 0.4f;

            yield return new WaitForSeconds(0.15f);

            // 2. Golpe frontal
            if (_playerHealth != null && !_playerHealth.IsDead)
            {
                float dist = Vector3.Distance(transform.position, _targetPlayer.position);
                float attackRange = enemyData != null ? enemyData.attackRange : 2.0f;

                if (dist <= attackRange + 0.5f)
                {
                    float dmg = enemyData != null ? enemyData.attackDamage : 12f;
                    float knockForce = enemyData != null ? enemyData.playerKnockbackForce : 10f;

                    Vector3 knockDir = (transform.forward + Vector3.up * 0.35f).normalized * knockForce;
                    _playerHealth.TakeDamage(dmg, transform.position, knockDir);
                }
            }

            yield return new WaitForSeconds(0.2f);
        }

        public void OnDamaged(Vector3 knockbackForce)
        {
            if (_health.IsDead) return;

            currentState = EnemyState.Knockback;
            if (_knockbackRoutine != null) StopCoroutine(_knockbackRoutine);
            _knockbackRoutine = StartCoroutine(KnockbackRoutine(knockbackForce));
        }

        private IEnumerator KnockbackRoutine(Vector3 knockbackForce)
        {
            if (_agent.isOnNavMesh) _agent.enabled = false;

            float duration = 0.22f;
            float elapsed = 0f;
            Vector3 velocity = knockbackForce;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position += velocity * Time.deltaTime;
                velocity = Vector3.Lerp(velocity, Vector3.zero, Time.deltaTime * 6f);
                yield return null;
            }

            // Tenta posicionar o agente de volta com segurança na malha de navegação
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
            }

            _agent.enabled = true;
            currentState = EnemyState.Chase; // Fica enfurecido e persegue quem bateu
            _knockbackRoutine = null;
        }

        private void CreateFallbackVisual()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0, 0.9f, 0);

            Vector3 scale = enemyData != null ? enemyData.fallbackScale : new Vector3(0.9f, 1.7f, 0.9f);
            body.transform.localScale = scale;

            var rend = body.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                rend.material = new Material(s) { color = enemyData != null ? enemyData.fallbackColor : Color.green };
            }

            // Pequenos "olhos" vermelhos para dar a cara cartoon ameaçadora
            var eyeLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(eyeLeft.GetComponent<Collider>());
            eyeLeft.transform.SetParent(body.transform, false);
            eyeLeft.transform.localScale = new Vector3(0.2f, 0.15f, 0.2f);
            eyeLeft.transform.localPosition = new Vector3(-0.2f, 0.35f, 0.45f);
            eyeLeft.GetComponent<Renderer>().material.color = Color.red;

            var eyeRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(eyeRight.GetComponent<Collider>());
            eyeRight.transform.SetParent(body.transform, false);
            eyeRight.transform.localScale = new Vector3(0.2f, 0.15f, 0.2f);
            eyeRight.transform.localPosition = new Vector3(0.2f, 0.35f, 0.45f);
            eyeRight.GetComponent<Renderer>().material.color = Color.red;
        }
    }
}
