using System;
using MuckClone.Items;
using MuckClone.Player;
using UnityEngine;

namespace MuckClone.Inventory
{
    /// <summary>
    /// Gerencia o inventário do jogador e os 5 slots da Hotbar.
    /// Responde a atalhos de teclado (1-5), rolagem do mouse e orquestra o item segurado em primeira pessoa.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        public const int HotbarSlotCount = 5;
        public const int TotalInventorySlotCount = 20;

        [Header("Slots do Inventário")]
        [SerializeField] private InventorySlot[] slots = new InventorySlot[TotalInventorySlotCount];

        [Header("Itens Iniciais para Teste")]
        [SerializeField] private ItemData startingTool;
        [SerializeField] private ItemData startingResource;

        [Header("Ponto de Empunhadura (Mão em 1ª Pessoa)")]
        [Tooltip("Transform onde o modelo do item ativo é instanciado na visão da câmera")]
        [SerializeField] private Transform handHoldPoint;

        public int ActiveHotbarIndex { get; private set; } = 0;
        public InventorySlot ActiveSlot => slots[ActiveHotbarIndex];
        public ItemData ActiveItem => slots[ActiveHotbarIndex]?.item;

        public event Action<int, InventorySlot> OnSlotChanged;
        public event Action<int> OnActiveHotbarChanged;

        private PlayerInputReader _inputReader;
        private GameObject _currentHeldInstance;

        private void Awake()
        {
            // Inicializa slots nulos se necessário
            for (int i = 0; i < TotalInventorySlotCount; i++)
            {
                if (slots[i] == null) slots[i] = new InventorySlot();
            }

            _inputReader = GetComponent<PlayerInputReader>() ?? GetComponentInParent<PlayerInputReader>();
        }

        private void Start()
        {
            // Equipamento inicial para testes se os slots estiverem vazios
            if (startingTool != null && slots[0].IsEmpty)
            {
                slots[0] = new InventorySlot(startingTool, 1);
            }
            if (startingResource != null && slots[1].IsEmpty)
            {
                slots[1] = new InventorySlot(startingResource, 10);
            }

            UpdateHeldItemVisual();
            NotifyAllSlotsChanged();
            OnActiveHotbarChanged?.Invoke(ActiveHotbarIndex);
        }

        private void OnEnable()
        {
            if (_inputReader != null)
            {
                _inputReader.OnHotbarSlotSelected += SelectHotbarIndex;
                _inputReader.OnHotbarScroll += HandleScroll;
            }
        }

        private void OnDisable()
        {
            if (_inputReader != null)
            {
                _inputReader.OnHotbarSlotSelected -= SelectHotbarIndex;
                _inputReader.OnHotbarScroll -= HandleScroll;
            }
        }

        public void SelectHotbarIndex(int index)
        {
            if (index < 0 || index >= HotbarSlotCount || index == ActiveHotbarIndex) return;

            ActiveHotbarIndex = index;
            UpdateHeldItemVisual();
            OnActiveHotbarChanged?.Invoke(ActiveHotbarIndex);
        }

        private void HandleScroll(float scrollDelta)
        {
            int newIndex = ActiveHotbarIndex;
            if (scrollDelta > 0)
            {
                newIndex = (newIndex - 1 + HotbarSlotCount) % HotbarSlotCount;
            }
            else if (scrollDelta < 0)
            {
                newIndex = (newIndex + 1) % HotbarSlotCount;
            }

            SelectHotbarIndex(newIndex);
        }

