# Store pages: Steam Workshop and Nexus Mods

Ready-to-paste descriptions (BBCode). Screenshots (`docs/screenshots/`) go on the page as images, not into
the description. Thumbnail: `workshop/Thumbnail.png`.

---

## Steam Workshop

**Title:** `Global Storage`

Description: [`workshop/description.bbcode`](../workshop/description.bbcode). It is sent with every
SteamCMD upload (the Workshop page itself does not let us edit it), so change it there.

---

## Nexus Mods

**Page:** https://www.nexusmods.com/graveyardkeeper2/mods/385
**Name:** `Global Storage`
**Summary:** Use items from any chest, in any area and on any level, for building, crafting, quests and trading. No BepInEx. Gamepad and Steam Deck friendly.
**Category:** Gameplay · **Main file:** `dist/GK2GlobalStorage-<version>-nexus.zip` · **Requirements:** none

```bbcode
[color=#d4d4d8]Tired of constantly fetching items from chest to chest? Suffer no more.[/color]
[color=#D4D4D8]
Use items from [b]any chest, in any area and on any level[/b] – for building, crafting, quests and trading. Items are taken from the chest they are in. No more running back for materials.[/color]
[color=#D4D4D8]
[size=5][b]Features[/b][/size][/color]
[list]
[*][color=#D4D4D8][b]Building, workbenches, alchemy, garden, prayers[/b] – materials from all your chests count[/color][/*]
[*][color=#D4D4D8][b]Quests and dialogs[/b] – required items can come from any chest[/color][/*]
[*][color=#D4D4D8][b]Vendors[/b] – sell straight from your chests[/color][/*]
[*][color=#D4D4D8][b]Item pickers[/b] (seeds, organs, grave decorations…) – show only the items they can use, from the chests that have them[/color][/*]
[*][color=#D4D4D8][b]Optional:[/b] pickups go to the nearest chest in the area, zombie carriers bring materials from all chests (nearest first), automatic crafters use all chests, opening a chest shows your other chests[/color][/*]
[*][color=#D4D4D8]Settings in game: [b]Shift+F2[/b], or [b]L3 + R3[/b] on a gamepad / Steam Deck[/color][/*]
[/list]
[color=#D4D4D8][size=5][b]Install[/b][/size][/color]
[list=1]
[*][color=#D4D4D8]Extract the ZIP.[/color][/*]
[*][color=#D4D4D8]Steam → right-click Graveyard Keeper 2 → Manage → [b]Browse local files[/b].[/color][/*]
[*][color=#D4D4D8]Copy the two folders from [b]CopyToGameFolder[/b] into the game folder (merge).[/color][/*]
[/list]
[color=#D4D4D8][b]Steam Deck:[/b] in Desktop Mode, extract the ZIP, open the game folder (Steam → Manage → Browse local files), copy the two folders from CopyToGameFolder into it (merge), back to Gaming Mode.[/color]
[color=#D4D4D8]
[size=5][b]Is it safe for my save?[/b][/size][/color]
[list]
[*][color=#D4D4D8][b]Nothing is added to your save.[/b] No extra data on items, chests or the world – a save made with the mod is a normal save, and you can remove the mod at any time.[/color][/*]
[*][color=#D4D4D8][b]It never saves the game itself.[/b] Only the game writes your save.[/color][/*]
[*][color=#D4D4D8][b]Items are only used or moved.[/b] Nothing is copied out of thin air: items are taken from a chest when you build, craft, finish a quest or sell, the same way the game takes them from your bag.[/color][/*]
[*][color=#D4D4D8][b]Worth knowing:[/b] the mod really uses any chest, so materials you keep far away for later can be used too. With the optional zombie and pickup settings, items are moved between chests.[/color][/*]
[*][color=#D4D4D8]As with any mod, keep a backup of your saves.[/color][/*]
[/list]
[size=5][b]Settings worth attention[/b][/size]
[list]
[*][color=#D4D4D8][b]Pickups go to nearest chest[/b] - everything you pick goes to the global storage. I recommend switching it on and using it all the time. By default it's off because the option can be very confusing if the player is not aware of it, but playing with it is much more convenient.[/color][/*]
[*][color=#D4D4D8][b]Factory Input Chests[/b] - allows you to feed factory chests from global storage - just pick output you want and scroll down for the item you want. By default this is off, so the factory logic stays intact.[/color][/*]
[/list]
[color=#D4D4D8][size=5][b]Good to know[/b][/size][/color]
[list]
[*][color=#D4D4D8]Any feedback on bugs and improvements welcomed! Please let me know if something's wrong before leaving a negative review :)[/color][/*]
[*][color=#D4D4D8]No BepInEx. Does not change game files or your save – remove it any time by deleting [i]GraveyardKeeper2_Data\Managed\GK2GlobalStorage.dll[/i], the folder [i]GraveyardKeeper2_Data\Managed\GK2GlobalStorage[/i] and [i]Languages\gk2globalstorage[/i].[/color][/*]
[*][color=#D4D4D8]Do not combine with other mods that share storage between areas.[/color][/*]
[*][color=#D4D4D8]Factory: workstations only use what the conveyor belts bring, as in the game. All factory chests (Conveyor Chests, Stockpiles, Garden and Alcohol Storage) count like any other chest.[/color][/*]
[*][color=#D4D4D8]Also on the Steam Workshop with automatic updates.[/color][/*]
[/list]

[color=#D4D4D8]Tested on game version 1.010.

Open source: [url=https://github.com/MaciejWanat/gk2-global-storage]GitHub[/url] · Uses Harmony (MIT).[/color]
```
