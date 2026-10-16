using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LazyBearTechnology;
using NUnit.Framework;

namespace GK2GlobalStorage.Tests
{
    // The other mods check: real Harmony patches made in this test process by a "foreign" owner on the installed
    // game's methods, read back the way the check reads them in the game (every Harmony copy, by reflection).
    public sealed class ModConflictsTests
    {
        private const string OtherMod = "com.example.biggerbag";
        private const string OwnPerfId = Patches.HarmonyId + ".perf";

        private static readonly KeyValuePair<string, string>[] NoPlugins = new KeyValuePair<string, string>[0];

        private static MethodBase CanAddCount => AccessTools.Method(typeof(Item), nameof(Item.CanAddItemCountToInventory),
            new[] { typeof(Item), typeof(bool), typeof(Item), typeof(bool) });

        [SetUp]
        public void Reset()
        {
            ConfigDefaults.Restore();
        }

        [TearDown]
        public void Unpatch()
        {
            new Harmony(OtherMod).UnpatchAll(OtherMod);
            new Harmony(OwnPerfId).UnpatchAll(OwnPerfId);
        }

        private static void Postfix()
        {
        }

        private static List<ModConflicts.Finding> PatchAndCheck(string owner, MethodBase target)
        {
            new Harmony(owner).Patch(target, postfix: new HarmonyMethod(typeof(ModConflictsTests), nameof(Postfix)));
            return ModConflicts.Classify(ModConflicts.ReadPatches(), NoPlugins);
        }

        [Test]
        public void TheModsOwnPatchesAreNotReported()
        {
            Assert.That(ModConflicts.ReadPatches().Select(p => p.Owner), Has.Some.EqualTo(Patches.HarmonyId), "the mod's patches are applied");

            Assert.That(ModConflicts.Classify(ModConflicts.ReadPatches(), NoPlugins), Is.Empty);
        }

        [Test]
        public void AnotherModChangingBagSpaceIsReported()
        {
            List<ModConflicts.Finding> found = PatchAndCheck(OtherMod, CanAddCount);

            Assert.That(found, Has.Count.EqualTo(1));
            Assert.That(found[0].Mod, Is.EqualTo(OtherMod));
            Assert.That(found[0].Name, Is.EqualTo(typeof(ModConflictsTests).Assembly.GetName().Name), "named after the assembly its patches live in");
            Assert.That(found[0].Areas, Is.EquivalentTo(new[] { ModConflicts.Space }));
            Assert.That(found[0].Methods, Is.EquivalentTo(new[] { "Item.CanAddItemCountToInventory" }));
        }

        [Test]
        public void AnotherModChangingPickupsIsReported()
        {
            List<ModConflicts.Finding> found = PatchAndCheck(OtherMod, AccessTools.Method(typeof(PlayerData), nameof(PlayerData.CollectDrop)));

            Assert.That(found.Single().Areas, Is.EquivalentTo(new[] { ModConflicts.Pickup }));
        }

        [Test]
        public void AnotherModOnAMethodThisModPatchesIsReported()
        {
            List<ModConflicts.Finding> found = PatchAndCheck(OtherMod, AccessTools.Method(typeof(WgoData), nameof(WgoData.GetCraftableMultiInventory)));

            Assert.That(found.Single().Areas, Is.EquivalentTo(new[] { ModConflicts.Workbenches }));
        }

        [Test]
        public void TheModsOtherHarmonyIdsAreNotReported()
        {
            // The performance report and the speed-up patch under their own ids (gk2globalstorage.perf / .speedup).
            Assert.That(PatchAndCheck(OwnPerfId, CanAddCount), Is.Empty);
        }

        [Test]
        public void PatchesOnUnrelatedGameMethodsAreNotReported()
        {
            ModConflicts.PatchOwner[] patches =
            {
                new ModConflicts.PatchOwner(AccessTools.Method(typeof(PlayerData), nameof(PlayerData.AddOverheadItem)), OtherMod, "BiggerBag"),
                new ModConflicts.PatchOwner(AccessTools.Method(typeof(LanguageModLoader), nameof(LanguageModLoader.AppendLanguages)), OtherMod, "BiggerBag"),
                new ModConflicts.PatchOwner(AccessTools.PropertyGetter(typeof(Item), nameof(Item.IsBag)), OtherMod, "BiggerBag"),
            };

            Assert.That(ModConflicts.Classify(patches, NoPlugins), Is.Empty);
        }

