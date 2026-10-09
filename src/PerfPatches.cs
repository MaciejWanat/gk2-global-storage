using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Debug = UnityEngine.Debug;

namespace GK2GlobalStorage
{
    // Harmony-free entry point, so Perf can be compiled without touching Harmony types.
    internal static class PerfPatchesInstaller
    {
        public static bool Install()
        {
            try
            {
                return PerfPatches.Install();
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Perf timing patches", ex);
                return false;
            }
        }
    }

    // Times whole game methods (first prefix to last postfix, so the mod's own patches are included).
    // Installed only when the performance report is switched on.
    internal static class PerfPatches
    {
        private static bool installed;
        private static readonly Dictionary<MethodBase, string> names = new Dictionary<MethodBase, string>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool Install()
        {
            if (installed)
            {
                return true;
            }
            installed = true;
            Harmony harmony = new Harmony(Patches.HarmonyId + ".perf");
            HarmonyMethod prefix = new HarmonyMethod(typeof(PerfPatches), nameof(Prefix)) { priority = Priority.First };
            HarmonyMethod postfix = new HarmonyMethod(typeof(PerfPatches), nameof(Postfix)) { priority = Priority.Last };
            // Lookups are lazy and run one by one, so one missing/ambiguous method does not disable the rest.
            var targets = new (string name, Func<MethodBase> find)[]
            {
                ("Open chest", () => AccessTools.Method(typeof(ChestInteractionHandler), nameof(ChestInteractionHandler.Interact), new[] { typeof(PlayerController) })),
                ("Open workbench window", () => AccessTools.DeclaredMethod(typeof(UIBaseCraftSelectionWindow), "Open", new[] { typeof(UIBaseCraftSelectionWindowData) })),
                ("Building window redraw", () => AccessTools.DeclaredMethod(typeof(UIBuildingWindow), nameof(UIBuildingWindow.Redraw), Type.EmptyTypes)),
                ("Open vendor", () => AccessTools.Method(typeof(Trading), nameof(Trading.FillVendorWindowData))),
                ("Pick up item", () => AccessTools.Method(typeof(PlayerData), nameof(PlayerData.CollectDrop))),
                ("Bag + area item list", () => AccessTools.Constructor(typeof(MultiInventory), new[] { typeof(PlayerData), typeof(bool) })),
                ("Workbench item list", () => AccessTools.Method(typeof(WgoData), nameof(WgoData.GetCraftableMultiInventory))),
                ("Workbench queue check", () => AccessTools.Method(typeof(CraftComponent), nameof(CraftComponent.UpdateQueueElementsCraftStatus))),
                ("Quest ready check", () => AccessTools.Method(typeof(QuestFinishCheck), nameof(QuestFinishCheck.IsReadyToFinish))),
            };
            int ok = 0;
            foreach (var (name, find) in targets)
            {
                try
                {
                    MethodBase method = find();
                    if (method == null)
                    {
                        Debug.LogWarning("[GK2GlobalStorage][Perf] Not found: " + name);
                        continue;
                    }
                    names[method] = name;
                    harmony.Patch(method, prefix: prefix, postfix: postfix);
                    ok++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[GK2GlobalStorage][Perf] Cannot time " + name + ": " + ex.Message);
                }
            }
            Debug.Log("[GK2GlobalStorage][Perf] Timing " + ok + "/" + targets.Length + " game methods");
            ok += InstallTransitions(harmony);
            return ok > 0;
        }

        // Area changes are async (fade, unload/load scene, stream objects, fade back), so they are timed between
        // three points: control taken for the teleport (start), PlayerController.OnPlayerTeleported (the player
        // is placed in the new area) and control given back (player can move).
        // PlayerController.Teleport and the fade methods are deliberately not patched: other mods change them.
        private static int InstallTransitions(Harmony harmony)
        {
            MethodInfo control = AccessTools.Method(typeof(PlayerController), nameof(PlayerController.SetControlTakenType));
            if (control == null)
            {
                Debug.LogWarning("[GK2GlobalStorage][Perf] Area change timing unavailable (method not found)");
                return 0;
            }
            try
            {
                harmony.Patch(control, postfix: new HarmonyMethod(typeof(PerfPatches), nameof(ControlPostfix)));
                PlayerController.OnPlayerTeleported += Perf.TransitLoaded;
                Debug.Log("[GK2GlobalStorage][Perf] Timing area changes");
                return 1;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GK2GlobalStorage][Perf] Cannot time area changes: " + ex.Message);
                return 0;
            }
        }

        private static void ControlPostfix(TakenControlType t, bool isEnabled)
        {
            if (t != TakenControlType.ByTeleport)
            {
                return;
            }
            if (isEnabled)
            {
                Perf.TransitEnd();
            }
            else
            {
                Perf.TransitBegin();
            }
        }

        private static void Prefix(out long __state)
        {
            __state = Perf.Active ? Stopwatch.GetTimestamp() : 0L;
        }

        private static void Postfix(long __state, MethodBase __originalMethod)
        {
            if (__state != 0L && names.TryGetValue(__originalMethod, out string name))
            {
                Perf.RecordGame(name, __state);
            }
        }
    }
}
