using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;
using UnityEngine.UI;

namespace GK2GlobalStorage
{
    // Whenever vendor windows show your chests (Config.VendorShowsChests), every section of your side (bag, bags,
    // chests) shows first the items the vendor buys, and among them first those that give the vendor's gratitude;
    // then the items the game greys out, then the empty cells. Gratitude: the game marks such items with an icon (a
    // weekly number per item, up to a weekly cap) for every vendor it flags townVendor, i.e. all but the Bishop and
    // the Woodcarver. Chests holding a sellable item that gives gratitude are listed first (TradePatches). Only the
    // order on screen changes: after the game draws your side and updates the icons, the cells are moved in their
    // grid; the inventories stay as they are, so nothing reaches the save.
    internal static class VendorOrder
    {
        private static FieldInfo windowData;
        private static FieldInfo playerSide;

        // True when patched (a hook of its own: trading works without it).
        public static bool Apply(Harmony harmony)
        {
            windowData = AccessTools.Field(typeof(LazyWidget<UIVendorWindowData>), "data");
            playerSide = AccessTools.Field(typeof(UIVendorWindow), "playerInventoryWidget");
            MethodInfo icons = AccessTools.DeclaredMethod(typeof(UIVendorWindow), "TryUpdateHappinessStatusIcons");
            if (windowData == null || playerSide == null || icons == null)
            {
                Debug.LogWarning("[GK2GlobalStorage] Vendor item order: UIVendorWindow members not found (game update?)");
                return false;
            }
            harmony.Patch(icons, postfix: new HarmonyMethod(typeof(VendorOrder), nameof(IconsPostfix)));
            return true;
        }

        // Whether selling this item now gives the vendor's gratitude, counting what is already in the deal, as the
        // game decides for the icon. Null when the vendor gives none (not townVendor) or vendor windows are off.
        internal static Func<Item, bool> Gives(UIVendorWindowData data)
        {
            Vendor vendor = data?.Vendor;
            if (!Config.VendorShowsChests || vendor?.Definition == null || !vendor.Definition.townVendor)
            {
                return null;
            }
            float inDeal = data.GetTotalHappinessDealDelegate?.Invoke() ?? 0f;
            Func<string, int> pending = data.GetPendingHappinessSoldCount;
            return item => item != null && !item.IsEmpty && vendor.HasHappinessForItem(item.id, pending?.Invoke(item.id) ?? 0, inDeal);
        }

        // Higher comes first: sellable with gratitude 3, sellable 2, greyed out with gratitude 1, greyed out 0, empty -1.
        // sellable: the section's own condition, the one the game greys cells out with (null: all sellable).
        internal static int Rank(Item item, Func<Item, bool> sellable, Func<Item, bool> gives)
        {
            if (item == null || item.IsEmpty)
            {
                return -1;
            }
            return (sellable == null || sellable(item) ? 2 : 0) + (gives != null && gives(item) ? 1 : 0);
        }

        // Highest rank first; equal ranks keep their order.
        internal static List<T> ByRank<T>(IEnumerable<T> list, Func<T, int> rank)
        {
            return list.OrderByDescending(rank).ToList();
        }

        // After the game has drawn your side and set the gratitude icons (it calls this on every redraw).
        private static void IconsPostfix(UIVendorWindow __instance)
        {
            try
            {
                if (!Config.VendorShowsChests)
                {
                    return;
                }
                MultiInventoryWidget side = playerSide.GetValue(__instance) as MultiInventoryWidget;
                if ((object)side == null)
                {
                    return;
                }
                Func<Item, bool> gives = Gives(windowData.GetValue(__instance) as UIVendorWindowData);
                foreach (InventoryWidget section in side.DrawnInventories)
                {
                    Func<Item, bool> sellable = section.Data?.CustomItemsAvailableCondition;
                    Reorder(section.Cells, cell => Rank(cell.DisplayingItem, sellable, gives));
                }
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Vendor item order", ex);
            }
        }

        // Every redraw puts the items into the cells in the game's order again, so the order is worked out from
        // scratch each time.
        private static void Reorder(List<UIItemCell> cells, Func<UIItemCell, int> rank)
        {
            if (cells.Count < 2)
            {
                return;
            }
            int first = int.MaxValue;
            foreach (UIItemCell cell in cells)
            {
                first = Math.Min(first, cell.transform.GetSiblingIndex());
            }
            List<UIItemCell> order = ByRank(cells, rank);
            for (int i = 0; i < order.Count; i++)
            {
                order[i].transform.SetSiblingIndex(first + i);
            }
            if (cells[0].transform.parent is RectTransform grid)
            {
                LayoutRebuilder.MarkLayoutForRebuild(grid);
            }
        }
    }
}
