using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GK2GlobalStorage
{
    // Optional performance report (settings: "Performance report"). While on, every 30 s it writes to
    // Player.log and GK2GlobalStorage-perf.txt:
    //  - frame times (avg, p95, p99, max, frames over 50 ms) and garbage collections,
    //  - full duration of the game methods the mod touches (open chest, workbench, vendor, pickup...),
    //  - time spent in the mod's own code.
    // Each report is labelled with the "Global storage" switch state, and switching it flushes the current
    // report, so ON and OFF can be compared in one session doing the same things.
    // Off: one bool check per instrumented call, and the timing patches are not even installed.
    public sealed class Perf : MonoBehaviour
    {
        private const float ReportSeconds = 30f;
        private const float HitchMs = 50f;
        // A gameplay frame this long is reported individually with what ran in it.
        private const float StutterMs = 25f;
        private const int MaxStutterLines = 40;
        // Frames right after an area change still stream objects in; keep them out of gameplay stats.
        private const float AfterTransitSeconds = 1f;

        private sealed class Stat
        {
            public long calls;
            public long ticks;
            public long maxTicks;
        }

        public static bool Active { get; private set; }

        private static readonly object sync = new object();
        private static readonly Dictionary<string, Stat> game = new Dictionary<string, Stat>();
        private static readonly Dictionary<string, Stat> mod = new Dictionary<string, Stat>();
        private static readonly List<float> frames = new List<float>(4096);
        private static readonly List<string> transitions = new List<string>();

        // Area transition in progress (door, house exit, travel to another level).
        private static long transitStart;
        private static long transitLoaded;
        private static string transitFromScene;
        private static float startedAt;
        private static int gcAtStart;
        private static bool skipFrame;
        private static float transitEndedAt = -100f;

        // What ran since the previous Update, i.e. (mostly) in the frame that just ended.
        private static long frameModTicks;
        private static string frameTopName;
        private static long frameTopTicks;
        private static int lastGcCount;
        private static readonly List<string> stutters = new List<string>();
        private static int stutterCount;
        private static int stutterWithGc;
        private static int stutterWithMod;
        private static int excludedFrames;

        public static string FilePath => Path.Combine(Application.persistentDataPath, "GK2GlobalStorage-perf.txt");

        // --- instrumentation of the mod's own code ---

        public static long Begin()
        {
            return Active ? Stopwatch.GetTimestamp() : 0L;
        }

        public static void End(string name, long start)
        {
            if (start != 0L)
            {
                Record(mod, name, Stopwatch.GetTimestamp() - start);
            }
        }

        // Used by the timing patches around game methods.
        internal static void RecordGame(string name, long start)
        {
            if (start != 0L)
            {
                Record(game, name, Stopwatch.GetTimestamp() - start);
            }
        }

        // --- area transitions: control taken -> player placed in the new area (loaded) -> control back (total) ---

        internal static void TransitBegin()
        {
            if (!Active)
            {
                return;
            }
            transitStart = Stopwatch.GetTimestamp();
            transitLoaded = 0L;
            transitFromScene = MainGame.PlayerData?.currentGameSceneId;
        }

        internal static void TransitLoaded()
        {
            if (transitStart != 0L && transitLoaded == 0L)
            {
                transitLoaded = Stopwatch.GetTimestamp();
            }
        }

        internal static void TransitEnd()
        {
            if (transitStart == 0L)
            {
                return;
            }
            long end = Stopwatch.GetTimestamp();
            long loaded = transitLoaded != 0L ? transitLoaded : end;
            string toScene = MainGame.PlayerData?.currentGameSceneId;
            bool transitOtherLevel = toScene != null && transitFromScene != null && toScene != transitFromScene;
            string transitDestination = (toScene ?? "?") + " / " + (MainGame.PlayerData?.CurrentWorldZoneData?.id ?? "no area");
            string kind = transitOtherLevel ? "Area change, other level" : "Area change, same level";
            Record(game, kind + ": loading", loaded - transitStart);
            Record(game, kind + ": total", end - transitStart);
            double msPerTick = 1000.0 / Stopwatch.Frequency;
            string line = string.Format("{0} -> {1}: loading {2:0} ms, total {3:0} ms",
                transitOtherLevel ? "other level" : "same level", transitDestination,
                (loaded - transitStart) * msPerTick, (end - transitStart) * msPerTick);
            lock (sync)
            {
                transitions.Add(line);
            }
            Debug.Log("[GK2GlobalStorage][Perf] Area change " + line + " (Global storage " + (Config.Enabled ? "ON" : "OFF") + ")");
            transitStart = 0L;
            transitEndedAt = Time.realtimeSinceStartup;
        }

        private static void Record(Dictionary<string, Stat> table, string name, long ticks)
        {
            lock (sync)
            {
                if (!table.TryGetValue(name, out Stat stat))
                {
                    table[name] = stat = new Stat();
                }
                stat.calls++;
                stat.ticks += ticks;
                if (ticks > stat.maxTicks)
                {
                    stat.maxTicks = ticks;
                }
                if (ReferenceEquals(table, mod))
                {
                    frameModTicks += ticks;
                }
                if (ticks > frameTopTicks)
                {
                    frameTopTicks = ticks;
                    frameTopName = name;
                }
            }
        }

        // --- on / off ---

        public static void SetActive(bool value)
        {
            if (value == Active)
            {
                return;
            }
            if (value)
            {
                if (!PerfPatchesInstaller.Install())
                {
                    Debug.LogWarning("[GK2GlobalStorage][Perf] Timing patches unavailable; only frame times and mod code are measured");
                }
                Active = true;
                Reset();
                Debug.Log("[GK2GlobalStorage][Perf] Performance report on, file: " + FilePath);
            }
            else
            {
                Flush("report turned off");
                Active = false;
            }
        }

        // Writes what was collected so far (if anything) and starts a new report.
        public static void Flush(string reason)
        {
            if (!Active)
            {
                return;
            }
            try
            {
                string report = Build(reason);
                if (report != null)
                {
                    Debug.Log(report);
                    File.AppendAllText(FilePath, report + Environment.NewLine + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Perf report", ex);
            }
            Reset();
        }

        private static void Reset()
        {
            lock (sync)
            {
                game.Clear();
                mod.Clear();
                frames.Clear();
                transitions.Clear();
                stutters.Clear();
                stutterCount = stutterWithGc = stutterWithMod = excludedFrames = 0;
                frameModTicks = frameTopTicks = 0L;
                frameTopName = null;
            }
            startedAt = Time.realtimeSinceStartup;
            gcAtStart = lastGcCount = GC.CollectionCount(0);
            skipFrame = true;
        }

        // Normal gameplay: in game, not in an area change and not in the second after one.
        private static bool IsGameplayFrame()
        {
            try
            {
                if (MainGame.Instance == null || MainGame.Instance.gameState != MainGame.GameState.InGame)
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
            return transitStart == 0L && Time.realtimeSinceStartup - transitEndedAt > AfterTransitSeconds;
        }

        private void Update()
        {
            if (!Active)
            {
                return;
            }
            float ms = Time.unscaledDeltaTime * 1000f;
            int gc = GC.CollectionCount(0);
            bool gcInFrame = gc != lastGcCount;
            lastGcCount = gc;
            if (skipFrame)
            {
                skipFrame = false;
            }
            else if (!IsGameplayFrame())
            {
                excludedFrames++;
            }
            else
            {
                frames.Add(ms);
                if (ms > StutterMs)
                {
                    RecordStutter(ms, gcInFrame);
                }
            }
            lock (sync)
            {
                frameModTicks = frameTopTicks = 0L;
                frameTopName = null;
            }
            if (Time.realtimeSinceStartup - startedAt >= ReportSeconds)
            {
                Flush("interval");
            }
        }

        private static void RecordStutter(float ms, bool gcInFrame)
        {
            double msPerTick = 1000.0 / Stopwatch.Frequency;
            double modMs;
            string top;
            lock (sync)
            {
                modMs = frameModTicks * msPerTick;
                top = frameTopName == null ? "none" : $"{frameTopName} {frameTopTicks * msPerTick:0.00} ms";
            }
            stutterCount++;
            if (gcInFrame)
            {
                stutterWithGc++;
            }
            if (modMs >= 1.0)
            {
                stutterWithMod++;
            }
            string line = $"{ms:0.0} ms frame: GC {(gcInFrame ? "YES" : "no")}, mod code {modMs:0.00} ms, longest measured call: {top}";
            if (stutters.Count < MaxStutterLines)
            {
                stutters.Add(line);
            }
            Debug.Log("[GK2GlobalStorage][Perf] Stutter " + line + " (Global storage " + (Config.Enabled ? "ON" : "OFF") + ")");
        }

        private static string Build(string reason)
        {
            float seconds = Time.realtimeSinceStartup - startedAt;
            if (seconds < 1f || frames.Count == 0)
            {
                return null;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("[GK2GlobalStorage][Perf] ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                .Append("  ").Append(seconds.ToString("0.0")).Append(" s  Global storage ").Append(Config.Enabled ? "ON" : "OFF")
                .Append("  (").Append(reason).Append(", mod ").Append(Plugin.Version).Append(')').AppendLine();

            List<float> sorted = new List<float>(frames);
            sorted.Sort();
            float avg = sorted.Average();
            sb.AppendFormat("  Gameplay frames: {0}, avg {1:0.00} ms ({2:0} fps), p50 {3:0.00}, p95 {4:0.00}, p99 {5:0.00}, p99.9 {6:0.00}, max {7:0.00} ms, over {8:0} ms: {9}, over {10:0} ms: {11}",
                sorted.Count, avg, 1000f / avg, Pct(sorted, 0.50f), Pct(sorted, 0.95f), Pct(sorted, 0.99f), Pct(sorted, 0.999f), sorted[sorted.Count - 1],
                StutterMs, sorted.Count(f => f > StutterMs), HitchMs, sorted.Count(f => f > HitchMs)).AppendLine();
            sb.AppendFormat("  Excluded frames (menu, loading, area changes + {0:0} s after): {1}; GC gen0 collections in total: {2}",
                AfterTransitSeconds, excludedFrames, GC.CollectionCount(0) - gcAtStart).AppendLine();

            lock (sync)
            {
                Table(sb, "Game action (full time, including the mod)", game);
                Table(sb, "Mod code only", mod);
                if (stutterCount > 0)
                {
                    sb.AppendFormat("  Gameplay stutters over {0:0} ms: {1} (with GC: {2}, with >= 1 ms of mod code: {3}):",
                        StutterMs, stutterCount, stutterWithGc, stutterWithMod).AppendLine();
                    foreach (string line in stutters)
                    {
                        sb.AppendLine("    " + line);
                    }
                    if (stutterCount > stutters.Count)
                    {
                        sb.AppendLine("    ... " + (stutterCount - stutters.Count) + " more");
                    }
                }
                if (transitions.Count > 0)
                {
                    sb.AppendLine("  Area changes (loading = until you are placed in the new area; total = until you can move):");
                    foreach (string line in transitions)
                    {
                        sb.AppendLine("    " + line);
                    }
                }
            }
            return sb.ToString().TrimEnd();
        }

        private static float Pct(List<float> sorted, float p)
        {
            int i = Mathf.Clamp(Mathf.CeilToInt(p * sorted.Count) - 1, 0, sorted.Count - 1);
            return sorted[i];
        }

        private static void Table(StringBuilder sb, string title, Dictionary<string, Stat> table)
        {
            sb.AppendLine("  " + title + ":");
            if (table.Count == 0)
            {
                sb.AppendLine("    (no calls)");
                return;
            }
            sb.AppendLine(string.Format("    {0,-34}{1,9}{2,11}{3,11}{4,12}", "", "calls", "avg ms", "max ms", "total ms"));
            double msPerTick = 1000.0 / Stopwatch.Frequency;
            foreach (KeyValuePair<string, Stat> row in table.OrderByDescending(r => r.Value.ticks))
            {
                Stat s = row.Value;
                sb.AppendLine(string.Format("    {0,-34}{1,9}{2,11:0.000}{3,11:0.000}{4,12:0.0}",
                    row.Key, s.calls, s.ticks * msPerTick / s.calls, s.maxTicks * msPerTick, s.ticks * msPerTick));
            }
        }
    }
}
