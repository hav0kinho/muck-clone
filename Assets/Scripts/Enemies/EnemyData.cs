using MuckClone.Items;
using UnityEngine;

namespace MuckClone.Enemies
{
    /// <summary>
    /// Definição Data-Driven de atributos e comportamento de inimigos (Goblins, Golens, etc.).
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemyData", menuName = "MuckClone/Enemy Data", order = 2)]
    public class EnemyData : ScriptableObject
    {
        [Header("Identificação")]
        public string enemyId = "goblin";
        public string enemyName = "Goblin";

        [Header("Atributos de Vida & Movimento")]
        public float maxHealth = 65f;
        public float moveSpeed = 5.8f;
        public float acceleration = 16f;
        public float stoppingDistance = 1.5f;

        [Header("Combate")]
        public float attackDamage = 12f;
        public float attackCooldown = 1.0f;
        public float attackRange = 1.8f;
        [Tooltip("Força do empurrão que o monstro causa no jogador ao acertar um golpe")]
        public float playerKnockbackForce = 10f;
        [Tooltip("Multiplicador do knockback que este inimigo recebe ao ser atingido (menor para golens, maior para goblins)")]
        [Range(0.1f, 2f)]
        public float knockbackVulnerability = 1.0f;

        [Header("Percepção & IA")]
        public float aggroRadius = 22f;
        public float loseAggroRadius = 35f;

        [Header("Recompensas (Drops)")]
        public ItemData dropItem;
        public int minDrop = 1;
        public int maxDrop = 3;

        [Header("Visual Fallback (Prototipagem Estilizada)")]
        public Color fallbackColor = new Color(0.25f, 0.75f, 0.25f);
        public Vector3 fallbackScale = new Vector3(0.9f, 1.7f, 0.9f);
        public GameObject customPrefab;
    }
}
