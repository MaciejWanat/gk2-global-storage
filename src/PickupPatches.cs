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
    // to the bag as usual. Manual moves between bag and chests are not affected. In the factory only with
    // PickupsInFactory: picked-up items would land in the chests that feed the belts.
    internal static class PickupPatches
    {
        private static FieldInfo dropCollectedEvent;

        // ItemDef.isQuestItem, read by reflection: some game builds players run do not have it, and naming it in code
        // made the whole prefix fail there, so nothing could be picked up (see Guard).
        private static FieldInfo questItemField;

        public static bool Apply(Harmony harmony)
        {
            MethodInfo collect = AccessTools.Method(typeof(PlayerData), nameof(PlayerData.CollectDrop));
            dropCollectedEvent = AccessTools.Field(typeof(PlayerData), "OnDropCollected");
            questItemField = AccessTools.Field(typeof(ItemDef), "isQuestItem");
            if (collect == null)
            {
                Debug.LogWarning("[GK2GlobalStorage] Pickup patch: PlayerData.CollectDrop not found (game update?)");
                return false;
            }
            if (questItemField == null || questItemField.FieldType != typeof(bool))
            {
                questItemField = null;
                Debug.Log("[GK2GlobalStorage] Pickup patch: this game version has no ItemDef.isQuestItem; quest items are treated like other items");
            }
            harmony.Patch(collect, prefix: new HarmonyMethod(typeof(PickupPatches), nameof(CollectPrefix)));
            return true;
        }

        internal static bool IsQuestItem(ItemDef definition)
        {
            return questItemField != null && definition != null && (bool)questItemField.GetValue(definition);
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
                if (drop.Item?.Definition == null || IsQuestItem(drop.Item.Definition) || IsPinned(__instance, drop.Item))
                {
                    return true;
                }
                WorldZoneData zone = __instance.CurrentWorldZoneData;
                PlayerController player = MainGame.PlayerController;
                if (!ChestsTakePickups(zone) || player == null)
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

        // Whether pickups go to the chests of this zone: not in the factory's belt hall and storage room unless
        // PickupsInFactory is on.
        internal static bool ChestsTakePickups(WorldZoneData zone)
        {
            if (zone == null)
            {
                return false;
            }
            return Config.PickupsInFactory
                || (zone.id != LazyConsts.CONVEYOR_WORLD_ZONE_ID && zone.id != LazyConsts.CONVEYOR_STORAGE_WORLD_ZONE_ID);
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
