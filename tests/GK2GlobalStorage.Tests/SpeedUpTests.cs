using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using NUnit.Framework;

namespace GK2GlobalStorage.Tests
{
    // The optional speed-up for doors and teleports: when a move counts as under way, which fades are shortened,
    // which level changes skip the asset sweep, and its patches on the installed game (only while the option is on).
    public sealed class SpeedUpTests
    {
        private static readonly MethodBase[] Targets =
        {
            AccessTools.Method(typeof(PlayerController), nameof(PlayerController.SetControlTakenType)),
            AccessTools.Method(typeof(PlayerController), nameof(PlayerController.Teleport)),
            AccessTools.Method(typeof(UIBasicFade), nameof(UIBasicFade.FadeIn), new[] { typeof(float), typeof(Action), typeof(FadeFlag), typeof(bool) }),
            AccessTools.Method(typeof(UIBasicFade), nameof(UIBasicFade.FadeOut), new[] { typeof(float), typeof(Action), typeof(FadeFlag) }),
            AccessTools.Method(typeof(MainGame), nameof(MainGame.HiddenOptimization)),
        };

        [SetUp]
        public void Reset()
        {
            ConfigDefaults.Restore();
            SpeedUp.Apply();
            SpeedUp.Reset();
        }

        [TearDown]
        public void Off()
        {
            Config.SpeedUp = false;
            SpeedUp.Apply();
            SpeedUp.Reset();
        }

        private static IEnumerable<string> Owners(MethodBase method)
        {
            return Harmony.GetPatchInfo(method)?.Owners ?? Enumerable.Empty<string>();
        }

        [Test]
        public void AMoveIsUnderWayFromTakingControlUntilGivingItBack()
        {
            Assert.That(SpeedUp.InTransit(10f), Is.False);

            SpeedUp.Transit(true, 10f);
            Assert.That(SpeedUp.InTransit(11f), Is.True);

            SpeedUp.Transit(false, 12f);
            Assert.That(SpeedUp.InTransit(12f), Is.False);
        }

        [Test]
        public void AMoveThatNeverGivesControlBackEndsAfterTheTimeout()
        {
            SpeedUp.Transit(true, 10f);

            Assert.That(SpeedUp.InTransit(10f + SpeedUp.TransitTimeout - 1f), Is.True);
            Assert.That(SpeedUp.InTransit(10f + SpeedUp.TransitTimeout), Is.False);
        }

        [Test]
        public void FadesAreShortenedOnlyDuringAMove()
        {
            Assert.That(SpeedUp.Fade(0.3f, 5f), Is.EqualTo(0.3f), "sleep, cutscenes and other fades keep their time");

            SpeedUp.Transit(true, 10f);

            Assert.That(SpeedUp.Fade(0.3f, 10f), Is.EqualTo(SpeedUp.FadeTime));
            Assert.That(SpeedUp.Fade(0.05f, 10f), Is.EqualTo(0.05f), "an already short fade is not made longer");
        }

        [Test]
        public void EveryFifthLevelChangeStillSweeps()
        {
            bool[] skipped = Enumerable.Range(0, 2 * SpeedUp.SweepEvery).Select(i =>
            {
                SpeedUp.Transit(true, i);
                bool skip = SpeedUp.SkipSweep(i);
                SpeedUp.Transit(false, i);
                return skip;
            }).ToArray();

            Assert.That(skipped, Is.EqualTo(new[] { true, true, true, true, false, true, true, true, true, false }));
        }

        [Test]
        public void SweepsOutsideAMoveAreNotSkipped()
        {
            // Loading a save sweeps too, with no move under way.
            Assert.That(SpeedUp.SkipSweep(5f), Is.False);
        }

        [Test]
        public void NothingIsPatchedWhileTheOptionIsOff()
        {
            Assert.That(SpeedUp.Installed, Is.False);
            Assert.That(Targets.SelectMany(Owners), Has.None.EqualTo(SpeedUpPatches.HarmonyId));
        }

        [Test]
        public void TurningItOnPatchesTheGameAndTurningItOffRemovesThePatches()
        {
            Config.SpeedUp = true;
            SpeedUp.Apply();

            Assert.That(SpeedUp.Installed, Is.True);
            foreach (MethodBase target in Targets)
            {
                Assert.That(Owners(target), Has.Some.EqualTo(SpeedUpPatches.HarmonyId), target.DeclaringType.Name + "." + target.Name);
            }
            Assert.That(TestLog.Instance.Lines, Has.Some.Contains("Speed-up on (" + SpeedUpPatches.Count + "/" + SpeedUpPatches.Count + " patches)"));

            Config.SpeedUp = false;
            SpeedUp.Apply();

            Assert.That(SpeedUp.Installed, Is.False);
            Assert.That(Targets.SelectMany(Owners), Has.None.EqualTo(SpeedUpPatches.HarmonyId));
        }

        [Test]
        public void TheSpeedUpPatchesDoNotCountAsAnotherMod()
        {
            Config.SpeedUp = true;
            SpeedUp.Apply();

            Assert.That(ModConflicts.Classify(ModConflicts.ReadPatches(), new KeyValuePair<string, string>[0]), Is.Empty);
        }
    }
}
