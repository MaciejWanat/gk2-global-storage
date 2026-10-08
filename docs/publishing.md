# Publishing

Channels: GitHub Releases (canonical download), Steam Workshop (auto-updates), Nexus Mods.
Store texts: [store-pages.md](store-pages.md).

## How Workshop auto-update works

Steam keeps the subscribed item in `steamapps/workshop/content/4358690/<WorkshopId>/`. The loader
(`Managed/GK2GlobalStorage.dll`, from `loader/`) compares the core in the game folder with the core in
`<WorkshopId>/CopyToGameFolder/GraveyardKeeper2_Data/Managed/GK2GlobalStorage/` and loads the newer one
(on a tie, the Workshop copy). Only the pinned `WorkshopId` from `Directory.Build.props` is ever looked at.

What auto-updates: `GK2GlobalStorage.Core.dll` and `0Harmony.dll`.
What does **not**: the loader DLL and `language.json` – players only get those by copying again.
Keep the loader small and change it only when unavoidable (and say so in the change notes).

The settings window header shows which copy is running: `v1.0.0 (Workshop)` or `(game folder)`.

## Workshop quirks (in-game Creator)

The game has a built-in uploader (Shift+F11), enabled with `"workshopCreatorMode": true` in
`%USERPROFILE%\AppData\LocalLow\Lazy Bear Games\Graveyard Keeper 2\Mods\workshop.json` (restart the game).
On **every** upload it:

- sets the description to the title (overwrites your description),
- sets visibility to Unlisted,
- offers a single tag: `Translation` (the only type this game's Workshop has).

So use the Creator only for the **first** upload; do updates with SteamCMD, which only changes what the
`.vdf` file lists (content + change note). Check the item page after the first SteamCMD update to confirm
title, description and visibility were kept.

## First release on the Workshop

1. Thumbnail: `workshop/Thumbnail.png`. Screenshots: `docs/screenshots/`.
2. Build the upload folder:
   ```
   dotnet build GK2GlobalStorage.csproj -c Release -t:Workshop
   ```
   Result: `dist/workshop/` (`CopyToGameFolder/`, `README.txt`, `Thumbnail.png`).
3. Enable the Creator (see above), start the game, press **Shift+F11**, pick `dist/workshop`, enter the
   title, tag `Translation`, upload. Accept the Steam Workshop legal agreement if Steam asks.
   The item is created **Unlisted** – nobody sees it yet.
4. Copy the item id (number in the item URL) into `Directory.Build.props`:
   `<WorkshopId>1234567890</WorkshopId>`.
5. Rebuild with the id (this build's loader knows where to look for updates) and upload it over the same item:
   ```
   dotnet build GK2GlobalStorage.csproj -c Release -t:Workshop -p:ChangeNote="Initial release"
   steamcmd +login <steam_user> +workshop_build_item "<repo>\dist\workshop_item.vdf" +quit
   ```
   SteamCMD: https://developer.valvesoftware.com/wiki/SteamCMD (log in yourself; Steam Guard will ask for a code).
6. On the item page: **Edit title & description** → paste the Steam BBCode from store-pages.md;
   **Add/edit images & videos** → the screenshots; then set visibility **Public**.
7. Subscribe from a clean install (or a second PC / Steam Deck) and follow the install steps from the
   description to verify. Player.log should say `Using the Workshop copy`.
8. Rebuild the release ZIPs with the id (`-t:Zip`, see below) for GitHub and Nexus, so manual installs auto-update
   too if the player later subscribes.

## Updating

1. Change code, test locally (`-t:Install`).
2. Release version: bump `<Version>` in `Directory.Build.props` (stays 1.0.0 until the first public release).
3. Workshop:
   ```
   dotnet build GK2GlobalStorage.csproj -c Release -t:Workshop -p:ChangeNote="What changed"
   steamcmd +login <steam_user> +workshop_build_item "<repo>\dist\workshop_item.vdf" +quit
   ```
   Do not use the in-game Creator for updates (it wipes the description and unlists the item).
4. GitHub: new release with the GitHub ZIP (see below). Nexus: upload the `-nexus` ZIP as a new main-file version.

## GitHub release ZIP

```
dotnet build GK2GlobalStorage.csproj -c Release -t:Zip
```
writes two ZIPs to `dist/` with the same files (`CopyToGameFolder/` + `README.txt`):

- `GK2GlobalStorage-<version>.zip` for GitHub,
- `GK2GlobalStorage-<version>-nexus.zip` for Nexus Mods.

Each also carries `Managed/GK2GlobalStorage/source.txt` (`GitHub` / `Nexus Mods`), which the main menu
label shows as "installed: ...". Workshop installs show "Steam Workshop"; anything else shows "manual".
Then:
```
gh release create v<version> dist/GK2GlobalStorage-<version>.zip --title "Global Storage <version>" --notes-file <notes.md>
```

## Nexus Mods

1. Create the mod page (Gameplay category), paste the Nexus BBCode from store-pages.md.
2. Upload `GK2GlobalStorage-<version>-nexus.zip` as the main file, version = `<Version>`.
3. Images tab: the thumbnail (full size) and the screenshots.
4. Permissions: open source (link GitHub); Harmony MIT license included.

## Before every release

- [ ] Player.log shows `(7/7 patches)` and `Quest patch: N method(s) redirected`
- [ ] Settings window opens with Shift+F2 and with L3+R3 on a gamepad; A toggles, B closes
- [ ] Craft in zone X with items only in a chest in zone Y → chest Y loses them
- [ ] Quest turn-in and vendor sale from chests work; cancelled vendor deal returns items to the chest
- [ ] Repo scan: no personal paths, e-mails, keys; no game files or decompiled code
