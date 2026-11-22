## 1. Shared Utilities

- [x] 1.1 Create `Tools/FileEnumerator.cs` with `EnumerateFiles(IEnumerable<string> roots, string pattern)` returning `IEnumerable<(string Full, string Relative)>`, including `/obj/`+`/bin/` exclusion and forward-slash normalization
- [x] 1.2 Create `Tools/HoconHelper.cs` with `StripComment(string line)` extracted from the identical implementations in HostingTools and ConfigMigrationTools
- [x] 1.3 Replace `StreamPersistenceTools.EnumerateFiles` usages with `FileEnumerator.EnumerateFiles`
- [x] 1.4 Replace `EcosystemTools.EnumerateFiles` usages with `FileEnumerator.EnumerateFiles`
- [x] 1.5 Replace `DeepIntelligenceTools.ReadSources` usages with `FileEnumerator.EnumerateFiles` (adapt callers to construct `SourceFile` from tuple)
- [x] 1.6 Replace `HostingTools.StripComment` with `HoconHelper.StripComment`
- [x] 1.7 Replace `ConfigMigrationTools.StripComment` with `HoconHelper.StripComment`
- [x] 1.8 Remove the now-unused private methods from all tool classes

## 2. Source Caching

- [x] 2.1 Add `_cachedRoots` field to `GitHubClient` and cache the result of `EnsureAllSourcesAsync` after first successful call
- [x] 2.2 Add static `ConcurrentDictionary<string, List<TypeDecl>>` cache to `DeepIntelligenceTools` keyed by sorted+joined root paths
- [x] 2.3 Update `ScanTypes` to check cache before scanning, and store result after scanning
- [x] 2.4 Add tests verifying `EnsureAllSourcesAsync` caching behavior (second call returns same instance)
- [x] 2.5 Add tests verifying `ScanTypes` caching behavior

## 3. GeneratedRegex Conversion

- [x] 3.1 Make `HostingTools` a `partial` class, convert `_methodPattern` to `[GeneratedRegex]`
- [x] 3.2 Make `StreamTools` a `partial` class, convert `_classPattern` to `[GeneratedRegex]`
- [x] 3.3 Make `ConfigMigrationTools` a `partial` class, convert `_methodPattern`, `_hoconKeyPattern`, `_entryPattern`, `_orderingPattern`, `_diHintPattern` to `[GeneratedRegex]`

## 4. Registry Optimization

- [x] 4.1 Change `RepoRegistry.GetAllRepos()` to return a pre-computed static `IReadOnlyCollection<string>` field

## 5. Project Cleanup

- [x] 5.1 Remove duplicate `DebugType` and `EmbedUntrackedSources` from `Akr.csproj`
- [x] 5.2 Bump `Microsoft.Extensions.Hosting` version in `Directory.Packages.props` from 9.0.6 to 11.x

## 6. Verification

- [x] 6.1 Run `dotnet build src/akr.slnx` — zero errors, zero warnings
- [x] 6.2 Run `dotnet format src/akr.slnx --verify-no-changes`
- [x] 6.3 Run `dotnet run --project src/Akr.Tests/Akr.Tests.csproj` — all 150+ tests pass
