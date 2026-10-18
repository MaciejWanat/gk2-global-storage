using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static GK2GlobalStorage.Tests.FakeGame;

namespace GK2GlobalStorage.Tests
{
    public sealed class GlobalStorageTests : GameTest
    {
        private WorldZoneData yard;
        private WorldZoneData graveyard;
        private WorldZoneData mine;

        [SetUp]
        public void Areas()
        {
            Item("wood");
            Item("stone");
            ObjectDef("chest");
            GameSceneData main = Scene("main");
            yard = Zone(main, "yard");
            graveyard = Zone(main, "graveyard");
            mine = Zone(Scene("mine_level"), "mine");
        }

        [Test]
        public void PoolHoldsTheChestsOfEveryZoneOnEveryLevel()
        {
            WgoData a = Place(yard, "chest");
            WgoData b = Place(graveyard, "chest");
            WgoData c = Place(mine, "chest");

            Assert.That(GlobalStorage.All(), Is.EqualTo(new[] { a.Inventory, b.Inventory, c.Inventory }));
        }

        [Test]
        public void ObjectsTheGameDoesNotTreatAsStorageAreLeftOut()
        {
            // open_in_multi_inventory = false, and not a factory chest.
            ObjectDef("display_stand", storage: false);
            Place(yard, "display_stand", items: ("wood", 5));

            Assert.That(GlobalStorage.All(), Is.Empty);
            Assert.That(GlobalStorage.CountInChests("wood"), Is.Zero);
        }

        [TestCase(ConveyorElementType.ChestOut)]
        [TestCase(ConveyorElementType.Chest)]
        [TestCase(ConveyorElementType.Pallet)]
        public void FactoryChestsCountEvenWhenTheGameDoesNotTreatThemAsStorage(ConveyorElementType type)
        {
            // Garden Storage and Alcohol Storage (ChestOut) have open_in_multi_inventory = false in the game data.
            ObjectDef("factory_chest", storage: false).conveyorType = type;
            WgoData chest = Place(Zone(Scene("main"), "conveyor"), "factory_chest", items: ("wood", 5));

            Assert.That(GlobalStorage.All(), Is.EqualTo(new[] { chest.Inventory }));
            Assert.That(GlobalStorage.CountInChests("wood"), Is.EqualTo(5));
        }

        [TestCase(ConveyorElementType.Cell)]
        [TestCase(ConveyorElementType.Splitter)]
        [TestCase(ConveyorElementType.Workbench)]
        public void BeltsAndFactoryWorkbenchesAreNotChests(ConveyorElementType type)
        {
            ObjectDef("belt_part", inventorySize: 1, storage: false).conveyorType = type;
            Place(Zone(Scene("main"), "conveyor"), "belt_part", items: ("wood", 1));

            Assert.That(GlobalStorage.All(), Is.Empty);
        }

        [Test]
        public void ObjectsWithoutInventoryAreLeftOut()
        {
            ObjectDef("sign", inventorySize: 0);
            Place(yard, "sign");

            Assert.That(GlobalStorage.All(), Is.Empty);
        }

        [Test]
        public void ZonesThatAreNotContainersAreLeftOut()
        {
            WorldZoneData road = Zone(Scene("outside"), "road", container: false);
            Place(road, "chest");

            Assert.That(GlobalStorage.All(), Is.Empty);
        }

        [Test]
        public void ExcludedZonesStayLocal()
        {
            Place(yard, "chest");
            WgoData kept = Place(mine, "chest");
            Config.ExcludedZones.Add("YARD");
            GlobalStorage.Invalidate();

            Assert.That(GlobalStorage.All(), Is.EqualTo(new[] { kept.Inventory }));
        }

        [Test]
        public void FuelContainersCountOnlyWhenEnabled()
        {
            WgoData fuel = Place(yard, "chest");
            fuel.Inventory.Data.AddProperty(new FuelContainerSerializedItemProperty());
            WgoData chest = Place(yard, "chest");

            Assert.That(GlobalStorage.All(), Is.EqualTo(new[] { chest.Inventory }));

            Config.FuelContainers = true;
            GlobalStorage.Invalidate();

            Assert.That(GlobalStorage.All(), Is.EqualTo(new[] { fuel.Inventory, chest.Inventory }));
        }

        [Test]
        public void NoSaveLoadedMeansNoChests()
        {
            Place(yard, "chest");
            Set(World, "cache", null);
            GlobalStorage.Invalidate();

            Assert.That(GlobalStorage.All(), Is.Empty);
        }

        [Test]
        public void PoolIsCachedUntilInvalidated()
        {
            Place(yard, "chest");
            Assert.That(GlobalStorage.All(), Has.Count.EqualTo(1));

            Place(mine, "chest");
            Assert.That(GlobalStorage.All(), Has.Count.EqualTo(1), "cached for a short while");

            GlobalStorage.Invalidate();
            Assert.That(GlobalStorage.All(), Has.Count.EqualTo(2));
        }

        [Test]
        public void AppendToKeepsTheListInFrontAndAddsEachChestOnce()
        {
            WgoData local = Place(yard, "chest");
            WgoData far = Place(mine, "chest");
            MultiInventory list = new MultiInventory();
            list.inventoryList.Add(Player.inventory);
            list.inventoryList.Add(local.Inventory);

            GlobalStorage.AppendTo(list);
            GlobalStorage.AppendTo(list);

            Assert.That(list.inventoryList, Is.EqualTo(new[] { Player.inventory, local.Inventory, far.Inventory }));
        }

        [Test]
        public void CountInChestsAddsUpEveryChest()
        {
            Place(yard, "chest", items: ("wood", 3));
            Place(mine, "chest", items: new[] { ("wood", 4), ("stone", 9) });

            Assert.That(GlobalStorage.CountInChests("wood"), Is.EqualTo(7));
        }

        [Test]
        public void RemoveFromChestsTakesInPoolOrderAndOnlyWhatIsAsked()
        {
            WgoData first = Place(yard, "chest", items: ("wood", 3));
            WgoData second = Place(graveyard, "chest", items: ("wood", 5));
            WgoData third = Place(mine, "chest", items: ("wood", 5));

            List<Item> removed = GlobalStorage.RemoveFromChests("wood", 6);

            Assert.That(removed.Sum(i => i.Count), Is.EqualTo(6));
            Assert.That(first.Inventory.Data.GetTotalCountInInventory("wood"), Is.Zero);
            Assert.That(second.Inventory.Data.GetTotalCountInInventory("wood"), Is.EqualTo(2));
            Assert.That(third.Inventory.Data.GetTotalCountInInventory("wood"), Is.EqualTo(5));
        }

        [Test]
        public void RemoveFromChestsReturnsWhatThereIsWhenNotEnough()
        {
            WgoData chest = Place(yard, "chest", items: ("wood", 2));

            List<Item> removed = GlobalStorage.RemoveFromChests("wood", 10);

            Assert.That(removed.Sum(i => i.Count), Is.EqualTo(2));
            Assert.That(chest.Inventory.Data.GetTotalCountInInventory("wood"), Is.Zero);
        }
    }
}
