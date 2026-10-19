using System;
using System.Collections.Generic;
using System.Linq;

namespace GK2GlobalStorage
{
    // What the mod hooks into in the game: each part goes in as a whole or not at all (Patches.Install), with its own
    // Harmony owner id, so a part that cannot run on the installed game version is left out alone.
    internal enum Hook
    {
        Building,
        Workbenches,
        CraftFinishCheck,
        Pickers,
        PickerCells,
        ChestWindows,
        LanguageList,
        Quests,
        Trading,
        VendorOrder,
        ZombieWorkers,
        Factory,
        FactorySlotPicker,
        Pickups,
    }

    // Of a settings option: every hook it needs is in, only a part of it is lost, or it cannot work at all.
    internal enum Availability
    {
        Available,
        Partly,
        Unavailable,
    }

    // Which hooks are in, for the settings window, the main menu badge and the log. No Harmony types here: the
    // settings window works even without Harmony.
    internal static class Hooks
    {
        public static readonly Hook[] All = (Hook[])Enum.GetValues(typeof(Hook));

        private static readonly HashSet<Hook> loaded = new HashSet<Hook>();
        private static readonly Dictionary<Hook, string> failures = new Dictionary<Hook, string>();

        public static int Total => All.Length;

        public static int LoadedCount => loaded.Count;

        // Tried and left out. Before the patches are applied nothing counts as unloaded.
        public static int UnloadedCount => failures.Count;

        public static IEnumerable<Hook> Unloaded => All.Where(failures.ContainsKey);

        // True when nothing of the mod could be loaded: no Harmony, patching failed as a whole, or no part can run
        // on this game version. Every option is then unavailable and the main menu says the mod is off.
        public static bool ModOff { get; set; }

        // Any part a player would notice (the language list hook only hides the loader pack).
        public static bool AnyFeatureLoaded => loaded.Any(h => h != Hook.LanguageList);

        public static bool IsLoaded(Hook hook) => loaded.Contains(hook);

        public static string Failure(Hook hook) => failures.TryGetValue(hook, out string failure) ? failure : null;

        // Null failure: the hook is in.
        public static void Record(Hook hook, string failure)
        {
            if (failure == null)
            {
                failures.Remove(hook);
                loaded.Add(hook);
            }
            else
            {
                loaded.Remove(hook);
                failures[hook] = failure;
            }
        }

        public static string HarmonyId(Hook hook) => Patches.HarmonyId + "." + hook.ToString().ToLowerInvariant();

        // For the log: the option (or the part of it) the hook is for.
        public static string Name(Hook hook)
        {
            switch (hook)
            {
                case Hook.Building: return "Building, alchemy, garden";
                case Hook.Workbenches: return "Workbenches";
                case Hook.CraftFinishCheck: return "Workbenches: zombie result room check";
                case Hook.Pickers: return "Item pickers";
                case Hook.PickerCells: return "Item pickers: hiding empty cells";
                case Hook.ChestWindows: return "Chest windows";
                case Hook.LanguageList: return "Hiding the loader from the language list";
                case Hook.Quests: return "Quests and dialogs";
                case Hook.Trading: return "Vendor windows";
                case Hook.VendorOrder: return "Vendor windows: item order";
                case Hook.ZombieWorkers: return "Zombie workers";
                case Hook.Factory: return "Factory input chests";
                case Hook.FactorySlotPicker: return "Factory input chests: slot item choice";
                case Hook.Pickups: return "Pickups go to nearest chest";
                default: return hook.ToString();
            }
        }

        // An option that needs the first hooks and only loses a part of itself without the second ones.
        public static Availability Of(Hook[] needs, Hook[] partly)
        {
            if (ModOff)
            {
                return Availability.Unavailable;
            }
            if (needs != null && needs.Any(failures.ContainsKey))
            {
                return Availability.Unavailable;
            }
            if (partly != null && partly.Any(failures.ContainsKey))
            {
                return Availability.Partly;
            }
            return Availability.Available;
        }
    }
}
