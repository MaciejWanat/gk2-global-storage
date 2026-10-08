using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2GlobalStorage
{
    // Vendor window: under your bag, every chest that holds something the vendor buys is listed as its own
    // section (the bag part stays exactly as in the game). Moving an item from a chest into the deal takes
    // it out of that chest and remembers it; taking it back, cancelling or closing the window returns it to
    // that chest instead of your bag.
    internal static class TradePatches
    {
        private sealed class Origin
        {
            public Inventory chest;
            public string itemId;
            public int count;
        }

        private sealed class State
        {
            public readonly HashSet<Inventory> listed = new HashSet<Inventory>(GlobalStorage.RefComparer.Instance);
            public readonly List<Origin> origins = new List<Origin>();
        }

        private static readonly ConditionalWeakTable<Trading, State> states = new ConditionalWeakTable<Trading, State>();

        // Plain reflection types only in static fields: a Harmony-typed field would make Mono load 0Harmony
        // whenever this type is inspected (e.g. by SoftMask scanning assemblies), possibly before it is resolvable.
        private static FieldInfo sellField;
        private static FieldInfo windowDataField;
        private static MethodInfo tryMoveItem;
        private static MethodInfo openItemCountWindow;
        private static MethodInfo priceInPlayerInventory;
        private static MethodInfo playerItemsAvailable;

        public static int Apply(Harmony harmony)
        {
            Type t = typeof(Trading);
            sellField = AccessTools.Field(t, "sellInventory");
            windowDataField = AccessTools.Field(t, "cachedWindowData");
            tryMoveItem = AccessTools.Method(t, "TryMoveItem");
            openItemCountWindow = AccessTools.Method(t, "OpenItemCountWindow");
            priceInPlayerInventory = AccessTools.Method(t, "GetSingleItemCostInPlayerInventory", new[] { typeof(string), typeof(int) });
            playerItemsAvailable = AccessTools.Method(t, "PlayerItemsAvailableCondition");
            MethodInfo fill = AccessTools.Method(t, nameof(Trading.FillVendorWindowData));
            MethodInfo reset = AccessTools.Method(t, "ResetDeal");
            MethodInfo accept = AccessTools.Method(t, "DoAcceptDeal");
            if (sellField == null || windowDataField == null || tryMoveItem == null || openItemCountWindow == null
                || priceInPlayerInventory == null || playerItemsAvailable == null || fill == null || reset == null || accept == null)
            {
                Debug.LogWarning("[GK2GlobalStorage] Trading patch: Trading members not found (game update?)");
                return 0;
            }
            harmony.Patch(fill, postfix: new HarmonyMethod(typeof(TradePatches), nameof(FillPostfix)));
            harmony.Patch(tryMoveItem, prefix: new HarmonyMethod(typeof(TradePatches), nameof(MovePrefix)));
            harmony.Patch(reset, prefix: new HarmonyMethod(typeof(TradePatches), nameof(ResetPrefix)));
            harmony.Patch(accept, postfix: new HarmonyMethod(typeof(TradePatches), nameof(AcceptPostfix)));
            return 1;
        }

        private static void FillPostfix(Trading __instance, UIVendorWindowData vendorWindowData)
        {
            try
            {
                if (!Config.VendorShowsChests || vendorWindowData?.PlayerMultiInventoryWidgetData == null)
                {
                    return;
                }
                State state = states.GetValue(__instance, _ => new State());
                state.origins.Clear();
                state.listed.Clear();
                Func<Item, bool> sellable = (Func<Item, bool>)Delegate.CreateDelegate(typeof(Func<Item, bool>), __instance, playerItemsAvailable);
                foreach (Inventory chest in GlobalStorage.All())
                {
                    if (!HasSellable(chest, sellable))
                    {
                        continue;
                    }
                    Inventory from = chest;
                    List<InventoryWidgetDataBase> widgets = InventoryWidgetDataHelper.GetWidgetsDataForInventory(from,
                        _ => LazyAudio.PlayAndForget("gui_hover_light"), null,
                        cell => Press1(__instance, from, cell),
                        cell => Press2(__instance, from, cell),
                        null, sellable, addBags: false);
                    foreach (InventoryWidgetDataBase widget in widgets)
                    {
                        widget.DrawEmptyCellsAsDisabledWhenUnavailable = true;
                    }
                    vendorWindowData.PlayerMultiInventoryWidgetData.AddRange(widgets);
                    state.listed.Add(from);
                }
                if (state.listed.Count > 0)
                {
                    Debug.Log("[GK2GlobalStorage] Vendor window: " + state.listed.Count + " chest(s) with sellable items listed");
                }
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Trading FillVendorWindowData", ex);
            }
        }

        private static bool HasSellable(Inventory chest, Func<Item, bool> sellable)
        {
            foreach (Item item in chest.Data.Inventory)
            {
                if (item != null && !item.IsEmpty && sellable(item))
                {
                    return true;
                }
            }
            return false;
        }

        private static Inventory sellInventory(Trading trading)
        {
            return (Inventory)sellField.GetValue(trading);
        }

        private static void Redraw(Trading trading)
        {
            (windowDataField.GetValue(trading) as UIVendorWindowData)?.OnRedraw?.Invoke();
        }

        // Same as the game's OnPlayerItemPress1/2, with the chest as the source instead of the bag.
        private static void Press1(Trading trading, Inventory chest, UIItemCell cell)
        {
            if (cell.DisplayingItem.Count <= 1)
            {
                Press2(trading, chest, cell);
                return;
            }
            string itemId = cell.DisplayingItem.id;
            UIItemCountWindowData.PriceCalculateDelegate price = amount =>
            {
                int sum = 0;
                for (int i = 0; i < amount; i++)
                {
                    sum += (int)priceInPlayerInventory.Invoke(trading, new object[] { itemId, i + 1 });
                }
                return sum;
            };
            openItemCountWindow.Invoke(trading, new object[] { cell, chest, sellInventory(trading), price });
        }

        private static void Press2(Trading trading, Inventory chest, UIItemCell cell)
        {
            if ((bool)tryMoveItem.Invoke(trading, new object[] { cell, 1, chest, sellInventory(trading) }))
            {
                LazyAudio.PlayAndForget("item_put");
            }
        }

        // TryMoveItem(cell, count, from, to). Returns false (skip the game's version) when handled here.
        private static bool MovePrefix(Trading __instance, UIItemCell itemCell, int count, Inventory from, Inventory to, ref bool __result)
        {
            try
            {
                if (!states.TryGetValue(__instance, out State state) || state.listed.Count == 0 || itemCell?.DisplayingItem == null)
                {
                    return true;
                }
                Inventory sell = sellInventory(__instance);
                string itemId = itemCell.DisplayingItem.id;
                if (to == sell && state.listed.Contains(from))
                {
                    __result = IntoDeal(state, from, sell, itemId, count);
                }
                else if (from == sell && to == MainGame.PlayerData?.inventory && HasOrigin(state, itemId))
                {
                    __result = BackToChests(state, sell, itemId, count, to);
                }
                else
                {
                    return true;
                }
                if (__result)
                {
                    Redraw(__instance);
                }
                return false;
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Trading TryMoveItem", ex);
                return true;
            }
        }

        private static bool IntoDeal(State state, Inventory chest, Inventory sell, string itemId, int count)
        {
            if (count <= 0 || !sell.CanAddItemToInventory(new Item(itemId, count)))
            {
                return false;
            }
            int moved = 0;
            foreach (Item item in chest.RemoveItemById(itemId, count))
            {
                moved += item.Count;
            }
            if (moved <= 0)
            {
                return false;
            }
            state.origins.Add(new Origin { chest = chest, itemId = itemId, count = moved });
            sell.AddItemToInventory(new Item(itemId, moved));
            return true;
        }

        // Newest origins first; anything a full chest cannot take goes to the bag like in the game.
        private static bool BackToChests(State state, Inventory sell, string itemId, int count, Inventory bag)
        {
            int left = Math.Min(count, sell.Data.GetTotalCountInInventory(itemId));
            for (int i = state.origins.Count - 1; i >= 0 && left > 0; i--)
            {
                Origin origin = state.origins[i];
                if (origin.itemId != itemId)
                {
                    continue;
                }
                int n = Math.Min(origin.count, left);
                if (MoveStack(sell, origin.chest, itemId, n))
                {
                    origin.count -= n;
                    left -= n;
                    if (origin.count == 0)
                    {
                        state.origins.RemoveAt(i);
                    }
                }
            }
            if (left > 0)
            {
                MoveStack(sell, bag, itemId, left);
            }
            return true;
        }

        private static bool HasOrigin(State state, string itemId)
        {
            foreach (Origin origin in state.origins)
            {
                if (origin.itemId == itemId)
                {
                    return true;
                }
            }
            return false;
        }

        // Moves n of itemId, keeping the item's properties (copied from the first stack found).
        private static bool MoveStack(Inventory from, Inventory to, string itemId, int n)
        {
            Item stack = FindStack(from, itemId);
            if (n <= 0 || stack == null)
            {
                return false;
            }
            Item copy = Item.Copy(stack);
            copy.Count = n;
            if (!to.AddItemToInventory(copy))
            {
                return false;
            }
            from.RemoveItemById(itemId, n);
            return true;
        }

        // Cancel / close: return chest items to their chests; the game then returns the rest to the bag.
        private static void ResetPrefix(Trading __instance)
        {
            try
            {
                if (!states.TryGetValue(__instance, out State state) || state.origins.Count == 0)
                {
                    return;
                }
                Inventory sell = sellInventory(__instance);
                foreach (Origin origin in state.origins)
                {
                    int n = Math.Min(origin.count, sell.Data.GetTotalCountInInventory(origin.itemId));
                    MoveStack(sell, origin.chest, origin.itemId, n);
                }
                state.origins.Clear();
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Trading ResetDeal", ex);
            }
        }

        // Deal done: sold items are gone for good.
        private static void AcceptPostfix(Trading __instance)
        {
            if (states.TryGetValue(__instance, out State state) && sellInventory(__instance).Data.InventoryCount <= 0)
            {
                state.origins.Clear();
            }
        }

        private static Item FindStack(Inventory inventory, string itemId)
        {
            foreach (Item item in inventory.Data.Inventory)
            {
                if (item != null && !item.IsEmpty && item.id == itemId)
                {
                    return item;
                }
            }
            return null;
        }
    }
}
