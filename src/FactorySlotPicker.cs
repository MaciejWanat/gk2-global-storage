using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2GlobalStorage
{
    // Option "Factory": choosing the item of a belt slot. In the game you click a slot of a Conveyor Chest, Garden
    // Storage or Alcohol Storage, then one of the items in that chest: while you choose, the game switches the
    // chest's cells from moving items to setting the slot. With the option on, a section "Global Storage" is added
    // below the chest for that time, one cell per item the refill can bring from your other chests (with the total
    // count). The game switches its cells like the chest's, so clicking one sets the slot, and FactoryPatches then
    // brings that item in.
    internal static class FactorySlotPicker
    {
        public const string Header = "Global Storage";

        // The windows of the three chest kinds; each has its own SetChestItemSettingMode(bool, ConveyorChestSlotData).
        private static readonly Type[] Windows =
        {
            typeof(UIConveyorChestWindow),
            typeof(UIConveyorVegetablesChestWindow),
            typeof(UIConveyorWineChestWindow),
        };

        private static readonly FieldInfo windowData = AccessTools.Field(typeof(LazyWidget<UIBaseChestWindowData>), "data");

        // The section added to the chest side of a window while a slot is being set.
        private static readonly ConditionalWeakTable<MultiInventoryWidgetData, InventoryWidgetData> shown =
            new ConditionalWeakTable<MultiInventoryWidgetData, InventoryWidgetData>();

        // True when the setting mode of every window is patched.
        public static bool Apply(Harmony harmony)
        {
            bool all = windowData != null;
            foreach (Type window in Windows)
            {
                MethodInfo mode = AccessTools.DeclaredMethod(window, "SetChestItemSettingMode", new[] { typeof(bool), typeof(ConveyorChestSlotData) });
                if (mode == null)
                {
                    Debug.LogWarning("[GK2GlobalStorage] Factory slot picker: " + window.Name + ".SetChestItemSettingMode not found (game update?)");
                    all = false;
                    continue;
                }
                harmony.Patch(mode, prefix: new HarmonyMethod(typeof(FactorySlotPicker), nameof(ModePrefix)));
            }
            return all;
        }

        // Before the game enters or leaves the slot setting mode. On entering, the section is added first so the game
        // switches its cells along with the chest's; on leaving, it is taken out before the game switches the cells
        // back to moving items.
        private static void ModePrefix(UIBaseChestWindow __instance, bool isActive, bool ___isItemSelectionModActive)
        {
            try
            {
                UIBaseChestWindowData data = windowData?.GetValue(__instance) as UIBaseChestWindowData;
                MultiInventoryWidgetData chestSide = data?.SecondMultiInventoryData;
                if (chestSide == null)
                {
                    return;
                }
                if (!isActive)
                {
                    Remove(chestSide);
                    return;
                }
                if (___isItemSelectionModActive || !Config.Enabled || !Config.Factory)
                {
                    return;
                }
                Inventory catalog = Catalog(data.Chest);
                if (catalog == null)
                {
                    return;
                }
                InventoryWidgetData section = new InventoryWidgetData(catalog,
                    new InventoryHeaderWidgetData(catalog, "comm-header_2-type_icon-simple_chest", Header),
                    _ => LazyAudio.PlayAndForget("gui_hover_light"), null, null, null, null);
                chestSide.Add(section);
                try
                {
                    chestSide.OnInventoryAdded?.Invoke(section);
                }
                catch
                {
                    // The window draws one widget per section: a section it could not draw must not stay listed.
                    chestSide.inventoriesData.Remove(section);
                    throw;
                }
                // The window sets this when it draws the section. Pressing a cell selects its section, and the
                // selected section of the chest side is where the game moves items to: this one never is.
                section.OnWidgetPressed = null;
                shown.Add(chestSide, section);
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Factory slot picker", ex);
            }
        }

        private static void Remove(MultiInventoryWidgetData chestSide)
        {
            if (!shown.TryGetValue(chestSide, out InventoryWidgetData section))
            {
                return;
            }
            shown.Remove(chestSide);
            int index = chestSide.inventoriesData.IndexOf(section);
            if (index >= 0)
            {
                chestSide.OnInventoryRemoved?.Invoke(index);
                chestSide.inventoriesData.RemoveAt(index);
            }
            if (chestSide.SelectedWidgetData == section)
            {
                // Back to the chest itself.
                chestSide.SelectedWidgetData = null;
            }
        }

        // One item per kind held (in bags too) by the chests the refill takes from, with the total count, that the
        // chest's filters let in. Chests that feed a belt are no source, so what only they hold is not offered. Items
        // the chest already holds are left out: its own cells offer them. Null when there is nothing to add.
        internal static Inventory Catalog(WgoData chest)
        {
            Item own = chest?.Inventory?.Data;
            if (own == null)
            {
                return null;
            }
            Dictionary<string, int> counts = new Dictionary<string, int>();
            List<Inventory> pool = GlobalStorage.All();
            List<WgoData> owners = GlobalStorage.Owners();
            for (int i = 0; i < pool.Count; i++)
            {
                if (FactoryPatches.IsSource(pool[i], owners[i], chest.Inventory))
                {
                    Count(pool[i].Data.Inventory, counts);
                }
            }
            List<Item> items = new List<Item>();
            foreach (KeyValuePair<string, int> pair in counts)
            {
                Item item = new Item(pair.Key, pair.Value);
                if (item.Definition != null && Accepts(own, item.Definition) && own.GetTotalCountInInventory(pair.Key) == 0)
                {
                    items.Add(item);
                }
            }
            if (items.Count == 0)
            {
                return null;
            }
            // The game's own item order.
            items.Sort((a, b) =>
            {
                int order = a.Definition.sortOrder.CompareTo(b.Definition.sortOrder);
                return order != 0 ? order : string.CompareOrdinal(a.id, b.id);
            });
            Inventory catalog = new Inventory("inventory", items.Count);
            catalog.Data.Inventory.AddRange(items);
            return catalog;
        }

        private static void Count(List<Item> items, Dictionary<string, int> counts)
        {
            foreach (Item item in items)
            {
                if (item == null || item.IsEmpty)
                {
                    continue;
                }
                if (item.IsBag)
                {
                    Count(item.Inventory, counts);
                    continue;
                }
                counts.TryGetValue(item.id, out int count);
                counts[item.id] = count + item.Count;
            }
        }

        // The chest's item filters, as the chest window checks them (Garden Storage takes only vegetable bags).
        private static bool Accepts(Item chest, ItemDef def)
        {
            if (chest.TryGetProperty(out WhiteListFilterSerializedItemProperty white) && !white.WhiteList.IsEmpty && !white.WhiteList.Contains(def))
            {
                return false;
            }
            return !(chest.TryGetProperty(out BlackListFilterSerializedItemProperty black) && black.BlackList.Contains(def));
        }
    }
}
