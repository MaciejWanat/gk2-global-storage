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
        public void EveryCountedPatchIsApplied()
        {
            Assert.That(FakeGame.PatchesApplied, Is.EqualTo(Plugin.PatchCount), string.Join("\n", FakeGame.PatchLog));
        }

        [Test]
        public void NoPatchReportsAMissingTargetOrAFailure()
        {
            // Includes the chest window and craft finish patches, which are not part of the count.
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
                .Where(m => Harmony.GetPatchInfo(m)?.Transpilers.Any(p => p.owner == Patches.HarmonyId) == true)
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
