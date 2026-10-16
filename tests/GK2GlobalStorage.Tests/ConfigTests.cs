using NUnit.Framework;

namespace GK2GlobalStorage.Tests
{
    public sealed class ConfigTests
    {
        [SetUp]
        public void Reset()
        {
            ConfigDefaults.Restore();
        }

        [Test]
        public void DefaultsMatchTheDocumentation()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Config.Enabled, Is.True, nameof(Config.Enabled));
                Assert.That(Config.Building, Is.True, nameof(Config.Building));
                Assert.That(Config.Workbenches, Is.True, nameof(Config.Workbenches));
                Assert.That(Config.Quests, Is.True, nameof(Config.Quests));
                Assert.That(Config.ShowOtherChests, Is.True, nameof(Config.ShowOtherChests));
                Assert.That(Config.Trading, Is.True, nameof(Config.Trading));
                Assert.That(Config.Pickers, Is.True, nameof(Config.Pickers));
                Assert.That(Config.PickersOnlyUsable, Is.True, nameof(Config.PickersOnlyUsable));
                Assert.That(Config.ChestWindows, Is.False, nameof(Config.ChestWindows));
                Assert.That(Config.ZombieWorkers, Is.False, nameof(Config.ZombieWorkers));
                Assert.That(Config.AutoCrafters, Is.False, nameof(Config.AutoCrafters));
                Assert.That(Config.Factory, Is.False, nameof(Config.Factory));
                Assert.That(Config.FuelContainers, Is.False, nameof(Config.FuelContainers));
                Assert.That(Config.PickupsToChest, Is.False, nameof(Config.PickupsToChest));
                Assert.That(Config.DeveloperMode, Is.False, nameof(Config.DeveloperMode));
                Assert.That(Config.PerfReport, Is.False, nameof(Config.PerfReport));
                Assert.That(Config.LogZones, Is.True, nameof(Config.LogZones));
                Assert.That(Config.ExcludedZones, Is.Empty, nameof(Config.ExcludedZones));
            });
        }

        [Test]
        public void RenderedFileReadsBackEveryOption()
        {
            Config.Enabled = false;
            Config.Building = false;
            Config.Workbenches = false;
            Config.Quests = false;
            Config.ShowOtherChests = false;
            Config.Trading = false;
            Config.Pickers = false;
            Config.PickersOnlyUsable = false;
            Config.ChestWindows = true;
            Config.ZombieWorkers = true;
            Config.AutoCrafters = true;
            Config.Factory = true;
            Config.FuelContainers = true;
            Config.PickupsToChest = true;
            Config.DeveloperMode = true;
            Config.PerfReport = true;
            Config.LogZones = false;
            Config.ExcludedZones.Add("graveyard");
            Config.ExcludedZones.Add("church");
            string file = Config.Render();

            ConfigDefaults.Restore();
            Config.Parse(file.Split('\n'));

            Assert.Multiple(() =>
            {
                Assert.That(Config.Enabled, Is.False);
                Assert.That(Config.Building, Is.False);
                Assert.That(Config.Workbenches, Is.False);
                Assert.That(Config.Quests, Is.False);
                Assert.That(Config.ShowOtherChests, Is.False);
                Assert.That(Config.Trading, Is.False);
                Assert.That(Config.Pickers, Is.False);
                Assert.That(Config.PickersOnlyUsable, Is.False);
                Assert.That(Config.ChestWindows, Is.True);
                Assert.That(Config.ZombieWorkers, Is.True);
                Assert.That(Config.AutoCrafters, Is.True);
                Assert.That(Config.Factory, Is.True);
                Assert.That(Config.FuelContainers, Is.True);
                Assert.That(Config.PickupsToChest, Is.True);
                Assert.That(Config.DeveloperMode, Is.True);
                Assert.That(Config.PerfReport, Is.True);
                Assert.That(Config.LogZones, Is.False);
                Assert.That(Config.ExcludedZones, Is.EquivalentTo(new[] { "church", "graveyard" }));
            });
        }

        [TestCase("true", true)]
        [TestCase("TRUE", true)]
        [TestCase("yes", true)]
        [TestCase("on", true)]
        [TestCase("1", true)]
        [TestCase("false", false)]
        [TestCase("No", false)]
        [TestCase("off", false)]
        [TestCase("0", false)]
        public void AcceptsCommonBooleanSpellings(string value, bool expected)
        {
            Config.Building = !expected;

            Config.Parse(new[] { "Building = " + value });

            Assert.That(Config.Building, Is.EqualTo(expected));
        }

        [Test]
        public void UnreadableValueKeepsTheCurrentSetting()
        {
            Config.Parse(new[] { "Building = maybe", "ZombieWorkers =" });

            Assert.That(Config.Building, Is.True);
            Assert.That(Config.ZombieWorkers, Is.False);
        }

        [Test]
        public void IgnoresCommentsBlankLinesUnknownKeysAndLinesWithoutValue()
        {
            Config.Parse(new[]
            {
                "# Enabled = false",
                "",
                "   ",
                "NoSuchOption = false",
                "Enabled false",
                "= false",
            });

            Assert.That(Config.Enabled, Is.True);
        }

        [Test]
        public void KeysAreCaseInsensitiveAndSpacesAreTrimmed()
        {
            Config.Parse(new[] { "   autocrafters=true   ", "QUESTS   =   off" });

            Assert.That(Config.AutoCrafters, Is.True);
            Assert.That(Config.Quests, Is.False);
        }

        [Test]
        public void OldOtherWindowsKeySetsChestWindows()
        {
            Config.Parse(new[] { "OtherWindows = true" });

            Assert.That(Config.ChestWindows, Is.True);
        }

        [Test]
        public void ExcludedZonesAreTrimmedSplitOnCommaOrSemicolonAndCaseInsensitive()
        {
            Config.Parse(new[] { "ExcludedZones =  Graveyard , church;; morgue ," });

            Assert.That(Config.ExcludedZones, Is.EquivalentTo(new[] { "Graveyard", "church", "morgue" }));
            Assert.That(Config.ExcludedZones.Contains("GRAVEYARD"), Is.True);
        }

        [Test]
        public void EmptyExcludedZonesClearsTheList()
        {
            Config.ExcludedZones.Add("graveyard");

            Config.Parse(new[] { "ExcludedZones =" });

            Assert.That(Config.ExcludedZones, Is.Empty);
        }

        [TestCase(true, true, true, true)]
        [TestCase(false, true, true, false)]
        [TestCase(true, false, true, false)]
        [TestCase(true, true, false, false)]
        public void VendorShowsChestsNeedsAllItsSwitches(bool enabled, bool showOther, bool trading, bool expected)
        {
            Config.Enabled = enabled;
            Config.ShowOtherChests = showOther;
            Config.Trading = trading;

            Assert.That(Config.VendorShowsChests, Is.EqualTo(expected));
        }

        [TestCase(true, true, true, true, true)]
        [TestCase(false, true, true, true, false)]
        [TestCase(true, false, true, true, false)]
        [TestCase(true, true, false, true, false)]
        [TestCase(true, true, true, false, false)]
        public void PickersShowChestsNeedsAllItsSwitches(bool enabled, bool building, bool showOther, bool pickers, bool expected)
        {
            Config.Enabled = enabled;
            Config.Building = building;
            Config.ShowOtherChests = showOther;
            Config.Pickers = pickers;

            Assert.That(Config.PickersShowChests, Is.EqualTo(expected));
        }

        [TestCase(true, true, true, true)]
        [TestCase(false, true, true, false)]
        [TestCase(true, false, true, false)]
        [TestCase(true, true, false, false)]
        public void ChestWindowShowsChestsNeedsAllItsSwitches(bool enabled, bool showOther, bool chestWindows, bool expected)
        {
            Config.Enabled = enabled;
            Config.ShowOtherChests = showOther;
            Config.ChestWindows = chestWindows;

            Assert.That(Config.ChestWindowShowsChests, Is.EqualTo(expected));
        }
    }
}
