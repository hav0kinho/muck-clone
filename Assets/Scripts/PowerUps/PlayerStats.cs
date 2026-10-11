using System;
using System.Collections.Generic;
using UnityEngine;

namespace MuckClone.PowerUps
{
    /// <summary>
    /// Motor Roguelike de Atributos do Jogador (estilo Muck).
    /// Gerencia modificadores acumuláveis (Flat e Percentuais) e aplica as fórmulas aos sistemas de movimento e combate.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        [Header("Estatísticas Base")]
        [SerializeField] private float baseCritChance = 0.05f; // 5% base
        [SerializeField] private float baseCritMultiplier = 2.0f;

        // Dicionário de Power-ups ativos por ID
        private readonly Dictionary<string, (PowerUpData data, int stacks)> _activePowerUps = new();
        
        public event Action<PowerUpData, int> OnPowerUpCollected;

        private float _bannerExpireTime;
        private string _bannerText = "";
        private Color _bannerColor = Color.white;
        private static GUIStyle _bannerStyle;
        private static GUIStyle _hudPowerUpStyle;

        public void AddPowerUp(PowerUpData powerUp, int count = 1)
        {
            if (powerUp == null || count <= 0) return;

            if (_activePowerUps.TryGetValue(powerUp.id, out var entry))
            {
                entry.stacks += count;
                _activePowerUps[powerUp.id] = entry;
            }
            else
            {
                _activePowerUps[powerUp.id] = (powerUp, count);
            }

            int currentTotal = _activePowerUps[powerUp.id].stacks;
            OnPowerUpCollected?.Invoke(powerUp, currentTotal);

            // Dispara banner comemorativo no topo da tela (estilo Muck)
            _bannerText = $"+ {powerUp.powerUpName} (x{currentTotal})\n{powerUp.description}";
            _bannerColor = powerUp.themeColor;
            _bannerExpireTime = Time.time + 3.5f;

            Debug.Log($"[MuckClone] Power-up adicionado: {powerUp.powerUpName} (Total: {currentTotal} stacks)");
        }

        public int GetPowerUpStacks(string powerUpId)
        {
            if (_activePowerUps.TryGetValue(powerUpId, out var entry))
            {
                return entry.stacks;
            }
            return 0;
        }

        /// <summary>
        /// Aplica a fórmula Roguelike: (Base + Soma(Flat)) * (1 + Soma(Percentual))
        /// </summary>
        public float CalculateStat(StatType statType, float baseValue)
        {
            float flatSum = 0f;
            float percentSum = 0f;

            foreach (var kvp in _activePowerUps.Values)
            {
                PowerUpData data = kvp.data;
                int stacks = kvp.stacks;

                if (data.modifiers == null) continue;

                for (int i = 0; i < data.modifiers.Length; i++)
                {
                    StatModifier mod = data.modifiers[i];
                    if (mod.statType == statType)
                    {
                        if (mod.modifierType == ModifierType.Flat)
                        {
                            flatSum += mod.valuePerStack * stacks;
                        }
                        else if (mod.modifierType == ModifierType.Percent)
                        {
                            percentSum += mod.valuePerStack * stacks;
                        }
                    }
                }
            }

            return (baseValue + flatSum) * (1f + percentSum);
        }

        public float GetModifiedDamage(float baseDamage, out bool isCrit)
        {
            float totalDamage = CalculateStat(StatType.AttackDamage, baseDamage);
            float critChance = CalculateStat(StatType.CritChance, baseCritChance);
            float critMultiplier = CalculateStat(StatType.CritMultiplier, baseCritMultiplier);

            isCrit = UnityEngine.Random.value < critChance;
            if (isCrit)
            {
                totalDamage *= critMultiplier;
            }

            return totalDamage;
        }

        public float GetModifiedSpeed(float baseSpeed)
        {
            return CalculateStat(StatType.MoveSpeed, baseSpeed);
        }

        public float GetModifiedJump(float baseJump)
        {
            return CalculateStat(StatType.JumpForce, baseJump);
        }

        public float GetModifiedAttackCooldown(float baseCooldown)
        {
            // AttackSpeed percentual reduz o tempo de cooldown
            float speedMod = CalculateStat(StatType.AttackSpeed, 1.0f);
            return Mathf.Max(0.12f, baseCooldown / Mathf.Max(0.5f, speedMod));
        }

        public float GetLifestealAmount(float damageDealt)
        {
            float lifestealPercent = CalculateStat(StatType.Lifesteal, 0f);
            return damageDealt * lifestealPercent;
        }

        private void OnGUI()
        {
            DrawPowerUpBanner();
            DrawCollectedPowerUpsHUD();
        }

        private void DrawPowerUpBanner()
        {
            if (Time.time > _bannerExpireTime) return;

            if (_bannerStyle == null)
            {
                _bannerStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
            }

            float width = 340f;
            float height = 55f;
            float x = (Screen.width - width) * 0.5f;
            float y = 40f;

            Color old = GUI.color;
            GUI.color = new Color(_bannerColor.r, _bannerColor.g, _bannerColor.b, 0.95f);
            GUI.Box(new Rect(x, y, width, height), _bannerText, _bannerStyle);
            GUI.color = old;
        }

        private void DrawCollectedPowerUpsHUD()
        {
            if (_activePowerUps.Count == 0) return;

            if (_hudPowerUpStyle == null)
            {
                _hudPowerUpStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
            }

            float itemWidth = 140f;
            float itemHeight = 24f;
            float startX = Screen.width - itemWidth - 15f;
            float startY = 80f;
            int index = 0;

            foreach (var kvp in _activePowerUps.Values)
            {
                PowerUpData p = kvp.data;
                int stacks = kvp.stacks;

                Rect rect = new Rect(startX, startY + index * (itemHeight + 4f), itemWidth, itemHeight);

                Color old = GUI.color;
                GUI.color = new Color(p.themeColor.r * 0.5f, p.themeColor.g * 0.5f, p.themeColor.b * 0.5f, 0.85f);
                GUI.Box(rect, GUIContent.none, _hudPowerUpStyle);

                // Badge de cor
                GUI.color = p.themeColor;
                GUI.DrawTexture(new Rect(rect.x + 4, rect.y + 4, 16, 16), Texture2D.whiteTexture);

                // Nome e stacks
                GUI.color = Color.white;
                GUI.Label(new Rect(rect.x + 24, rect.y, itemWidth - 28, itemHeight), $"{p.powerUpName} x{stacks}", _hudPowerUpStyle);

                GUI.color = old;
                index++;
            }
        }
    }
}
