using System.Collections.Generic;
using LazyBearTechnology;
using NUnit.Framework;
using static GK2GlobalStorage.Tests.FakeGame;

namespace GK2GlobalStorage.Tests
{
    // The patched game methods themselves (the mod's patches are applied by FakeGame.Install), called the way
    // the game calls them.
    public sealed class PatchesTests : GameTest
    {
        private WorldZoneData yard;
        private WorldZoneData mine;
        private WgoData localChest;
        private WgoData farChest;

        [SetUp]
        public void Areas()
        {
            Item("wood");
            ObjectDef("chest");
            ObjectDef("workbench", inventorySize: 0);
            ObjectDef("furnace", inventorySize: 0).isAutoCrafter = true;
            ObjectDef("conveyor_workbench", inventorySize: 4).conveyorType = ConveyorElementType.Workbench;
            yard = Zone(Scene("main"), "yard");
            mine = Zone(Scene("mine_level"), "mine");
            localChest = Place(yard, "chest");
            farChest = Place(mine, "chest");
        }

        // --- bag + area lists: building, alchemy, garden, prayers, tooltips ---

        [Test]
        public void BuildingListGetsEveryChestAfterTheBagAndTheArea()
        {
            EnterZone(yard);

            List<Inventory> list = new MultiInventory(Player, true).inventoryList;

            Assert.That(list, Is.EqualTo(new[] { Player.inventory, localChest.Inventory, farChest.Inventory }));
        }

        [Test]
        public void BuildingListStaysLocalWithBuildingOff()
        {
            EnterZone(yard);
            Config.Building = false;

            Assert.That(new MultiInventory(Player, true).inventoryList, Is.EqualTo(new[] { Player.inventory, localChest.Inventory }));
        }

        [Test]
        public void BuildingListStaysLocalWithTheModOff()
        {
            EnterZone(yard);
            Config.Enabled = false;

            Assert.That(new MultiInventory(Player, true).inventoryList, Is.EqualTo(new[] { Player.inventory, localChest.Inventory }));
        }

        [Test]
        public void BagOnlyListStaysBagOnly()
        {
            EnterZone(yard);

            Assert.That(new MultiInventory(Player, false).inventoryList, Is.EqualTo(new[] { Player.inventory }));
        }

        // --- workbench material pool (WgoData.GetCraftableMultiInventory) ---

        [Test]
        public void WorkbenchUsesEveryChest()
        {
            WgoData workbench = Place(yard, "workbench");

            List<Inventory> pool = workbench.GetCraftableMultiInventory().inventoryList;

            Assert.That(pool, Does.Contain(localChest.Inventory).And.Contain(farChest.Inventory));
        }

        [Test]
        public void WorkbenchStaysLocalWithWorkbenchesOff()
        {
            WgoData workbench = Place(yard, "workbench");
            Config.Workbenches = false;

            List<Inventory> pool = workbench.GetCraftableMultiInventory().inventoryList;

            Assert.That(pool, Does.Contain(localChest.Inventory).And.Not.Contain(farChest.Inventory));
        }

        [Test]
        public void ZombieRunWorkbenchUsesEveryChestOnlyWithZombieWorkers()
        {
            WgoData workbench = Place(yard, "workbench");
            SetWorker(workbench, Zombie(workbench));

            Assert.That(workbench.GetCraftableMultiInventory().inventoryList, Does.Not.Contain(farChest.Inventory));

            Config.ZombieWorkers = true;

            Assert.That(workbench.GetCraftableMultiInventory().inventoryList, Does.Contain(farChest.Inventory));
        }

        // --- zombie workbench result (CraftElementBase.CanFinishCraft) ---

        [Test]
        public void ZombieWaitsWhenItsResultDoesNotFitInTheAreasChests()
        {
            // Carriers put the result only into a chest of the zombie's area, so room elsewhere must not count.
            Item("stone");
            Fill(localChest.Inventory, ("stone", 1000));
            WgoData workbench = ZombieWorkbench();
            Config.ZombieWorkers = true;

            Assert.That(Craft("wood").CanFinishCraft(workbench), Is.EqualTo(CraftStatus.NotEnoughSpaceInMultiInventory));
        }

        [Test]
        public void ZombieFinishesWhenItsResultFitsInTheAreasChests()
        {
            WgoData workbench = ZombieWorkbench();
            Config.ZombieWorkers = true;

            Assert.That(Craft("wood").CanFinishCraft(workbench), Is.EqualTo(CraftStatus.OK));
        }

        [Test]
        public void MaterialsAndFuelStillComeFromEveryChestAfterAFinishCheck()
        {
            WgoData workbench = ZombieWorkbench();
            Config.ZombieWorkers = true;

            Craft("wood").CanFinishCraft(workbench);

            Assert.That(workbench.GetCraftableMultiInventory().inventoryList, Does.Contain(farChest.Inventory));
        }

        [Test]
        public void AutomaticCrafterUsesEveryChestOnlyWithAutoCrafters()
        {
            WgoData furnace = Place(yard, "furnace");

            Assert.That(furnace.GetCraftableMultiInventory().inventoryList, Does.Not.Contain(farChest.Inventory));

            Config.AutoCrafters = true;

            Assert.That(furnace.GetCraftableMultiInventory().inventoryList, Does.Contain(farChest.Inventory));
        }

        [Test]
        public void FactoryWorkstationsTakeOnlyFromTheirOwnInventories()
        {
            // Conveyor objects override GetCraftableMultiInventory without calling the patched base method:
            // materials arrive by conveyor belt, no chest is used, not even the area's.
            ConveyorWgoData workstation = Place<ConveyorWgoData>(yard, "conveyor_workbench");
            Config.ZombieWorkers = true;
            Config.AutoCrafters = true;

            List<Inventory> pool = workstation.GetCraftableMultiInventory().inventoryList;

            Assert.That(pool, Is.EqualTo(new[] { workstation.Inventory, workstation.CraftInventory }));
        }

        // --- chest windows (option "Chest windows") ---

        [Test]
        public void ChestWindowListsOtherAreasChestsButNotTheOpenedOne()
        {
            WgoData opened = Place(yard, "chest");
            MultiInventory areaChests = new MultiInventory();
            areaChests.inventoryList.Add(localChest.Inventory);

            ChestWindowView.AppendOtherChests(areaChests, opened);

            Assert.That(areaChests.inventoryList, Is.EqualTo(new[] { localChest.Inventory, farChest.Inventory }));
        }

        private WgoData ZombieWorkbench()
        {
            WgoData workbench = Place(yard, "workbench");
            SetWorker(workbench, Zombie(workbench));
            return workbench;
        }

        // A started, not automatic craft whose result is one item.
        private static CraftElementBase Craft(string resultId)
        {
            CraftElementBase craft = Uninitialized<CraftElementBase>();
            Set(craft, "def", new CraftDefBase { id = "test_craft" });
            CraftParamsData paramsData = Uninitialized<CraftParamsData>();
            paramsData.customRes = new GameRes();
            Set(craft, "paramsData", paramsData);
            Set(craft, "preOutputItems", new List<ItemCount> { new ItemCount(resultId, 1) });
            return craft;
        }

        // A crafter zombie at the workbench: its inventory is the workbench's craft inventory.
        private static ZombieWgoData Zombie(WgoData workbench)
        {
            ZombieWgoData zombie = Uninitialized<ZombieWgoData>();
            Set(zombie, "zombieType", ZombieType.Crafter);
            Set(zombie, "attachedWgoData", workbench);
            return zombie;
        }
    }
}
