## Why

Every MCP tool call re-scans the entire Akka.NET source tree (thousands of `.cs` files), re-resolves all repository roots, and re-allocates intermediate collections. This adds unnecessary latency to every request. Additionally, the codebase has accumulated duplicate helper methods, non-source-generated regex, a stale package version, and duplicated MSBuild properties that add maintenance burden.

## What Changes

- Cache `EnsureAllSourcesAsync` result so root paths are resolved once per `GitHubClient` lifetime
- Cache `ScanTypes` results in `DeepIntelligenceTools` so type declarations are parsed once per source tree state
- Convert all static `new Regex(...)` fields to `[GeneratedRegex]` in `HostingTools`, `StreamTools`, `ConfigMigrationTools`
- Extract shared `FileEnumerator` utility replacing three duplicate `EnumerateFiles` implementations and `ReadSources`
- Extract shared `HoconHelper.StripComment` replacing identical methods in `HostingTools` and `ConfigMigrationTools`
- Make `RepoRegistry.GetAllRepos()` return a pre-computed static field instead of re-computing on each call
- Bump `Microsoft.Extensions.Hosting` from 9.0.6 to 11.x to match net10.0 target framework
- Remove duplicate `DebugType`/`EmbedUntrackedSources` properties from `Akr.csproj` (already inherited from `Directory.Build.props`)

## Capabilities

### New Capabilities

- `source-caching`: Session-scoped caching for source roots and parsed type declarations to avoid redundant I/O on every tool call
- `shared-file-utilities`: Consolidated file enumeration and HOCON comment stripping, replacing duplicate implementations across tool classes

### Modified Capabilities

- `repo-registry`: `GetAllRepos()` changes from computed-per-call to static pre-computed field

## Impact

- **Files changed**: `GitHubClient.cs`, `DeepIntelligenceTools.cs`, `HostingTools.cs`, `StreamTools.cs`, `ConfigMigrationTools.cs`, `StreamPersistenceTools.cs`, `EcosystemTools.cs`, `RepoRegistry.cs`, `Akr.csproj`, `Directory.Packages.props`
- **New files**: `Tools/FileEnumerator.cs`, `Tools/HoconHelper.cs`
- **Dependencies**: `Microsoft.Extensions.Hosting` version bump (minor)
- **Breaking changes**: None (all changes are internal implementation details)
