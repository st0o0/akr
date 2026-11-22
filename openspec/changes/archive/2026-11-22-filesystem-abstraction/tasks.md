## 1. Package Setup

- [x] 1.1 Add `System.IO.Abstractions` to `Directory.Packages.props` and `Akr.csproj`
- [x] 1.2 Add `System.IO.Abstractions.TestingHelpers` to `Directory.Packages.props` and `Akr.Tests.csproj`

## 2. FileService Creation

- [x] 2.1 Create `FileService.cs` with constructor taking `IFileSystem`, implementing: `ReadAllText`, `ReadAllLines`, `ReadAllBytes`, `WriteAtomic`, `FileExists`, `DirectoryExists`, `CreateDirectory`, `DeleteDirectory`, `MoveDirectory`, `EnumerateDirectories`, `Combine`, `GetFullPath`, `GetFileName`, `GetFileNameWithoutExtension`, `GetDirectoryName`, `GetRelativePosixPath`
- [x] 2.2 Add `EnumerateFiles(roots, pattern)` method (absorb `FileEnumerator` logic including `/obj/`+`/bin/` exclusion)
- [x] 2.3 Add `EnumerateConfigFiles(roots)` method (absorb `ConfigFiles` logic including test/example exclusion and known config file detection)
- [x] 2.4 Add `StripHoconComment(line)` method (absorb `HoconHelper.StripComment` logic)
- [x] 2.5 Add `ExtractTarEntryAsync(entry, path, ct)` method wrapping `entry.ExtractToFileAsync` with directory creation

## 3. DI Registration

- [x] 3.1 Register `IFileSystem` (singleton `FileSystem`), `FileService` (singleton), and `SourceSearcher` (singleton) in `McpSetup`
- [x] 3.2 Add `FileService` parameter to `GitHubClient` constructor, replace all direct I/O calls

## 4. Tool Class Migration

- [x] 4.1 Convert `TypeTools` — add `FileService` constructor parameter, replace `File.ReadAllText`, `Directory.Exists`, `Directory.EnumerateFiles`, `Path.GetRelativePath` calls
- [x] 4.2 Convert `HostingTools` — add `FileService` constructor parameter, replace `File.ReadAllLines`, `File.ReadAllText`, `Directory.Exists`, `Directory.EnumerateFiles`, `Path.GetRelativePath`, `HoconHelper.StripComment` calls
- [x] 4.3 Convert `StreamTools` — add `FileService` and `SourceSearcher` constructor parameters, replace `File.ReadAllText`, `Directory.Exists`, `Directory.EnumerateFiles`, `Path.GetRelativePath` calls
- [x] 4.4 Convert `DeepIntelligenceTools` — add `FileService` constructor parameter, replace `File.ReadAllText`, `FileEnumerator.EnumerateFiles` calls
- [x] 4.5 Convert `StreamPersistenceTools` — add `FileService` constructor parameter, replace `File.ReadAllText`, `FileEnumerator.EnumerateFiles` calls
- [x] 4.6 Convert `ConfigMigrationTools` — add `FileService` constructor parameter, replace `File.ReadAllLines`, `File.ReadLines`, `Directory.Exists`, `Directory.EnumerateFiles`, `Path.GetRelativePath`, `Path.GetFileName`, `HoconHelper.StripComment`, `ConfigFiles.EnumerateConfigFiles` calls
- [x] 4.7 Convert `EcosystemTools` — add `FileService` constructor parameter, replace `File.ReadAllLines`, `File.ReadAllText`, `Path.GetFileNameWithoutExtension`, `FileEnumerator.EnumerateFiles` calls
- [x] 4.8 Convert `SourceSearcher` — make non-static class with `FileService` constructor parameter, replace `File.ReadAllLines`, `Directory.Exists`, `Directory.EnumerateFiles`, `Path.GetRelativePath` calls

## 5. Cleanup

- [x] 5.1 Delete `FileEnumerator.cs`
- [x] 5.2 Delete `HoconHelper.cs`
- [x] 5.3 Delete `ConfigFiles.cs`
- [x] 5.4 Verify no remaining direct `System.IO` calls in `src/Akr/` (except `FileService.cs` itself)

## 6. Test Updates

- [x] 6.1 Update `GitHubClientCachingTests` — pass `FileService` with real `FileSystem` to `GitHubClient` constructor
- [x] 6.2 Update all 10 tool test files that construct tool classes directly to pass `FileService`
- [x] 6.3 Verify `SourceSearcher` tests still work after non-static conversion

## 7. Verification

- [x] 7.1 Run `dotnet build src/akr.slnx` — zero errors, zero warnings
- [x] 7.2 Run `dotnet format src/akr.slnx --verify-no-changes`
- [x] 7.3 Run `dotnet run --project src/Akr.Tests/Akr.Tests.csproj` — all 152 tests pass
- [x] 7.4 Grep for remaining `File\.|Directory\.|Path\.` in `src/Akr/` — only `FileService.cs` has direct I/O