        /// <summary>
        /// Adiciona um item ao inventário respeitando empilhamento máximo.
        /// Retorna a quantidade restante que não coube (0 = tudo adicionado).
        /// </summary>
        public int AddItem(ItemData item, int amountToAdd)
        {
            if (item == null || amountToAdd <= 0) return 0;

            int remaining = amountToAdd;

            // 1. Tenta empilhar em slots existentes com o mesmo item
            for (int i = 0; i < TotalInventorySlotCount; i++)
            {
                if (!slots[i].IsEmpty && slots[i].item == item && slots[i].amount < item.maxStack)
                {
                    int spaceInSlot = item.maxStack - slots[i].amount;
                    int toAdd = Mathf.Min(spaceInSlot, remaining);

                    slots[i].amount += toAdd;
                    remaining -= toAdd;
                    OnSlotChanged?.Invoke(i, slots[i]);

                    if (remaining <= 0)
                    {
                        if (i == ActiveHotbarIndex) UpdateHeldItemVisual();
                        return 0;
                    }
                }
            }

            // 2. Procura primeiro slot vazio
            for (int i = 0; i < TotalInventorySlotCount; i++)
            {
                if (slots[i].IsEmpty)
                {
                    int toAdd = Mathf.Min(item.maxStack, remaining);
                    slots[i].item = item;
                    slots[i].amount = toAdd;
                    remaining -= toAdd;
                    OnSlotChanged?.Invoke(i, slots[i]);

                    if (i == ActiveHotbarIndex) UpdateHeldItemVisual();

                    if (remaining <= 0) return 0;
                }
            }

            return remaining;
        }

        /// <summary>
        /// Remove uma quantidade específica de um item do inventário.
        /// </summary>
        public bool RemoveItem(ItemData item, int amountToRemove)
        {
            if (item == null || amountToRemove <= 0) return false;
            if (GetTotalItemCount(item) < amountToRemove) return false;

            int remainingToRemove = amountToRemove;

            for (int i = TotalInventorySlotCount - 1; i >= 0; i--)
            {
                if (!slots[i].IsEmpty && slots[i].item == item)
                {
                    if (slots[i].amount <= remainingToRemove)
                    {
                        remainingToRemove -= slots[i].amount;
                        slots[i].Clear();
                    }
                    else
                    {
                        slots[i].amount -= remainingToRemove;
                        remainingToRemove = 0;
                    }

                    OnSlotChanged?.Invoke(i, slots[i]);
                    if (i == ActiveHotbarIndex) UpdateHeldItemVisual();

                    if (remainingToRemove <= 0) break;
                }
            }

            return true;
        }

        public int GetTotalItemCount(ItemData item)
        {
            int total = 0;
            for (int i = 0; i < TotalInventorySlotCount; i++)
            {
                if (!slots[i].IsEmpty && slots[i].item == item)
                {
                    total += slots[i].amount;
                }
            }
            return total;
        }

        public InventorySlot GetSlot(int index)
        {
            if (index < 0 || index >= TotalInventorySlotCount) return null;
            return slots[index];
        }

        private void UpdateHeldItemVisual()
        {
            if (_currentHeldInstance != null)
            {
                Destroy(_currentHeldInstance);
                _currentHeldInstance = null;
            }

            if (handHoldPoint == null)
            {
                // Tenta achar ou criar um ponto de empunhadura na câmera do player
                var cam = GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    var existingHold = cam.transform.Find("HandHoldPoint");
                    if (existingHold != null)
                    {
                        handHoldPoint = existingHold;
                    }
                    else
                    {
                        var newHold = new GameObject("HandHoldPoint");
                        newHold.transform.SetParent(cam.transform, false);
                        newHold.transform.localPosition = new Vector3(0.42f, -0.32f, 0.65f);
                        newHold.transform.localRotation = Quaternion.Euler(15f, -25f, 5f);
                        handHoldPoint = newHold.transform;
                    }
                }
            }

            if (handHoldPoint == null) return;

            ItemData active = ActiveItem;
            if (active != null)
            {
                if (active.heldPrefab != null)
                {
                    _currentHeldInstance = Instantiate(active.heldPrefab, handHoldPoint);
                }
                else
                {
                    // Fallback visual estilizado (pequena ferramenta/recurso segurado na mão)
                    _currentHeldInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(_currentHeldInstance.GetComponent<Collider>());
                    _currentHeldInstance.transform.SetParent(handHoldPoint, false);
                    _currentHeldInstance.transform.localScale = new Vector3(0.12f, 0.45f, 0.12f);
                    _currentHeldInstance.transform.localPosition = Vector3.zero;

                    var rend = _currentHeldInstance.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                        rend.material = new Material(s) { color = active.fallbackColor };
                    }
                }
            }
        }

        public void NotifyAllSlotsChanged()
        {
            for (int i = 0; i < TotalInventorySlotCount; i++)
            {
                OnSlotChanged?.Invoke(i, slots[i]);
            }
        }
    }
}
