using System.Collections;
using MuckClone.Combat;
using MuckClone.Harvesting;
using MuckClone.Interactables;
using MuckClone.Inventory;
using MuckClone.Items;
using MuckClone.PowerUps;
using UnityEngine;

namespace MuckClone.Player
{
    /// <summary>
    /// Gerencia ataques, coleta de recursos com ferramentas, interação com baús e absorção de modificadores Roguelike.
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Configuração de Ataque / Coleta")]
        [Tooltip("Alcance padrão do soco desarmado")]
        [SerializeField] private float baseReach = 3.2f;
        [Tooltip("Dano desarmado (mão nua)")]
        [SerializeField] private float baseDamage = 6f;
        [Tooltip("Tempo de recarga entre golpes desarmados")]
        [SerializeField] private float baseCooldown = 0.35f;
        [Tooltip("Raio de tolerância do golpe para acertar alvos pequenos")]
        [SerializeField] private float attackRadius = 0.25f;
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("Ponto da Câmera")]
        [SerializeField] private Camera playerCamera;

        private PlayerInputReader _inputReader;
        private PlayerInventory _inventory;
        private PlayerStats _stats;
        private PlayerStats Stats
        {
            get
            {
                if (_stats == null) _stats = GetComponent<PlayerStats>() ?? GetComponentInParent<PlayerStats>();
                return _stats;
            }
        }
        private PlayerHealth _health;
        private PlayerHealth Health
        {
            get
            {
                if (_health == null) _health = GetComponent<PlayerHealth>() ?? GetComponentInParent<PlayerHealth>();
                return _health;
            }
        }
        private float _nextAttackTime;
        private Coroutine _swingCoroutine;

        private void Awake()
        {
            _inputReader = GetComponent<PlayerInputReader>() ?? GetComponentInParent<PlayerInputReader>();
            _inventory = GetComponent<PlayerInventory>() ?? GetComponentInParent<PlayerInventory>();
            _stats = GetComponent<PlayerStats>() ?? GetComponentInParent<PlayerStats>();
            _health = GetComponent<PlayerHealth>() ?? GetComponentInParent<PlayerHealth>();

            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
            }
        }

        private void OnEnable()
        {
            if (_inputReader != null)
            {
                _inputReader.OnAttackPerformed += HandleAttack;
                _inputReader.OnInteractPerformed += HandleInteract;
            }
        }

        private void OnDisable()
        {
            if (_inputReader != null)
            {
                _inputReader.OnAttackPerformed -= HandleAttack;
                _inputReader.OnInteractPerformed -= HandleInteract;
            }
        }

        private void HandleAttack()
        {
            if (Time.time < _nextAttackTime) return;

            // Determina parâmetros com base na ferramenta equipada ou soco desarmado
            ItemData activeItem = _inventory != null ? _inventory.ActiveItem : null;

            float reach = activeItem != null ? activeItem.attackRange : baseReach;
            float damage = activeItem != null ? activeItem.damage : baseDamage;
            float cooldown = activeItem != null ? activeItem.attackCooldown : baseCooldown;
            ToolType toolType = activeItem != null ? activeItem.toolType : ToolType.None;
            int tier = activeItem != null ? activeItem.tier : 0;

            // Aplica modificadores Roguelike (dano, acerto crítico e velocidade de ataque)
            if (Stats != null)
            {
                damage = Stats.GetModifiedDamage(damage, out _);
                cooldown = Stats.GetModifiedAttackCooldown(cooldown);
            }

            _nextAttackTime = Time.time + cooldown;

            // Animação procedural de balanço da ferramenta / mão
            TriggerProceduralSwing();

            if (playerCamera == null) return;

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.SphereCast(ray, attackRadius, out RaycastHit hit, reach, hitLayers, QueryTriggerInteraction.Ignore))
            {
                // 1. Verifica se atingiu nó de recurso (árvore, pedra, minério)
                var resource = hit.collider.GetComponentInParent<ResourceNode>();
                if (resource != null)
                {
                    resource.Hit(damage, toolType, tier, hit.point, hit.normal);
                    return;
                }

                // 2. Verifica se atingiu entidade combatente (inimigo com IDamageable)
                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null && (damageable as Component)?.transform.root != transform.root)
                {
                    Vector3 knockDir = (playerCamera.transform.forward + Vector3.up * 0.35f).normalized * (damage * 1.1f + 5f);
                    damageable.TakeDamage(damage, hit.point, knockDir);

                    // Efeito Roguelike: Lifesteal
                    if (Stats != null && Health != null)
                    {
                        float lifesteal = Stats.GetLifestealAmount(damage);
                        if (lifesteal > 0f) Health.Heal(lifesteal);
                    }
                }
            }
        }

        private void HandleInteract()
        {
            if (playerCamera == null) return;

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, 3.8f, hitLayers, QueryTriggerInteraction.Ignore))
            {
                var interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null)
                {
                    interactable.Interact(gameObject);
                }
            }
        }

        private void TriggerProceduralSwing()
        {
            if (playerCamera == null) return;

            Transform hand = playerCamera.transform.Find("HandHoldPoint");
            if (hand == null) return;

            if (_swingCoroutine != null) StopCoroutine(_swingCoroutine);
            _swingCoroutine = StartCoroutine(SwingRoutine(hand));
        }

        private IEnumerator SwingRoutine(Transform hand)
        {
            Vector3 originalPos = new Vector3(0.42f, -0.32f, 0.65f);
            Quaternion originalRot = Quaternion.Euler(15f, -25f, 5f);

            Vector3 punchPos = originalPos + new Vector3(-0.1f, -0.15f, 0.25f);
            Quaternion punchRot = Quaternion.Euler(45f, -10f, -30f);

            float forwardDuration = 0.08f;
            float returnDuration = 0.12f;

            // 1. Golpe rápido para frente
            float t = 0f;
            while (t < forwardDuration)
            {
                t += Time.deltaTime;
                float progress = t / forwardDuration;
                hand.localPosition = Vector3.Lerp(originalPos, punchPos, progress);
                hand.localRotation = Quaternion.Slerp(originalRot, punchRot, progress);
                yield return null;
            }

            // 2. Retorno suave para a posição de guarda
            t = 0f;
            while (t < returnDuration)
            {
                t += Time.deltaTime;
                float progress = t / returnDuration;
                hand.localPosition = Vector3.Lerp(punchPos, originalPos, progress);
                hand.localRotation = Quaternion.Slerp(punchRot, originalRot, progress);
                yield return null;
            }

            hand.localPosition = originalPos;
            hand.localRotation = originalRot;
            _swingCoroutine = null;
        }
    }
}
