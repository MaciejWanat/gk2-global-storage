using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace GK2GlobalStorage
{
    // Harmony-free entry points, so SpeedUp and the settings window can be compiled without touching Harmony types.
    internal static class SpeedUpPatchesInstaller
    {
        public static int Install()
        {
            try
            {
                return SpeedUpPatches.Install();
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Speed-up patches", ex);
                return 0;
            }
        }

        public static void Uninstall()
        {
            try
            {
                SpeedUpPatches.Uninstall();
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Speed-up patches removal", ex);
            }
        }
    }

    // The speed-up's patches (see SpeedUp), under their own Harmony id so they can be removed when it is turned off.
    internal static class SpeedUpPatches
    {
        public const string HarmonyId = Patches.HarmonyId + ".speedup";
        public const int Count = 5;

        private static Harmony harmony;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Install()
        {
            harmony = harmony ?? new Harmony(HarmonyId);
            int ok = 0;
            ok += Patch("PlayerController.SetControlTakenType",
                AccessTools.Method(typeof(PlayerController), nameof(PlayerController.SetControlTakenType)), postfix: nameof(ControlPostfix));
            ok += Patch("PlayerController.Teleport",
                AccessTools.Method(typeof(PlayerController), nameof(PlayerController.Teleport)), prefix: nameof(TeleportPrefix));
            ok += Patch("UIBasicFade.FadeIn",
                AccessTools.Method(typeof(UIBasicFade), nameof(UIBasicFade.FadeIn), new[] { typeof(float), typeof(Action), typeof(FadeFlag), typeof(bool) }),
                prefix: nameof(FadePrefix));
            ok += Patch("UIBasicFade.FadeOut",
                AccessTools.Method(typeof(UIBasicFade), nameof(UIBasicFade.FadeOut), new[] { typeof(float), typeof(Action), typeof(FadeFlag) }),
                prefix: nameof(FadePrefix));
            ok += Patch("MainGame.HiddenOptimization",
                AccessTools.Method(typeof(MainGame), nameof(MainGame.HiddenOptimization)), prefix: nameof(SweepPrefix));
            return ok;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Uninstall()
        {
            harmony?.UnpatchAll(HarmonyId);
        }

        private static int Patch(string label, MethodBase target, string prefix = null, string postfix = null)
        {
            if (target == null)
            {
                Debug.LogWarning("[GK2GlobalStorage] Speed-up: patch target not found (game update?): " + label);
                return 0;
            }
            try
            {
                harmony.Patch(target,
                    prefix: prefix == null ? null : new HarmonyMethod(typeof(SpeedUpPatches), prefix),
                    postfix: postfix == null ? null : new HarmonyMethod(typeof(SpeedUpPatches), postfix));
                return 1;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GK2GlobalStorage] Speed-up: cannot patch " + label + ": " + ex.Message);
                return 0;
            }
        }

        // Control is taken for a move before the fade starts and given back after the fade back has ended.
        private static void ControlPostfix(TakenControlType t, bool isEnabled)
        {
            try
            {
                if (t == TakenControlType.ByTeleport)
                {
                    SpeedUp.Transit(!isEnabled, Time.unscaledTime);
                }
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Speed-up move", ex);
            }
        }

        // The pause in black before the player is moved (doors, house exits, teleports).
        private static void TeleportPrefix(TeleportDataBase teleportData)
        {
            if (teleportData != null)
            {
                teleportData.delayInFade = 0f;
            }
        }

        // Both fades of a move: Teleport takes control before fading in and gives it back once faded out.
        private static void FadePrefix(ref float fadeTime)
        {
            try
            {
                fadeTime = SpeedUp.Fade(fadeTime, Time.unscaledTime);
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Speed-up fades", ex);
            }
        }

        // Same as the game's method without Resources.UnloadUnusedAssets, when SpeedUp skips this sweep.
        private static bool SweepPrefix(ref UniTask __result)
        {
            try
            {
                if (!SpeedUp.SkipSweep(Time.unscaledTime))
                {
                    return true;
                }
                EditorOnlyComponentStripper.StripAll();
                __result = UniTask.CompletedTask;
                return false;
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Speed-up level change", ex);
                return true;
            }
        }
    }
}
