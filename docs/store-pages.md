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

**Name:** `Global Storage`
**Summary:** Use items from any chest, in any area and on any level, for building, crafting, quests and trading. No BepInEx. Gamepad and Steam Deck friendly.
**Category:** Gameplay · **Main file:** `dist/GK2GlobalStorage-<version>-nexus.zip` · **Requirements:** none

```bbcode
Use items from [b]any chest, in any area and on any level[/b] – for building, crafting, quests and trading. Items are taken from the chest they are in. No more running back for materials.

[size=5][b]Features[/b][/size]
[list]
[*][b]Building, workbenches, alchemy, garden, prayers[/b] – materials from all your chests count
[*][b]Quests and dialogs[/b] – required items can come from any chest
[*][b]Vendors[/b] – sell straight from your chests
[*][b]Item pickers[/b] (seeds, organs, grave decorations…) – show only the items they can use, from the chests that have them
[*][b]Optional:[/b] pickups go to the nearest chest in the area, zombie carriers bring materials from all chests (nearest first), automatic crafters use all chests, opening a chest shows your other chests
[*]Settings in game: [b]Shift+F2[/b], or [b]L3 + R3[/b] on a gamepad / Steam Deck
[/list]

[size=5][b]Install[/b][/size]
[list=1]
[*]Extract the ZIP.
[*]Steam → right-click Graveyard Keeper 2 → Manage → [b]Browse local files[/b].
[*]Copy the two folders from [b]CopyToGameFolder[/b] into the game folder (merge).
[/list]
[b]Steam Deck:[/b] in Desktop Mode, extract the ZIP, open the game folder (Steam → Manage → Browse local files), copy the two folders from CopyToGameFolder into it (merge), back to Gaming Mode.

[size=5][b]Is it safe for my save?[/b][/size]
[list]
[*][b]Nothing is added to your save.[/b] No extra data on items, chests or the world – a save made with the mod is a normal save, and you can remove the mod at any time.
[*][b]It never saves the game itself.[/b] Only the game writes your save.
[*][b]Items are only used or moved.[/b] Nothing is copied out of thin air: items are taken from a chest when you build, craft, finish a quest or sell, the same way the game takes them from your bag.
[*][b]Worth knowing:[/b] the mod really uses any chest, so materials you keep far away for later can be used too. With the optional zombie and pickup settings, items are moved between chests.
[*]As with any mod, keep a backup of your saves.
[/list]

[size=5][b]Good to know[/b][/size]
[list]
[*]No BepInEx. Does not change game files or your save – remove it any time by deleting [i]GraveyardKeeper2_Data\Managed\GK2GlobalStorage.dll[/i], the folder [i]GraveyardKeeper2_Data\Managed\GK2GlobalStorage[/i] and [i]Languages\gk2globalstorage[/i].
[*]Do not combine with other mods that share storage between areas.
[*]Also on the Steam Workshop with automatic updates.
[/list]

Open source: [url=https://github.com/MaciejWanat/gk2-global-storage]GitHub[/url] · Uses Harmony (MIT).
```
