using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GK2GlobalStorage
{
    internal static class Config
    {
        public static bool Enabled = true;
        public static bool Building = true;
        public static bool Workbenches = true;
        public static bool Quests = true;
        public static bool ShowOtherChests = true;
        public static bool Trading = true;
        public static bool Pickers = true;
        public static bool PickersOnlyUsable = true;
        public static bool ChestWindows = false;
        public static bool ZombieWorkers = false;
        public static bool AutoCrafters = false;
        public static bool FuelContainers = false;
        public static bool PickupsToChest = false;
        public static bool PerfReport = false;
        public static bool DeveloperMode = false;
        public static bool LogZones = true;
        public static readonly HashSet<string> ExcludedZones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // What the windows show: the master switches combined with each window switch.
        public static bool VendorShowsChests => Enabled && ShowOtherChests && Trading;
        public static bool PickersShowChests => Enabled && Building && ShowOtherChests && Pickers;
        public static bool ChestWindowShowsChests => Enabled && ShowOtherChests && ChestWindows;

        public static string FilePath => Path.Combine(Application.persistentDataPath, "GK2GlobalStorage.cfg");

        private static string Render()
        {
            return
$@"# Global Storage for Graveyard Keeper 2
# Edit in game with Shift+F2 (changes apply immediately), or here (restart the game afterwards).

# Master switch.
Enabled = {B(Enabled)}

# Building, alchemy, garden, prayers and other things that use your bag + the chests of the zone you are in
# can also use the chests in every other zone and level.
Building = {B(Building)}

# Workbenches can use materials from the chests in every zone (when you craft at them yourself).
Workbenches = {B(Workbenches)}

# Quest and dialog requirements are checked against, and taken from, your bag plus every chest.
Quests = {B(Quests)}

# Zombie workbenches: their carriers fetch from the nearest chest of the area, and what the area lacks is
# brought over from chests in other areas. Fuel for zombie workbenches comes from every zone.
ZombieWorkers = {B(ZombieWorkers)}

# Automatic crafters (no worker) also take materials from every zone. Can quietly drain chests everywhere.
AutoCrafters = {B(AutoCrafters)}

# Include fuel containers (e.g. furnace fuel slots) in the global pool.
FuelContainers = {B(FuelContainers)}

# Items you pick up from the ground (harvest, loot, workbench results) go to the nearest chest with room
# in the area you are in, instead of your bag. No chest with room there: bag as usual.
PickupsToChest = {B(PickupsToChest)}

# --- Showing chests of other areas in windows (each chest as its own section) ---
# Master switch for the two options below. The rest of each window always stays as in the game.
ShowOtherChests = {B(ShowOtherChests)}

# Vendor windows: chests holding something the vendor buys, so you can sell straight from them.
Trading = {B(Trading)}

# Item pickers: windows where you choose an item (seeds, organs, sermons, grave decorations, alchemy
# ingredients...). The picked item is taken from the chest it is in.
Pickers = {B(Pickers)}

# Item pickers show only the items they can use, and only the chests that hold such items.
PickersOnlyUsable = {B(PickersOnlyUsable)}

# Chest windows: opening any chest also lists the chests of other areas, view only.
# Off by default: chest windows open a little slower.
ChestWindows = {B(ChestWindows)}

# Write the list of zones and chest counts to Player.log once per loaded save.
LogZones = {B(LogZones)}

# Comma-separated zone ids that stay local (ids are printed in Player.log when LogZones = true).
ExcludedZones = {string.Join(", ", ExcludedZones.OrderBy(z => z))}

# --- Developer options (file only) ---
# true = show developer options in the Shift+F2 settings page. Restart the game after changing.
DeveloperMode = {B(DeveloperMode)}

# Every 30 s write timings (frame times, chest/workbench/vendor opening, mod code) to Player.log and
# GK2GlobalStorage-perf.txt next to this file. Only works with DeveloperMode = true.
PerfReport = {B(PerfReport)}
";
        }

        private static string B(bool value)
        {
            return value ? "true" : "false";
        }

        public static void Load()
        {
            string path = FilePath;
            try
            {
                if (!File.Exists(path))
                {
                    Save();
                    Debug.Log("[GK2GlobalStorage] Created config: " + path);
                    return;
                }
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#"))
                    {
                        continue;
                    }
                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }
                    Apply(line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim());
                }
                // Rewrite so options added in newer versions appear in the file.
                Save();
                Debug.Log("[GK2GlobalStorage] Config loaded: " + path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GK2GlobalStorage] Cannot read config, using defaults: " + ex.Message);
            }
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, Render());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GK2GlobalStorage] Cannot write config: " + ex.Message);
            }
        }

        private static void Apply(string key, string value)
        {
            switch (key.ToLowerInvariant())
            {
                case "enabled": Enabled = Bool(value, Enabled); break;
                case "building": Building = Bool(value, Building); break;
                case "workbenches": Workbenches = Bool(value, Workbenches); break;
                case "quests": Quests = Bool(value, Quests); break;
                case "showotherchests": ShowOtherChests = Bool(value, ShowOtherChests); break;
                case "trading": Trading = Bool(value, Trading); break;
                case "pickers": Pickers = Bool(value, Pickers); break;
                case "pickersonlyusable": PickersOnlyUsable = Bool(value, PickersOnlyUsable); break;
                case "chestwindows": ChestWindows = Bool(value, ChestWindows); break;
                // Before 1.0.0 was released: one switch for pickers and chest windows.
                case "otherwindows": ChestWindows = Bool(value, ChestWindows); break;
                case "zombieworkers": ZombieWorkers = Bool(value, ZombieWorkers); break;
                case "autocrafters": AutoCrafters = Bool(value, AutoCrafters); break;
                case "fuelcontainers": FuelContainers = Bool(value, FuelContainers); break;
                case "pickupstochest": PickupsToChest = Bool(value, PickupsToChest); break;
                case "perfreport": PerfReport = Bool(value, PerfReport); break;
                case "developermode": DeveloperMode = Bool(value, DeveloperMode); break;
                case "logzones": LogZones = Bool(value, LogZones); break;
                case "excludedzones":
                    ExcludedZones.Clear();
                    foreach (string id in value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (id.Trim().Length > 0)
                        {
                            ExcludedZones.Add(id.Trim());
                        }
                    }
                    break;
            }
        }

        private static bool Bool(string value, bool fallback)
        {
            switch (value.ToLowerInvariant())
            {
                case "true": case "yes": case "on": case "1": return true;
                case "false": case "no": case "off": case "0": return false;
                default: return fallback;
            }
        }
    }
}
