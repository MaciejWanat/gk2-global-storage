# gk2-global-storage

Every feature has unit tests in `tests/GK2GlobalStorage.Tests` (NUnit, .NET 8, run against the installed game's DLLs):

```
dotnet test tests\GK2GlobalStorage.Tests -p:GameDir="<game folder>"
```

`tests/GK2GlobalStorage.Tests/FakeGame.cs` builds a loaded save without the engine; README "Tests" says what is covered.
