using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using NUnit.Framework;

namespace GK2GlobalStorage.Tests
{
    // The startup compile check (src/Guard.cs) and how a hook goes in (Patches.TryInstall). Code that cannot run is
    // made with Mono.Security: the tests compile against the game's copy, but it is not next to them at run time, so
    // compiling a method that uses it fails the way mod code naming something the installed game lacks fails there.
    public sealed class GuardTests
    {
        private const string TestId = "gk2globalstorage.tests.guard";

        [TearDown]
        public void TakeOutTestPatches()
        {
            new Harmony(TestId).UnpatchAll(TestId);
        }

        // --- the check ---

        [Test]
        public void CodeThatCanRunPasses()
        {
            Assert.That(Guard.Check(Method(nameof(Fine))), Is.Null);
        }

        [Test]
        public void AMethodThatCannotRunIsFound()
        {
            Assert.That(Guard.Check(Method(nameof(UsesMissingAssembly))), Does.Contain(nameof(UsesMissingAssembly)).And.Contain("FileNotFoundException"));
        }

        [Test]
        public void MethodsItCallsAreCheckedToo()
        {
            Assert.That(Guard.Check(Method(nameof(CallsBrokenMethod))), Does.Contain(nameof(UsesMissingAssembly)));
        }

        [Test]
        public void LambdasItCreatesAreCheckedToo()
        {
            Assert.That(Guard.Check(Method(nameof(MakesBrokenLambda))), Does.Contain(nameof(UsesMissingAssembly)));
        }

        [Test]
        public void IteratorBodiesAreCheckedToo()
        {
            Assert.That(Guard.Check(Method(nameof(BrokenIterator))), Does.Contain(nameof(UsesMissingAssembly)));
        }

        // FakeGame.Install checked every hook: the mod's code they reach (not a hook of its own) was compiled too.
        [TestCase(typeof(GlobalStorage), nameof(GlobalStorage.Storage))]
        [TestCase(typeof(GlobalStorage), nameof(GlobalStorage.CountInChests))]
        [TestCase(typeof(PickerFilter), nameof(PickerFilter.HideEmptyCells))]
        public void TheModsCodeTheHooksReachIsChecked(Type type, string method)
        {
            Assert.That(Guard.WasChecked(AccessTools.Method(type, method)), Is.True);
        }

        // --- a hook going in ---

        [Test]
        public void AHookWhoseCodeCannotRunIsNotPatched()
        {
            bool applied = false;

            string failure = Patches.TryInstall(TestId, () => "Some.Method: MissingFieldException", h => applied = true);

            Assert.That(applied, Is.False);
            Assert.That(failure, Does.StartWith("Its code cannot run on this game version").And.Contain("MissingFieldException"));
        }

        [Test]
        public void AHookThatGoesInStaysIn()
        {
            string failure = Patches.TryInstall(TestId, () => null, h => PatchTarget(h));

            Assert.That(failure, Is.Null);
            Assert.That(TargetPrefixOwners(), Does.Contain(TestId));
        }

        [Test]
        public void AHookThatFailsHalfwayIsTakenOutAgain()
        {
            string failure = Patches.TryInstall(TestId, () => null, h =>
            {
                PatchTarget(h);
                throw new InvalidOperationException("second patch refused");
            });

            Assert.That(failure, Does.Contain("second patch refused"));
            Assert.That(TargetPrefixOwners(), Does.Not.Contain(TestId));
        }

        [Test]
        public void AHookMissingAGameMethodIsTakenOutAgain()
        {
            string failure = Patches.TryInstall(TestId, () => null, h => PatchTarget(h) && false);

            Assert.That(failure, Does.Contain("not found"));
            Assert.That(TargetPrefixOwners(), Does.Not.Contain(TestId));
        }

        private static IEnumerable<MethodBase> Method(string name)
        {
            return new MethodBase[] { AccessTools.Method(typeof(GuardTests), name) };
        }

        private static bool PatchTarget(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(GuardTests), nameof(Target)), prefix: new HarmonyMethod(typeof(GuardTests), nameof(TargetPrefix)));
            return true;
        }

        private static IEnumerable<string> TargetPrefixOwners()
        {
            return Harmony.GetPatchInfo(AccessTools.Method(typeof(GuardTests), nameof(Target)))?.Prefixes.Select(p => p.owner) ?? Enumerable.Empty<string>();
        }

        private static int Fine()
        {
            return Environment.TickCount;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object UsesMissingAssembly()
        {
            return new Mono.Security.ASN1();
        }

        private static object CallsBrokenMethod()
        {
            return UsesMissingAssembly();
        }

        private static Func<object> MakesBrokenLambda()
        {
            return () => UsesMissingAssembly();
        }

        private static IEnumerable<object> BrokenIterator()
        {
            yield return UsesMissingAssembly();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Target()
        {
            return 1;
        }

        private static void TargetPrefix()
        {
        }
    }
}
