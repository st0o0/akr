## Why

GitHubClient is a 509-line God-Object handling HTTP fetching, tar.gz extraction, version resolution, cache metadata, and LRU eviction. Tool files are grouped arbitrarily — EcosystemTools (733 lines) contains 9 unrelated tools, StreamPersistenceTools mixes Streams and Persistence, `search_source` is in StreamTools. Namespace names describe mechanisms (`Analysis/`, `Pipelines/`) not domain concepts. Docs come from archived getakka.net (frozen 2019) while `akkadotnet/akka.net` has 158 current docs under `docs/articles/` already cached with every source fetch.

## What Changes

- Split `GitHubClient` into `Source/` layer: `SourceFetcher` (HTTP + extraction), `SourceCache` (metadata + LRU), `VersionResolver` (GitHub API + ProjectScanner)
- Move `SourcePipeline` into `Source/`, eliminate `DocsPipeline` (docs are free from cached akka.net repo)
- Rename `Analysis/` → `Scanning/` (domain: "scanning cached files for patterns")
- Remove `Pipelines/` folder entirely
- Reorganize `Tools/` into 12 domain-themed files: TypeTools (8), ConfigTools (4), HostingTools (5), StreamTools (4), PersistenceTools (4), SerializationTools (3), ClusterTools (3), SearchTools (1), TestKitTools (1), DocsTools (2), VersionTools (2), ProjectContextTools (1)
- Drop getakka.net — use `docs/articles/` from `akkadotnet/akka.net` (version-matched, 158 files, 5 years newer)
- Move `FileService` to root namespace (shared service, not a tool)

## Capabilities

### New Capabilities
- `source-layer`: Split GitHubClient into SourceFetcher, SourceCache, VersionResolver with clear responsibilities
- `scanning-layer`: Rename Analysis to Scanning, absorb DocsIndex and SourceSearcher, move FileService to root
- `tool-reorganization`: Regroup 37 tools into 12 domain-themed files following Akka-style naming
- `docs-from-monorepo`: Replace getakka.net with akkadotnet/akka.net docs/articles/, version-matched docs

### Modified Capabilities

## Impact

- **All files move** — this is a namespace and folder restructure. Every `using` statement in every file changes.
- **Zero behavior changes** — all 37 MCP tools keep their names, signatures, and behavior. This is purely structural.
- **Tests** — test files need updated `using` statements and constructor changes for split classes.
- **DI registration** — `McpSetup.cs` registers new classes instead of GitHubClient for orchestration.
- **getakka.net removed** — `DocsRepo` constant, `EnsureDocsAsync`, branch-based fetching all removed. Docs path changes from `src/docs/` to `docs/articles/`.
