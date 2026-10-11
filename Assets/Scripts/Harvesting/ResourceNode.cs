using System.Collections;
using MuckClone.Items;
using UnityEngine;

namespace MuckClone.Harvesting
{
    public enum ResourceType
    {
        Tree,
        Rock,
        Ore
    }

    /// <summary>
    /// Representa um nó colhível no mundo (árvore, pedra, minério).
    /// Controla pontos de vida, verificação de Tier de ferramentas, explosão de drops físicos
    /// e barra de vida flutuante renderizada via Script para debug e feedback visual.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ResourceNode : MonoBehaviour
    {
        [Header("Tipo e Requisitos")]
        [SerializeField] private ResourceType nodeType = ResourceType.Tree;
        [SerializeField] private ToolType preferredTool = ToolType.Axe;
        [Tooltip("0: Mão/Madeira, 1: Sílex/Pedra, 2: Ferro, 3: Ouro, 4: Mitril, 5: Adamantite")]
        [SerializeField] private int requiredTier = 0;

        [Header("Vida e Resistência")]
        [SerializeField] private float maxHealth = 50f;
        [SerializeField] private float currentHealth;

        [Header("Debug: Barra de Vida Flutuante")]
        [Tooltip("Exibe a barra de vida no topo do recurso")]
        [SerializeField] private bool showHealthBar = true;
        [Tooltip("Se verdadeiro, mostra sempre. Se falso, mostra apenas ao sofrer dano recente")]
        [SerializeField] private bool alwaysShowHealthBar = true;
        [Tooltip("Distância máxima da câmera para renderizar a barra")]
        [SerializeField] private float maxHealthBarDistance = 24f;

        [Header("Drops de Itens")]
        [SerializeField] private ItemData dropItemData;
        [SerializeField] private int minDropCount = 2;
        [SerializeField] private int maxDropCount = 5;

        private Vector3 _originalScale;
        private Coroutine _wobbleCoroutine;
        private bool _isDestroyed;
        private float _lastHitTime = -99f;
        private static GUIStyle _healthTextStyle;

        public ResourceType Type => nodeType;
        public ToolType PreferredTool => preferredTool;
        public int RequiredTier => requiredTier;
        public float CurrentHealth => currentHealth;

        private void Awake()
        {
            currentHealth = maxHealth;
            _originalScale = transform.localScale;
        }

        public void Initialize(ResourceType type, ToolType tool, int tier, float health, ItemData dropItem, int minDrop, int maxDrop)
        {
            nodeType = type;
            preferredTool = tool;
            requiredTier = tier;
            maxHealth = health;
            currentHealth = health;
            dropItemData = dropItem;
            minDropCount = minDrop;
            maxDropCount = maxDrop;
        }

        /// <summary>
        /// Aplica dano ao recurso considerando a ferramenta e o Tier do atacante.
        /// Retorna true se o golpe causou dano efetivo, false se foi bloqueado por falta de Tier.
        /// </summary>
        public bool Hit(float baseDamage, ToolType attackerTool, int attackerTier, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (_isDestroyed) return false;

            float actualDamage = baseDamage;
            bool isEffective = true;

            // 1. Verificação de Tier
            if (attackerTier < requiredTier)
            {
                actualDamage = 1f; // Dano residual
                isEffective = false;
            }
            else
            {
                // 2. Bônus por ferramenta correta
                if (preferredTool != ToolType.None && attackerTool == preferredTool)
                {
                    actualDamage = baseDamage;
                }
                else
                {
                    actualDamage = Mathf.Max(1f, baseDamage * 0.3f); // Penalidade por ferramenta errada
                }
            }

            currentHealth -= actualDamage;
            _lastHitTime = Time.time;

            // Feedback visual: tremor procedural de impacto
            if (_wobbleCoroutine != null) StopCoroutine(_wobbleCoroutine);
            _wobbleCoroutine = StartCoroutine(HitWobbleRoutine(hitNormal));

            if (currentHealth <= 0f)
            {
                BreakNode();
            }

            return isEffective;
        }

        private IEnumerator HitWobbleRoutine(Vector3 hitNormal)
        {
            float duration = 0.12f;
            float elapsed = 0f;

            // Contração momentânea na direção do golpe
            Vector3 squishedScale = new Vector3(_originalScale.x * 0.92f, _originalScale.y * 1.08f, _originalScale.z * 0.92f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.localScale = Vector3.Lerp(squishedScale, _originalScale, t);
                yield return null;
            }

            transform.localScale = _originalScale;
            _wobbleCoroutine = null;
        }

        private void BreakNode()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;

            // Spawna os itens físicos (drops) no mundo com impulso radial
            int count = Random.Range(minDropCount, maxDropCount + 1);
            Vector3 center = transform.position + Vector3.up * 1f;

            for (int i = 0; i < count; i++)
            {
                SpawnDrop(center);
            }

            Destroy(gameObject);
        }

