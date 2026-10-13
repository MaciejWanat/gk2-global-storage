using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static GK2GlobalStorage.Tests.FakeGame;

namespace GK2GlobalStorage.Tests
{
    // Quest and dialog code calls these instead of the bag's own methods (swapped in by QuestPatches).
    public sealed class QuestRedirectTests : GameTest
    {
        private WgoData yardChest;
        private WgoData mineChest;

        [SetUp]
        public void BagAndChests()
        {
            Item("wood");
            ObjectDef("chest");
            yardChest = Place(Zone(Scene("main"), "yard"), "chest", items: ("wood", 3));
            mineChest = Place(Zone(Scene("mine_level"), "mine"), "chest", items: ("wood", 2));
            Fill(Player.inventory, ("wood", 2));
        }

        [Test]
        public void BagCountIncludesEveryChest()
        {
            Assert.That(QuestRedirect.GetTotalCountInInventory(Player.inventory.Data, "wood", null, false), Is.EqualTo(7));
            Assert.That(QuestRedirect.HasItemQuantityInInventory(Player.inventory.Data, "wood", 7), Is.True);
            Assert.That(QuestRedirect.HasItemQuantityInInventory(Player.inventory.Data, "wood", 8), Is.False);
        }

        [Test]
        public void OtherInventoriesCountOnlyThemselves()
        {
            Assert.That(QuestRedirect.GetTotalCountInInventory(yardChest.Inventory.Data, "wood", null, false), Is.EqualTo(3));
            Assert.That(QuestRedirect.HasItemQuantityInInventory(yardChest.Inventory.Data, "wood", 4), Is.False);
        }

        [Test]
        public void QuestsOffMeansBagOnly()
        {
            Config.Quests = false;

            Assert.That(QuestRedirect.GetTotalCountInInventory(Player.inventory.Data, "wood", null, false), Is.EqualTo(2));
            Assert.That(QuestRedirect.HasItemQuantityInInventory(Player.inventory.Data, "wood", 3), Is.False);
        }

        [Test]
        public void ModOffMeansBagOnly()
        {
            Config.Enabled = false;

            Assert.That(QuestRedirect.GetTotalCountInInventory(Player.inventory.Data, "wood", null, false), Is.EqualTo(2));
        }

        [Test]
        public void TakingFromTheBagEmptiesTheBagFirstThenChestsInPoolOrder()
        {
            List<Item> removed = QuestRedirect.RemoveItemById(Player.inventory, "wood", 4, null, null, false);

            Assert.That(removed.Sum(i => i.Count), Is.EqualTo(4));
            Assert.That(Player.inventory.Data.GetTotalCountInInventory("wood"), Is.Zero);
            Assert.That(yardChest.Inventory.Data.GetTotalCountInInventory("wood"), Is.EqualTo(1));
            Assert.That(mineChest.Inventory.Data.GetTotalCountInInventory("wood"), Is.EqualTo(2));
        }

        [Test]
        public void TakingEverythingStaysBagOnly()
        {
            // count = -1 is the game's "remove all of it".
            QuestRedirect.RemoveItemById(Player.inventory, "wood", -1, null, null, false);

            Assert.That(Player.inventory.Data.GetTotalCountInInventory("wood"), Is.Zero);
            Assert.That(GlobalStorage.CountInChests("wood"), Is.EqualTo(5));
        }

        [Test]
        public void TakingFromAChestDoesNotTouchOtherChests()
        {
            QuestRedirect.RemoveItemById(yardChest.Inventory, "wood", 5, null, null, false);

            Assert.That(yardChest.Inventory.Data.GetTotalCountInInventory("wood"), Is.Zero);
            Assert.That(mineChest.Inventory.Data.GetTotalCountInInventory("wood"), Is.EqualTo(2));
            Assert.That(Player.inventory.Data.GetTotalCountInInventory("wood"), Is.EqualTo(2));
        }
    }
}
