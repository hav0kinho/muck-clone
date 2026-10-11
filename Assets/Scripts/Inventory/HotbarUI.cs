using MuckClone.Items;
using UnityEngine;

namespace MuckClone.Inventory
{
    /// <summary>
    /// HUD de Hotbar renderizado em tempo real (OnGUI).
    /// Exibe os 5 slots na base da tela, slot ativo destacado, quantidades, Tiers e mira central (+).
    /// Funciona imediatamente sem depender de Canvas pré-configurado.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class HotbarUI : MonoBehaviour
    {
        [Header("Estilo Visual")]
        [SerializeField] private bool showCrosshair = true;
        [SerializeField] private Color activeSlotColor = new Color(1f, 0.85f, 0.2f, 0.9f);
        [SerializeField] private Color inactiveSlotColor = new Color(0.12f, 0.12f, 0.14f, 0.8f);

        private PlayerInventory _inventory;
        private GUIStyle _slotStyle;
        private GUIStyle _activeSlotStyle;
        private GUIStyle _itemLabelStyle;
        private GUIStyle _countLabelStyle;
        private GUIStyle _keyLabelStyle;
        private GUIStyle _crosshairStyle;

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
        }

        private void OnGUI()
        {
            InitializeStyles();

            if (showCrosshair)
            {
                DrawCrosshair();
            }

            DrawHotbar();
        }

        private void InitializeStyles()
        {
            if (_slotStyle != null) return;

            Texture2D inactiveBg = MakeTex(2, 2, inactiveSlotColor);
            Texture2D activeBg = MakeTex(2, 2, new Color(0.22f, 0.25f, 0.32f, 0.95f));

            _slotStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = inactiveBg },
                border = new RectOffset(4, 4, 4, 4)
            };

            _activeSlotStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = activeBg },
                border = new RectOffset(4, 4, 4, 4)
            };

            _keyLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.7f, 0.7f, 0.75f) },
                alignment = TextAnchor.UpperLeft
            };

            _itemLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            _countLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.9f, 0.4f) },
                alignment = TextAnchor.LowerRight
            };

            _crosshairStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 1f, 1f, 0.75f) },
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void DrawCrosshair()
        {
            float size = 20f;
            float x = (Screen.width - size) * 0.5f;
            float y = (Screen.height - size) * 0.5f;
            GUI.Label(new Rect(x, y, size, size), "+", _crosshairStyle);
        }

        private void DrawHotbar()
        {
            if (_inventory == null) return;

            float slotWidth = 72f;
            float slotHeight = 72f;
            float spacing = 8f;
            float totalWidth = (slotWidth * PlayerInventory.HotbarSlotCount) + (spacing * (PlayerInventory.HotbarSlotCount - 1));

            float startX = (Screen.width - totalWidth) * 0.5f;
            float startY = Screen.height - slotHeight - 20f;

            for (int i = 0; i < PlayerInventory.HotbarSlotCount; i++)
            {
                float x = startX + i * (slotWidth + spacing);
                Rect slotRect = new Rect(x, startY, slotWidth, slotHeight);

                bool isActive = (i == _inventory.ActiveHotbarIndex);
                GUIStyle currentStyle = isActive ? _activeSlotStyle : _slotStyle;

                // Moldura do slot
                GUI.Box(slotRect, GUIContent.none, currentStyle);

                // Borda dourada de destaque se for o slot selecionado
                if (isActive)
                {
                    DrawOutline(slotRect, 2, activeSlotColor);
                }

                // Número do atalho (1, 2, 3, 4, 5)
                GUI.Label(new Rect(x + 5, startY + 3, 20, 16), (i + 1).ToString(), _keyLabelStyle);

                InventorySlot slot = _inventory.GetSlot(i);
                if (slot != null && !slot.IsEmpty && slot.item != null)
                {
                    // Mini indicador de cor da categoria/material
                    Rect colorBadge = new Rect(x + slotWidth - 14, startY + 5, 9, 9);
                    DrawColorBox(colorBadge, slot.item.fallbackColor);

                    // Nome do item e Tier
                    string displayName = slot.item.itemName;
                    if (slot.item.category == ItemCategory.Tool || slot.item.category == ItemCategory.Weapon)
                    {
                        displayName += $"\n[T{slot.item.tier}]";
                    }

                    GUI.Label(new Rect(x + 4, startY + 14, slotWidth - 8, slotHeight - 28), displayName, _itemLabelStyle);

                    // Quantidade acumulada no canto inferior direito
                    if (slot.amount > 1)
                    {
                        GUI.Label(new Rect(x + 6, startY + slotHeight - 22, slotWidth - 12, 18), $"x{slot.amount}", _countLabelStyle);
                    }
                }
            }
        }

        private void DrawOutline(Rect rect, int thickness, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void DrawColorBox(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
