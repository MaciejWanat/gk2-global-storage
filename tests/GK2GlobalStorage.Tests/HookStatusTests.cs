using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace GK2GlobalStorage.Tests
{
    // What a hook left out (it cannot run on the installed game version, see src/Guard.cs) does to the settings window
    // and the main menu badge: the options it takes away are greyed out, shown OFF and point to Player.log, the menu
    // says how many patches are unloaded, and with nothing loaded at all the mod is off.
    public sealed class HookStatusTests
    {
        private readonly List<Hook> failed = new List<Hook>();

        // Every hook is in on the installed game (GamePatchTests), so the test failures are undone by recording them in.
        [TearDown]
        public void Restore()
        {
            foreach (Hook hook in failed)
            {
                Hooks.Record(hook, null);
            }
            failed.Clear();
            Hooks.ModOff = false;
        }

        private void Fail(Hook hook)
        {
            failed.Add(hook);
            Hooks.Record(hook, "test");
        }

        [Test]
        public void EveryOptionIsAvailableWhenEveryHookIsIn()
        {
            Assert.That(SettingsWindow.StateOf("Pickups go to nearest chest"), Is.EqualTo(Availability.Available));
            Assert.That(SettingsWindow.StateOf("Vendor windows"), Is.EqualTo(Availability.Available));
        }

        [Test]
        public void AnOptionWhoseHookIsUnloadedIsUnavailable()
        {
            Fail(Hook.Pickups);

            Assert.That(SettingsWindow.StateOf("Pickups go to nearest chest"), Is.EqualTo(Availability.Unavailable));
            Assert.That(SettingsWindow.StateOf("Also in the factory"), Is.EqualTo(Availability.Unavailable));
            Assert.That(SettingsWindow.StateOf("Workbenches"), Is.EqualTo(Availability.Available));
        }

        [Test]
        public void TwoOptionsOnOneHookAreBothUnavailable()
        {
            Fail(Hook.Workbenches);

            Assert.That(SettingsWindow.StateOf("Workbenches"), Is.EqualTo(Availability.Unavailable));
            Assert.That(SettingsWindow.StateOf("Automatic crafters"), Is.EqualTo(Availability.Unavailable));
        }

        [Test]
        public void AnOptionLosingOnlyAPartStaysEditable()
        {
            Fail(Hook.VendorOrder);

            Assert.That(SettingsWindow.StateOf("Vendor windows"), Is.EqualTo(Availability.Partly));
        }

        [Test]
        public void EveryOptionIsUnavailableWhenTheModIsOff()
        {
            Hooks.ModOff = true;

            Assert.That(SettingsWindow.StateOf("Global storage"), Is.EqualTo(Availability.Unavailable));
            Assert.That(SettingsWindow.StateOf("Fuel containers"), Is.EqualTo(Availability.Unavailable));
        }

        // A new hook must say which option it belongs to; the language list hook only hides the loader pack.
        [Test]
        public void EveryHookBelongsToAnOption()
        {
            Assert.That(Hooks.All.Where(h => h != Hook.LanguageList), Is.SubsetOf(SettingsWindow.RowHooks().Distinct()));
        }

        [Test]
        public void TheMenuSaysHowManyPatchesAreUnloaded()
        {
            Assert.That(MenuBadge.Status(13, true, 1, out string color), Is.EqualTo("on, 1 patch unloaded (Shift+F2)"));
            Assert.That(color, Is.Not.Null);
            Assert.That(MenuBadge.Status(12, true, 2, out _), Is.EqualTo("on, 2 patches unloaded (Shift+F2)"));
        }

        [Test]
        public void TheMenuSaysOnWhenEverythingIsLoaded()
        {
            Assert.That(MenuBadge.Status(Hooks.Total, true, 0, out string color), Is.EqualTo("on"));
            Assert.That(color, Is.Null);
        }

        [Test]
        public void TheMenuSaysOffWhenSwitchedOffInTheSettings()
        {
            Assert.That(MenuBadge.Status(Hooks.Total, false, 1, out _), Is.EqualTo("off (Shift+F2)"));
        }

        [Test]
        public void TheMenuSaysTheModFailedWhenNothingLoaded()
        {
            Assert.That(MenuBadge.Status(-1, true, Hooks.Total, out _), Is.EqualTo("off, failed to load (see Player.log)"));
        }
    }
}
