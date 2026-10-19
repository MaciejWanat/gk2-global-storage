using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using NUnit.Framework;
using static GK2GlobalStorage.Tests.FakeGame;

namespace GK2GlobalStorage.Tests
{
    // Option "Factory", choosing a belt slot's item: while the game is in its "set this slot" mode, a section with
    // every item of the other chests is added below the chest (FactorySlotPicker). The game's mode switch itself
    // draws widgets, which needs the engine, so the patch's prefix is called the way Harmony calls it.
    public sealed class FactorySlotPickerTests : GameTest
    {
        private static readonly MethodInfo modePrefix = AccessTools.Method(typeof(FactorySlotPicker), "ModePrefix");

        private WorldZoneData factory;
        private WgoData farChest;
        private ConveyorWgoData input;

        [SetUp]
        public void Areas()
        {
            Item("wood", stackCount: 100);
            Item("stone", stackCount: 100);
            ObjectDef("chest");
            ObjectDef("conveyor_chest", inventorySize: 4).conveyorType = ConveyorElementType.Chest;
            ObjectDef("conveyor_cell", inventorySize: 1, storage: false).conveyorType = ConveyorElementType.Cell;
            factory = Zone(Scene("main"), "conveyor");
            farChest = Place(Zone(Scene("mine_level"), "mine"), "chest", items: ("wood", 250));
            input = PlaceConveyor(factory, "conveyor_chest");
            Config.Factory = true;
        }

        [Test]
        public void CatalogHasOneCellPerItemOfTheOtherChestsWithTheTotalCount()
        {
            Place(Zone(Scene("main"), "yard"), "chest", items: new[] { ("wood", 5), ("stone", 30) });

            Assert.That(Cells(FactorySlotPicker.Catalog(input)), Is.EqualTo(new[] { ("stone", 30), ("wood", 255) }));
        }

        [Test]
        public void CatalogFollowsTheGameItemOrder()
        {
            Item("apple").sortOrder = 2;
            Item("nail").sortOrder = 1;
            Fill(farChest.Inventory, ("apple", 1), ("nail", 1));

            Assert.That(Cells(FactorySlotPicker.Catalog(input)).Select(c => c.id), Is.EqualTo(new[] { "wood", "nail", "apple" }));
        }

        [Test]
        public void ItemsTheChestHoldsAreLeftOut()
        {
            // Its own cells offer them.
            Fill(input.Inventory, ("wood", 1));
            Fill(farChest.Inventory, ("stone", 3));

            Assert.That(Cells(FactorySlotPicker.Catalog(input)), Is.EqualTo(new[] { ("stone", 3) }));
        }

        [Test]
        public void ChestsThatFeedABeltAreNotCounted()
        {
            // The refill never takes from them, so what only they hold could not arrive.
            ConveyorWgoData otherInput = PlaceConveyor(factory, "conveyor_chest", ("stone", 40), ("wood", 10));
            Feed(otherInput, PlaceConveyor(factory, "conveyor_cell"), "stone");

            Assert.That(Cells(FactorySlotPicker.Catalog(input)), Is.EqualTo(new[] { ("wood", 250) }));
        }

        [Test]
        public void ChestAtTheEndOfALineIsCounted()
        {
            // What one line delivers into a conveyor chest can feed another line.
            PlaceConveyor(factory, "conveyor_chest", ("stone", 40));

            Assert.That(Cells(FactorySlotPicker.Catalog(input)), Is.EqualTo(new[] { ("stone", 40), ("wood", 250) }));
        }

        [Test]
        public void ItemsInBagsCount()
        {
            Item("bag").isBag = true;
            Item bag = new Item("bag", 1);
            bag.Inventory.Add(new Item("stone", 7));
            farChest.Inventory.Data.Inventory.Add(bag);

            Assert.That(Cells(FactorySlotPicker.Catalog(input)), Is.EqualTo(new[] { ("stone", 7), ("wood", 250) }));
        }

        [Test]
        public void OnlyItemsTheChestsFiltersLetIn()
        {
            // Garden Storage takes vegetable bags only.
            WhiteListItemFilter vegetables = new WhiteListItemFilter();
            vegetables.itemsIds.Add("stone");
            input.Inventory.Data.AddProperty(new WhiteListFilterSerializedItemProperty(vegetables));
            Fill(farChest.Inventory, ("stone", 3));

            Assert.That(Cells(FactorySlotPicker.Catalog(input)), Is.EqualTo(new[] { ("stone", 3) }));
        }

        [Test]
        public void BlackListedItemsAreLeftOut()
        {
            BlackListItemFilter noWood = new BlackListItemFilter();
            noWood.itemsIds.Add("wood");
            input.Inventory.Data.AddProperty(new BlackListFilterSerializedItemProperty(noWood));
            Fill(farChest.Inventory, ("stone", 3));

            Assert.That(Cells(FactorySlotPicker.Catalog(input)), Is.EqualTo(new[] { ("stone", 3) }));
        }

        [Test]
        public void ExcludedAreasAreNotListed()
        {
            Config.ExcludedZones.Add("mine");
            GlobalStorage.Invalidate();

            Assert.That(FactorySlotPicker.Catalog(input), Is.Null);
        }

