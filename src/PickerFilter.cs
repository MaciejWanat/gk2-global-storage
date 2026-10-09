using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace GK2GlobalStorage
{
    // Option "Usable items only" (item pickers with other chests listed). A picker then shows only the items
    // it accepts: chests (and pouches) without such items are left out, and within the remaining sections
    // other items and empty slots are hidden. The bag section always stays. Uses the game's own
    // "do not show" item condition, so picking works exactly as in the game.
    internal static class PickerFilter
    {
        private static readonly MethodInfo NotShowSetter =
            AccessTools.PropertySetter(typeof(InventoryWidgetDataBase), nameof(InventoryWidgetDataBase.CustomItemsNotShowCondition));

        // Sections whose empty slots are hidden when drawn.
        private static readonly ConditionalWeakTable<InventoryWidgetDataBase, object> compact =
            new ConditionalWeakTable<InventoryWidgetDataBase, object>();

        public static void UsableOnly(MultiInventoryWidgetData widgets, Func<Item, bool> usable)
        {
            List<InventoryWidgetDataBase> list = widgets?.inventoriesData;
            if (list == null || NotShowSetter == null)
            {
                return;
            }
            long t = Perf.Begin();
            Inventory bag = MainGame.PlayerData?.inventory;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                InventoryWidgetDataBase widget = list[i];
                if (widget == null)
                {
                    continue;
                }
                if (!ReferenceEquals(widget.Inventory, bag) && !PickerOrder.HasUsable(widget.Inventory, usable))
                {
                    list.RemoveAt(i);
                    continue;
                }
                Func<Item, bool> notShown = widget.CustomItemsNotShowCondition;
                Func<Item, bool> hide = notShown == null
                    ? (Func<Item, bool>)(item => !usable(item))
                    : item => notShown(item) || !usable(item);
                NotShowSetter.Invoke(widget, new object[] { hide });
                compact.Remove(widget);
                compact.Add(widget, null);
            }
            Perf.End("Picker usable items only", t);
        }

        // After the game drew a section: hide its empty slots too, so the list is only the usable items.
        public static void HideEmptyCells(InventoryWidget widget)
        {
            InventoryWidgetDataBase data = widget?.Data;
            if (data == null || !compact.TryGetValue(data, out _))
            {
                return;
            }
            List<UIItemCell> cells = widget.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                UIItemCell cell = cells[i];
                if (cell != null && cell.gameObject.activeSelf && (cell.DisplayingItem == null || cell.DisplayingItem.IsEmpty))
                {
                    cell.gameObject.SetActive(false);
                }
            }
        }
    }
}
