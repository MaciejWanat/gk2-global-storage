# gk2-global-storage

Mod for a global storage in Graveyard Keeper 2: items in any chest, in any zone and on any level,
can be used for building, crafting, quests and trading. Items are taken from the chest they are in.
No BepInEx; install is copy-and-paste. Player-facing docs: [package/README.txt](package/README.txt).

## How it loads without a mod loader

The game ships a language-pack loader. `Languages/<pack>/language.json` may list `preprocessors`,
which the loader resolves with `Type.GetType` at startup. Naming `GK2GlobalStorage.Hook, GK2GlobalStorage`
there makes the game load `GK2GlobalStorage.dll` from `GraveyardKeeper2_Data/Managed`. The fake pack is
removed from the language list.

Two assemblies:

- `loader/` → `Managed/GK2GlobalStorage.dll`. Tiny, no Harmony reference. Its module initializer schedules
  the start on the main thread, loads `Managed/GK2GlobalStorage/0Harmony.dll` (or reuses one another mod
  loaded), then loads the core **from bytes** and calls `Plugin.Init`.
- `src/` → `Managed/GK2GlobalStorage/GK2GlobalStorage.Core.dll`. The mod and its Harmony patches.

Why the split: an assembly loaded by the game from `Managed/` sits in Mono's default load context, which
does not bind its `0Harmony` reference to a copy loaded from a subfolder (`FileNotFoundException` even
though Harmony is in memory). A byte-loaded assembly resolves the reference through `AssemblyResolve`,
which hands it the Harmony already loaded.

## What is patched

| Patch | Effect |
| --- | --- |
| `MultiInventory(PlayerData, bool)` ctor | bag + current zone gets every chest appended (building, alchemy, garden, pickers, tooltips) |
| `WgoData.GetCraftableMultiInventory` | workbench material pool gets every chest (not zombie-run / auto crafters unless enabled) |
| `CraftElementBase.CanFinishCraft` | while it runs, the pool stays the game's: a zombie's result must fit in its area's chests, the only ones carriers store it in |
| `UIMultiInventoryWindowData` ctor | item picker windows: other areas' chests appended as their own sections only with `Pickers` on (default); chests holding an item the picker accepts come first (after the bag); with `PickersOnlyUsable` (default) only usable items and the chests holding them are shown (game's "not shown" item condition + empty slots hidden in an `InventoryWidget.Redraw` postfix); otherwise exactly as in the game |
| Transpiler on quest/dialog classes | bag item has/count/remove calls go through `QuestRedirect` (bag first, then chests) |
| Transpiler on `ReservoirInteractionHandler.Interact` and `UIFishingWindowData.UpdateAvailableFishingAndBaits` + `UIFishingWindow.OnSubmitPressed` / `Inventory.RemoveItemById` (option `Fishing`) | fishing bait: the water's "no bait" check and the window's bait counts go through `FishingRedirect` (bag + every chest); while the cast button runs, a bag removal that comes up short takes the rest from the chests (`OnSubmitBait` itself calls Unity internals, which .NET 8 cannot patch in the tests) |
| `Trading` | chests with sellable items listed under the bag; moved items are taken from the chest and return there on cancel |
| `UIVendorWindow.TryUpdateHappinessStatusIcons` (with `Trading`, i.e. whenever vendor windows show your chests) | on your side, after every redraw, each section's cells are reordered in their grid: items the vendor buys (the section's own greying condition) first, among them those that give gratitude (the game's `Vendor.HasHappinessForItem`, as for the icon; `townVendor` vendors = all but the Bishop and the Woodcarver), then greyed-out items, then empty cells; chests holding a sellable item with gratitude are listed first; inventories are not touched (`src/VendorOrder.cs`) |
| `WorldZoneData.CanDeliveryOrderBeTakenOnExecution` + `ZombieWgoData.CaretakerTryMoveToNearestInventoryWithRequiredItemCountToPickUp` (option `ZombieWorkers`) | when the area lacks a zombie delivery, the missing amount is moved in from other areas' chests; carriers walk to the nearest chest holding any of the item |
| `ConveyorChestComponent` / `ConveyorChestOutComponent` / `ConveyorPalletComponent` `.CanGiveItem` (option `Factory`, off by default) | a belt input chest (Conveyor Chest, Garden/Alcohol Storage) out of the item set on the asking cell's slot, or an empty Stockpile of one item, first gets one stack of it from the pool; only when the cell is empty (a stopped belt pulls nothing), never for a slot without an item; chests that feed a belt are never sources (`src/FactoryPatches.cs`) |
| `UIConveyorChestWindow` / `UIConveyorVegetablesChestWindow` / `UIConveyorWineChestWindow` `.SetChestItemSettingMode` (option `Factory`) | while you choose a belt slot's item, a section "Global Storage" under the chest lists every item the refill can bring, i.e. of the other chests except those that feed a belt (one cell each, total count, only what the chest's filters let in); the game switches its cells like the chest's, so clicking one sets the slot; taken out again before the cells go back to moving items (`src/FactorySlotPicker.cs`) |
| `PlayerData.CollectDrop` (optional, off by default) | picked-up items go to the nearest chest with room in the current zone; bag if none, for quest items, for items on the quick bar and in the factory (zones `conveyor`, `conveyor_storage`) unless `PickupsInFactory` is on |
| `ChestInteractionHandler.Interact` + `MultiInventory(WorldZoneData, WgoData, bool)` ctor (option `ChestWindows`, off by default) | opening a chest also lists other areas' chests, view only like the game shows the area's chests |
| `LanguageModLoader.AppendLanguages` | hides the loader pack |

