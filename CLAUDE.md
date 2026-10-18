# gk2-global-storage

Every feature must come with unit tests in `tests/GK2GlobalStorage.Tests` (NUnit, .NET 8, run against the installed
game's DLLs):

```
dotnet test tests\GK2GlobalStorage.Tests
```

Add `-p:GameDir="<game folder>"` when the game is not in the default Steam folder (see `Directory.Build.props`).
`tests/GK2GlobalStorage.Tests/FakeGame.cs` builds a loaded save without the engine; README "Tests" says what is covered.

## Releases

Steps: [docs/publishing.md](docs/publishing.md). The Workshop change note (`dist/changenote.bbcode`) starts with the
update name on its own line, then the list of changes:

```
Update 1.0.4
[list]
[*][b]Area:[/b] what changed for the player.
[*]Tested on game version 1.010.
[/list]
```
