using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static GK2GlobalStorage.Tests.FakeGame;

namespace GK2GlobalStorage.Tests
{
    // Bait in the fishing code (FishingPatches): the check at the water and the bait counts of the fishing window call
    // FishingRedirect instead of the bag's own methods; casting (the cast button, here its patch's prefix and finalizer
    // around the game's bag removal, whose real postfix is applied) takes what the bag lacks from the chests.
    public sealed class FishingRedirectTests : GameTest
    {
        private WgoData yardChest;
        private WgoData mineChest;

        [SetUp]
        public void BagAndChests()
        {
            Item("worm").type = ItemType.Bait;
            Item("bread").type = ItemType.Bait;
            Item("wood");
            ObjectDef("chest");
            yardChest = Place(Zone(Scene("main"), "yard"), "chest", items: new[] { ("worm", 3), ("wood", 5) });
            mineChest = Place(Zone(Scene("mine_level"), "mine"), "chest", items: ("bread", 2));
            Fill(Player.inventory, ("worm", 1));
        }

        // What the game does when you cast: takes one of the chosen bait from the bag.
        private static List<Item> Cast(string bait, int count = 1)
        {
            return DuringACast(() => Player.inventory.RemoveItemById(bait, count));
        }

        private static List<Item> DuringACast(System.Func<List<Item>> remove)
        {
            FishingPatches.CastPrefix();
            try
            {
                return remove();
            }
            finally
            {
                FishingPatches.CastFinalizer(null);
            }
        }

        [Test]
        public void BaitCountIncludesEveryChest()
        {
            Assert.That(FishingRedirect.GetTotalCountInInventory(Player.inventory.Data, "worm", null, false), Is.EqualTo(4));
            Assert.That(FishingRedirect.GetTotalCountInInventory(Player.inventory.Data, "bread", null, false), Is.EqualTo(2));
        }

        [Test]
        public void OtherInventoriesCountOnlyThemselves()
        {
            Assert.That(FishingRedirect.GetTotalCountInInventory(yardChest.Inventory.Data, "worm", null, false), Is.EqualTo(3));
            Assert.That(FishingRedirect.GetItemsByType(mineChest.Inventory, ItemType.Bait, true).Select(i => i.id), Is.EqualTo(new[] { "bread" }));
        }

        [Test]
        public void TheWaterCheckSeesBaitInTheBagAndEveryChest()
        {
            List<Item> baits = FishingRedirect.GetItemsByType(Player.inventory, ItemType.Bait, true);

            Assert.That(baits.Select(i => i.id), Is.EqualTo(new[] { "worm", "worm", "bread" }));
        }

        [Test]
        public void CastingTakesTheBaitFromTheBagFirst()
        {
            List<Item> removed = Cast("worm");

            Assert.That(removed.Sum(i => i.Count), Is.EqualTo(1));
            Assert.That(Player.inventory.Data.GetTotalCountInInventory("worm"), Is.Zero);
            Assert.That(yardChest.Inventory.Data.GetTotalCountInInventory("worm"), Is.EqualTo(3));
        }

        [Test]
        public void CastingWithoutTheBaitInTheBagTakesItFromAChest()
        {
            List<Item> removed = Cast("bread");

            Assert.That(removed.Sum(i => i.Count), Is.EqualTo(1));
            Assert.That(mineChest.Inventory.Data.GetTotalCountInInventory("bread"), Is.EqualTo(1));
            Assert.That(Player.inventory.Data.GetTotalCountInInventory("worm"), Is.EqualTo(1));
        }

        [Test]
        public void TakingFromTheBagOutsideACastLeavesTheChestsAlone()
        {
            List<Item> removed = Player.inventory.RemoveItemById("bread", 1);

            Assert.That(removed, Is.Empty);
            Assert.That(mineChest.Inventory.Data.GetTotalCountInInventory("bread"), Is.EqualTo(2));
        }

        [Test]
        public void TakingFromAChestDuringACastLeavesOtherChestsAlone()
        {
            DuringACast(() => yardChest.Inventory.RemoveItemById("bread", 1));

            Assert.That(mineChest.Inventory.Data.GetTotalCountInInventory("bread"), Is.EqualTo(2));
        }

        [Test]
        public void TakingEverythingStaysBagOnly()
        {
            // count = -1 is the game's "remove all of it".
            Cast("worm", -1);

            Assert.That(Player.inventory.Data.GetTotalCountInInventory("worm"), Is.Zero);
            Assert.That(GlobalStorage.CountInChests("worm"), Is.EqualTo(3));
        }

        [Test]
        public void FishingOffMeansBagOnly()
        {
            Config.Fishing = false;

            Assert.That(FishingRedirect.GetTotalCountInInventory(Player.inventory.Data, "worm", null, false), Is.EqualTo(1));
            Assert.That(FishingRedirect.GetItemsByType(Player.inventory, ItemType.Bait, true).Select(i => i.id), Is.EqualTo(new[] { "worm" }));
            Assert.That(Cast("bread"), Is.Empty);
            Assert.That(mineChest.Inventory.Data.GetTotalCountInInventory("bread"), Is.EqualTo(2));
        }

        [Test]
        public void ModOffMeansBagOnly()
        {
            Config.Enabled = false;

            Assert.That(FishingRedirect.GetTotalCountInInventory(Player.inventory.Data, "worm", null, false), Is.EqualTo(1));
            Assert.That(Cast("bread"), Is.Empty);
        }
    }
}
