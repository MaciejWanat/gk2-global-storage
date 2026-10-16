using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GK2GlobalStorage
{
    // Option "Factory". Factory belts are fed only by their input chests: Conveyor Chests, Garden Storage, Alcohol
    // Storage and Stockpiles, filled by hand, by the cargo lift or by a zombie porter. Every belt step each empty belt
    // cell asks the chest behind it (CanGiveItem) for one item: a Conveyor Chest, Garden Storage or Alcohol Storage gives
    // the item set on that cell's slot, a Stockpile its first stack. With the option on, a chest that has run out of
    // that item first gets a stack of it from the chests in every zone, so the belt keeps going. Slots without an
    // item stay as in the game. A chest that feeds a belt is never a source, so two input chests set to the same item
    // do not pass it back and forth.
    internal static class FactoryPatches
    {
        private const int RetryMs = 3000;

        // Chest + item that could not be refilled recently; belts ask every step.
        private static readonly Dictionary<string, int> notAvailable = new Dictionary<string, int>();

        public static int Apply(Harmony harmony)
        {
            Type[] receiver = { typeof(ConveyorComponent) };
            MethodInfo[] targets =
            {
                AccessTools.DeclaredMethod(typeof(ConveyorChestComponent), nameof(ConveyorComponent.CanGiveItem), receiver),
                AccessTools.DeclaredMethod(typeof(ConveyorChestOutComponent), nameof(ConveyorComponent.CanGiveItem), receiver),
                AccessTools.DeclaredMethod(typeof(ConveyorPalletComponent), nameof(ConveyorComponent.CanGiveItem), receiver),
            };
            if (Array.IndexOf(targets, null) >= 0)
            {
                Debug.LogWarning("[GK2GlobalStorage] Factory patch: game methods not found (game update?)");
                return 0;
            }
            foreach (MethodInfo target in targets)
            {
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(FactoryPatches), nameof(CanGivePrefix)));
            }
            return 1;
        }

        // Before the game checks what the chest can give: refill it if it has run out.
        private static void CanGivePrefix(ConveyorComponent __instance, ConveyorComponent conveyorComponent)
        {
            if (!Config.Enabled || !Config.Factory)
            {
                return;
            }
            try
            {
                Refill(__instance, conveyorComponent);
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Factory top-up", ex);
            }
        }

        // How many items were moved into the giver for the cell asking it.
        internal static int Refill(ConveyorComponent giver, ConveyorComponent cell)
        {
            ConveyorWgoData chest = giver?.WgoData;
            Inventory inventory = chest?.Inventory;
            Inventory cellInventory = cell?.WgoData?.Inventory;
            // A cell only takes an item when it is empty: a stopped belt pulls nothing.
            if (inventory?.Data == null || cellInventory?.Data == null || cellInventory.Data.Inventory.Count > 0)
            {
                return 0;
            }
            // Hidden: the town workshop belts the game switches off (Flow_DisableWorkshopConveyors).
            if (chest.IsHidden || Config.ExcludedZones.Contains(chest.WorldZoneData?.id ?? ""))
            {
                return 0;
            }
            string itemId = WantedItem(giver, cell);
            if (string.IsNullOrEmpty(itemId) || inventory.Data.GetTotalCountInInventory(itemId) > 0)
            {
                return 0;
            }
            string key = chest.UniqueId + "|" + itemId;
            if (notAvailable.TryGetValue(key, out int until) && unchecked(Environment.TickCount - until) < 0)
            {
                return 0;
            }
            long t = Perf.Begin();
            int moved = MoveStack(chest, itemId);
            Perf.End("Factory top-up", t);
            if (moved > 0)
            {
                notAvailable.Remove(key);
            }
            else
            {
                notAvailable[key] = unchecked(Environment.TickCount + RetryMs);
            }
            return moved;
        }

        // The item the giver hands to this cell when it has one; null when that is not known.
        internal static string WantedItem(ConveyorComponent giver, ConveyorComponent cell)
        {
            switch (giver)
            {
                case ConveyorChestComponent chest:
                    return SlotItem(chest.SlotsData, cell);
                case ConveyorChestOutComponent chestOut:
                    // Splitters, underground belts and supply stations do not take from these.
                    return cell is ConveyorCellComponent ? SlotItem(chestOut.SlotsData, cell) : null;
                case ConveyorPalletComponent _:
                    // A Stockpile gives its first stack; it holds one item only.
                    WhiteListItemFilter whiteList = giver.WgoData.Definition?.inventoryWhiteList;
                    return whiteList != null && whiteList.itemsIds.Count == 1 && whiteList.groupsIds.Count == 0
                        ? whiteList.itemsIds[0]
                        : null;
                default:
                    return null;
            }
        }

        private static string SlotItem(List<ConveyorChestSlotData> slots, ConveyorComponent cell)
        {
            ConveyorChestSlotData slot = slots?.Find(s => s.conveyorWgoDataUniqueId == cell.WgoData.UniqueId);
            return slot?.slotItemId;
        }

        // Input chests that give to a belt cell.
        internal static bool FeedsBelt(WgoData wgoData)
        {
            switch ((wgoData as ConveyorWgoData)?.ConveyorComponent)
            {
                case ConveyorChestComponent chest:
                    return chest.ConnectedWgoData.Count > 0;
                case ConveyorChestOutComponent chestOut:
                    return chestOut.ConnectedWgoData.Count > 0;
                case ConveyorPalletComponent pallet:
                    return pallet.ConnectedWgoData != null;
                default:
                    return false;
            }
        }

        // Up to one stack, as much as fits and as the other chests hold.
        private static int MoveStack(ConveyorWgoData chest, string itemId)
        {
            Inventory target = chest.Inventory;
            int stack = Math.Max(1, new Item(itemId).Definition?.stackCount ?? 1);
            // Of one stack, what fits (also checks the chest's own item filter).
            int want = target.Data.CanAddItemCountToInventory(new Item(itemId, stack));
            if (want <= 0)
            {
                return 0;
            }
            List<Inventory> pool = GlobalStorage.All();
            List<WgoData> owners = GlobalStorage.Owners();
            List<Inventory> sources = new List<Inventory>();
            int available = 0;
            for (int i = 0; i < pool.Count && available < want; i++)
            {
                if (ReferenceEquals(pool[i], target) || FeedsBelt(owners[i]))
                {
                    continue;
                }
                int count = pool[i].Data.GetTotalCountInInventory(itemId);
                if (count > 0)
                {
                    sources.Add(pool[i]);
                    available += count;
                }
            }
            if (available == 0)
            {
                return 0;
            }
            int moved = GlobalStorage.Move(itemId, Math.Min(want, available), sources, new List<Inventory> { target });
            Debug.Log("[GK2GlobalStorage] Factory: " + moved + " x " + itemId + " moved into " + chest.id
                + " (" + chest.WorldZoneData?.id + ")");
            return moved;
        }
    }
}
