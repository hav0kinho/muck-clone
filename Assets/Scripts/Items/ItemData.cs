using UnityEngine;

namespace MuckClone.Items
{
    public enum ItemCategory
    {
        Resource,
        Tool,
        Weapon,
        Consumable,
        Armor
    }

    public enum ToolType
    {
        None,
        Axe,
        Pickaxe,
        Sword
    }

    /// <summary>
    /// Definição Data-Driven de itens no jogo (compatível com Muck).
    /// Suporta ferramentas com Tiers, recursos acumuláveis e armas.
    /// </summary>
    [CreateAssetMenu(fileName = "NewItemData", menuName = "MuckClone/Item Data", order = 1)]
    public class ItemData : ScriptableObject
    {
        [Header("Identificação Básica")]
        public string id = "item_wood";
        public string itemName = "Madeira";
        [TextArea(2, 3)]
        public string description = "Tronco de madeira básica para construção e ferramentas.";
        public Sprite icon;
        public Color fallbackColor = new Color(0.6f, 0.4f, 0.2f);

        [Header("Categoria & Empilhamento")]
        public ItemCategory category = ItemCategory.Resource;
        public int maxStack = 64;

        [Header("Propriedades de Ferramenta / Arma")]
        public ToolType toolType = ToolType.None;
        [Tooltip("0: Mão/Madeira, 1: Sílex/Casca, 2: Pedra, 3: Ferro, 4: Ouro, 5: Mitril, 6: Adamantite")]
        [Range(0, 6)]
        public int tier = 0;
        public float damage = 8f;
        public float attackCooldown = 0.4f;
        [Tooltip("Alcance do ataque/coleta com esta ferramenta")]
        public float attackRange = 3.2f;

        [Header("Prefabs Visuais")]
        [Tooltip("Prefab do item no chão com física 3D")]
        public GameObject dropPrefab;
        [Tooltip("Prefab segurado na mão do jogador em 1ª pessoa")]
        public GameObject heldPrefab;
    }
}
