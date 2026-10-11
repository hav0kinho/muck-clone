using UnityEngine;

namespace MuckClone.PowerUps
{
    /// <summary>
    /// Definição Data-Driven de um Power-up Roguelike acumulável (estilo Muck).
    /// </summary>
    [CreateAssetMenu(fileName = "NewPowerUp", menuName = "MuckClone/PowerUp Data", order = 3)]
    public class PowerUpData : ScriptableObject
    {
        [Header("Identificação")]
        public string id = "danis_sneaker";
        public string powerUpName = "Dani's Sneaker";
        [TextArea(2, 3)]
        public string description = "Aumenta a velocidade de movimento em +15% e a força do pulo.";
        public Sprite icon;
        public Color themeColor = new Color(0.2f, 0.75f, 1.0f);
        public PowerUpRarity rarity = PowerUpRarity.Common;

        [Header("Modificadores de Atributos por Stack")]
        public StatModifier[] modifiers;
    }
}
