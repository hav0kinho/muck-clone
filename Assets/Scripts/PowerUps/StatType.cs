using System;
using UnityEngine;

namespace MuckClone.PowerUps
{
    public enum StatType
    {
        MoveSpeed,
        AttackDamage,
        AttackSpeed,
        JumpForce,
        CritChance,
        CritMultiplier,
        Lifesteal,
        MaxHealth,
        HealthRegen
    }

    public enum ModifierType
    {
        Flat,
        Percent
    }

    public enum PowerUpRarity
    {
        Common,
        Rare,
        Legendary
    }

    [Serializable]
    public struct StatModifier
    {
        public StatType statType;
        public ModifierType modifierType;
        [Tooltip("Valor adicionado por cada stack (unidade ou percentual, ex: 0.15 = +15%)")]
        public float valuePerStack;

        public StatModifier(StatType stat, ModifierType mod, float value)
        {
            statType = stat;
            modifierType = mod;
            valuePerStack = value;
        }
    }
}
