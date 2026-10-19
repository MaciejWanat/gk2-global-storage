using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LazyBearTechnology;
using NUnit.Framework;
using static GK2GlobalStorage.Tests.FakeGame;

namespace GK2GlobalStorage.Tests
{
    // Vendor windows showing your chests: on your side, the items the vendor buys come first, those that give its
    // gratitude before the rest (VendorOrder). Which items give gratitude is the game's own rule
    // (Vendor.HasHappinessForItem, as for the icon); moving the cells needs the engine, so the order and the rule
    // are checked here.
    public sealed class VendorOrderTests : GameTest
    {
        [SetUp]
        public void Items()
        {
            Item("wood");
            Item("stone");
            Item("onion");
            Item("nail");
        }

        [Test]
        public void SellableItemsComeFirstThoseWithGratitudeAtTheFrontEmptyCellsLast()
        {
            // Onion gives gratitude but this vendor does not buy it: sellable items still come before it.
            Func<Item, bool> sellable = item => item.id != "onion" && item.id != "nail";
            Func<Item, bool> gives = item => item.id == "onion" || item.id == "wood";
            Item[] cells = { new Item("onion", 1), new Item("nail", 1), new Item("stone", 1), new Item("wood", 1), Item.Empty };

            List<Item> order = VendorOrder.ByRank(cells, item => VendorOrder.Rank(item, sellable, gives));

            Assert.That(order.Select(i => i.id), Is.EqualTo(new[] { "wood", "stone", "onion", "nail", Item.Empty.id }));
        }

        [Test]
        public void ItemsOfTheSameRankKeepTheGameOrder()
        {
            string[] cells = { "b", "a", "d", "c" };

            List<string> order = VendorOrder.ByRank(cells, id => id == "d" || id == "a" ? 1 : 0);

            Assert.That(order, Is.EqualTo(new[] { "a", "d", "b", "c" }));
        }

        [Test]
        public void WithoutGratitudeSellableItemsStillComeFirst()
        {
            // The Bishop and the Woodcarver give no gratitude.
            Func<Item, bool> sellable = item => item.id == "stone";
            Item[] cells = { new Item("wood", 1), new Item("stone", 1) };

            List<Item> order = VendorOrder.ByRank(cells, item => VendorOrder.Rank(item, sellable, null));

            Assert.That(order.Select(i => i.id), Is.EqualTo(new[] { "stone", "wood" }));
        }

        [Test]
        public void ItemsTheVendorThanksForGiveGratitude()
        {
            Func<Item, bool> gives = VendorOrder.Gives(Window(TownVendor(("wood", 5))));

            Assert.That(gives(new Item("wood", 1)), Is.True);
            Assert.That(gives(new Item("stone", 1)), Is.False);
            Assert.That(gives(Item.Empty), Is.False);
        }

        [Test]
        public void WhatIsAlreadyInTheDealCounts()
        {
            UIVendorWindowData window = Window(TownVendor(("wood", 5)));
            window.GetPendingHappinessSoldCount = id => id == "wood" ? 5 : 0;

            Assert.That(VendorOrder.Gives(window)(new Item("wood", 1)), Is.False);
        }

        [Test]
        public void NoneOnceTheWeeklyCapIsUsed()
        {
            UIVendorWindowData window = Window(TownVendor(("wood", 5)));
            window.GetTotalHappinessDealDelegate = () => 100f;

            Assert.That(VendorOrder.Gives(window)(new Item("wood", 1)), Is.False);
        }

        [Test]
        public void VendorsWithoutGratitudeGiveNone()
        {
            Vendor vendor = TownVendor(("wood", 5));
            vendor.Definition.townVendor = false;

            Assert.That(VendorOrder.Gives(Window(vendor)), Is.Null);
        }

        [Test]
        public void NothingWithVendorWindowsOff()
        {
            Config.Trading = false;

            Assert.That(VendorOrder.Gives(Window(TownVendor(("wood", 5)))), Is.Null);
        }

        [Test]
        public void NothingWithOtherChestsHidden()
        {
            Config.ShowOtherChests = false;

            Assert.That(VendorOrder.Gives(Window(TownVendor(("wood", 5)))), Is.Null);
        }

        [Test]
        public void NoGratitudeWithTheModOff()
        {
            Config.Enabled = false;

            Assert.That(VendorOrder.Gives(Window(TownVendor(("wood", 5)))), Is.Null);
        }

        [Test]
        public void VendorWindowIconUpdateIsPatched()
        {
            MethodInfo icons = AccessTools.DeclaredMethod(typeof(UIVendorWindow), "TryUpdateHappinessStatusIcons");

            Assert.That(Harmony.GetPatchInfo(icons)?.Postfixes.Select(p => p.owner), Does.Contain(Hooks.HarmonyId(Hook.VendorOrder)));
        }

        // A vendor that gives gratitude (townVendor: all but the Bishop and the Woodcarver) on its first tier,
        // thankful for the given items (that many per week, weekly cap 100).
        private static Vendor TownVendor(params (string id, int count)[] products)
        {
            VendorTierData tier = new VendorTierData { happinessCap = new LazyExpression("100") };
            foreach ((string id, int count) in products)
            {
                tier.townVendorProductInfos.Add(new TownVendorProductInfo { itemId = id, itemCount = count });
            }
            VendorDef def = new VendorDef { id = "test_vendor", townVendor = true, tierDataList = new List<VendorTierData> { tier } };
            Vendor vendor = new Vendor { id = def.id };
            Set(vendor, "definition", def);
            Set(vendor, "cachedId", def.id);
            Set(vendor, "curTier", 1);
            return vendor;
        }

        private static UIVendorWindowData Window(Vendor vendor)
        {
            return new UIVendorWindowData { Vendor = vendor };
        }
    }
}
