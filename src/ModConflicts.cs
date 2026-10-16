using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GK2GlobalStorage
{
    // Other mods that also change the inventory. This mod relies on how the game adds, counts, takes and picks up
    // items; another mod changing the same code (bigger bag, bigger stacks, its own chest sharing, pickup range...)
    // can make the two disagree, e.g. items pulled toward the player but never picked up. Found by:
    // - Harmony patches of any other owner on the inventory methods below or on a method this mod patches, read
    //   from every Harmony copy in memory (ours, BepInEx's HarmonyX, copies other mods ship);
    // - BepInEx plugins whose name suggests inventory changes: mods that edit bag or chest sizes in the save
    //   leave no patch to find.
    // Checked once the main menu is up and again when a save has loaded (ModConflictCheck); the player is told in a
    // window (SettingsWindow) and can turn the warning off per mod (Config.IgnoredMods).
    internal static class ModConflicts
    {
        public const string Pickup = "picking up items";
        public const string Space = "bag and chest space";
        public const string Counts = "counting and taking items";
        public const string Workbenches = "workbenches";
        public const string Windows = "item windows";
        public const string Trading = "trading";
        public const string Zombies = "zombie workers";
        public const string Quests = "quests and dialogs";
        public const string Other = "other game code this mod changes";
        public const string ByName = "bag, chests or stacks (judged by its name)";

        // This mod's own patches: its Harmony ids (gk2globalstorage, .perf, .speedup) and its assembly.
        private static readonly string OwnAssembly = typeof(ModConflicts).Assembly.GetName().Name;

        private static readonly string[] PatchKinds = { "Prefixes", "Postfixes", "Transpilers", "Finalizers", "ILManipulators" };

        private static readonly Regex InventoryName = new Regex(
            @"inventor|stack|storage|chest|container|backpack|\bbags?\b|pick.?up|loot|magnet|stash", RegexOptions.IgnoreCase);

        internal sealed class PatchOwner
        {
            public readonly MethodBase Method;
            public readonly string Owner;
            public readonly string Assembly;

            public PatchOwner(MethodBase method, string owner, string assembly)
            {
                Method = method;
                Owner = owner ?? string.Empty;
                Assembly = assembly ?? string.Empty;
            }
        }

        internal sealed class Finding
        {
            // Harmony owner id or BepInEx GUID: what "don't warn again" stores.
            public string Mod;
            // Shown to the player: the plugin name, or the assembly the patches live in.
            public string Name;
            public readonly SortedSet<string> Areas = new SortedSet<string>();
            public readonly SortedSet<string> Methods = new SortedSet<string>();
        }

        private static readonly HashSet<string> logged = new HashSet<string>();
        private static readonly HashSet<string> noticed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Result of the last check (empty before the first one).
        public static IReadOnlyList<Finding> Found { get; private set; } = new List<Finding>();

        public static void Run(string when)
        {
            List<Finding> found = Classify(ReadPatches(), ReadPlugins());
            Found = found;
            foreach (Finding f in found)
            {
                string line = f.Name + " (id " + f.Mod + "): " + string.Join(", ", f.Areas)
                    + (f.Methods.Count > 0 ? "; patches " + string.Join(", ", f.Methods) : string.Empty);
                if (logged.Add(line))
                {
                    Debug.LogWarning("[GK2GlobalStorage] Another mod also changes the inventory (" + when + "): " + line);
                }
            }
            List<Finding> toTell = NeedsNotice(found).Where(f => !noticed.Contains(f.Mod)).ToList();
            if (toTell.Count > 0)
            {
                foreach (Finding f in toTell)
                {
                    noticed.Add(f.Mod);
                }
                SettingsWindow.RequestNotice();
            }
        }

        // Findings the player is still warned about.
        internal static List<Finding> NeedsNotice(IEnumerable<Finding> found)
        {
            if (!Config.WarnOtherMods)
            {
                return new List<Finding>();
            }
            return found.Where(f => !Config.IgnoredMods.Contains(f.Mod)).ToList();
        }

        // "Don't warn about these again".
        internal static void Ignore(IEnumerable<Finding> found)
        {
            foreach (Finding f in found)
            {
                Config.IgnoredMods.Add(f.Mod);
            }
            Config.Save();
        }

        internal static List<Finding> Classify(IEnumerable<PatchOwner> patches, IEnumerable<KeyValuePair<string, string>> plugins)
        {
            List<PatchOwner> all = patches.ToList();
            HashSet<MethodBase> ours = new HashSet<MethodBase>(all.Where(p => p.Owner == Patches.HarmonyId).Select(p => p.Method));
            Dictionary<string, string> pluginNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> plugin in plugins)
            {
                if (!string.IsNullOrEmpty(plugin.Key))
                {
                    pluginNames[plugin.Key] = string.IsNullOrEmpty(plugin.Value) ? plugin.Key : plugin.Value;
                }
            }

            Dictionary<string, Finding> byMod = new Dictionary<string, Finding>(StringComparer.OrdinalIgnoreCase);
            foreach (PatchOwner p in all)
            {
                if (IsOwn(p.Owner) || IsOwnAssembly(p.Assembly))
                {
                    continue;
                }
                string area = AreaOf(p.Method, ours.Contains(p.Method));
                if (area == null)
                {
                    continue;
                }
                string mod = p.Owner.Length > 0 ? p.Owner : p.Assembly;
                if (!byMod.TryGetValue(mod, out Finding f))
                {
                    string name = pluginNames.TryGetValue(mod, out string pluginName) ? pluginName : (p.Assembly.Length > 0 ? p.Assembly : mod);
                    f = byMod[mod] = new Finding { Mod = mod, Name = name };
                }
                f.Areas.Add(area);
                f.Methods.Add(Describe(p.Method));
            }
            foreach (KeyValuePair<string, string> plugin in pluginNames)
            {
                if (byMod.ContainsKey(plugin.Key) || IsOwn(plugin.Key)
                    || !(NameSuggestsInventory(plugin.Value) || NameSuggestsInventory(plugin.Key)))
                {
                    continue;
                }
                Finding f = byMod[plugin.Key] = new Finding { Mod = plugin.Key, Name = plugin.Value };
                f.Areas.Add(ByName);
            }
            return byMod.Values.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // What part of the game a patched method belongs to, or null when another mod may patch it freely.
        internal static string AreaOf(MethodBase method, bool patchedByThisMod)
        {
            Type type = method?.DeclaringType;
            if (type == null)
            {
                return null;
            }
            // Lambdas and iterators live in nested types.
            while (type.DeclaringType != null)
            {
                type = type.DeclaringType;
            }
            string name = method.Name;
            switch (type.FullName)
            {
                case "DropSearcher":
                case "DropCollector":
                    return Pickup;
                case "DropView":
                    return name.IndexOf("Collect", StringComparison.Ordinal) >= 0 ? Pickup : null;
                case "PlayerData":
                    if (name == nameof(PlayerData.CollectDrop) || name == nameof(PlayerData.CollectResDrop))
                    {
                        return Pickup;
                    }
                    break;
                case "Item":
                case "Inventory":
                    // Space first: CanAddItemCountToInventory is about room, not about counting.
                    if (Contains(name, "CanAdd", "AddItem", "TryAdd", "Insert", "FillSize", "InventorySize", "Stack", "Split"))
                    {
                        return Space;
                    }
                    if (Contains(name, "GetTotalCount", "HasItem", "Remove", "Take", "GetItem", "Count"))
                    {
                        return Counts;
                    }
                    if (type.FullName == "Inventory")
                    {
                        return Space;
                    }
                    break;
                case "MultiInventory":
                    return Counts;
                case "LazyBearTechnology.LanguageModLoader":
                    // Every language-pack loader mod hooks this; nothing to do with items.
                    return null;
            }
            if (!patchedByThisMod)
            {
                return null;
            }
            switch (type.FullName)
            {
                case "WgoData":
                case "CraftElementBase":
                    return Workbenches;
                case "Trading":
                    return Trading;
                case "WorldZoneData":
                case "ZombieWgoData":
                    return Zombies;
                case "UIMultiInventoryWindowData":
                case "InventoryWidget":
                case "ChestInteractionHandler":
                    return Windows;
            }
            return Array.IndexOf(QuestPatches.TargetTypes, type.FullName) >= 0 ? Quests : Other;
        }

        internal static bool NameSuggestsInventory(string name)
        {
            return !string.IsNullOrEmpty(name) && InventoryName.IsMatch(name);
        }

        private static bool IsOwn(string id)
        {
            return !string.IsNullOrEmpty(id) && id.StartsWith(Patches.HarmonyId, StringComparison.OrdinalIgnoreCase);
        }

        // By its exact name: other assemblies may start with the same words.
        private static bool IsOwnAssembly(string assembly)
        {
            return assembly == OwnAssembly;
        }

        private static bool Contains(string name, params string[] parts)
        {
            foreach (string part in parts)
            {
                if (name.IndexOf(part, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static string Describe(MethodBase method)
        {
            return (method.DeclaringType != null ? method.DeclaringType.FullName + "." : string.Empty) + method.Name;
        }

        // Every patch of every Harmony copy in memory, read by reflection (other copies are other types).
        internal static List<PatchOwner> ReadPatches()
        {
            List<PatchOwner> result = new List<PatchOwner>();
            HashSet<string> seen = new HashSet<string>();
            foreach (Assembly harmony in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (harmony.GetName().Name != "0Harmony")
                {
                    continue;
                }
                try
                {
                    Type type = harmony.GetType("HarmonyLib.Harmony", false);
                    MethodInfo all = type?.GetMethod("GetAllPatchedMethods", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                    MethodInfo info = type?.GetMethod("GetPatchInfo", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(MethodBase) }, null);
                    if (all == null || info == null || !(all.Invoke(null, null) is IEnumerable methods))
                    {
                        continue;
                    }
                    foreach (object o in methods)
                    {
                        if (!(o is MethodBase method) || !((info.Invoke(null, new object[] { method })) is object patches))
                        {
                            continue;
                        }
                        foreach (string kind in PatchKinds)
                        {
                            if (!(Member(patches, kind) is IEnumerable list))
                            {
                                continue;
                            }
                            foreach (object patch in list)
                            {
                                string owner = Member(patch, "owner") as string;
                                string assembly = (Member(patch, "PatchMethod") as MethodInfo)?.DeclaringType?.Assembly.GetName().Name;
                                if (seen.Add(method.Module.ModuleVersionId + ":" + method.MetadataToken + "|" + owner + "|" + assembly))
                                {
                                    result.Add(new PatchOwner(method, owner, assembly));
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[GK2GlobalStorage] Other mods check: cannot read the patches of Harmony "
                        + harmony.GetName().Version + ": " + (ex.InnerException ?? ex).Message);
                }
            }
            return result;
        }

        // BepInEx plugins (GUID, name), when BepInEx is loaded.
        internal static List<KeyValuePair<string, string>> ReadPlugins()
        {
            List<KeyValuePair<string, string>> result = new List<KeyValuePair<string, string>>();
            try
            {
                Assembly bepInEx = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "BepInEx");
                Type chainloader = bepInEx?.GetType("BepInEx.Bootstrap.Chainloader", false);
                if (!(chainloader?.GetProperty("PluginInfos", BindingFlags.Public | BindingFlags.Static)?.GetValue(null, null) is IDictionary infos))
                {
                    return result;
                }
                foreach (DictionaryEntry entry in infos)
                {
                    object metadata = Member(entry.Value, "Metadata");
                    string guid = Member(metadata, "GUID") as string ?? entry.Key as string;
                    string name = Member(metadata, "Name") as string;
                    result.Add(new KeyValuePair<string, string>(guid ?? string.Empty, name ?? guid ?? string.Empty));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GK2GlobalStorage] Other mods check: cannot read the BepInEx plugin list: " + (ex.InnerException ?? ex).Message);
            }
            return result;
        }

        private static object Member(object target, string name)
        {
            if (target == null)
            {
                return null;
            }
            Type type = target.GetType();
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                return field.GetValue(target);
            }
            return type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(target, null);
        }
    }

    // Runs the other mods check once the main menu has been up for a moment (all mods loaded), and again after
    // each save load (some mods patch only then).
    public sealed class ModConflictCheck : MonoBehaviour
    {
        private const float MenuDelay = 2f;
        private const float SaveDelay = 3f;

        private float nextCheck;
        private float stateSince;
        private MainGame.GameState? state;
        private bool menuChecked;
        private object checkedSave;

        private void Update()
        {
            if (Time.unscaledTime < nextCheck)
            {
                return;
            }
            nextCheck = Time.unscaledTime + 0.5f;
            try
            {
                MainGame game = MainGame.Instance;
                if (game == null)
                {
                    return;
                }
                if (state != game.gameState)
                {
                    state = game.gameState;
                    stateSince = Time.unscaledTime;
                }
                float since = Time.unscaledTime - stateSince;
                if (state == MainGame.GameState.MainMenu && !menuChecked && since >= MenuDelay)
                {
                    menuChecked = true;
                    ModConflicts.Run("main menu");
                }
                else if (state == MainGame.GameState.InGame && game.GameSave != null && !ReferenceEquals(game.GameSave, checkedSave) && since >= SaveDelay)
                {
                    checkedSave = game.GameSave;
                    ModConflicts.Run("save loaded");
                }
            }
            catch (Exception ex)
            {
                enabled = false;
                Plugin.ReportOnce("Other mods check", ex);
            }
        }
    }
}
