using System;
using UnityEngine;

namespace MuckClone.Combat
{
    /// <summary>
    /// Gerencia a integridade física do jogador: Vida, Estamina e Fome (estilo Muck).
    /// Implementa IDamageable, recebe knockback físico e desenha o HUD clássico de 3 barras no canto inferior esquerdo.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Header("Vida")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;
        [SerializeField] private float healthRegenRate = 4f;

        [Header("Estamina (Stamina)")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float currentStamina;
        [SerializeField] private float staminaRegenRate = 22f;

        [Header("Fome (Hunger)")]
        [SerializeField] private float maxHunger = 100f;
        [SerializeField] private float currentHunger;
        [Tooltip("Consumo de fome por segundo")]
        [SerializeField] private float hungerDrainRate = 0.25f;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float CurrentStamina => currentStamina;
        public float CurrentHunger => currentHunger;
        public bool IsDead => currentHealth <= 0f;

        public event Action<float, float> OnHealthChanged;
        public event Action OnPlayerDied;

        private Rigidbody _rb;
        private float _lastDamageTime = -99f;
        private float _damageFlashAlpha;
        private static GUIStyle _barLabelStyle;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            currentHealth = maxHealth;
            currentStamina = maxStamina;
            currentHunger = maxHunger;
        }

        private void Update()
        {
            if (IsDead) return;

            // 1. Dreno contínuo de fome
            if (currentHunger > 0f)
            {
                currentHunger = Mathf.Max(0f, currentHunger - hungerDrainRate * Time.deltaTime);
            }

            // 2. Regeneração de vida passiva se alimentado e sem dano recente
            if (currentHunger > 30f && Time.time > _lastDamageTime + 5f && currentHealth < maxHealth)
            {
                currentHealth = Mathf.Min(maxHealth, currentHealth + healthRegenRate * Time.deltaTime);
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
            }

            // 3. Regeneração contínua de estamina
            if (currentStamina < maxStamina)
            {
                currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenRate * Time.deltaTime);
            }

            // 4. Fade do efeito de dano na tela
            if (_damageFlashAlpha > 0f)
            {
                _damageFlashAlpha = Mathf.Max(0f, _damageFlashAlpha - Time.deltaTime * 2.5f);
            }
        }

        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 knockbackForce)
        {
            if (IsDead) return;

            currentHealth = Mathf.Max(0f, currentHealth - damage);
            _lastDamageTime = Time.time;
            _damageFlashAlpha = 0.45f;

            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            // Aplica impulso físico (Knockback) no jogador
            if (_rb != null && knockbackForce.sqrMagnitude > 0.01f)
            {
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x * 0.2f, _rb.linearVelocity.y, _rb.linearVelocity.z * 0.2f);
                _rb.AddForce(knockbackForce, ForceMode.Impulse);
            }

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        public bool ConsumeStamina(float amount)
        {
            if (currentStamina >= amount)
            {
                currentStamina -= amount;
                return true;
            }
            return false;
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void RestoreHunger(float amount)
        {
            currentHunger = Mathf.Min(maxHunger, currentHunger + amount);
        }

        private void Die()
        {
            OnPlayerDied?.Invoke();
        }

        private void OnGUI()
        {
            // Flash vermelho ao receber dano
            if (_damageFlashAlpha > 0.01f)
            {
                Color oldC = GUI.color;
                GUI.color = new Color(0.85f, 0.1f, 0.1f, _damageFlashAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = oldC;
            }

            DrawSurvivalBars();
        }

        private void DrawSurvivalBars()
        {
            if (_barLabelStyle == null)
            {
                _barLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
            }

            float barWidth = 180f;
            float barHeight = 15f;
            float spacing = 5f;
            float startX = 20f;
            float startY = Screen.height - 75f;

            // 1. Barra de Vida (Vermelha)
            DrawBar(startX, startY, barWidth, barHeight, currentHealth / maxHealth, new Color(0.9f, 0.2f, 0.2f), $"HP {Mathf.CeilToInt(currentHealth)} / {maxHealth}");

            // 2. Barra de Fome (Laranja)
            DrawBar(startX, startY + (barHeight + spacing), barWidth, barHeight, currentHunger / maxHunger, new Color(0.95f, 0.55f, 0.15f), $"Fome {Mathf.CeilToInt(currentHunger)}%");

            // 3. Barra de Estamina (Amarela)
            DrawBar(startX, startY + (barHeight + spacing) * 2, barWidth, barHeight, currentStamina / maxStamina, new Color(0.95f, 0.85f, 0.2f), $"Estamina {Mathf.CeilToInt(currentStamina)}%");
        }

        private void DrawBar(float x, float y, float width, float height, float percent, Color barColor, string label)
        {
            percent = Mathf.Clamp01(percent);
            Color old = GUI.color;

            // Fundo da barra
            GUI.color = new Color(0.08f, 0.08f, 0.10f, 0.85f);
            GUI.DrawTexture(new Rect(x - 2, y - 2, width + 4, height + 4), Texture2D.whiteTexture);

            // Preenchimento
            GUI.color = barColor;
            GUI.DrawTexture(new Rect(x, y, width * percent, height), Texture2D.whiteTexture);

            // Rótulo centralizado
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, width, height), label, _barLabelStyle);

            GUI.color = old;
        }
    }
}
