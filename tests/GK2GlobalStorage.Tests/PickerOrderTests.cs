using NUnit.Framework;
using static GK2GlobalStorage.Tests.FakeGame;

namespace GK2GlobalStorage.Tests
{
    public sealed class PickerOrderTests : GameTest
    {
        private WgoData stones1;
        private WgoData seeds1;
        private WgoData stones2;
        private WgoData seeds2;

        [SetUp]
        public void Chests()
        {
            Item("seed");
            Item("stone");
            ObjectDef("chest");
            WorldZoneData yard = Zone(Scene("main"), "yard");
            stones1 = Place(yard, "chest", items: ("stone", 1));
            seeds1 = Place(yard, "chest", items: ("seed", 1));
            stones2 = Place(yard, "chest", items: ("stone", 1));
            seeds2 = Place(yard, "chest", items: ("seed", 1));
        }

        [Test]
        public void BagStaysFirstThenChestsWithUsableItemsThenTheRestEachInGameOrder()
        {
            MultiInventory list = new MultiInventory();
            list.inventoryList.AddRange(new[] { Player.inventory, stones1.Inventory, seeds1.Inventory, stones2.Inventory, seeds2.Inventory });

            PickerOrder.UsableFirst(list, item => item.id == "seed");

            Assert.That(list.inventoryList, Is.EqualTo(new[] { Player.inventory, seeds1.Inventory, seeds2.Inventory, stones1.Inventory, stones2.Inventory }));
        }

        [Test]
        public void WithoutTheBagInFrontEveryInventoryIsSorted()
        {
            MultiInventory list = new MultiInventory();
            list.inventoryList.AddRange(new[] { stones1.Inventory, seeds1.Inventory });

            PickerOrder.UsableFirst(list, item => item.id == "seed");

            Assert.That(list.inventoryList, Is.EqualTo(new[] { seeds1.Inventory, stones1.Inventory }));
        }

        [Test]
        public void EmptyOrMissingInventoryHasNothingUsable()
        {
            Assert.That(PickerOrder.HasUsable(Place(Zone(Scene("other"), "empty_room"), "chest").Inventory, item => true), Is.False);
            Assert.That(PickerOrder.HasUsable(null, item => true), Is.False);
        }
    }
}
