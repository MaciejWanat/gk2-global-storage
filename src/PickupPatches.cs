using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GK2GlobalStorage
{
    // Optional: items you pick up from the ground (harvest, chopping, mining, loot, your workbench craft
    // results, which the game drops for you to collect) go to the nearest chest with room in the area
    // (zone) you are standing in. Whatever does not fit, or everything when there is no such chest, goes
    // to the bag as usual. Manual moves between bag and chests are not affected.
    internal static class PickupPatches
    {
        private static FieldInfo dropCollectedEvent;

        public static int Apply(Harmony harmony)
        {
            MethodInfo collect = AccessTools.Method(typeof(PlayerData), nameof(PlayerData.CollectDrop));
            dropCollectedEvent = AccessTools.Field(typeof(PlayerData), "OnDropCollected");
            if (collect == null)
            {
                Debug.LogWarning("[GK2GlobalStorage] Pickup patch: PlayerData.CollectDrop not found (game update?)");
                return 0;
            }
            harmony.Patch(collect, prefix: new HarmonyMethod(typeof(PickupPatches), nameof(CollectPrefix)));
            return 1;
        }

        // Returns false (skip the game's method) only when the whole drop went into chests.
        private static bool CollectPrefix(PlayerData __instance, DropView dropView)
        {
            try
            {
                if (!Config.Enabled || !Config.PickupsToChest || !ReferenceEquals(__instance, MainGame.PlayerData))
                {
                    return true;
                }
                DropData drop = dropView?.Data;
                if (drop == null || drop.Count <= 0 || drop.IsResDrop || drop.IsRemoving || drop.Size == ItemSize.Big)
                {
                    return true;
                }
                // Quest items, and items pinned to the quick bar (it uses them from the bag), always go to the bag.
                if (drop.Item?.Definition == null || drop.Item.Definition.isQuestItem || IsPinned(__instance, drop.Item))
                {
                    return true;
                }
                WorldZoneData zone = __instance.CurrentWorldZoneData;
                PlayerController player = MainGame.PlayerController;
                if (zone == null || player == null)
                {
                    return true;
                }
                Vector3 from = player.transform.position;
                List<Item> stored = new List<Item>();
                HashSet<Inventory> full = new HashSet<Inventory>();
                while (drop.Count > 0)
                {
                    Inventory chest = NearestWithRoom(zone, drop.Item, from, full);
                    if (chest == null)
                    {
                        break;
                    }
                    int before = drop.Count;
                    if (!chest.AddItemToInventory(drop.Item, out List<Item> added) || drop.Count >= before)
                    {
                        full.Add(chest);
                        continue;
                    }
                    if (added != null)
                    {
                        stored.AddRange(added);
                    }
                }
                if (stored.Count == 0)
                {
                    return true;
                }
                // Same side effects as a normal pickup: item expressions and the pickup notification.
                foreach (LazyExpression expression in stored[0].Definition.onDropCollected)
                {
                    expression.Evaluate(stored[0]);
                }
                (dropCollectedEvent?.GetValue(__instance) as Action<List<Item>>)?.Invoke(stored);
                if (drop.Count == 0)
                {
                    MainGame.Instance.dropSystem.RemoveDrop(drop, drop.WorldId);
                    return false;
                }
                drop.NotifyCountChanged();
                return true;
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Pickup to chest", ex);
                return true;
            }
        }

        private static bool IsPinned(PlayerData player, Item item)
        {
            string[] pinned = player.pinnedItems;
            if (pinned == null || item == null || string.IsNullOrEmpty(item.id))
            {
                return false;
            }
            return Array.IndexOf(pinned, item.id) >= 0;
        }

        private static Inventory NearestWithRoom(WorldZoneData zone, Item item, Vector3 from, HashSet<Inventory> skip)
        {
            WorldData worldData = MainGame.Instance?.GameSave?.worldData;
            if (worldData == null)
            {
                return null;
            }
            Inventory best = null;
            float bestDistance = float.MaxValue;
            foreach (SGuid guid in zone.wgoDataList)
            {
                WgoData wgo = worldData.GetWgoData(guid);
                Inventory inventory = GlobalStorage.Storage(wgo);
                if (inventory == null || skip.Contains(inventory) || inventory.Data.CanAddItemCountToInventory(item) <= 0)
                {
                    continue;
                }
                float distance = (wgo.Position - from).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = inventory;
                }
            }
            return best;
        }
    }
}
