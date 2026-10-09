using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace GK2GlobalStorage
{
    // Loads Harmony, then the mod (GK2GlobalStorage.Core.dll) from bytes, then calls its Plugin.Init.
    //
    // Why two assemblies: this one is loaded by the game from Managed\ (default load context), where Mono
    // will not bind a reference to a Harmony copy living in a subfolder. A byte-loaded assembly has no load
    // path, so its Harmony reference goes through AssemblyResolve below and gets the copy already in memory.
    //
    // Auto-update: when subscribed on the Steam Workshop, Steam keeps a copy of the mod in
    // steamapps\workshop\content\4358690\<WorkshopId>\CopyToGameFolder\... The core (and Harmony) are taken
    // from whichever copy is newer, so Workshop updates apply on the next game start. This loader itself is
    // never updated that way, so it must stay small and stable.
    internal static class Launcher
    {
        private const string AppId = "4358690";
        private const string Folder = "GK2GlobalStorage";
        private const string HarmonyName = "0Harmony";
        private const string CoreName = "GK2GlobalStorage.Core";

        private static Assembly harmony;
        private static Assembly core;

        public static void Start()
        {
            if (core != null)
            {
                return;
            }
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;

            string local = LocalFolder();
            string workshop = WorkshopFolder();
            Version localVersion = CoreVersion(local);
            Version workshopVersion = CoreVersion(workshop);
            // Newer wins; on a tie prefer the Workshop copy (it is what Steam last delivered).
            bool useWorkshop = workshopVersion != null && (localVersion == null || workshopVersion >= localVersion);
            string source = useWorkshop ? "Workshop" : "game folder";
            string dir = useWorkshop ? workshop : local;
            if (dir == null || (useWorkshop ? workshopVersion : localVersion) == null)
            {
                Debug.LogWarning("[GK2GlobalStorage] " + CoreName + ".dll not found in " + local);
                return;
            }
            Debug.Log($"[GK2GlobalStorage] Using the {source} copy {(useWorkshop ? workshopVersion : localVersion)}"
                + (workshopVersion != null && localVersion != null ? $" (game folder {localVersion}, Workshop {workshopVersion})" : ""));

            harmony = Find(HarmonyName, 2);
            if (harmony != null)
            {
                Debug.Log("[GK2GlobalStorage] Reusing Harmony " + harmony.GetName().Version + " loaded by another mod");
            }
            else
            {
                harmony = LoadHarmony(dir) ?? LoadHarmony(useWorkshop ? local : workshop);
            }

            string corePath = Path.Combine(dir, CoreName + ".dll");
            try
            {
                core = Assembly.Load(File.ReadAllBytes(corePath));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GK2GlobalStorage] Cannot load " + corePath + ": " + ex.Message);
                return;
            }
            AppDomain.CurrentDomain.SetData("GK2GlobalStorage.CoreSource", source);
            MethodInfo init = core.GetType("GK2GlobalStorage.Plugin", throwOnError: true)
                .GetMethod("Init", BindingFlags.Public | BindingFlags.Static);
            init.Invoke(null, new object[] { harmony != null });
        }

        // Managed\GK2GlobalStorage\, next to this assembly.
        private static string LocalFolder()
        {
            string managed = null;
            try
            {
                managed = Path.GetDirectoryName(typeof(Launcher).Assembly.Location);
            }
            catch
            {
            }
            if (string.IsNullOrEmpty(managed))
            {
                managed = Path.Combine(Application.dataPath, "Managed");
            }
            return Path.Combine(managed, Folder);
        }

        // steamapps\workshop\content\4358690\<id>\CopyToGameFolder\GraveyardKeeper2_Data\Managed\GK2GlobalStorage,
        // or null when this build has no Workshop id. Only our own pinned item id is ever looked at.
        private static string WorkshopFolder()
        {
            try
            {
                string id = typeof(Launcher).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                    .OfType<AssemblyMetadataAttribute>()
                    .FirstOrDefault(a => a.Key == "GK2GlobalStorage.WorkshopId")?.Value;
                if (string.IsNullOrEmpty(id) || !id.All(char.IsDigit))
                {
                    return null;
                }
                // <library>\steamapps\common\Graveyard Keeper 2\GraveyardKeeper2_Data -> <library>\steamapps
                string steamapps = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Application.dataPath)));
                return Path.Combine(steamapps, "workshop", "content", AppId, id, "CopyToGameFolder",
                    "GraveyardKeeper2_Data", "Managed", Folder);
            }
            catch
            {
                return null;
            }
        }

        private static Version CoreVersion(string dir)
        {
            if (dir == null)
            {
                return null;
            }
            string path = Path.Combine(dir, CoreName + ".dll");
            try
            {
                return File.Exists(path) ? AssemblyName.GetAssemblyName(path).Version : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GK2GlobalStorage] Cannot read " + path + ": " + ex.Message);
                return null;
            }
        }

        private static Assembly LoadHarmony(string dir)
        {
            if (dir == null)
            {
                return null;
            }
            string path = Path.Combine(dir, "0Harmony.dll");
            if (!File.Exists(path))
            {
                return null;
            }
            try
            {
                Assembly assembly = Assembly.LoadFrom(path);
                Debug.Log("[GK2GlobalStorage] Harmony " + assembly.GetName().Version + " loaded");
                return assembly;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GK2GlobalStorage] Cannot load " + path + ": " + ex.Message);
                return null;
            }
        }

        private static Assembly Find(string name, int major)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == name && (major < 0 || a.GetName().Version.Major == major));
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;
            if (name == HarmonyName)
            {
                return harmony ?? Find(HarmonyName, 2);
            }
            if (name == CoreName)
            {
                return core;
            }
            return null;
        }
    }
}
