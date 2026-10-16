using UnityEngine;

namespace GK2GlobalStorage
{
    // Optional, off by default (Config.SpeedUp): doors, house exits and teleports take less time.
    // - The 0.3 s pause in black between fading out and moving the player is skipped (TeleportDataBase.delayInFade).
    // - Both fades of the move take 0.1 s instead of 0.3 s.
    // - A move to another level skips Unity's sweep of unused assets (MainGame.HiddenOptimization, which the game
    //   itself skips on PS4). Every 5th level change still sweeps and loading a save always does, so memory is freed.
    // Nothing is patched while the option is off (SpeedUpPatches). Not meant to be combined with another mod's
    // speed-up for doors and teleports: both would patch the same methods.
    internal static class SpeedUp
    {
        public const float FadeTime = 0.1f;
        public const int SweepEvery = 5;
        // A move that never gives control back (failed load) does not leave the speed-up on.
        public const float TransitTimeout = 30f;

        private static float transitStart = -1f;
        private static int skippedSweeps;

        // The patches are in (Config.SpeedUp on and installed without errors).
        public static bool Installed { get; private set; }

        // Installs or removes the patches to match Config.SpeedUp.
        public static void Apply()
        {
            if (Config.SpeedUp == Installed)
            {
                return;
            }
            Reset();
            if (Config.SpeedUp)
            {
                int ok = SpeedUpPatchesInstaller.Install();
                Installed = ok > 0;
                Debug.Log("[GK2GlobalStorage] Speed-up on (" + ok + "/" + SpeedUpPatches.Count + " patches)");
            }
            else
            {
                SpeedUpPatchesInstaller.Uninstall();
                Installed = false;
                Debug.Log("[GK2GlobalStorage] Speed-up off");
            }
        }

        // Player control taken (starting = true) or given back for a move.
        internal static void Transit(bool starting, float now)
        {
            transitStart = starting ? now : -1f;
        }

        internal static bool InTransit(float now)
        {
            return transitStart >= 0f && now - transitStart < TransitTimeout;
        }

        // Fade duration to use: shortened only while a move is under way (sleep, cutscenes and the rest keep theirs).
        internal static float Fade(float requested, float now)
        {
            return InTransit(now) && requested > FadeTime ? FadeTime : requested;
        }

        // Asked when the game is about to sweep unused assets: true = skip this sweep.
        internal static bool SkipSweep(float now)
        {
            if (!InTransit(now))
            {
                return false;
            }
            if (skippedSweeps >= SweepEvery - 1)
            {
                skippedSweeps = 0;
                return false;
            }
            skippedSweeps++;
            return true;
        }

        internal static void Reset()
        {
            transitStart = -1f;
            skippedSweeps = 0;
        }
    }
}
