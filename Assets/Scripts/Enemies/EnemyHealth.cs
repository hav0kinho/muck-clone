using System.Collections;
using MuckClone.Combat;
using MuckClone.Items;
using UnityEngine;
using UnityEngine.AI;

namespace MuckClone.Enemies
{
    /// <summary>
    /// Gerencia os pontos de vida dos inimigos, reações a dano, knockback físico,
    /// barra de vida suspensa (estilo Muck) e dispersão de drops ao ser derrotado.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [Header("Dados do Inimigo")]
        [SerializeField] private EnemyData enemyData;
        [SerializeField] private float currentHealth;

        [Header("Debug: Barra de Vida")]
        [SerializeField] private bool showHealthBar = true;
        [SerializeField] private bool alwaysShowHealthBar = true;
        [SerializeField] private float maxHealthBarDistance = 40f;

        private Renderer _renderer;
        private Color _originalColor = Color.white;
        private NavMeshAgent _agent;
        private Rigidbody _rb;
        private EnemyController _controller;
        private float _lastDamageTime = -99f;
        private bool _isDead;
        private static GUIStyle _healthTextStyle;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => enemyData != null ? enemyData.maxHealth : 100f;
        public bool IsDead => _isDead;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _rb = GetComponent<Rigidbody>();
            _controller = GetComponent<EnemyController>();
            _renderer = GetComponentInChildren<Renderer>();

            if (_renderer != null && _renderer.material != null)
            {
                _originalColor = _renderer.material.color;
            }
        }

        private void Start()
        {
            if (enemyData != null)
            {
                currentHealth = enemyData.maxHealth;
            }
            else
            {
                currentHealth = 65f;
            }
        }

        public void Initialize(EnemyData data)
        {
            enemyData = data;
            currentHealth = data.maxHealth;

            if (_renderer != null && _renderer.material != null)
            {
                _renderer.material.color = data.fallbackColor;
                _originalColor = data.fallbackColor;
            }
        }

        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 knockbackForce)
        {
            if (_isDead) return;

            currentHealth -= damage;
            _lastDamageTime = Time.time;

            // Flash vermelho de dano
            StartCoroutine(DamageFlashRoutine());

            // Notifica o controlador para aplicar o knockback e entrar em aggro
            if (_controller != null)
            {
                float vulnerability = enemyData != null ? enemyData.knockbackVulnerability : 1.0f;
                _controller.OnDamaged(knockbackForce * vulnerability);
            }

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private IEnumerator DamageFlashRoutine()
        {
            if (_renderer != null && _renderer.material != null)
            {
                _renderer.material.color = Color.red;
                yield return new WaitForSeconds(0.1f);
                if (_renderer != null && _renderer.material != null)
                {
                    _renderer.material.color = _originalColor;
                }
            }
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;

            // Spawna drops físicos com impulso
            if (enemyData != null && enemyData.dropItem != null)
            {
                int count = Random.Range(enemyData.minDrop, enemyData.maxDrop + 1);
                Vector3 center = transform.position + Vector3.up * 0.8f;

                for (int i = 0; i < count; i++)
                {
                    GameObject dropGo = new GameObject($"Drop_{enemyData.dropItem.itemName}");
                    dropGo.transform.position = center;
                    dropGo.AddComponent<Rigidbody>();
                    dropGo.AddComponent<BoxCollider>();

                    var pickup = dropGo.AddComponent<ItemPickup>();
                    Vector3 popDir = (Random.insideUnitSphere + Vector3.up * 1.5f).normalized;
                    pickup.Initialize(enemyData.dropItem, 1, popDir * Random.Range(3.5f, 6.0f));
                }
            }

            Destroy(gameObject);
        }

        private void OnGUI()
        {
            if (!showHealthBar || _isDead || currentHealth <= 0f) return;
            if (!alwaysShowHealthBar && Time.time > _lastDamageTime + 5f && currentHealth >= MaxHealth) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 worldPos = transform.position + Vector3.up * 2.1f;
            float dist = Vector3.Distance(cam.transform.position, worldPos);
            if (dist > maxHealthBarDistance) return;

            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            if (screenPos.z <= 0.1f) return;

            if (_healthTextStyle == null)
            {
                _healthTextStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
            }

            float guiY = Screen.height - screenPos.y;
            float barWidth = Mathf.Clamp(1000f / dist, 55f, 90f);
            float barHeight = 8f;
            Rect barRect = new Rect(screenPos.x - barWidth * 0.5f, guiY, barWidth, barHeight);

            Color oldColor = GUI.color;

            // Fundo da barra
            GUI.color = new Color(0.08f, 0.08f, 0.10f, 0.85f);
            GUI.DrawTexture(new Rect(barRect.x - 1, barRect.y - 1, barRect.width + 2, barRect.height + 2), Texture2D.whiteTexture);

            // Preenchimento (Verde -> Vermelho)
            float hpPercent = Mathf.Clamp01(currentHealth / MaxHealth);
            Color barColor = Color.Lerp(new Color(0.95f, 0.25f, 0.25f), new Color(0.28f, 0.88f, 0.35f), hpPercent);
            GUI.color = barColor;
            GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * hpPercent, barRect.height), Texture2D.whiteTexture);

            // Nome e HP numérico
            GUI.color = Color.white;
            string enemyName = enemyData != null ? enemyData.enemyName : "Inimigo";
            string label = $"{enemyName} [{Mathf.CeilToInt(currentHealth)}/{Mathf.CeilToInt(MaxHealth)}]";
            GUI.Label(new Rect(screenPos.x - 80f, guiY - 18f, 160f, 16f), label, _healthTextStyle);

            GUI.color = oldColor;
        }
    }
}
