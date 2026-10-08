using System;
using System.Runtime.CompilerServices;
using System.Threading;
using SoftMasking;
using TMPro;
using UnityEngine;

// The game has no code-mod loader. It does, however, resolve the "preprocessors" type names listed in
// Languages/<pack>/language.json with Type.GetType at startup, which loads this assembly from Managed.
// Loading the assembly runs the module initializer below, which schedules the real start on the main thread.
// Hook and SoftMaskStarter are fallbacks in case the module initializer does not run.

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute
    {
    }
}

namespace GK2GlobalStorage
{
    internal static class ModuleInit
    {
        [ModuleInitializer]
        internal static void Init()
        {
            AutoStart.Request("ModuleInit");
        }
    }

    // Referenced by language.json. Only instantiated if someone actually selects the loader pack as language.
    public sealed class Hook : MonoBehaviour, ITextPreprocessor
    {
        private void Awake()
        {
            AutoStart.Request("Hook");
        }

        public string PreprocessText(string text)
        {
            return text;
        }
    }

    // SoftMask scans loaded assemblies for [GlobalMaterialReplacer] types and instantiates them.
    [GlobalMaterialReplacer]
    public sealed class SoftMaskStarter : IMaterialReplacer
    {
        public int order => int.MaxValue;

        public SoftMaskStarter()
        {
            AutoStart.Request("SoftMask");
        }

        public Material Replace(Material material)
        {
            return null;
        }
    }

    internal static class AutoStart
    {
        private static int requested;

        public static void Request(string via)
        {
            if (Interlocked.Exchange(ref requested, 1) == 1)
            {
                return;
            }
            Debug.Log($"[GK2GlobalStorage] Start trigger: {via}");
            Canvas.willRenderCanvases += OnMainThread;
        }

        private static void OnMainThread()
        {
            Canvas.willRenderCanvases -= OnMainThread;
            try
            {
                Launcher.Start();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GK2GlobalStorage] Failed to start: " + ex);
            }
        }
    }
}
