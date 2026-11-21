## 1. Create GitHubClient

- [x] 1.1 Create `Akr/GitHubClient.cs` with `ResolveLatestAsync`, `EnsureSourceAsync`, `EnsureAllSourcesAsync`
- [x] 1.2 Move tarball download + extraction logic from `GitHubArchiveFetcher`
- [x] 1.3 Move tag resolution logic from `RepoRegistry.ResolveTags`
- [x] 1.4 Inline cache path logic (`~/.akr/sources/{repo}/{version}/`)
- [x] 1.5 Session-level latest version cache (`Dictionary<string, string>`)

## 2. Merge Akr.Core into Akr

- [x] 2.1 Move `Registry/` folder (PackageEntry, RepoRegistry) into `Akr/`
- [x] 2.2 Move `Tools/` folder (SourceSearcher, TypeFinder, records) into `Akr/Tools/`
- [x] 2.3 Move `SourceFetchException.cs` into `Akr/`
- [x] 2.4 Delete `Akr.Core/` project directory
- [x] 2.5 Delete `Akr.Tests.Shared/` project directory
- [x] 2.6 Update `akr.slnx`: remove Akr.Core and Akr.Tests.Shared
- [x] 2.7 Remove `ProjectReference` to Akr.Core from Akr.csproj and test csproj files
- [x] 2.8 Update all namespaces from `Akr.Core.*` to `Akr.*`
- [x] 2.9 Delete `Placeholder.cs` and `PlaceholderTests.cs`

## 3. Rewire tools and DI

- [x] 3.1 Update `SearchSourceTool` to take `GitHubClient` instead of `ISourceProvider`
- [x] 3.2 Update `LookupTypeTool` to take `GitHubClient` instead of `ISourceProvider`
- [x] 3.3 Update `McpSetup`: register `GitHubClient` via `AddHttpClient<GitHubClient>`, remove old registrations
- [x] 3.4 Delete all old interfaces and implementations

## 4. Fix tests

- [x] 4.1 Rewrite `GitHubClient` tests (merge fetcher + provider tests)
- [x] 4.2 Update smoke tests to use `GitHubClient` directly
- [x] 4.3 Update architecture tests (single assembly now)
- [x] 4.4 Delete obsolete test files (SourceProviderTests, FileSystemSourceCacheTests, EnvironmentVersionConfigTests)
- [x] 4.5 Build, test, format, slopwatch
