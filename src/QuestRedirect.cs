using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2GlobalStorage
{
    // Stand-ins for the player-bag item calls inside quest and dialog code (swapped in by a transpiler).
    // Each has the instance as first parameter, so it can replace the original call one-for-one.
    // Calls on any inventory other than the player's bag behave exactly like the original.
    public static class QuestRedirect
    {
        private static bool IsPlayerBag(Item data)
        {
            PlayerData player = MainGame.PlayerData;
            return Config.Enabled && Config.Quests && data != null && player?.inventory != null && ReferenceEquals(data, player.inventory.Data);
        }

        private static bool IsPlayerBag(Inventory inventory)
        {
            PlayerData player = MainGame.PlayerData;
            return Config.Enabled && Config.Quests && inventory != null && ReferenceEquals(inventory, player?.inventory);
        }

        public static bool HasItemQuantityInInventory(Item data, string itemId, int count)
        {
            if (data.HasItemQuantityInInventory(itemId, count))
            {
                return true;
            }
            try
            {
                if (IsPlayerBag(data))
                {
                    return data.GetTotalCountInInventory(itemId) + GlobalStorage.CountInChests(itemId) >= count;
                }
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Quest HasItemQuantity", ex);
            }
            return false;
        }

        public static int GetTotalCountInInventory(Item data, string itemId, Item ignoredBag, bool ignoreAllBags)
        {
            int count = data.GetTotalCountInInventory(itemId, ignoredBag, ignoreAllBags);
            try
            {
                if (IsPlayerBag(data))
                {
                    count += GlobalStorage.CountInChests(itemId);
                }
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Quest GetTotalCount", ex);
            }
            return count;
        }

        // Bag first, the rest from the chests. count = -1 ("remove all") stays bag-only.
        public static List<Item> RemoveItemById(Inventory inventory, string itemId, int count, Item ignoredBag, Item sourceBag, bool ignoreAllBags)
        {
            List<Item> removed = inventory.RemoveItemById(itemId, count, ignoredBag, sourceBag, ignoreAllBags);
            try
            {
                if (count > 0 && IsPlayerBag(inventory))
                {
                    int left = count;
                    for (int i = 0; i < removed.Count; i++)
                    {
                        left -= removed[i].Count;
                    }
                    if (left > 0)
                    {
                        List<Item> fromChests = GlobalStorage.RemoveFromChests(itemId, left);
                        removed.AddRange(fromChests);
                        Debug.Log($"[GK2GlobalStorage] Quest took {itemId} x{count}: {count - left} from bag, {Sum(fromChests)} from chests");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Quest RemoveItemById", ex);
            }
            return removed;
        }

        private static int Sum(List<Item> items)
        {
            int n = 0;
            for (int i = 0; i < items.Count; i++)
            {
                n += items[i].Count;
            }
            return n;
        }
    }
}
