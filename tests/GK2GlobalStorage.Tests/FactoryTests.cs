using System.Collections;
using HarmonyLib;
using NUnit.Framework;
using static GK2GlobalStorage.Tests.FakeGame;

namespace GK2GlobalStorage.Tests
{
    // Option "Factory": every belt step an empty belt cell asks the input chest behind it for one item
    // (CanGiveItem, then GiveItem). With the option on, a chest out of the item set on that cell's slot first gets a
    // stack of it from the other chests (patched CanGiveItem of Conveyor Chests, Garden/Alcohol Storage, Stockpiles).
    public sealed class FactoryTests : GameTest
    {
        private WorldZoneData factory;
        private WgoData farChest;
        private ConveyorWgoData input;
        private ConveyorWgoData cell;

        [SetUp]
        public void Areas()
        {
            // Chest + item pairs that recently could not be refilled wait 3 s before the next try.
            ((IDictionary)AccessTools.Field(typeof(FactoryPatches), "notAvailable").GetValue(null)).Clear();
            Item("wood", stackCount: 100);
            Item("stone", stackCount: 100);
            ObjectDef("chest");
            ObjectDef("conveyor_chest", inventorySize: 4).conveyorType = ConveyorElementType.Chest;
            ObjectDef("garden_storage", inventorySize: 20, storage: false).conveyorType = ConveyorElementType.ChestOut;
            ObjectDef("conveyor_cell", inventorySize: 1, storage: false).conveyorType = ConveyorElementType.Cell;
            ObjectDef("conveyor_splitter", inventorySize: 1, storage: false).conveyorType = ConveyorElementType.Splitter;
            WGODef stockpile = ObjectDef("wood_stockpile", inventorySize: 9);
            stockpile.conveyorType = ConveyorElementType.Pallet;
            stockpile.inventoryWhiteList.itemsIds.Add("wood");

            GameSceneData main = Scene("main");
            factory = Zone(main, "conveyor");
            farChest = Place(Zone(Scene("mine_level"), "mine"), "chest", items: ("wood", 250));
            input = PlaceConveyor(factory, "conveyor_chest");
            cell = PlaceConveyor(factory, "conveyor_cell");
            Config.Factory = true;
        }

        [Test]
        public void ChestOutOfTheSlotItemGetsAStackAndTheBeltItsItem()
        {
            Feed(input, cell, "wood");

            Assert.That(CanGive(input, cell), Is.True);
            Assert.That(Count(input, "wood"), Is.EqualTo(100));
            Assert.That(Count(farChest, "wood"), Is.EqualTo(150));

            Give(input, cell);

            Assert.That(Count(cell, "wood"), Is.EqualTo(1));
            Assert.That(Count(input, "wood"), Is.EqualTo(99));
        }

        [Test]
        public void NothingMovesWithTheOptionOff()
        {
            Config.Factory = false;
            Feed(input, cell, "wood");

            Assert.That(CanGive(input, cell), Is.False);
            Assert.That(Count(farChest, "wood"), Is.EqualTo(250));
        }

        [Test]
        public void NothingMovesWithTheModOff()
        {
            Config.Enabled = false;
            Feed(input, cell, "wood");

            Assert.That(CanGive(input, cell), Is.False);
            Assert.That(Count(farChest, "wood"), Is.EqualTo(250));
        }

        [Test]
        public void SlotWithoutAnItemStaysAsInTheGame()
        {
            Feed(input, cell);

            Assert.That(CanGive(input, cell), Is.False);
            Assert.That(Count(farChest, "wood"), Is.EqualTo(250));
        }

        [Test]
        public void FullCellPullsNothing()
        {
            // The belt is stopped: the cell takes nothing this step.
            Feed(input, cell, "wood");
            Fill(cell.Inventory, ("stone", 1));

            CanGive(input, cell);

            Assert.That(Count(input, "wood"), Is.Zero);
            Assert.That(Count(farChest, "wood"), Is.EqualTo(250));
        }

        [Test]
        public void ChestThatStillHasTheItemIsNotRefilled()
        {
            Feed(input, cell, "wood");
            Fill(input.Inventory, ("wood", 1));

            Assert.That(CanGive(input, cell), Is.True);
            Assert.That(Count(input, "wood"), Is.EqualTo(1));
            Assert.That(Count(farChest, "wood"), Is.EqualTo(250));
        }

        [Test]
        public void OnlyWhatTheOtherChestsHoldIsMoved()
        {
            WgoData smallChest = Place(Zone(Scene("main"), "yard"), "chest", items: ("stone", 30));
            Feed(input, cell, "stone");

            Assert.That(CanGive(input, cell), Is.True);
            Assert.That(Count(input, "stone"), Is.EqualTo(30));
            Assert.That(Count(smallChest, "stone"), Is.Zero);
        }

        [Test]
        public void NothingMovesWhenTheChestHasNoRoom()
        {
            ObjectDef("tiny_conveyor_chest", inventorySize: 1).conveyorType = ConveyorElementType.Chest;
            ConveyorWgoData tiny = PlaceConveyor(factory, "tiny_conveyor_chest", ("stone", 100));
            Feed(tiny, cell, "wood");

            Assert.That(CanGive(tiny, cell), Is.False);
            Assert.That(Count(farChest, "wood"), Is.EqualTo(250));
        }

