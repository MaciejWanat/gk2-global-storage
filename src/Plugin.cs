using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace GK2GlobalStorage
{
    // Entry point of the core, called by the loader (loader\Launcher.cs) once Harmony is in memory.
    public static class Plugin
    {
        internal const int PatchCount = 7;

        // Patches applied, or -1 when the mod could not patch the game at all.
        internal static int Patched { get; private set; } = -1;

        private static bool started;

        private static readonly HashSet<string> reported = new HashSet<string>();

        public static string Version => typeof(Plugin).Assembly.GetName().Version.ToString(3);

        // Where the player got the mod, for the main menu: "Steam Workshop", the channel named in
        // Managed\GK2GlobalStorage\source.txt (written into the GitHub and Nexus ZIPs), or "manual".
        public static string InstallSource
        {
            get
            {
                if (Source == "Workshop")
                {
                    return "Steam Workshop";
                }
                try
                {
                    string path = System.IO.Path.Combine(Application.dataPath, "Managed", "GK2GlobalStorage", "source.txt");
                    if (System.IO.File.Exists(path))
                    {
                        string channel = System.IO.File.ReadAllText(path).Trim();
                        if (channel.Length > 0 && channel.Length <= 40)
                        {
                            return channel;
                        }
                    }
                }
                catch
                {
                }
                return "manual";
            }
        }

        // "Workshop" or "game folder": which copy the loader picked (see loader\Launcher.cs).
        public static string Source => AppDomain.CurrentDomain.GetData("GK2GlobalStorage.CoreSource") as string ?? "game folder";

        public static void Init(bool harmonyAvailable)
        {
            if (started)
            {
                return;
            }
            started = true;
            Config.Load();
            // Settings page first: it does not need Harmony, so it works even if patching fails.
            GameObject host = new GameObject("GK2GlobalStorage");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<SettingsWindow>();
            host.AddComponent<MenuBadge>();
            if (!harmonyAvailable)
            {
                Debug.LogWarning("[GK2GlobalStorage] Patches not applied: Harmony is not available");
                return;
            }
            int ok;
            try
            {
                ok = ApplyPatches();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GK2GlobalStorage] Patches not applied: " + ex);
                return;
            }
            Patched = ok;
            host.AddComponent<Perf>();
            if (Config.DeveloperMode && Config.PerfReport)
            {
                Perf.SetActive(true);
            }
            Debug.Log($"[GK2GlobalStorage] Global Storage {Version} ({Source}) loaded ({ok}/{PatchCount} patches){(Config.Enabled ? "" : ", disabled in settings")}. Settings: Shift+F2");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ApplyPatches()
        {
            return Patches.Apply();
        }

        // Patches run very often; log each failure kind once instead of flooding Player.log.
        public static void ReportOnce(string where, Exception ex)
        {
            if (reported.Add(where))
            {
                Debug.LogWarning("[GK2GlobalStorage] Error in " + where + " (further errors here are not logged): " + ex);
            }
        }
    }
}
