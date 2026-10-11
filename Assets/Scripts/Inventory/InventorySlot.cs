using System;
using MuckClone.Items;
using UnityEngine;

namespace MuckClone.Inventory
{
    /// <summary>
    /// Representa um slot de inventário contendo um item e sua quantidade acumulada.
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        public ItemData item;
        public int amount;

        public bool IsEmpty => item == null || amount <= 0;

        public InventorySlot()
        {
            item = null;
            amount = 0;
        }

        public InventorySlot(ItemData item, int amount)
        {
            this.item = item;
            this.amount = amount;
        }

        public bool CanStack(ItemData otherItem)
        {
            if (IsEmpty) return true;
            if (item != otherItem) return false;
            return amount < item.maxStack;
        }

        public void Clear()
        {
            item = null;
            amount = 0;
        }
    }
}
