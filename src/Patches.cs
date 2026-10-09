using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2GlobalStorage
{
    // Everything that touches Harmony types lives here, so this class is only JIT-compiled
    // after HarmonyLoader has made 0Harmony resolvable.
    internal static class Patches
    {
        public const string HarmonyId = "gk2globalstorage";

        private const string PackPrefix = "gk2globalstorage";

        // >0 while an item picker window (UIMultiInventoryWindowData) is being built that must not list other chests.
        [ThreadStatic]
        private static int pickerDepth;

        // >0 while an item picker window is being built that lists every chest.
        [ThreadStatic]
        private static int globalPickerDepth;

        // Item filter of the picker window being built (null outside pickers or for pickers without one).
        [ThreadStatic]
        private static Func<Item, bool> pickerFilter;

        // >0 while ChestInteractionHandler.Interact (opening a chest) runs.
        [ThreadStatic]
        private static int chestOpenDepth;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Apply()
        {
            Harmony harmony = new Harmony(HarmonyId);
            int ok = 0;

            ok += Patch(harmony, "MultiInventory(PlayerData, bool)",
                AccessTools.Constructor(typeof(MultiInventory), new[] { typeof(PlayerData), typeof(bool) }),
                postfix: nameof(PlayerMultiInventoryPostfix));

            ok += Patch(harmony, "WgoData.GetCraftableMultiInventory",
                AccessTools.Method(typeof(WgoData), nameof(WgoData.GetCraftableMultiInventory)),
                postfix: nameof(CraftableMultiInventoryPostfix));

            ok += Patch(harmony, "UIMultiInventoryWindowData..ctor",
                FirstConstructor(typeof(UIMultiInventoryWindowData)),
                prefix: nameof(PickerPrefix), postfix: nameof(PickerPostfix), finalizer: nameof(PickerFinalizer));

            ok += Patch(harmony, "InventoryWidget.Redraw",
                AccessTools.DeclaredMethod(typeof(InventoryWidget), nameof(InventoryWidget.Redraw)),
                postfix: nameof(InventoryRedrawPostfix));

            // Chest windows: the chest window itself is left alone (other mods change it). Instead, while a chest
            // is being opened, other areas' chests are appended to the area-chest list the game builds for that
            // window (see ChestWindowView).
            Patch(harmony, "ChestInteractionHandler.Interact",
                AccessTools.Method(typeof(ChestInteractionHandler), nameof(ChestInteractionHandler.Interact), new[] { typeof(PlayerController) }),
                prefix: nameof(ChestOpenPrefix), finalizer: nameof(ChestOpenFinalizer));
            Patch(harmony, "MultiInventory(WorldZoneData, WgoData, bool)",
                AccessTools.Constructor(typeof(MultiInventory), new[] { typeof(WorldZoneData), typeof(WgoData), typeof(bool) }),
                postfix: nameof(AreaChestsPostfix));

            ok += Patch(harmony, "LanguageModLoader.AppendLanguages",
                AccessTools.Method(typeof(LanguageModLoader), nameof(LanguageModLoader.AppendLanguages)),
                postfix: nameof(HideLoaderPack));

            try
            {
                int quest = QuestPatches.Apply(harmony, out int methods);
                Debug.Log("[GK2GlobalStorage] Quest patch: " + methods + " method(s) redirected");
                ok += quest;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GK2GlobalStorage] Cannot patch quests: " + ex.Message);
            }

            try
            {
                ok += TradePatches.Apply(harmony);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GK2GlobalStorage] Cannot patch trading: " + ex.Message);
            }

            try
            {
                ok += ZombiePatches.Apply(harmony);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GK2GlobalStorage] Cannot patch zombie carriers: " + ex.Message);
            }

            try
            {
                ok += PickupPatches.Apply(harmony);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GK2GlobalStorage] Cannot patch pickups: " + ex.Message);
            }

            return ok;
        }

        private static int Patch(Harmony harmony, string label, MethodBase target, string prefix = null, string postfix = null, string finalizer = null)
        {
            if (target == null)
            {
                Debug.LogWarning("[GK2GlobalStorage] Patch target not found (game update?): " + label);
                return 0;
            }
            try
            {
                harmony.Patch(target,
                    prefix: prefix == null ? null : new HarmonyMethod(typeof(Patches), prefix),
                    postfix: postfix == null ? null : new HarmonyMethod(typeof(Patches), postfix),
                    finalizer: finalizer == null ? null : new HarmonyMethod(typeof(Patches), finalizer));
                return 1;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GK2GlobalStorage] Cannot patch " + label + ": " + ex.Message);
                return 0;
            }
        }

        private static ConstructorInfo FirstConstructor(Type type)
        {
            ConstructorInfo[] ctors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
            return ctors.Length > 0 ? ctors[0] : null;
        }

        // Bag + current zone. Used by building, alchemy, garden, prayers, tooltips and item pickers.
        private static void PlayerMultiInventoryPostfix(MultiInventory __instance, bool addCurrentPlayerWorldZone)
        {
            try
            {
                if (!Config.Enabled || !Config.Building || pickerDepth > 0)
                {
                    return;
                }
                // Bag-only lists stay bag-only, except in pickers: zombie equipment pickers are bag-only in the game.
                if (!addCurrentPlayerWorldZone && globalPickerDepth == 0)
                {
                    return;
                }
                GlobalStorage.AppendTo(__instance);
                if (pickerFilter != null)
                {
                    PickerOrder.UsableFirst(__instance, pickerFilter);
                }
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("PlayerMultiInventory", ex);
            }
        }

        // Workbenches: zone chests (+ worker). Zombie and conveyor objects override this method and are not affected.
        private static void CraftableMultiInventoryPostfix(WgoData __instance, MultiInventory __result)
        {
            try
            {
                if (!Config.Enabled || !Config.Workbenches || __result == null)
                {
                    return;
                }
                if (!Config.ZombieWorkers && __instance.Worker is ZombieWgoData)
                {
                    return;
                }
                if (!Config.AutoCrafters && __instance.Definition != null && __instance.Definition.isAutoCrafter)
                {
                    return;
                }
                GlobalStorage.AppendTo(__result);
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("CraftableMultiInventory", ex);
            }
        }

        // Item picker windows: with "Item pickers" on, PlayerMultiInventoryPostfix appends every chest to the
        // window's list, so each chest shows as its own section and picked items come out of the real chest.
        // Otherwise the window is built exactly as in the game.
        private sealed class PickerState
        {
            public bool local;
            public Func<Item, bool> outerFilter;
            public Func<Item, bool> filter;
        }

        private static void PickerPrefix(Func<Item, bool> itemsAvailableCondition, out PickerState __state)
        {
            __state = new PickerState { local = !Config.PickersShowChests, outerFilter = pickerFilter };
            if (__state.local)
            {
                pickerDepth++;
            }
            else
            {
                globalPickerDepth++;
            }
            pickerFilter = itemsAvailableCondition;
            __state.filter = itemsAvailableCondition;
        }

        // "Usable items only": the picker lists only what it can use, from the chests that have it.
        private static void PickerPostfix(UIMultiInventoryWindowData __instance, PickerState __state)
        {
            try
            {
                if (__state?.filter != null && !__state.local && Config.PickersOnlyUsable)
                {
                    PickerFilter.UsableOnly(__instance.MultiInventoryWidgetData, __state.filter);
                }
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Picker usable items only", ex);
            }
        }

        private static void InventoryRedrawPostfix(InventoryWidget __instance)
        {
            try
            {
                PickerFilter.HideEmptyCells(__instance);
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Picker empty cells", ex);
            }
        }

        private static Exception PickerFinalizer(Exception __exception, PickerState __state)
        {
            if (__state != null)
            {
                if (__state.local && pickerDepth > 0)
                {
                    pickerDepth--;
                }
                else if (!__state.local && globalPickerDepth > 0)
                {
                    globalPickerDepth--;
                }
                pickerFilter = __state.outerFilter;
            }
            return __exception;
        }

        private static void ChestOpenPrefix()
        {
            chestOpenDepth++;
        }

        private static Exception ChestOpenFinalizer(Exception __exception)
        {
            if (chestOpenDepth > 0)
            {
                chestOpenDepth--;
            }
            return __exception;
        }

        // Only the area-chest list built while opening a chest; every other use of this constructor is untouched.
        private static void AreaChestsPostfix(MultiInventory __instance, WgoData excludeWgoData)
        {
            if (chestOpenDepth == 0 || !Config.ChestWindowShowsChests)
            {
                return;
            }
            try
            {
                ChestWindowView.AppendOtherChests(__instance, excludeWgoData);
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Chest window chests", ex);
            }
        }

        // The loader pack is only a vehicle for the DLL; keep it out of the language list.
        private static void HideLoaderPack(Dictionary<string, LLBase.LanguageInfo> languages)
        {
            try
            {
                List<string> remove = null;
                foreach (string key in languages.Keys)
                {
                    if (key.StartsWith(PackPrefix, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(key, LLBase.CurrentLang, StringComparison.OrdinalIgnoreCase))
                    {
                        (remove ??= new List<string>()).Add(key);
                    }
                }
                if (remove != null)
                {
                    foreach (string key in remove)
                    {
                        languages.Remove(key);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("HideLoaderPack", ex);
            }
        }
    }
}