        [Test]
        public void ChestThatFeedsABeltIsNotASource()
        {
            // Two input chests set to the same item must not pass it back and forth.
            farChest.Inventory.RemoveItemById("wood", 250);
            ConveyorWgoData otherInput = PlaceConveyor(factory, "conveyor_chest", ("wood", 50));
            Feed(otherInput, PlaceConveyor(factory, "conveyor_cell"), "wood");
            Feed(input, cell, "wood");

            Assert.That(CanGive(input, cell), Is.False);
            Assert.That(Count(otherInput, "wood"), Is.EqualTo(50));
        }

        [Test]
        public void ChestAtTheEndOfALineIsASource()
        {
            // What one line delivers into a conveyor chest can feed another line.
            farChest.Inventory.RemoveItemById("wood", 250);
            ConveyorWgoData lineEnd = PlaceConveyor(factory, "conveyor_chest", ("wood", 40));
            Feed(input, cell, "wood");

            Assert.That(CanGive(input, cell), Is.True);
            Assert.That(Count(input, "wood"), Is.EqualTo(40));
            Assert.That(Count(lineEnd, "wood"), Is.Zero);
        }

        [Test]
        public void StockpileGetsItsOnlyItem()
        {
            ConveyorWgoData stockpile = PlaceConveyor(factory, "wood_stockpile");
            Feed(stockpile, cell);

            Assert.That(CanGive(stockpile, cell), Is.True);
            Assert.That(Count(stockpile, "wood"), Is.EqualTo(100));

            Give(stockpile, cell);

            Assert.That(Count(cell, "wood"), Is.EqualTo(1));
        }

        [Test]
        public void StockpileForSeveralItemsIsNotRefilled()
        {
            WGODef mixed = ObjectDef("mixed_stockpile", inventorySize: 9);
            mixed.conveyorType = ConveyorElementType.Pallet;
            mixed.inventoryWhiteList.itemsIds.Add("wood");
            mixed.inventoryWhiteList.itemsIds.Add("stone");
            ConveyorWgoData stockpile = PlaceConveyor(factory, "mixed_stockpile");
            Feed(stockpile, cell);

            Assert.That(CanGive(stockpile, cell), Is.False);
            Assert.That(Count(farChest, "wood"), Is.EqualTo(250));
        }

        [Test]
        public void GardenStorageSlotItemIsRefilled()
        {
            ConveyorWgoData garden = PlaceConveyor(factory, "garden_storage");
            Feed(garden, cell, "wood");

            Assert.That(CanGive(garden, cell), Is.True);
            Assert.That(Count(garden, "wood"), Is.EqualTo(100));
        }

        [Test]
        public void GardenStorageIsNotRefilledForASplitter()
        {
            // Splitters do not take from Garden or Alcohol Storage.
            ConveyorWgoData garden = PlaceConveyor(factory, "garden_storage");
            ConveyorWgoData splitter = PlaceConveyor(factory, "conveyor_splitter");
            Feed(garden, splitter, "wood");

            CanGive(garden, splitter);

            Assert.That(Count(garden, "wood"), Is.Zero);
            Assert.That(Count(farChest, "wood"), Is.EqualTo(250));
        }

        [Test]
        public void HiddenChestIsNotRefilled()
        {
            // The town workshop belts the game switches off.
            Set(input, "isHidden", true);
            Feed(input, cell, "wood");

            Assert.That(CanGive(input, cell), Is.False);
            Assert.That(Count(farChest, "wood"), Is.EqualTo(250));
        }

        [Test]
        public void ChestInAnExcludedAreaIsNotRefilled()
        {
            Config.ExcludedZones.Add("conveyor");
            Feed(input, cell, "wood");

            Assert.That(CanGive(input, cell), Is.False);
            Assert.That(Count(farChest, "wood"), Is.EqualTo(250));
        }

        [Test]
        public void ChestsOfExcludedAreasAreNotUsedAsSources()
        {
            Config.ExcludedZones.Add("mine");
            GlobalStorage.Invalidate();
            Feed(input, cell, "wood");

            Assert.That(CanGive(input, cell), Is.False);
            Assert.That(Count(farChest, "wood"), Is.EqualTo(250));
        }

        [Test]
        public void AfterAFailedRefillTheSameItemWaitsBeforeTheNextTry()
        {
            Feed(input, cell, "stone");
            Assert.That(CanGive(input, cell), Is.False);
            Fill(farChest.Inventory, ("stone", 5));

            // Belts ask every step; the next try comes after a few seconds.
            Assert.That(CanGive(input, cell), Is.False);
            Assert.That(Count(farChest, "stone"), Is.EqualTo(5));
        }

        private static bool CanGive(ConveyorWgoData giver, ConveyorWgoData receiver)
        {
            return giver.ConveyorComponent.CanGiveItem(receiver.ConveyorComponent);
        }

        // One item onto the belt, as an empty cell takes it.
        private static void Give(ConveyorWgoData giver, ConveyorWgoData receiver)
        {
            receiver.Inventory.AddItemsToInventory(giver.ConveyorComponent.GiveItem(receiver.ConveyorComponent));
        }

        private static int Count(WgoData wgoData, string itemId)
        {
            return wgoData.Inventory.Data.GetTotalCountInInventory(itemId);
        }
    }
}
