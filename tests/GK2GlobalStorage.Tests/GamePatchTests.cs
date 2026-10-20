using System;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using NUnit.Framework;

namespace GK2GlobalStorage.Tests
{
    // The mod's real patches, applied to the installed game's DLLs by FakeGame.Install (in this test process
    // only): a game update that renames or removes a patched method, field or quest call shows up here instead
    // of in Player.log.
    public sealed class GamePatchTests
    {
        [Test]
        public void EveryHookIsApplied()
        {
            Assert.That(FakeGame.PatchesApplied, Is.EqualTo(Plugin.PatchCount), string.Join("\n", FakeGame.PatchLog));
            Assert.That(Hooks.Unloaded.Select(h => Hooks.Name(h) + ": " + Hooks.Failure(h)), Is.Empty);
        }

        [Test]
        public void NoPatchReportsAMissingTargetOrAFailure()
        {
            Assert.That(FakeGame.PatchLog.Where(line => line.StartsWith("Warning") || line.StartsWith("Error") || line.StartsWith("Exception")), Is.Empty);
        }

        [Test]
        public void QuestCallsAreRedirected()
        {
            Match match = FakeGame.PatchLog.Select(line => Regex.Match(line, @"Quest patch: (\d+) method\(s\) redirected")).FirstOrDefault(m => m.Success);

            Assert.That(match, Is.Not.Null, string.Join("\n", FakeGame.PatchLog));
            Assert.That(int.Parse(match.Groups[1].Value), Is.GreaterThan(0));
        }

        [Test]
        public void EveryQuestTargetTypeHasARedirectedMethod()
        {
            string[] patchedTypes = Harmony.GetAllPatchedMethods()
                .Where(m => Harmony.GetPatchInfo(m)?.Transpilers.Any(p => p.owner == Hooks.HarmonyId(Hook.Quests)) == true)
                .Select(m => OuterType(m.DeclaringType).FullName)
                .Distinct()
                .ToArray();

            Assert.That(patchedTypes, Is.SupersetOf(new[]
            {
                "QuestFinishCheck",
                "UIMultiAnswerOption",
                "UIMultiAnswerIcon",
                "UIQuestInfoWindow",
                "GK2.FlowCanvasNodes.Flow_HasItem",
                "GK2.FlowCanvasNodes.Flow_AddItem",
                "LazyExpression",
            }));
        }

        [TestCase("ReservoirInteractionHandler", "Interact")]
        [TestCase("UIFishingWindowData", "UpdateAvailableFishingAndBaits")]
        public void FishingBaitReadersAreRedirected(string type, string method)
        {
            HarmonyLib.Patches patches = Harmony.GetPatchInfo(AccessTools.DeclaredMethod(AccessTools.TypeByName(type), method));

            Assert.That(patches?.Transpilers.Select(p => p.owner), Does.Contain(Hooks.HarmonyId(Hook.Fishing)));
        }

        [Test]
        public void CastingIsWatchedForTheBaitRemoval()
        {
            HarmonyLib.Patches cast = Harmony.GetPatchInfo(AccessTools.DeclaredMethod(typeof(UIFishingWindow), "OnSubmitPressed"));
            HarmonyLib.Patches remove = Harmony.GetPatchInfo(AccessTools.Method(typeof(Inventory), nameof(Inventory.RemoveItemById),
                new[] { typeof(string), typeof(int), typeof(Item), typeof(Item), typeof(bool) }));

            Assert.That(cast?.Prefixes.Select(p => p.owner), Does.Contain(Hooks.HarmonyId(Hook.Fishing)));
            Assert.That(cast?.Finalizers.Select(p => p.owner), Does.Contain(Hooks.HarmonyId(Hook.Fishing)));
            Assert.That(remove?.Postfixes.Select(p => p.owner), Does.Contain(Hooks.HarmonyId(Hook.Fishing)));
        }

        private static Type OuterType(Type type)
        {
            while (type.DeclaringType != null)
            {
                type = type.DeclaringType;
            }
            return type;
        }
    }
}
