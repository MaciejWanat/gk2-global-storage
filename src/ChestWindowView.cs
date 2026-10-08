using System.Collections.Generic;
using UnityEngine;

namespace GK2GlobalStorage
{
    // Chest windows (option "Chest windows", off by default). When you open a chest, the game builds the list
    // of the area's other chests (MultiInventory(WorldZoneData, opened chest)) and shows them view only next
    // to your bag. While a chest is being opened, the chests of all other areas are appended to that list, so
    // the game draws them the same way: view only, nothing can be moved or duplicated through them.
    //
    // Done on that list rather than on the chest window itself, because other mods (No More Running Back)
    // speed up the chest window and step aside when another mod patches it. Its speed-up then also covers
    // the extra chests.
    internal static class ChestWindowView
    {
        public static void AppendOtherChests(MultiInventory areaChests, WgoData opened)
        {
            if (areaChests == null)
            {
                return;
            }
            HashSet<Inventory> shown = new HashSet<Inventory>(areaChests.inventoryList, GlobalStorage.RefComparer.Instance);
            if (opened?.Inventory != null)
            {
                shown.Add(opened.Inventory);
            }
            int added = 0;
            foreach (Inventory inventory in GlobalStorage.All())
            {
                if (shown.Add(inventory))
                {
                    areaChests.inventoryList.Add(inventory);
                    added++;
                }
            }
            if (added > 0)
            {
                Debug.Log("[GK2GlobalStorage] Chest window: " + added + " chest(s) from other areas listed");
            }
        }
    }
}
