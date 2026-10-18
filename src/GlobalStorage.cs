using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace GK2GlobalStorage
{
    // Every storage container of every zone in the save, i.e. what the game's own per-zone
    // MultiInventory(WorldZoneData) would collect plus the factory chests it leaves out, across all zones and scenes.
    // Items are never moved; the inventories are just appended to the lists the game already builds.
    internal static class GlobalStorage
    {
        private const int CacheMs = 500;

        private static readonly List<Inventory> cached = new List<Inventory>();

        // Object of each cached inventory, same order.
        private static readonly List<WgoData> cachedOwners = new List<WgoData>();

        private static WorldData cachedFor;

        private static int cachedAt;

        private static WorldData loggedFor;

        [ThreadStatic]
        private static HashSet<Inventory> scratch;

        // Appends every global chest not already in the list. Local inventories keep their place in front,
        // so the game still takes from your bag and the local chests first.
        public static void AppendTo(MultiInventory multiInventory)
        {
            if (multiInventory == null)
            {
                return;
            }
            long t = Perf.Begin();
            List<Inventory> all = Get();
            if (all.Count > 0)
            {
                List<Inventory> list = multiInventory.inventoryList;
                // Reused set: this runs several times per second (workbench checks), avoid garbage per call.
                HashSet<Inventory> present = scratch ??= new HashSet<Inventory>(RefComparer.Instance);
                present.Clear();
                for (int i = 0; i < list.Count; i++)
                {
                    present.Add(list[i]);
                }
                for (int i = 0; i < all.Count; i++)
                {
                    if (present.Add(all[i]))
                    {
                        list.Add(all[i]);
                    }
                }
                present.Clear();
            }
            Perf.End("Add chests to item list", t);
        }

        // Forces the next lookup to rebuild (after settings that affect the pool change).
        public static void Invalidate()
        {
            cachedFor = null;
        }

        // Read-only snapshot of every global chest. Do not modify the returned list.
        public static List<Inventory> All()
        {
            return Get();
        }

        // The object each inventory of All() belongs to, same order. Call right after All(); do not modify.
        public static List<WgoData> Owners()
        {
            Get();
            return cachedOwners;
        }

        public static int CountInChests(string itemId)
        {
            long t = Perf.Begin();
            int total = 0;
            List<Inventory> all = Get();
            for (int i = 0; i < all.Count; i++)
            {
                total += all[i].Data.GetTotalCountInInventory(itemId);
            }
            Perf.End("Count item in all chests", t);
            return total;
        }

        // Removes up to count items from the chests, in pool order. Returns the removed stacks.
        public static List<Item> RemoveFromChests(string itemId, int count)
        {
            List<Item> removed = new List<Item>();
            List<Inventory> all = Get();
            for (int i = 0; i < all.Count && count > 0; i++)
            {
                List<Item> part = all[i].RemoveItemById(itemId, count);
                for (int j = 0; j < part.Count; j++)
                {
                    count -= part[j].Count;
                }
                removed.AddRange(part);
            }
            return removed;
        }

        // Moves up to count items from the sources into the targets, both in list order. What does not fit in any
        // target goes back to the chest it came from. Returns how many were moved.
        public static int Move(string itemId, int count, List<Inventory> sources, List<Inventory> targets)
        {
            int left = count;
            for (int s = 0; s < sources.Count && left > 0; s++)
            {
                foreach (Item stack in sources[s].RemoveItemById(itemId, left))
                {
                    left -= stack.Count;
                    // The game adds what fits and keeps the rest on the stack (non-stackable items one at a time).
                    for (int i = 0; i < targets.Count && stack.Count > 0; i++)
                    {
                        while (stack.Count > 0 && targets[i].AddItemToInventory(stack, out _))
                        {
                        }
                    }
                    if (stack.Count > 0)
                    {
                        // Should not happen (callers check room first): put it back where it came from.
                        left += stack.Count;
                        while (stack.Count > 0 && sources[s].AddItemToInventory(stack, out _))
                        {
                        }
                    }
                }
            }
            return count - left;
        }

        private static List<Inventory> Get()
        {
            WorldData worldData = MainGame.Instance?.GameSave?.worldData;
            if (worldData == null || !worldData.HasCache)
            {
                cached.Clear();
                cachedOwners.Clear();
                cachedFor = null;
                return cached;
            }
            int now = Environment.TickCount;
            if (cachedFor == worldData && unchecked(now - cachedAt) < CacheMs)
            {
                return cached;
            }
            long t = Perf.Begin();
            Collect(worldData, cached, cachedOwners);
            Perf.End("Rebuild chest pool", t);
            cachedFor = worldData;
            cachedAt = now;
            return cached;
        }

        private static void Collect(WorldData worldData, List<Inventory> result, List<WgoData> owners)
        {
            result.Clear();
            owners.Clear();
            HashSet<Inventory> seen = new HashSet<Inventory>(RefComparer.Instance);
            bool log = Config.LogZones && loggedFor != worldData;
            StringBuilder report = log ? new StringBuilder() : null;
            foreach (GameSceneData scene in worldData.gameSceneDataList)
            {
                if (scene?.worldZones == null)
                {
                    continue;
                }
                foreach (WorldZoneData zone in scene.worldZones)
                {
                    if (zone == null || !zone.IsContainer)
                    {
                        continue;
                    }
                    bool excluded = Config.ExcludedZones.Contains(zone.id ?? "");
                    int count = 0;
                    if (!excluded)
                    {
                        foreach (SGuid guid in zone.wgoDataList)
                        {
                            WgoData wgoData = worldData.GetWgoData(guid);
                            Inventory inventory = Storage(wgoData);
                            if (inventory != null && seen.Add(inventory))
                            {
                                result.Add(inventory);
                                owners.Add(Holder(worldData, wgoData));
                                count++;
                            }
                        }
                    }
                    if (report != null && (count > 0 || excluded))
                    {
                        report.Append("\n  ").Append(scene.id).Append(" / ").Append(zone.id).Append(": ")
                            .Append(excluded ? "excluded" : count + " chest(s)");
                    }
                }
            }
            if (log)
            {
                loggedFor = worldData;
                Debug.Log("[GK2GlobalStorage] Global pool: " + result.Count + " container(s)" + report);
            }
        }

        // Some objects show another one's inventory: the cargo lift tops in the yard show the factory Stockpiles'.
        // The pool lists such an inventory once, with the object that holds it as its owner.
        private static WgoData Holder(WorldData worldData, WgoData wgoData)
        {
            WGODef def = wgoData.Definition;
            return def.hasRefToOtherWgoInventory ? worldData.GetWgoData(def.refToOtherWgoInventory) ?? wgoData : wgoData;
        }

        // The chest's inventory if this object is a storage container that counts (null otherwise).
        internal static Inventory Storage(WgoData wgoData)
        {
            WGODef def = wgoData?.Definition;
            if (def == null || def.inventorySize == 0 || !(def.OpenInMultiInventory || IsFactoryChest(def)))
            {
                return null;
            }
            Inventory inventory = wgoData.Inventory;
            if (inventory?.Data == null)
            {
                return null;
            }
            if (!Config.FuelContainers && inventory.Data.HasProperty<FuelContainerSerializedItemProperty>())
            {
                return null;
            }
            return inventory;
        }

        // Chests of the factory belts: Conveyor Chests, Stockpiles, Garden Storage and Alcohol Storage. The game marks
        // Garden and Alcohol Storage as not storage (open_in_multi_inventory = false), so its own area lists skip
        // them; the pool takes them like any other chest.
        internal static bool IsFactoryChest(WGODef def)
        {
            switch (def.conveyorType)
            {
                case ConveyorElementType.Chest:
                case ConveyorElementType.ChestOut:
                case ConveyorElementType.Pallet:
                    return true;
                default:
                    return false;
            }
        }

        internal sealed class RefComparer : IEqualityComparer<Inventory>
        {
            public static readonly RefComparer Instance = new RefComparer();

            public bool Equals(Inventory x, Inventory y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(Inventory obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
