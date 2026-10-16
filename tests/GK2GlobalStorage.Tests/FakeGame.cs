using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2GlobalStorage.Tests
{
    // A loaded save without the engine: the real game classes (inventories, items, zones, multi-inventories)
    // with just enough state for the mod's code. Built around three facts of running outside Unity:
    //  - anything that touches UnityEngine.Object's statics (==, bool conversion, creating one) fails, so
    //    MonoBehaviours and ScriptableObjects (MainGame, GameBalance) are created without constructors and
    //    the balance getters, which compare with ==, are replaced by a prefix returning the fake balance;
    //  - item and object definitions come from that fake balance, registered per test;
    //  - world objects (WgoData) are created without their constructor (it builds visuals and navigation),
    //    with only the fields the inventory code reads.
    // The mod's own patches are applied once for the whole run, as in the game.
    internal static class FakeGame
    {
        private const string HarmonyId = "gk2globalstorage.tests";

        // Item id of every container inventory (chests, bag); inventories are items holding items.
        public const string InventoryItem = "inventory";

        private static GameBalance balance;
        private static List<ItemDef> itemDefs;
        private static List<WGODef> wgoDefs;
        private static GameSave save;
        private static readonly Dictionary<WgoData, IWorker> workers = new Dictionary<WgoData, IWorker>(ReferenceEqualityComparer.Instance);

        public static WorldData World { get; private set; }

        public static PlayerData Player { get; private set; }

        // Result of the mod's Patches.Apply() against the installed game, and what it logged.
        public static int PatchesApplied { get; private set; }

        public static string[] PatchLog { get; private set; }

        public static void Install()
        {
            balance = Uninitialized<GameBalance>();
            itemDefs = new List<ItemDef>();
            wgoDefs = new List<WGODef>();
            Set(balance, "types", new List<Type> { typeof(ItemDef), typeof(WGODef) });
            Set(balance, "datas", new List<IList> { itemDefs, wgoDefs });
            Set(balance, "cache", new List<Dictionary<string, int>> { new Dictionary<string, int>(), new Dictionary<string, int>() });

            Harmony harmony = new Harmony(HarmonyId);
            harmony.Patch(AccessTools.PropertyGetter(typeof(GameBalanceBase), nameof(GameBalanceBase.Instance)),
                prefix: new HarmonyMethod(typeof(FakeGame), nameof(BalanceBasePrefix)));
            harmony.Patch(AccessTools.PropertyGetter(typeof(GameBalance), nameof(GameBalance.Me)),
                prefix: new HarmonyMethod(typeof(FakeGame), nameof(BalancePrefix)));
            harmony.Patch(AccessTools.PropertyGetter(typeof(WgoData), nameof(WgoData.Worker)),
                prefix: new HarmonyMethod(typeof(FakeGame), nameof(WorkerPrefix)));

            MainGame game = Uninitialized<MainGame>();
            save = Uninitialized<GameSave>();
            Set(game, "gameSave", save);
            AccessTools.Property(typeof(MainGame), nameof(MainGame.Instance)).SetValue(null, game);

            NewWorld();
            TestLog.Instance.Lines.Clear();
            PatchesApplied = Patches.Apply();
            PatchLog = TestLog.Instance.Lines.ToArray();
        }

        // An empty save: no scenes, an empty bag, default settings, only the container item defined.
        public static void NewWorld()
        {
            ConfigDefaults.Restore();
            itemDefs.Clear();
            wgoDefs.Clear();
            workers.Clear();
            Item(InventoryItem);

            World = new WorldData();
            Set(World, "cache", new WgoDataCache());
            save.worldData = World;

            Player = Uninitialized<PlayerData>();
            Set(Player, "guid", new SGuid());
            Player.inventory = new Inventory(InventoryItem, 20);
            AccessTools.Property(typeof(MainGame), nameof(MainGame.PlayerData)).SetValue(null, Player);

            GlobalStorage.Invalidate();
        }

        public static ItemDef Item(string id, int stackCount = 100)
        {
            ItemDef def = new ItemDef { id = id, stackCount = stackCount };
            itemDefs.Add(def);
            return def;
        }

        // A storage object definition. storage = false: not a chest for the game (like Garden Storage).
        public static WGODef ObjectDef(string id, int inventorySize = 10, bool storage = true)
        {
            WGODef def = new WGODef { id = id, inventorySize = inventorySize };
            Set(def, "openInMultiInventory", storage);
            wgoDefs.Add(def);
            return def;
        }

        public static GameSceneData Scene(string id)
        {
            GameSceneData scene = new GameSceneData { id = id };
            World.gameSceneDataList.Add(scene);
            return scene;
        }

        public static WorldZoneData Zone(GameSceneData scene, string id, bool container = true)
        {
            WorldZoneData zone = new WorldZoneData
            {
                id = id,
                gameSceneId = scene.id,
                worldZoneType = container ? WorldZoneData.WorldZoneType.Default : WorldZoneData.WorldZoneType.SimpleNotContainer,
            };
            Set(zone, "multiInventoryWgoDatas", new List<WgoData>());
            scene.worldZones.Add(zone);
            return zone;
        }

        // A placed object of a registered definition, with its items.
        public static WgoData Place(WorldZoneData zone, string defId, Vector3 position = default, params (string id, int count)[] items)
        {
            return Place<WgoData>(zone, defId, position, items);
        }

        public static T Place<T>(WorldZoneData zone, string defId, Vector3 position = default, params (string id, int count)[] items)
            where T : WgoData
        {
            WGODef def = wgoDefs.Find(d => d.id == defId) ?? throw new ArgumentException("Unknown object definition: " + defId);
            T wgo = Uninitialized<T>();
            wgo.id = defId;
            Set(wgo, "uniqueId", new SGuid());
            Set(wgo, "workerId", SGuid.Empty);
            Set(wgo, "position", position);
            Set(wgo, "worldId", zone?.gameSceneId);
            Set(wgo, "inventory", new Inventory(InventoryItem, def.inventorySize));
            Set(wgo, "craftInventory", new Inventory(InventoryItem, 10));
            Set(wgo, "worldZoneData", zone);
            World.Cache.wgoDataByUidCache[wgo.UniqueId.Guid] = wgo;
            if (zone != null)
            {
                zone.wgoDataList.Add(wgo.UniqueId);
                if (def.OpenInMultiInventory)
                {
                    zone.MultiInventoryWgoDatas.Add(wgo);
                }
            }
            Fill(wgo.Inventory, items);
            return wgo;
        }

        // A placed factory object (belt input chest, belt cell...) with the game's component for its conveyorType.
        public static ConveyorWgoData PlaceConveyor(WorldZoneData zone, string defId, params (string id, int count)[] items)
        {
            ConveyorWgoData wgo = Place<ConveyorWgoData>(zone, defId, default, items);
            AccessTools.Method(typeof(ConveyorWgoData), "TryCreateConveyorComponent").Invoke(wgo, new object[] { wgo.Definition.conveyorType });
            return wgo;
        }

        // Connects a free belt slot of an input chest (or a Stockpile) to a belt cell, as the game's Connect does but
        // without redrawing the belt, which needs the engine. slotItem: the item set on that slot.
        public static void Feed(ConveyorWgoData chest, ConveyorWgoData cell, string slotItem = null)
        {
            ConveyorComponent giver = chest.ConveyorComponent;
            if (giver is ConveyorPalletComponent)
            {
                Set(giver, "connectedWgoDataUniqueId", cell.UniqueId);
                return;
            }
            ((List<SGuid>)AccessTools.Field(giver.GetType(), "connectedWgoDataUniqueId").GetValue(giver)).Add(cell.UniqueId);
            List<ConveyorChestSlotData> slots = giver is ConveyorChestComponent c ? c.SlotsData : ((ConveyorChestOutComponent)giver).SlotsData;
            ConveyorChestSlotData slot = slots.Find(s => SGuid.IsNullOrEmpty(s.conveyorWgoDataUniqueId));
            slot.conveyorWgoDataUniqueId = cell.UniqueId;
            slot.slotItemId = slotItem;
        }

        public static void Fill(Inventory inventory, params (string id, int count)[] items)
        {
            foreach ((string id, int count) in items)
            {
                if (!inventory.AddItemToInventory(new Item(id, count)))
                {
                    throw new InvalidOperationException($"{count} x {id} does not fit");
                }
            }
        }

        public static void EnterZone(WorldZoneData zone)
        {
            Set(Player, "currentWorldZoneData", zone);
        }

        public static void SetWorker(WgoData workbench, IWorker worker)
        {
            workers[workbench] = worker;
        }

        public static T Uninitialized<T>()
        {
            return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        }

        public static void Set(object target, string field, object value)
        {
            (AccessTools.Field(target.GetType(), field) ?? throw new MissingFieldException(target.GetType().Name, field)).SetValue(target, value);
        }

        private static bool BalanceBasePrefix(ref GameBalanceBase __result)
        {
            __result = balance;
            return false;
        }

        private static bool BalancePrefix(ref GameBalance __result)
        {
            __result = balance;
            return false;
        }

        private static bool WorkerPrefix(WgoData __instance, ref IWorker __result)
        {
            if (workers.TryGetValue(__instance, out IWorker worker))
            {
                __result = worker;
                return false;
            }
            return true;
        }
    }
}
