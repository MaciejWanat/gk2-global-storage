using System.Collections.Generic;
using HarmonyLib;
using NUnit.Framework;
using static GK2GlobalStorage.Tests.FakeGame;

namespace GK2GlobalStorage.Tests
{
    // Option "Zombie workers": a zombie carrier can take a delivery order only when its area's chests hold the
    // amount; with the option on, what the area lacks is moved in from other areas first
    // (patched WorldZoneData.CanDeliveryOrderBeTakenOnExecution).
    public sealed class ZombieDeliveryTests : GameTest
    {
        private GameSceneData main;
        private WorldZoneData workshop;
        private WgoData workshopChest;
        private WgoData yardChest;

        [SetUp]
        public void Areas()
        {
            // Zone + item pairs that recently could not be topped up wait 3 s before the next try.
            ((Dictionary<string, int>)AccessTools.Field(typeof(ZombiePatches), "notAvailable").GetValue(null)).Clear();
            Item("wood");
            Item("stone");
            ObjectDef("chest");
            main = Scene("main");
            workshop = Zone(main, "workshop");
            workshopChest = Place(workshop, "chest");
            yardChest = Place(Zone(main, "yard"), "chest", items: ("wood", 10));
            Config.ZombieWorkers = true;
        }

        [Test]
        public void MissingAmountIsMovedIntoTheAreaFromOtherAreas()
        {
            bool canTake = workshop.CanDeliveryOrderBeTakenOnExecution(Order("wood", 6));

            Assert.That(canTake, Is.True);
            Assert.That(Count(workshopChest, "wood"), Is.EqualTo(6));
            Assert.That(Count(yardChest, "wood"), Is.EqualTo(4));
        }

        [Test]
        public void OnlyWhatTheAreaAndTheCarrierLackIsMoved()
        {
            Fill(workshopChest.Inventory, ("wood", 1));

            bool canTake = workshop.CanDeliveryOrderBeTakenOnExecution(Order("wood", 6), 2);

            Assert.That(canTake, Is.True);
            Assert.That(Count(workshopChest, "wood"), Is.EqualTo(4));
            Assert.That(Count(yardChest, "wood"), Is.EqualTo(7));
        }

        [Test]
        public void NothingMovesWithTheOptionOff()
        {
            Config.ZombieWorkers = false;

            Assert.That(workshop.CanDeliveryOrderBeTakenOnExecution(Order("wood", 6)), Is.False);
            Assert.That(Count(yardChest, "wood"), Is.EqualTo(10));
        }

        [Test]
        public void NothingMovesWhenTheAreaHasEnough()
        {
            Fill(workshopChest.Inventory, ("wood", 6));

            Assert.That(workshop.CanDeliveryOrderBeTakenOnExecution(Order("wood", 6)), Is.True);
            Assert.That(Count(yardChest, "wood"), Is.EqualTo(10));
        }

        [Test]
        public void NothingMovesWhenOtherAreasDoNotHaveTheWholeAmount()
        {
            Assert.That(workshop.CanDeliveryOrderBeTakenOnExecution(Order("wood", 11)), Is.False);
            Assert.That(Count(yardChest, "wood"), Is.EqualTo(10));
            Assert.That(Count(workshopChest, "wood"), Is.Zero);
        }

        [Test]
        public void NothingMovesWhenTheAreaHasNoRoom()
        {
            ObjectDef("small_chest", inventorySize: 1);
            WorldZoneData shed = Zone(main, "shed");
            Place(shed, "small_chest", items: ("stone", 100));

            Assert.That(shed.CanDeliveryOrderBeTakenOnExecution(Order("wood", 3)), Is.False);
            Assert.That(Count(yardChest, "wood"), Is.EqualTo(10));
        }

        [Test]
        public void OnlyChestsThatAcceptTheItemReceiveIt()
        {
            WGODef stoneOnly = ObjectDef("stone_chest");
            stoneOnly.inventoryWhiteList.itemsIds.Add("stone");
            WorldZoneData quarry = Zone(main, "quarry");
            WgoData stoneChest = Place(quarry, "stone_chest");

            Assert.That(quarry.CanDeliveryOrderBeTakenOnExecution(Order("wood", 3)), Is.False);
            Assert.That(Count(stoneChest, "wood"), Is.Zero);
            Assert.That(Count(yardChest, "wood"), Is.EqualTo(10));
        }

        [Test]
        public void ExcludedAreaGetsNothing()
        {
            Config.ExcludedZones.Add("workshop");

            Assert.That(workshop.CanDeliveryOrderBeTakenOnExecution(Order("wood", 6)), Is.False);
            Assert.That(Count(yardChest, "wood"), Is.EqualTo(10));
        }

        [Test]
        public void ChestsOfExcludedAreasAreNotUsedAsSources()
        {
            Config.ExcludedZones.Add("yard");
            GlobalStorage.Invalidate();

            Assert.That(workshop.CanDeliveryOrderBeTakenOnExecution(Order("wood", 6)), Is.False);
            Assert.That(Count(yardChest, "wood"), Is.EqualTo(10));
        }

        [Test]
        public void AfterAFailedTopUpTheSameItemWaitsBeforeTheNextTry()
        {
            Assert.That(workshop.CanDeliveryOrderBeTakenOnExecution(Order("wood", 12)), Is.False);
            Fill(yardChest.Inventory, ("wood", 5));
            GlobalStorage.Invalidate();

            // Carriers ask every frame; the next try comes after a few seconds.
            Assert.That(workshop.CanDeliveryOrderBeTakenOnExecution(Order("wood", 12)), Is.False);
            Assert.That(Count(yardChest, "wood"), Is.EqualTo(15));
        }

        private static DeliveryOrder Order(string itemId, int count)
        {
            return new DeliveryOrder(new SGuid(), new Item(itemId, count));
        }

        private static int Count(WgoData chest, string itemId)
        {
            return chest.Inventory.Data.GetTotalCountInInventory(itemId);
        }
    }
}
