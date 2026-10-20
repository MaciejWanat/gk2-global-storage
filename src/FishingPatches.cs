using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace GK2GlobalStorage
{
    // Fishing reads bait from the player's bag only: the check at the water (any bait for it?), the bait list of the
    // fishing window with its counts, and taking the chosen bait when you cast. The two readers get their bag calls
    // swapped for FishingRedirect, which adds every chest (option Fishing). Taking the bait happens in
    // UIFishingWindow.OnSubmitBait, which calls Unity internals and so cannot be patched outside the engine (tests);
    // instead, while the cast button's OnSubmitPressed runs, a bag removal that comes up short takes the rest from
    // the chests.
    internal static class FishingPatches
    {
        private static Dictionary<MethodInfo, MethodInfo> map;

        // Calls swapped by the transpiler run in progress.
        private static int swapped;

        // >0 while the fishing window's cast button runs.
        [ThreadStatic]
        private static int castDepth;

        public static bool Apply(Harmony harmony)
        {
            map = new Dictionary<MethodInfo, MethodInfo>
            {
                [AccessTools.Method(typeof(Inventory), nameof(Inventory.GetItemsByType), new[] { typeof(ItemType), typeof(bool) })]
                    = AccessTools.Method(typeof(FishingRedirect), nameof(FishingRedirect.GetItemsByType)),
                [AccessTools.Method(typeof(Item), nameof(Item.GetTotalCountInInventory), new[] { typeof(string), typeof(Item), typeof(bool) })]
                    = AccessTools.Method(typeof(FishingRedirect), nameof(FishingRedirect.GetTotalCountInInventory)),
            };
            foreach (KeyValuePair<MethodInfo, MethodInfo> pair in map)
            {
                if (pair.Key == null || pair.Value == null)
                {
                    Debug.LogWarning("[GK2GlobalStorage] Fishing patch: inventory methods not found (game update?)");
                    return false;
                }
            }
            HarmonyMethod transpiler = new HarmonyMethod(typeof(FishingPatches), nameof(Transpiler));
            // "No bait" check before the window opens.
            if (!Redirect(harmony, transpiler, "ReservoirInteractionHandler.Interact",
                    AccessTools.DeclaredMethod(typeof(ReservoirInteractionHandler), nameof(ReservoirInteractionHandler.Interact), new[] { typeof(PlayerController) }))
                // Bait list and counts.
                || !Redirect(harmony, transpiler, "UIFishingWindowData.UpdateAvailableFishingAndBaits",
                    AccessTools.DeclaredMethod(typeof(UIFishingWindowData), nameof(UIFishingWindowData.UpdateAvailableFishingAndBaits))))
            {
                return false;
            }

            MethodInfo cast = AccessTools.DeclaredMethod(typeof(UIFishingWindow), "OnSubmitPressed");
            MethodInfo remove = AccessTools.Method(typeof(Inventory), nameof(Inventory.RemoveItemById), new[] { typeof(string), typeof(int), typeof(Item), typeof(Item), typeof(bool) });
            if (cast == null || remove == null)
            {
                Debug.LogWarning("[GK2GlobalStorage] Patch target not found (game update?): " + (cast == null ? "UIFishingWindow.OnSubmitPressed" : "Inventory.RemoveItemById"));
                return false;
            }
            harmony.Patch(cast, prefix: new HarmonyMethod(typeof(FishingPatches), nameof(CastPrefix)),
                finalizer: new HarmonyMethod(typeof(FishingPatches), nameof(CastFinalizer)));
            harmony.Patch(remove, postfix: new HarmonyMethod(typeof(FishingPatches), nameof(RemovePostfix)));
            return true;
        }

        private static bool Redirect(Harmony harmony, HarmonyMethod transpiler, string label, MethodInfo method)
        {
            if (method == null)
            {
                Debug.LogWarning("[GK2GlobalStorage] Patch target not found (game update?): " + label);
                return false;
            }
            swapped = 0;
            harmony.Patch(method, transpiler: transpiler);
            // The method is there but no longer reads the bag this way: it would quietly stay bag-only.
            if (swapped == 0)
            {
                Debug.LogWarning("[GK2GlobalStorage] Fishing patch: no bag call found in " + label + " (game update?)");
                return false;
            }
            return true;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction ins in instructions)
            {
                if ((ins.opcode == OpCodes.Call || ins.opcode == OpCodes.Callvirt)
                    && ins.operand is MethodInfo called && map.TryGetValue(called, out MethodInfo replacement))
                {
                    ins.opcode = OpCodes.Call;
                    ins.operand = replacement;
                    swapped++;
                }
                yield return ins;
            }
        }

        internal static void CastPrefix()
        {
            castDepth++;
        }

        internal static Exception CastFinalizer(Exception __exception)
        {
            if (castDepth > 0)
            {
                castDepth--;
            }
            return __exception;
        }

        private static void RemovePostfix(Inventory __instance, string itemId, int count, List<Item> __result)
        {
            if (castDepth > 0)
            {
                FishingRedirect.TakeRestFromChests(__instance, itemId, count, __result);
            }
        }
    }
}
