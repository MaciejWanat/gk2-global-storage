using NUnit.Framework;
using static GK2GlobalStorage.Tests.FakeGame;

namespace GK2GlobalStorage.Tests
{
    // Option "Pickups go to nearest chest": the areas whose chests take what you pick up. Not the factory's belt
    // hall and storage room unless "Also in the factory" is on, so pickups do not end up in the belts' input chests.
    public sealed class PickupTests : GameTest
    {
        [TestCase("conveyor")]
        [TestCase("conveyor_storage")]
        public void FactoryChestsTakeNoPickupsByDefault(string factoryArea)
        {
            Assert.That(PickupPatches.ChestsTakePickups(Zone(Scene("main"), factoryArea)), Is.False);
        }

        [TestCase("conveyor")]
        [TestCase("conveyor_storage")]
        public void FactoryChestsTakePickupsWithTheFactoryOptionOn(string factoryArea)
        {
            Config.PickupsInFactory = true;

            Assert.That(PickupPatches.ChestsTakePickups(Zone(Scene("main"), factoryArea)), Is.True);
        }

        [Test]
        public void OtherAreasTakePickups()
        {
            Assert.That(PickupPatches.ChestsTakePickups(Zone(Scene("main"), "yard")), Is.True);
        }

        [Test]
        public void OutsideAnyAreaPickupsGoToTheBag()
        {
            Assert.That(PickupPatches.ChestsTakePickups(null), Is.False);
        }

        // Quest items always go to the bag. The flag is read by reflection: some game builds players run lack it.
        [Test]
        public void QuestItemsAreRecognised()
        {
            ItemDef note = Item("quest_note");
            note.isQuestItem = true;

            Assert.That(PickupPatches.IsQuestItem(note), Is.True);
        }

        [Test]
        public void OtherItemsAreNoQuestItems()
        {
            Assert.That(PickupPatches.IsQuestItem(Item("wood")), Is.False);
        }
    }
}
