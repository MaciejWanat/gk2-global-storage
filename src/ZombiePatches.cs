using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GK2GlobalStorage
{
    // Option "Zombie workers". A zombie at a workbench only works with what a zombie carrier (caretaker)
    // brings it, and carriers only walk to the chests of their own area. With the option on:
    //  - when the area's chests do not hold enough for a delivery, the missing amount is moved from chests in
    //    other areas into a chest of this area (one that can hold it), so the carrier can fetch it;
    //  - the carrier walks to the nearest chest that holds any of the item (and comes back for the rest)
    //    instead of the nearest one that holds the whole amount, which can be far away.
    internal static class ZombiePatches
    {
        private const int RetryMs = 3000;

        private static MethodInfo executingOrder;
        private static FieldInfo currentTarget;
        private static MethodInfo moveToTarget;

        // Zone + item that could not be topped up recently; carriers ask again every frame.
        private static readonly Dictionary<string, int> notAvailable = new Dictionary<string, int>();

        public static int Apply(Harmony harmony)
        {
            MethodInfo pickUp = AccessTools.Method(typeof(ZombieWgoData), "CaretakerTryMoveToNearestInventoryWithRequiredItemCountToPickUp");
            MethodInfo canTake = AccessTools.Method(typeof(WorldZoneData), nameof(WorldZoneData.CanDeliveryOrderBeTakenOnExecution));
            executingOrder = AccessTools.PropertyGetter(typeof(ZombieWgoData), "CaretakerExecutingOrder");
            currentTarget = AccessTools.Field(typeof(ZombieWgoData), "caretakerCurrentTargetUniqueId");
            moveToTarget = AccessTools.Method(typeof(ZombieWgoData), "CaretakerTryMoveToCurrentTarget");
            if (pickUp == null || canTake == null || executingOrder == null || currentTarget == null || moveToTarget == null)
            {
                Debug.LogWarning("[GK2GlobalStorage] Zombie carrier patch: game methods not found (game update?)");
                return 0;
            }
            harmony.Patch(canTake, postfix: new HarmonyMethod(typeof(ZombiePatches), nameof(CanTakePostfix)));
            harmony.Patch(pickUp, prefix: new HarmonyMethod(typeof(ZombiePatches), nameof(NearestPrefix)));
            return 1;
        }

        private static bool Accepts(WgoData chest, ItemDef def)
        {
            WGODef wgoDef = chest.Definition;
            return wgoDef.inventoryWhiteList.Contains(def) && !wgoDef.inventoryBlackList.Contains(def);
        }

        // The area cannot fill the order: bring the missing amount over from other areas.
        private static void CanTakePostfix(WorldZoneData __instance, DeliveryOrder deliveryOrder, int currentCount, ref bool __result)
        {
            if (__result || !Config.Enabled || !Config.ZombieWorkers || deliveryOrder?.Item == null)
            {
                return;
            }
            try
            {
                Item need = deliveryOrder.Item;
                string key = __instance.id + "|" + need.id;
                if (notAvailable.TryGetValue(key, out int until) && unchecked(Environment.TickCount - until) < 0)
                {
                    return;
                }
                if (Config.ExcludedZones.Contains(__instance.id ?? ""))
                {
                    return;
                }
                long t = Perf.Begin();
                bool moved = TopUp(__instance, need, currentCount);
                Perf.End("Zombie delivery top-up", t);
                if (moved)
                {
                    notAvailable.Remove(key);
                    __result = true;
                }
                else
                {
                    notAvailable[key] = unchecked(Environment.TickCount + RetryMs);
                }
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Zombie delivery top-up", ex);
            }
        }

        private static bool TopUp(WorldZoneData zone, Item need, int currentCount)
        {
            ItemDef def = need.Definition;
            HashSet<Inventory> zoneChests = new HashSet<Inventory>(GlobalStorage.RefComparer.Instance);
            List<Inventory> targets = new List<Inventory>();
            int inZone = 0;
            foreach (WgoData chest in zone.MultiInventoryWgoDatas)
            {
                if (chest?.Inventory?.Data == null)
                {
                    continue;
                }
                zoneChests.Add(chest.Inventory);
                if (Accepts(chest, def))
                {
                    int count = chest.Inventory.Data.GetTotalCountInInventory(need.id);
                    inZone += count;
                    // Chests that already hold the item first, so it stays together.
                    if (count > 0)
                    {
                        targets.Insert(0, chest.Inventory);
                    }
                    else
                    {
                        targets.Add(chest.Inventory);
                    }
                }
            }
            int missing = need.Count - currentCount - inZone;
            if (missing <= 0 || targets.Count == 0)
            {
                return false;
            }

            List<Inventory> sources = new List<Inventory>();
            int available = 0;
            foreach (Inventory chest in GlobalStorage.All())
            {
                if (available >= missing || zoneChests.Contains(chest))
                {
                    continue;
                }
                int count = chest.Data.GetTotalCountInInventory(need.id);
                if (count > 0)
                {
                    sources.Add(chest);
                    available += count;
                }
            }
            int room = 0;
            for (int i = 0; i < targets.Count && room < missing; i++)
            {
                room += targets[i].Data.CanAddItemCountToInventory(new Item(need.id, missing));
            }
            if (available < missing || room < missing)
            {
                return false;
            }

            int left = missing;
            for (int s = 0; s < sources.Count && left > 0; s++)
            {
                foreach (Item stack in sources[s].RemoveItemById(need.id, left))
                {
                    left -= stack.Count;
                    // The game adds what fits and keeps the rest on the stack (non-stackable items one at a time).
                    for (int i = 0; i < targets.Count && stack.Count > 0; i++)
                    {
                        while (stack.Count > 0 && targets[i].AddItemToInventory(stack, out _))
                        {
                        }
                    }
                    if (stack.Count > 0)
                    {
                        // Should not happen (room was checked): put it back where it came from.
                        left += stack.Count;
                        while (stack.Count > 0 && sources[s].AddItemToInventory(stack, out _))
                        {
                        }
                    }
                }
            }
            GlobalStorage.Invalidate();
            Debug.Log("[GK2GlobalStorage] Zombie delivery: " + (missing - left) + " x " + need.id
                + " moved from other areas into " + zone.id);
            return left <= 0;
        }

        // Same as the game, except the nearest chest holding any of the item wins.
        private static bool NearestPrefix(ZombieWgoData __instance)
        {
            if (!Config.Enabled || !Config.ZombieWorkers)
            {
                return true;
            }
            try
            {
                OrderBase order = executingOrder.Invoke(__instance, null) as OrderBase;
                Item item = order?.Item;
                WorldZoneData zone = __instance.WorldZoneData;
                if (item == null || zone == null)
                {
                    return true;
                }
                Item carried = __instance.CaretakerPortableItem;
                int carriedCount = carried == null || carried.IsEmpty ? 0 : carried.Count;
                if (!zone.CanDeliveryOrderBeTakenOnExecution(order as DeliveryOrder, carriedCount))
                {
                    return true;
                }
                WgoData best = null;
                float bestDistance = float.MaxValue;
                foreach (WgoData chest in zone.MultiInventoryWgoDatas)
                {
                    if (chest?.Inventory?.Data == null || chest.Inventory.Data.GetTotalCountInInventory(item.id) <= 0)
                    {
                        continue;
                    }
                    float distance = (chest.Position - __instance.Position).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = chest;
                    }
                }
                if (best == null)
                {
                    return true;
                }
                currentTarget.SetValue(__instance, best.UniqueId);
                moveToTarget.Invoke(__instance, null);
                return false;
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Zombie carrier nearest chest", ex);
                return true;
            }
        }
    }
}
