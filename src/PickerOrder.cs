using System;
using System.Collections.Generic;

namespace GK2GlobalStorage
{
    // Item pickers with every chest listed can get very long. The bag stays first; after it, chests holding
    // something the picker accepts come before the rest, each group keeping the game's order.
    internal static class PickerOrder
    {
        public static void UsableFirst(MultiInventory multiInventory, Func<Item, bool> usable)
        {
            long t = Perf.Begin();
            List<Inventory> list = multiInventory.inventoryList;
            Inventory bag = MainGame.PlayerData?.inventory;
            int start = list.Count > 0 && ReferenceEquals(list[0], bag) ? 1 : 0;
            List<Inventory> rest = null;
            int write = start;
            for (int i = start; i < list.Count; i++)
            {
                Inventory inventory = list[i];
                if (HasUsable(inventory, usable))
                {
                    list[write++] = inventory;
                }
                else
                {
                    (rest ??= new List<Inventory>()).Add(inventory);
                }
            }
            if (rest != null)
            {
                for (int i = 0; i < rest.Count; i++)
                {
                    list[write++] = rest[i];
                }
            }
            Perf.End("Picker chest order", t);
        }

        internal static bool HasUsable(Inventory inventory, Func<Item, bool> usable)
        {
            List<Item> items = inventory?.Data?.Inventory;
            if (items == null)
            {
                return false;
            }
            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                if (item != null && !item.IsEmpty && usable(item))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