        [TestCase("GetTotalCountInInventory", ModConflicts.Counts)]
        [TestCase("HasItemQuantityInInventory", ModConflicts.Counts)]
        [TestCase("RemoveItemFromInventoryById", ModConflicts.Counts)]
        [TestCase("RemoveAllItems", ModConflicts.Counts)]
        [TestCase("AddItemToInventory", ModConflicts.Space)]
        [TestCase("CanAddItemToInventory", ModConflicts.Space)]
        [TestCase("CanAddItemCountToInventory", ModConflicts.Space)]
        [TestCase("CalculateInventoryFillSize", ModConflicts.Space)]
        public void ItemMethodsAreSortedByWhatTheyDo(string method, string area)
        {
            MethodBase target = AccessTools.GetDeclaredMethods(typeof(Item)).First(m => m.Name == method);

            Assert.That(ModConflicts.AreaOf(target, patchedByThisMod: false), Is.EqualTo(area));
        }

        [Test]
        public void APluginNamedLikeAnInventoryModIsReported()
        {
            KeyValuePair<string, string>[] plugins =
            {
                new KeyValuePair<string, string>("nizyl.betterplayerinventory", "Better Player Inventory"),
                new KeyValuePair<string, string>("nizyl.fastermovement", "Faster Player Movement"),
            };

            List<ModConflicts.Finding> found = ModConflicts.Classify(new ModConflicts.PatchOwner[0], plugins);

            Assert.That(found.Select(f => f.Name), Is.EquivalentTo(new[] { "Better Player Inventory" }));
            Assert.That(found[0].Mod, Is.EqualTo("nizyl.betterplayerinventory"));
            Assert.That(found[0].Areas, Is.EquivalentTo(new[] { ModConflicts.ByName }));
        }

        [Test]
        public void APluginsPatchesAreListedUnderThePluginName()
        {
            ModConflicts.PatchOwner[] patches = { new ModConflicts.PatchOwner(CanAddCount, "nizyl.betterplayerinventory", "BetterPlayerInventory") };
            KeyValuePair<string, string>[] plugins = { new KeyValuePair<string, string>("nizyl.betterplayerinventory", "Better Player Inventory") };

            List<ModConflicts.Finding> found = ModConflicts.Classify(patches, plugins);

            Assert.That(found, Has.Count.EqualTo(1));
            Assert.That(found[0].Name, Is.EqualTo("Better Player Inventory"));
            Assert.That(found[0].Areas, Is.EquivalentTo(new[] { ModConflicts.Space }), "what it patches, not a guess from its name");
        }

        [TestCase("Better Player Inventory", true)]
        [TestCase("Better Item Stacks", true)]
        [TestCase("Big Item Stacking", true)]
        [TestCase("Better Container", true)]
        [TestCase("Better Bag Storage", true)]
        [TestCase("Linked Storage - Shared Crafting and Storage Browser", true)]
        [TestCase("Auto Pickup", true)]
        [TestCase("Faster Player Movement (Walk and Sprint)", false)]
        [TestCase("Instant Fishing", false)]
        [TestCase("Better Energy Control", false)]
        [TestCase("Move Buildings", false)]
        [TestCase("Shopping List", false)]
        public void InventoryModsAreRecognisedByName(string name, bool expected)
        {
            Assert.That(ModConflicts.NameSuggestsInventory(name), Is.EqualTo(expected));
        }

        [Test]
        public void DismissedModsAreNoLongerNoticed()
        {
            ModConflicts.PatchOwner[] patches =
            {
                new ModConflicts.PatchOwner(CanAddCount, OtherMod, "BiggerBag"),
                new ModConflicts.PatchOwner(CanAddCount, "linked.storage", "LinkedStorage"),
            };
            List<ModConflicts.Finding> found = ModConflicts.Classify(patches, NoPlugins);
            Config.IgnoredMods.Add(OtherMod);

            Assert.That(ModConflicts.NeedsNotice(found).Select(f => f.Mod), Is.EquivalentTo(new[] { "linked.storage" }));
        }

        [Test]
        public void NothingIsNoticedWithTheWarningOff()
        {
            List<ModConflicts.Finding> found = ModConflicts.Classify(new[] { new ModConflicts.PatchOwner(CanAddCount, OtherMod, "BiggerBag") }, NoPlugins);
            Config.WarnOtherMods = false;

            Assert.That(ModConflicts.NeedsNotice(found), Is.Empty);
        }
    }
}
