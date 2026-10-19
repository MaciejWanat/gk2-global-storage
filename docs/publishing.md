# Publishing

Channels: [GitHub Releases](https://github.com/MaciejWanat/gk2-global-storage/releases) (canonical download),
[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3815861361) (auto-updates),
[Nexus Mods](https://www.nexusmods.com/graveyardkeeper2/mods/385).
Store texts: [store-pages.md](store-pages.md).

## How Workshop auto-update works

Steam keeps the subscribed item in `steamapps/workshop/content/4358690/<WorkshopId>/`. The loader
(`Managed/GK2GlobalStorage.dll`, from `loader/`) compares the core in the game folder with the core in
`<WorkshopId>/CopyToGameFolder/GraveyardKeeper2_Data/Managed/GK2GlobalStorage/` and loads the newer one
(on a tie, the Workshop copy). Only the pinned `WorkshopId` from `Directory.Build.props` is ever looked at.

What auto-updates: `GK2GlobalStorage.Core.dll` and `0Harmony.dll`.
What does **not**: the loader DLL and `language.json` – players only get those by copying again.
Keep the loader small and change it only when unavoidable (and say so in the change notes).

The settings window header shows which copy is running: `v1.0.1 (Workshop)` or `(game folder)`.

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
2. Release version: bump `<Version>` in `Directory.Build.props` (1.0.0 was the first public release; every update gets a new version, or the Workshop copy may not win over a newer local one).
3. Workshop:
   ```
   dotnet build GK2GlobalStorage.csproj -c Release -t:Workshop -p:ChangeNoteFile=dist\changenote.bbcode
   steamcmd +login <steam_user> +workshop_build_item "<repo>\dist\workshop_item.vdf" +quit
   ```
   The change note starts with the update name on its own line, then a bulleted list, one change per item, in a
   file (`dist\` is not committed):
   ```
   Update 1.0.4
   [list]
   [*]Zombie workers: ...
   [*]...
   [/list]
   ```
   A one-line note can also go in `-p:ChangeNote="..."`, but there write `%3B` instead of `;` and `%2C` instead
   of `,` (MSBuild splits properties on them); the file needs no escapes.
   No straight double quotes (") in the change note or in `workshop/description.bbcode`: SteamCMD reads the
   .vdf without escapes and would cut the text there (the build stops with an error). Use “ ” or ' instead.
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

Mod page: https://www.nexusmods.com/graveyardkeeper2/mods/385 (game `graveyardkeeper2`, mod id 385), created with
1.0.3. The page itself can only be created and set up on the website; the Nexus API (v3) can add file versions.

Update:
1. Files: upload `GK2GlobalStorage-<version>-nexus.zip` as a new version of the main file, version = `<Version>`,
   manual download only (the mod is installed by copying folders, not by a mod manager). Archive the old one.
2. If the store text changed, paste the Nexus BBCode from store-pages.md into the description.

Images (header 1300×372, gallery 1920×1080, cover first), made from the thumbnail and the screenshots with
ImageMagick (Git Bash, into `dist/nexus-images/`, which is not committed):
```
mkdir -p dist/nexus-images
magick docs/screenshots/s1.jpg -resize 1300x -gravity center -crop 1300x372+0+0 +repage -blur 0x6 -fill black -colorize 45% \( workshop/Thumbnail.png -resize 340x340 \) -gravity center -composite dist/nexus-images/header.jpg
magick docs/screenshots/s1.jpg -resize 1920x1080^ -gravity center -extent 1920x1080 -blur 0x8 -fill black -colorize 40% \( workshop/Thumbnail.png -filter point -resize 200% \) -gravity center -composite dist/nexus-images/01-cover.jpg
magick docs/screenshots/s1.jpg -resize 1920x1080^ -gravity center -extent 1920x1080 dist/nexus-images/02-chest-window.jpg
magick docs/screenshots/s2.jpg -resize 1920x1080^ -gravity center -extent 1920x1080 dist/nexus-images/03-building.jpg
magick -size 1920x1080 xc:"#16151a" docs/screenshots/s3.jpg -gravity center -composite dist/nexus-images/04-settings.jpg
```
Permissions: open source (link GitHub); Harmony MIT license included.

## Before every release

- [ ] Player.log shows `(14/14 patches)`, `Quest patch: N method(s) redirected`, no `Not loaded:` line, and the main menu says `status: on`
- [ ] Settings window opens with Shift+F2 and with L3+R3 on a gamepad; A toggles, B closes
- [ ] Craft in zone X with items only in a chest in zone Y → chest Y loses them
- [ ] Quest turn-in and vendor sale from chests work; cancelled vendor deal returns items to the chest
- [ ] Repo scan: no personal paths, e-mails, keys; no game files or decompiled code