### Other game builds

The patches come in 15 hooks (`src/Hooks.cs`, e.g. trading and the vendor item order are two), each going in as a
whole or not at all under its own Harmony owner id (`gk2globalstorage.<hook>`). The mod is built against one game
build, and the runtime looks up the game members a method names only when it compiles it, on its first call. On
another build (another store's version, an older one, or an `Assembly-CSharp.dll` replaced by a mod) a missing member
makes the whole patch method fail before its own try/catch, every time the patched game method runs: that is how
picking up items broke for some Nexus players. So before a hook goes in, `src/Guard.cs` compiles its patch methods
and every method of the mod they reach (calls, lambdas, iterators, static constructors); about 160 methods in under
20 ms at startup, most of which the game would compile on first use anyway. A hook that fails this check, whose game
method is missing, or that Harmony refuses halfway is taken out again and logged as `Not loaded: <option>. <reason>`.
Its options are greyed out and shown OFF in the settings window, pointing to Player.log, and the main menu says
`on, N patches unloaded`. When no hook a player would notice goes in, the mod is off (`off, failed to load`).
Player.log also gets the game version and the Assembly-CSharp module id, which tell two builds apart (the id is also
in Mono stack traces: `(at <module id>:0)`).

Global chests = every container with `OpenInMultiInventory` in every `WorldZoneData` of every
`GameSceneData` in the save (the same rule the game uses per zone), plus the factory's belt chests
(`conveyorType` Chest, ChestOut or Pallet): Garden Storage and Alcohol Storage have
`open_in_multi_inventory = false` in the game data, so the game's own area lists skip them. An inventory
shown by two objects (the cargo lift tops in the yard show the factory Stockpiles') is listed once, owned by
the object that holds it, so a Stockpile that feeds a belt is never a refill source through its lift.

Settings page: Shift+F2, or L3+R3 on a gamepad / Steam Deck (D-pad + A/B inside). The gamepad is read
straight from the game's Rewired player (`src/Pad.cs`), because the game's own input layer is paused while
the window is open. The game is Windows-only and Steam Deck Verified (runs through Proton), so the same
build works on the Deck.

When subscribed on the Steam Workshop, the loader picks the newer core from the Workshop folder, so
updates apply automatically. Publishing steps: [docs/publishing.md](docs/publishing.md); store texts:
[docs/store-pages.md](docs/store-pages.md).

## Developer mode and performance report

Set `DeveloperMode = true` in `GK2GlobalStorage.cfg` (persistentDataPath, next to Player.log) and restart
the game: the Shift+F2 page then also shows **Performance report**. While on, every 30 s it writes frame
times (p95/p99, hitches, GC) and the full duration of the game actions the mod touches (open chest,
workbench, vendor, pickup, item lists, quest checks), area changes (door / house exit / other level:
time until loaded and until the player can move) plus the mod's own code to Player.log and
`GK2GlobalStorage-perf.txt`. Each report is labelled with the Global storage switch; switching it starts
a new report, so ON vs OFF can be compared in one session (`src/Perf.cs`, `src/PerfPatches.cs`).
Off, it costs one flag check and its timing patches are not installed.

## Build

Requires the .NET SDK and the game installed (compile references come from its `Managed` folder;
nothing from the game is copied into this repo).

```
dotnet build GK2GlobalStorage.csproj -c Release
```

This builds both projects (the core build also builds `loader/`).

Output ready to copy: `package/CopyToGameFolder/`. To build and copy straight into the game (game closed):

```
dotnet build GK2GlobalStorage.csproj -c Release -t:Install
```

Steam Workshop upload folder (`dist/workshop/`, plus `dist/workshop_item.vdf` for SteamCMD once
`WorkshopId` is set in `Directory.Build.props`):

```
dotnet build GK2GlobalStorage.csproj -c Release -t:Workshop -p:ChangeNote="What changed"
```

Release ZIPs for GitHub and Nexus Mods (`dist/GK2GlobalStorage-<version>.zip` and `...-nexus.zip`; each
names its channel in `source.txt`, shown in the main menu as "installed: ..."):

```
dotnet build GK2GlobalStorage.csproj -c Release -t:Zip
```

Other install location: add `-p:GameDir="D:\SteamLibrary\steamapps\common\Graveyard Keeper 2"`.

## Tests

```
dotnet test tests\GK2GlobalStorage.Tests
```

(with `-p:GameDir=...` as above for another install location). The tests run outside the game, on .NET 8, against the
installed game's DLLs: some game types have default interface methods, which .NET Framework cannot load. They
cover the config file, the chest pool, the patched game methods (building lists, workbenches, zombie and automatic
crafters, factory workstations, chest windows), quest redirection, fishing bait, picker order, zombie deliveries, factory input
chest refills and slot item choice, the areas whose chests take pickups, the startup compile check (code that cannot
run is found, also through the calls, lambdas and iterators of a hook; a hook failing halfway is taken out again) and
what an unloaded hook does to the settings window and the main menu. They apply the mod's real Harmony patches, so a
game update that removes or renames a patched method fails a test.
`tests\GK2GlobalStorage.Tests\FakeGame.cs` builds a loaded save without the engine (how and why is described there).

Harmony 2.4.2 (MIT) comes from NuGet.
