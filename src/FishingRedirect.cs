using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2GlobalStorage
{
    // Bait in the fishing code: the bag's item calls of the water check and the bait list are swapped for the stand-ins
    // below (FishingPatches), each with the instance as first parameter so it replaces the original call one-for-one;
    // casting takes what the bag lacks from the chests. Bag first, then every chest; any other inventory behaves
    // exactly like in the game.
    public static class FishingRedirect
    {
        private static bool IsPlayerBag(Item data)
        {
            PlayerData player = MainGame.PlayerData;
            return Config.Enabled && Config.Fishing && data != null && player?.inventory != null && ReferenceEquals(data, player.inventory.Data);
        }

        private static bool IsPlayerBag(Inventory inventory)
        {
            PlayerData player = MainGame.PlayerData;
            return Config.Enabled && Config.Fishing && inventory != null && ReferenceEquals(inventory, player?.inventory);
        }

        // The check at the water: bait stacks in the bag, then those in the chests.
        public static List<Item> GetItemsByType(Inventory inventory, ItemType itemType, bool includeBagContents)
        {
            List<Item> items = inventory.GetItemsByType(itemType, includeBagContents);
            try
            {
                if (IsPlayerBag(inventory))
                {
                    List<Inventory> chests = GlobalStorage.All();
                    for (int i = 0; i < chests.Count; i++)
                    {
                        items.AddRange(chests[i].GetItemsByType(itemType, includeBagContents));
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Fishing GetItemsByType", ex);
            }
            return items;
        }

        // Bait counts in the fishing window.
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
                Plugin.ReportOnce("Fishing GetTotalCount", ex);
            }
            return count;
        }

        // Casting took the bait from the bag (inventory, after Inventory.RemoveItemById returned removed): what the bag
        // was short of comes from the chests and is added to removed. count = -1 ("remove all") stays bag-only.
        public static void TakeRestFromChests(Inventory inventory, string itemId, int count, List<Item> removed)
        {
            try
            {
                if (count > 0 && removed != null && IsPlayerBag(inventory))
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
                        Debug.Log($"[GK2GlobalStorage] Fishing took bait {itemId} x{count}: {count - left} from bag, {Sum(fromChests)} from chests");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Fishing bait from chests", ex);
            }
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
