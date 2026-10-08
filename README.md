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
| `UIMultiInventoryWindowData` ctor | item picker windows: other areas' chests appended as their own sections only with `Pickers` on (default); chests holding an item the picker accepts come first (after the bag); otherwise exactly as in the game |
| Transpiler on quest/dialog classes | bag item has/count/remove calls go through `QuestRedirect` (bag first, then chests) |
| `Trading` | chests with sellable items listed under the bag; moved items are taken from the chest and return there on cancel |
| `PlayerData.CollectDrop` (optional, off by default) | picked-up items go to the nearest chest with room in the current zone; bag if none, for quest items and for items on the quick bar |
| `ChestInteractionHandler.Interact` + `MultiInventory(WorldZoneData, WgoData, bool)` ctor (option `ChestWindows`, off by default) | opening a chest also lists other areas' chests, view only like the game shows the area's chests |
| `LanguageModLoader.AppendLanguages` | hides the loader pack |

Global chests = every container with `OpenInMultiInventory` in every `WorldZoneData` of every
`GameSceneData` in the save (the same rule the game uses per zone).

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

Harmony 2.4.2 (MIT) comes from NuGet.
