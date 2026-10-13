using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GK2GlobalStorage.Tests
{
    // Runs once before any test: Unity's log goes to a list instead of the engine (which is not running), the
    // config defaults are remembered so every test can start from them, and the fake game is set up with the
    // mod's patches applied.
    [SetUpFixture]
    public sealed class TestSetup
    {
        [OneTimeSetUp]
        public void Start()
        {
            Debug.unityLogger.logHandler = TestLog.Instance;
            ConfigDefaults.Capture();
            FakeGame.Install();
        }
    }

    // Tests that run against the fake game start from an empty save with default settings.
    public abstract class GameTest
    {
        [SetUp]
        public void NewWorld()
        {
            FakeGame.NewWorld();
            TestLog.Instance.Lines.Clear();
        }
    }

    internal sealed class TestLog : ILogHandler
    {
        public static readonly TestLog Instance = new TestLog();

        public readonly List<string> Lines = new List<string>();

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            Lines.Add(logType + ": " + (args == null || args.Length == 0 ? format : string.Format(format, args)));
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            Lines.Add("Exception: " + exception);
        }
    }

    internal static class ConfigDefaults
    {
        private static string defaults;

        public static void Capture()
        {
            defaults ??= Config.Render();
        }

        // Back to the values the mod ships with (the rendered file of a fresh start, parsed again).
        public static void Restore()
        {
            Config.Parse(defaults.Split('\n'));
        }
    }
}
