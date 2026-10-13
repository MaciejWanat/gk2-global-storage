# gk2-global-storage

Every feature must come with unit tests in `tests/GK2GlobalStorage.Tests` (NUnit, .NET 8, run against the installed
game's DLLs):

```
dotnet test tests\GK2GlobalStorage.Tests
```

Add `-p:GameDir="<game folder>"` when the game is not in the default Steam folder (see `Directory.Build.props`).
`tests/GK2GlobalStorage.Tests/FakeGame.cs` builds a loaded save without the engine; README "Tests" says what is covered.