        private void SpawnDrop(Vector3 spawnPosition)
        {
            GameObject pickupGo;

            if (dropItemData != null && dropItemData.dropPrefab != null)
            {
                pickupGo = Instantiate(dropItemData.dropPrefab, spawnPosition, Random.rotation);
            }
            else
            {
                // Criação procedural de pickup com física pronta
                pickupGo = new GameObject(dropItemData != null ? $"Drop_{dropItemData.itemName}" : "Drop_Item");
                pickupGo.transform.position = spawnPosition;
                pickupGo.AddComponent<Rigidbody>();
                pickupGo.AddComponent<BoxCollider>();
            }

            var pickup = pickupGo.GetComponent<ItemPickup>();
            if (pickup == null) pickup = pickupGo.AddComponent<ItemPickup>();

            // Impulso físico de dispersão para cima e para os lados (estilo Muck)
            Vector3 popDir = (Random.insideUnitSphere + Vector3.up * 1.5f).normalized;
            Vector3 popVelocity = popDir * Random.Range(3.5f, 6.5f);

            pickup.Initialize(dropItemData, 1, popVelocity);
        }

        private void OnGUI()
        {
            if (!showHealthBar || _isDestroyed || currentHealth <= 0f) return;
            if (!alwaysShowHealthBar && Time.time > _lastHitTime + 3.5f) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            // Posição no topo da árvore ou rocha
            float topOffset = (nodeType == ResourceType.Tree) ? 3.0f : 1.6f;
            Vector3 worldPos = transform.position + Vector3.up * topOffset;
            float dist = Vector3.Distance(cam.transform.position, worldPos);
            if (dist > maxHealthBarDistance) return;

            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            if (screenPos.z <= 0.1f) return; // Ignora se estiver atrás da câmera

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
            float barWidth = Mathf.Clamp(1100f / dist, 55f, 95f);
            float barHeight = 8f;
            Rect barRect = new Rect(screenPos.x - barWidth * 0.5f, guiY, barWidth, barHeight);

            Color oldColor = GUI.color;

            // 1. Moldura / Fundo escuro
            GUI.color = new Color(0.08f, 0.08f, 0.10f, 0.85f);
            GUI.DrawTexture(new Rect(barRect.x - 1, barRect.y - 1, barRect.width + 2, barRect.height + 2), Texture2D.whiteTexture);

            // 2. Preenchimento da barra com gradiente dinâmico (Verde -> Amarelo -> Vermelho)
            float hpPercent = Mathf.Clamp01(currentHealth / maxHealth);
            Color barColor = Color.Lerp(new Color(0.95f, 0.25f, 0.25f), new Color(0.28f, 0.88f, 0.35f), hpPercent);
            GUI.color = barColor;
            GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * hpPercent, barRect.height), Texture2D.whiteTexture);

            // 3. Texto do Recurso e Quantidade Numérica de HP
            GUI.color = Color.white;
            string label = $"{nodeType} [{Mathf.CeilToInt(currentHealth)}/{Mathf.CeilToInt(maxHealth)}]";
            GUI.Label(new Rect(screenPos.x - 80f, guiY - 18f, 160f, 16f), label, _healthTextStyle);

            GUI.color = oldColor;
        }
    }
}
