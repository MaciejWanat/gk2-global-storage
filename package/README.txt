Global Storage for Graveyard Keeper 2  (v1.0.1)
================================================

Items in ANY of your storage chests, in ANY zone and on ANY level, can be used for:
  - building (blueprints, building desks, town buildings)
  - crafting at workbenches you operate yourself
  - alchemy, garden beds, prayers, resurrection, tooltips
  - quest and dialog requirements (checked against, and taken from, bag + chests)
  - trading: chests holding what the vendor buys are listed in the vendor window

Items are taken from the chest where they are, wherever it is: craft in area X
with wood that sits in a chest in area Y, and the wood disappears from that chest.
Your bag and the chests of the zone you are in are used first.
No BepInEx. Does not modify game files and does not write anything extra to your save.

INSTALL
-------
1. Steam -> right-click Graveyard Keeper 2 -> Manage -> Browse local files.
2. Copy the two folders from CopyToGameFolder into that game folder
   (answer "Yes" to merge/replace).
3. Start the game.

Steam Deck: do the same in Desktop Mode (Dolphin file manager; Ctrl+H shows
hidden folders such as ~/.local/share/Steam). Then return to Gaming Mode.

Steam Workshop: subscribe, then copy the two folders from
steamapps\workshop\content\4358690\<item id>\CopyToGameFolder once. After that,
Workshop updates apply automatically on the next game start.

Game folder after install:
  Graveyard Keeper 2
  ├─ GraveyardKeeper2_Data
  │  └─ Managed
  │     ├─ GK2GlobalStorage.dll                       <- new (loader)
  │     └─ GK2GlobalStorage\                          <- new
  │        ├─ GK2GlobalStorage.Core.dll
  │        └─ 0Harmony.dll
  └─ Languages
     └─ gk2globalstorage\language.json   <- new

How it loads: the game's language-pack loader looks up the type named in
language.json, which loads GK2GlobalStorage.dll; that small loader loads Harmony and
then the mod itself (GK2GlobalStorage.Core.dll). The pack is hidden from the
language list.

Check it works: the main menu shows "Global Storage v1.0.1 / status: on" in the
top right corner. Player.log in %USERPROFILE%\AppData\LocalLow\Lazy Bear Games\Graveyard Keeper 2
should contain "[GK2GlobalStorage] Global Storage 1.0.1 (game folder) loaded (9/9 patches)".

SETTINGS
--------
Open:      Shift+F2 (keyboard) or click both sticks, L3 + R3 (gamepad / Steam Deck)
Gamepad:   D-pad or left stick = select, A = toggle, B = close
Keyboard:  arrows = select, Enter/Space = toggle, Esc = close; the mouse works too
Changes apply immediately and are saved to
%USERPROFILE%\AppData\LocalLow\Lazy Bear Games\Graveyard Keeper 2\GK2GlobalStorage.cfg
  Enabled         turns the whole mod on or off                  (default on)
  Building        building, alchemy, garden beds and prayers     (default on)
                  use items from all your chests
  Workbenches     workbenches you use yourself take materials    (default on)
                  from all your chests
  Quests          items a quest or dialog asks for can come      (default on)
                  from any chest
  ZombieWorkers   zombie carriers fetch from the nearest chest;   (default off)
                  what the area lacks is brought over from
                  chests in other areas
  AutoCrafters    crafters with no worker also take from all     (default off)
                  your chests; can empty them unnoticed
  FuelContainers  fuel slots of furnaces and similar objects     (default off)
                  count as chests
  PickupsToChest  items you pick up go to the nearest chest      (default off)
                  with room in this area, otherwise to your bag;
                  quest items and items on your quick bar always
                  go to the bag
  ShowOtherChests chests from other areas appear below your       (default on)
                  inventory
    Trading       vendor windows: sell straight from your chests (default on)
    Pickers       choosing seeds, organs, grave decorations and  (default on)
                  similar; chests with something usable first
      PickersOnlyUsable  pickers show only usable items and   (default on)
                  the chests holding them
    ChestWindows  opening any chest; other chests are view only  (default off)
                  there. Slightly affects performance
  ExcludedZones   comma-separated zone ids that stay local (file only)

Trading: items you put into a deal from a chest go back to that chest if you
cancel the deal or close the window.

UNINSTALL
---------
Close the game and delete:
  GraveyardKeeper2_Data\Managed\GK2GlobalStorage.dll
  GraveyardKeeper2_Data\Managed\GK2GlobalStorage\
  Languages\gk2globalstorage\

Harmony (MIT license, see Harmony_LICENSE.txt) by Andreas Pardeike.