        [Test]
        public void NoOtherChestsNoCatalog()
        {
            farChest.Inventory.RemoveItemById("wood", 250);

            Assert.That(FactorySlotPicker.Catalog(input), Is.Null);
        }

        [Test]
        public void SettingASlotAddsTheSectionBelowTheChestAndLeavingTakesItOut()
        {
            UIConveyorChestWindow window = Window(input);
            MultiInventoryWidgetData chestSide = ChestSide(window);
            int before = chestSide.inventoriesData.Count;

            Mode(window, isActive: true, wasActive: false);

            Assert.That(chestSide.inventoriesData, Has.Count.EqualTo(before + 1));
            InventoryWidgetData section = (InventoryWidgetData)chestSide.inventoriesData.Last();
            Assert.That(section.InventoryHeaderWidgetData.CustomHeaderId, Is.EqualTo(FactorySlotPicker.Header));
            Assert.That(Cells(section.Inventory), Is.EqualTo(new[] { ("wood", 250) }));
            // Never the chest side's selected inventory, where the game moves items to.
            Assert.That(section.OnWidgetPressed, Is.Null);
            Assert.That(chestSide.SelectedWidgetData, Is.SameAs(chestSide.inventoriesData[0]));

            Mode(window, isActive: false, wasActive: true);

            Assert.That(chestSide.inventoriesData, Has.Count.EqualTo(before));
            Assert.That(chestSide.inventoriesData, Does.Not.Contain(section));
        }

        [Test]
        public void NoSectionWithTheOptionOff()
        {
            Config.Factory = false;
            UIConveyorChestWindow window = Window(input);
            int before = ChestSide(window).inventoriesData.Count;

            Mode(window, isActive: true, wasActive: false);

            Assert.That(ChestSide(window).inventoriesData, Has.Count.EqualTo(before));
        }

        [Test]
        public void NoSectionWithTheModOff()
        {
            Config.Enabled = false;
            UIConveyorChestWindow window = Window(input);
            int before = ChestSide(window).inventoriesData.Count;

            Mode(window, isActive: true, wasActive: false);

            Assert.That(ChestSide(window).inventoriesData, Has.Count.EqualTo(before));
        }

        [Test]
        public void NoSecondSectionWhenTheModeIsAlreadyOn()
        {
            UIConveyorChestWindow window = Window(input);
            int before = ChestSide(window).inventoriesData.Count;

            Mode(window, isActive: true, wasActive: false);
            Mode(window, isActive: true, wasActive: true);

            Assert.That(ChestSide(window).inventoriesData, Has.Count.EqualTo(before + 1));
        }

        [Test]
        public void SectionIsTakenOutEvenAfterTheOptionWasSwitchedOff()
        {
            UIConveyorChestWindow window = Window(input);
            int before = ChestSide(window).inventoriesData.Count;
            Mode(window, isActive: true, wasActive: false);

            Config.Factory = false;
            Mode(window, isActive: false, wasActive: true);

            Assert.That(ChestSide(window).inventoriesData, Has.Count.EqualTo(before));
        }

        [TestCase(typeof(UIConveyorChestWindow))]
        [TestCase(typeof(UIConveyorVegetablesChestWindow))]
        [TestCase(typeof(UIConveyorWineChestWindow))]
        public void SlotSettingModeOfEveryFactoryChestWindowIsPatched(System.Type window)
        {
            MethodInfo mode = AccessTools.DeclaredMethod(window, "SetChestItemSettingMode", new[] { typeof(bool), typeof(ConveyorChestSlotData) });

            Assert.That(Harmony.GetPatchInfo(mode)?.Prefixes.Select(p => p.owner), Does.Contain(Hooks.HarmonyId(Hook.FactorySlotPicker)));
        }

        // The window the game opens for a Conveyor Chest, with only its data (no engine objects).
        private static UIConveyorChestWindow Window(WgoData chest)
        {
            UIConveyorChestWindow window = Uninitialized<UIConveyorChestWindow>();
            Set(window, "data", new UIBaseChestWindowData(Player.inventory, null, chest));
            return window;
        }

        private static MultiInventoryWidgetData ChestSide(UIConveyorChestWindow window)
        {
            return ((UIBaseChestWindowData)AccessTools.Field(typeof(UIConveyorChestWindow), "data").GetValue(window)).SecondMultiInventoryData;
        }

        // What Harmony runs before UIConveyorChestWindow.SetChestItemSettingMode(isActive, slot).
        private static void Mode(UIConveyorChestWindow window, bool isActive, bool wasActive)
        {
            modePrefix.Invoke(null, new object[] { window, isActive, wasActive });
        }

        private static IEnumerable<(string id, int count)> Cells(Inventory inventory)
        {
            Assert.That(inventory, Is.Not.Null);
            Assert.That(inventory.Data.InventorySize, Is.EqualTo(inventory.Data.Inventory.Count), "no empty cells");
            return inventory.Data.Inventory.Select(i => (i.id, i.Count)).ToArray();
        }
    }
}
